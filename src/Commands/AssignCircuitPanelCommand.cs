using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Reassigns one existing electrical system to a user-picked electrical panel.</summary>
    [Transaction(TransactionMode.Manual)]
    public class AssignCircuitPanelCommand : ElecCommand
    {
        private sealed class PanelFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return element is FamilyInstance
                    && element.Category != null
                    && element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_ElectricalEquipment;
            }

            public bool AllowReference(Reference reference, XYZ position) { return false; }
        }

        private static double? ReadDouble(Element element, params string[] names)
        {
            foreach (string name in names)
            {
                Parameter p = element == null ? null : element.LookupParameter(name);
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
            foreach (string name in names)
            {
                Parameter p = element == null ? null : element.LookupParameter(name);
                if (p != null && p.StorageType == StorageType.Integer && p.AsInteger() > 0) return p.AsInteger();
                int value;
                if (p != null && p.StorageType == StorageType.String && int.TryParse(p.AsString(), out value) && value > 0) return value;
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
                string raw = p.AsValueString() ?? p.AsString();
                if (string.IsNullOrWhiteSpace(raw) && p.StorageType == StorageType.Double)
                    raw = p.AsDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
                var match = System.Text.RegularExpressions.Regex.Match(raw ?? "", @"[-+]?[0-9]*[\.,]?[0-9]+");
                double value;
                if (match.Success && double.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0)
                    return name.IndexOf("kVA", StringComparison.OrdinalIgnoreCase) >= 0 ? value * 1000.0 : value;
            }
            return null;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            Reference circuitReference;
            try
            {
                circuitReference = ctx.UiDoc.Selection.PickObject(
                    ObjectType.Element, "Pick an element belonging to the circuit to reassign");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            Element picked = circuitReference == null ? null : ctx.Doc.GetElement(circuitReference);
            if (picked == null)
            {
                TaskDialog.Show("Assign Circuit Panel", "The picked element is no longer available.");
                return Result.Cancelled;
            }

            var systems = new List<ElectricalSystem>();
            foreach (Element element in new FilteredElementCollector(ctx.Doc).OfClass(typeof(ElectricalSystem)))
            {
                var system = element as ElectricalSystem;
                if (system == null) continue;
                foreach (Element member in system.Elements)
                {
                    if (member.Id == picked.Id)
                    {
                        systems.Add(system);
                        break;
                    }
                }
            }

            if (systems.Count == 0)
            {
                TaskDialog.Show("Assign Circuit Panel", "The picked element does not belong to an electrical system.");
                return Result.Cancelled;
            }

            ElectricalSystem targetSystem = systems[0];
            if (systems.Count > 1)
            {
                var choices = systems.Select(s =>
                    "Circuit " + (s.CircuitNumber ?? "(unnumbered)") +
                    " | Panel: " + (string.IsNullOrEmpty(s.PanelName) ? "(unassigned)" : s.PanelName) +
                    " | System ID: " + s.Id.IntegerValue).ToList();
                int choice = Dialogs.Choose("Assign Circuit Panel",
                    "The picked element belongs to more than one circuit. Choose the circuit to change.",
                    choices, 0);
                if (choice < 0 || choice >= systems.Count) return Result.Cancelled;
                targetSystem = systems[choice];
            }

            Reference panelReference;
            try
            {
                panelReference = ctx.UiDoc.Selection.PickObject(
                    ObjectType.Element, new PanelFilter(),
                    "Pick the destination electrical panel");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            FamilyInstance panel = panelReference == null ? null : ctx.Doc.GetElement(panelReference) as FamilyInstance;
            if (panel == null)
            {
                TaskDialog.Show("Assign Circuit Panel", "The destination must be an electrical equipment family instance.");
                return Result.Cancelled;
            }

            if (targetSystem.BaseEquipment != null && targetSystem.BaseEquipment.Id == panel.Id)
            {
                TaskDialog.Show("Assign Circuit Panel", "This circuit is already assigned to the selected panel.");
                return Result.Succeeded;
            }

            double? circuitVoltage = ReadDouble(targetSystem, "Voltage", "Nominal Voltage", "Circuit Voltage");
            if (!circuitVoltage.HasValue)
            {
                Parameter voltage = targetSystem.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE);
                if (voltage != null && voltage.StorageType == StorageType.Double && voltage.AsDouble() > 0)
                    circuitVoltage = voltage.AsDouble();
            }
            double? panelVoltage = ReadDouble(panel, "Voltage", "Rated Voltage", "Nominal Voltage");
            int? circuitPoles = ReadInt(targetSystem, "Number of Poles", "Poles", "Pole Count");
            int? panelPoles = ReadInt(panel, "Number of Poles", "Poles", "Pole Count");
            Parameter loadParameter = targetSystem.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD);
            double? loadVa = loadParameter != null && loadParameter.StorageType == StorageType.Double ? (double?)loadParameter.AsDouble() : null;
            CircuitPanelCheck preflight = CircuitPanelValidation.Evaluate(
                circuitVoltage, panelVoltage, circuitPoles, panelPoles, loadVa, ReadCapacityVa(panel));
            if (!preflight.CanAssign)
            {
                TaskDialog.Show("Assign Circuit Panel", "Assignment blocked because the available model data shows a known conflict:\n\n" +
                    string.Join("\n", preflight.Errors) +
                    "\n\nCorrect the circuit/panel data or verify the destination manually before retrying.");
                return Result.Cancelled;
            }

            string oldPanel = string.IsNullOrEmpty(targetSystem.PanelName) ? "(unassigned)" : targetSystem.PanelName;
            ctx.Transact("Assign circuit to panel", delegate { targetSystem.SelectPanel(panel); });

            string resultText = "Circuit: " + (targetSystem.CircuitNumber ?? "(unnumbered)") +
                "\nPrevious panel: " + oldPanel + "\nDestination panel: " + panel.Name +
                "\n\nRevit accepted the panel assignment.";
            if (preflight.Warnings.Count > 0)
                resultText += "\n\nManual checks required:\n" + string.Join("\n", preflight.Warnings);
            resultText += "\nConfirm Revit panel schedule, protective-device ratings, phase balance and electrical design before issue.";
            TaskDialog.Show("Assign Circuit Panel", resultText);
            return Result.Succeeded;
        }
    }
}
