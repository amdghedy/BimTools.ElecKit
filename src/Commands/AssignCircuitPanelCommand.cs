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
                    && RevitApiCompat.ElementIdValue(element.Category.Id) == (int)BuiltInCategory.OST_ElectricalEquipment;
            }

            public bool AllowReference(Reference reference, XYZ position) { return false; }
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
                    " | System ID: " + RevitApiCompat.ElementIdValue(s.Id)).ToList();
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

            string oldPanel = string.IsNullOrEmpty(targetSystem.PanelName) ? "(unassigned)" : targetSystem.PanelName;
            ctx.Transact("Assign circuit to panel", delegate
            {
                targetSystem.SelectPanel(panel);
            });

            TaskDialog.Show("Assign Circuit Panel",
                "Circuit: " + (targetSystem.CircuitNumber ?? "(unnumbered)") +
                "\nPrevious panel: " + oldPanel +
                "\nDestination panel: " + panel.Name +
                "\n\nRevit accepted the panel assignment. Verify circuit capacity, voltage/phase compatibility and panel schedule in the model.");
            return Result.Succeeded;
        }
    }
}
