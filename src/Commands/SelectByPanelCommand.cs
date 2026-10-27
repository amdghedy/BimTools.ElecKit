using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Selects every element fed by a panel (all the elements of all its circuits).</summary>
    [Transaction(TransactionMode.Manual)]
    public class SelectByPanelCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            Element panel = ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element,
                new PredicateFilter(e => e is FamilyInstance && e.Category != null &&
                                         e.Category.Id.Val() == (int)BuiltInCategory.OST_ElectricalEquipment),
                "Pick the panel"));

            var ids = new List<ElementId>();
            var seen = new HashSet<int>();
            int circuits = 0;

            foreach (Element item in new FilteredElementCollector(ctx.Doc).OfClass(typeof(ElectricalSystem)))
            {
                var system = item as ElectricalSystem;
                if (system == null) continue;
                FamilyInstance equipment = system.BaseEquipment;
                if (equipment == null || equipment.Id.Val() != panel.Id.Val()) continue;

                circuits++;
                foreach (Element member in system.Elements)
                {
                    if (seen.Add(member.Id.Val())) ids.Add(member.Id);
                }
            }

            if (circuits == 0)
            {
                TaskDialog.Show("Select by Panel", "No circuits are fed from that panel.");
                return Result.Cancelled;
            }

            ctx.UiDoc.Selection.SetElementIds(ids);
            TaskDialog.Show("Select by Panel", ids.Count + " element(s) on " + circuits + " circuit(s) are selected.");
            return Result.Succeeded;
        }
    }
}
