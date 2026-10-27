using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Estimates the voltage drop of each circuit from its load, voltage, poles and path length, using the cables of the
    /// Cable Catalog. Either picks the smallest cable that stays within your limit, or applies one cable to every circuit.
    /// Resistive drop only: it is a screening aid, not a design calculation.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class VoltageDropCommand : ElecCommand
    {
        private sealed class Row
        {
            public string Panel, Circuit, Load, Cable, Status;
            public int Poles;
            public double Volts, Va, Amps, LengthM, Section, DropV, DropPct;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            List<CableDefinition> cables = CableCatalogStore.Load()
                .Where(c => c.CrossSectionMm2 > 0)
                .OrderBy(c => c.CrossSectionMm2)
                .ToList();
            if (cables.Count == 0)
            {
                TaskDialog.Show("Voltage Drop", "The Cable Catalog has no cable with a cross-section. Fill it in with the Cable Catalog tool first.");
                return Result.Cancelled;
            }

            int scope = Dialogs.Choose("Voltage Drop", "Circuits:", new List<string>
            {
                "Every circuit in the model",
                "The circuits of one panel (pick it next)"
            }, 0);
            if (scope < 0) return Result.Cancelled;

            Element panel = null;
            if (scope == 1)
            {
                panel = ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element,
                    new PredicateFilter(e => e is FamilyInstance && e.Category != null &&
                                             e.Category.Id.Val() == (int)BuiltInCategory.OST_ElectricalEquipment),
                    "Pick the panel"));
            }

            int cableMode = Dialogs.Choose("Voltage Drop", "Cable:", new List<string>
            {
                "The smallest catalog cable that keeps the drop within the limit",
                "One cable from the catalog for every circuit (pick it next)"
            }, 0);
            if (cableMode < 0) return Result.Cancelled;

            CableDefinition fixedCable = null;
            if (cableMode == 1)
            {
                int index = ListPicker.Pick("Voltage Drop", "Cable for every circuit:",
                    cables.Select(c => c.Name + "   (" + UnitConv.Format(c.CrossSectionMm2) + " mm2)").ToList());
                if (index < 0) return Result.Cancelled;
                fixedCable = cables[index];
            }

            double? limitValue = Dialogs.AskNumber("Voltage Drop", "Largest allowed voltage drop:",
                ElecSettings.Current.VoltageDropLimitPct ?? 3.0, true, "%");
            if (limitValue == null) return Result.Cancelled;
            double? rhoValue = Dialogs.AskNumber("Voltage Drop",
                "Conductor resistivity (copper at working temperature is about 0.0225, aluminium about 0.036):",
                ElecSettings.Current.ResistivityOhmMm2PerM ?? 0.0225, true, "ohm x mm2 / m");
            if (rhoValue == null) return Result.Cancelled;
            double limit = limitValue.Value;
            double rho = rhoValue.Value;
            ElecSettings.Current.VoltageDropLimitPct = limit;
            ElecSettings.Current.ResistivityOhmMm2PerM = rho;
            ElecSettings.Current.Save();

            Busy.On();
            var rows = new List<Row>();
            int skipped = 0;
            foreach (Element item in new FilteredElementCollector(ctx.Doc).OfClass(typeof(ElectricalSystem)))
            {
                var system = item as ElectricalSystem;
                if (system == null) continue;
                if (panel != null)
                {
                    FamilyInstance equipment = system.BaseEquipment;
                    if (equipment == null || equipment.Id.Val() != panel.Id.Val()) continue;
                }

                double lengthM = system.Length * 0.3048;
                Parameter vaParameter = system.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD);
                Parameter voltParameter = system.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE);
                Parameter poleParameter = system.get_Parameter(BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES);
                if (vaParameter == null || voltParameter == null || lengthM <= 0) { skipped++; continue; }

                double va = ElectricalUnits.ToVoltAmperes(vaParameter.AsDouble());
                double volts = ElectricalUnits.ToVolts(voltParameter.AsDouble());
                int poles = poleParameter != null ? poleParameter.AsInteger() : 1;
                if (va <= 0 || volts <= 0) { skipped++; continue; }

                bool threePhase = poles >= 3;
                double amps = threePhase ? va / (Math.Sqrt(3.0) * volts) : va / volts;
                double k = threePhase ? Math.Sqrt(3.0) : 2.0;

                CableDefinition chosen = fixedCable;
                string status = "OK";
                if (chosen == null)
                {
                    chosen = cables.FirstOrDefault(c => DropPercent(k, amps, lengthM, rho, c.CrossSectionMm2, volts) <= limit &&
                                                        (c.AmpacityA <= 0 || c.AmpacityA >= amps));
                    if (chosen == null)
                    {
                        chosen = cables[cables.Count - 1];
                        status = "NO CABLE IN THE CATALOG FITS";
                    }
                }

                double dropPct = DropPercent(k, amps, lengthM, rho, chosen.CrossSectionMm2, volts);
                if (status == "OK" && dropPct > limit) status = "OVER THE LIMIT";
                if (status == "OK" && chosen.AmpacityA > 0 && chosen.AmpacityA < amps) status = "CABLE CAPACITY TOO LOW";

                rows.Add(new Row
                {
                    Panel = system.PanelName ?? "",
                    Circuit = system.CircuitNumber ?? "",
                    Load = system.LoadName ?? "",
                    Poles = poles,
                    Volts = volts,
                    Va = va,
                    Amps = amps,
                    LengthM = lengthM,
                    Cable = chosen.Name,
                    Section = chosen.CrossSectionMm2,
                    DropV = dropPct / 100.0 * volts,
                    DropPct = dropPct,
                    Status = status
                });
            }

            Busy.Off();
            if (rows.Count == 0)
            {
                TaskDialog.Show("Voltage Drop", "No circuit had both a load and a path length." +
                    (skipped > 0 ? "\n" + skipped + " circuit(s) were skipped (no load, voltage or length yet)." : ""));
                return Result.Cancelled;
            }
            rows = rows.OrderBy(r => r.Panel, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Circuit, StringComparer.OrdinalIgnoreCase).ToList();

            CultureInfo ci = CultureInfo.InvariantCulture;
            var csv = new StringBuilder();
            csv.AppendLine("Panel,Circuit,Load,Poles,Voltage (V),Load (VA),Current (A),Length (m),Cable,Cross-section (mm2),Drop (V),Drop (%),Status");
            foreach (Row r in rows)
                csv.AppendLine(string.Join(",", Csv.Escape(r.Panel), Csv.Escape(r.Circuit), Csv.Escape(r.Load), r.Poles,
                    r.Volts.ToString("0.#", ci), r.Va.ToString("0", ci), r.Amps.ToString("0.0", ci), r.LengthM.ToString("0.0", ci),
                    Csv.Escape(r.Cable), r.Section.ToString("0.##", ci), r.DropV.ToString("0.00", ci), r.DropPct.ToString("0.00", ci),
                    Csv.Escape(r.Status)));
            string path = Reports.PathFor("VoltageDrop", ".csv");
            System.IO.File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));

            int ok = rows.Count(r => r.Status == "OK");
            var text = new StringBuilder();
            text.AppendLine("VOLTAGE DROP: " + rows.Count + " circuit(s), limit " + UnitConv.Format(limit) + " %, resistivity " + UnitConv.Format(rho));
            text.AppendLine(ok + " OK, " + (rows.Count - ok) + " need attention" + (skipped > 0 ? ", " + skipped + " skipped (no load, voltage or length)" : ""));
            text.AppendLine();
            text.AppendLine("Panel".PadRight(10) + "Circ".PadRight(6) + "Amps".PadLeft(7) + "Len m".PadLeft(8) + "  " + "Cable".PadRight(16) + "Drop %".PadLeft(7) + "  Status");
            foreach (Row r in rows.OrderByDescending(r => r.DropPct).Take(60))
                text.AppendLine(Fit(r.Panel, 10) + Fit(r.Circuit, 6) + r.Amps.ToString("0.0", ci).PadLeft(7) + r.LengthM.ToString("0.0", ci).PadLeft(8) +
                                "  " + Fit(r.Cable, 16) + r.DropPct.ToString("0.00", ci).PadLeft(7) + "  " + r.Status);
            if (rows.Count > 60) text.AppendLine("... showing the 60 worst of " + rows.Count + ". The CSV has all of them.");
            text.AppendLine();
            text.AppendLine("Resistive drop only (no reactance, temperature, grouping or installation method). Verify the cable sizes in your design calculations.");
            text.AppendLine("CSV: " + path);
            Dialogs.ShowText("Voltage Drop", text.ToString());
            return Result.Succeeded;
        }

        /// <summary>Drop in percent: k * I * L * rho / section, as a share of the voltage. k = 2 (single phase) or sqrt(3).</summary>
        private static double DropPercent(double k, double amps, double lengthM, double rho, double sectionMm2, double volts)
        {
            return k * amps * lengthM * rho / sectionMm2 / volts * 100.0;
        }

        private static string Fit(string text, int width)
        {
            text = text ?? "";
            return (text.Length > width - 1 ? text.Substring(0, width - 1) : text).PadRight(width);
        }
    }
}
