using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using ElecKit.Core;
namespace ElecKit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CircuitPanelAuditCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var systems = new FilteredElementCollector(ctx.Doc).OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>().OrderBy(s => s.PanelName).ThenBy(s => s.CircuitNumber).ToList();
            var panelLoadTotals = new Dictionary<long, double>();
            var panelLoadComplete = new Dictionary<long, bool>();
            foreach (ElectricalSystem system in systems)
            {
                FamilyInstance assigned = null;
                try { assigned = system.BaseEquipment; } catch { }
                if (assigned == null) continue;
                long panelId = assigned.Id.Val();
                double? load = ReadApparentLoadVa(system);
                if (!panelLoadTotals.ContainsKey(panelId))
                {
                    panelLoadTotals[panelId] = 0.0;
                    panelLoadComplete[panelId] = true;
                }
                if (!load.HasValue) panelLoadComplete[panelId] = false;
                else
                {
                    panelLoadTotals[panelId] += load.Value;
                    if (double.IsNaN(panelLoadTotals[panelId]) || double.IsInfinity(panelLoadTotals[panelId]))
                        panelLoadComplete[panelId] = false;
                }
            }

            var rows = new StringBuilder();
            rows.AppendLine("SystemElementId,Panel,Circuit,Load,ConnectedElements,CircuitVoltage,PanelVoltage,CircuitPoles,PanelPoles,CircuitApparentLoadVA,DeclaredPanelCapacityVA,Status,Findings");
            int errors = 0, warnings = 0;
            foreach (ElectricalSystem system in systems)
            {
                FamilyInstance panel = null;
                try { panel = system.BaseEquipment; } catch { }
                double? circuitVoltage = ReadDouble(system, "Voltage", "Nominal Voltage", "Circuit Voltage");
                if (!circuitVoltage.HasValue)
                {
                    Parameter p = system.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE);
                    if (p != null && p.StorageType == StorageType.Double && p.AsDouble() > 0) circuitVoltage = p.AsDouble();
                }
                double? panelVoltage = ReadDouble(panel, "Voltage", "Rated Voltage", "Nominal Voltage");
                int? circuitPoles = ReadInt(system, "Number of Poles", "Poles", "Pole Count");
                int? panelPoles = ReadInt(panel, "Number of Poles", "Poles", "Pole Count");
                double? loadVa = ReadDouble(system, "Apparent Load (VA)", "Connected Load (VA)");
                Parameter loadParameter = system.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD);
                if (!loadVa.HasValue && loadParameter != null && loadParameter.StorageType == StorageType.Double && loadParameter.AsDouble() >= 0)
                    loadVa = loadParameter.AsDouble();
                double? capacityVa = panel == null ? null : ReadCapacityVa(panel);
                double? existingPanelLoadVa = null;
                if (panel != null && loadVa.HasValue && panelLoadComplete.ContainsKey(panel.Id.Val()) &&
                    panelLoadComplete[panel.Id.Val()])
                    existingPanelLoadVa = Math.Max(0.0, panelLoadTotals[panel.Id.Val()] - loadVa.Value);
                CircuitPanelCheck check = CircuitPanelValidation.Evaluate(
                    circuitVoltage, panelVoltage, circuitPoles, panelPoles, loadVa, capacityVa, existingPanelLoadVa);
                if (check.Errors.Count > 0) errors++;
                if (check.Warnings.Count > 0) warnings++;
                string findings = string.Join(" | ", check.Errors.Concat(check.Warnings));
                rows.AppendLine(string.Join(",", new[]
                {
                    Csv.Escape(system.Id.Val().ToString(CultureInfo.InvariantCulture)),
                    Csv.Escape(system.PanelName), Csv.Escape(system.CircuitNumber), Csv.Escape(system.LoadName),
                    Csv.Escape(SafeElementCount(system).ToString(CultureInfo.InvariantCulture)),
                    Csv.Escape(Format(circuitVoltage)), Csv.Escape(Format(panelVoltage)),
                    Csv.Escape(circuitPoles.HasValue ? circuitPoles.Value.ToString(CultureInfo.InvariantCulture) : ""),
                    Csv.Escape(panelPoles.HasValue ? panelPoles.Value.ToString(CultureInfo.InvariantCulture) : ""),
                    Csv.Escape(Format(loadVa)), Csv.Escape(Format(capacityVa)),
                    Csv.Escape(check.Errors.Count > 0 ? "FAIL" : check.Warnings.Count > 0 ? "REVIEW" : "PASS"),
                    Csv.Escape(findings)
                }));
            }
            string folder = ElecSettings.Current.ResolveReportFolder();
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "CircuitPanelAudit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
            File.WriteAllText(file, rows.ToString(), new UTF8Encoding(true));
            TaskDialog.Show("Circuit & Panel Audit", "Circuits audited: " + systems.Count +
                "\nKnown compatibility/capacity failures: " + errors + "\nCircuits requiring manual review: " + warnings +
                "\nReport: " + file + "\n\nOnly explicit parameters are treated as voltage, poles or VA capacity. Missing data is REVIEW, never assumed safe.");
            if (!ElecSettings.Current.DoNotOpenReports)
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            return Result.Succeeded;
        }
        private static int SafeElementCount(ElectricalSystem system) { try { return system.Elements == null ? 0 : system.Elements.Size; } catch { return 0; } }
        private static double? ReadDouble(Element element, params string[] names)
        {
            if (element == null) return null;
            foreach (string name in names)
            {
                Parameter p = element.LookupParameter(name);
                if (p != null && p.StorageType == StorageType.Double)
                {
                    double value = p.AsDouble();
                    if (!double.IsNaN(value) && !double.IsInfinity(value) && value > 0) return value;
                }
            }
            return null;
        }
        private static int? ReadInt(Element element, params string[] names)
        {
            if (element == null) return null;
            foreach (string name in names)
            {
                Parameter p = element.LookupParameter(name);
                if (p != null && p.StorageType == StorageType.Integer && p.AsInteger() > 0) return p.AsInteger();
                int parsed;
                if (p != null && p.StorageType == StorageType.String && int.TryParse(p.AsString(), out parsed) && parsed > 0) return parsed;
            }
            return null;
        }
        private static double? ReadApparentLoadVa(ElectricalSystem system)
        {
            if (system == null) return null;
            Parameter p = system.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD);
            if (p != null && p.StorageType == StorageType.Double && !double.IsNaN(p.AsDouble()) &&
                !double.IsInfinity(p.AsDouble()) && p.AsDouble() >= 0) return p.AsDouble();
            foreach (string name in new[] { "Apparent Load (VA)", "Connected Load (VA)" })
            {
                p = system.LookupParameter(name);
                if (p != null && p.StorageType == StorageType.Double && !double.IsNaN(p.AsDouble()) &&
                    !double.IsInfinity(p.AsDouble()) && p.AsDouble() >= 0) return p.AsDouble();
            }
            return null;
        }
        private static double? ReadCapacityVa(Element panel)
        {
            string[] names = { "Panel Capacity (VA)", "Maximum Apparent Load (VA)", "Connected Load Capacity (VA)", "Panel Capacity (kVA)" };
            foreach (string name in names)
            {
                Parameter p = panel.LookupParameter(name);
                if (p == null) continue;
                string displayed = p.AsValueString() ?? p.AsString();
                if (string.IsNullOrWhiteSpace(displayed) && p.StorageType == StorageType.Double)
                    displayed = p.AsDouble().ToString(CultureInfo.InvariantCulture);
                Match m = Regex.Match(displayed ?? "", @"[-+]?[0-9]*[\.,]?[0-9]+");
                double number;
                if (!m.Success || !double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number) || number <= 0) continue;
                return name.IndexOf("kVA", StringComparison.OrdinalIgnoreCase) >= 0 ? number * 1000.0 : number;
            }
            return null;
        }
        private static string Format(double? value) { return value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : ""; }
    }
}
