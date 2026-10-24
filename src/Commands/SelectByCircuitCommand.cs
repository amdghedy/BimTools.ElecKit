using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SelectByCircuitCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            Reference picked = ctx.UiDoc.Selection.PickObject(
                ObjectType.Element, "Pick an element that is on a circuit");

            var instance = ctx.Doc.GetElement(picked) as FamilyInstance;
            if (instance == null || instance.MEPModel == null)
            {
                TaskDialog.Show("Select by Circuit", "That element is not an electrical family instance.");
                return Result.Cancelled;
            }

            var ids = new List<ElementId>();
            var seen = new HashSet<ElementId>();
            var systems = instance.MEPModel.GetElectricalSystems();
            if (systems != null)
            {
                foreach (ElectricalSystem system in systems)
                {
                    foreach (Element e in system.Elements)
                    {
                        if (seen.Add(e.Id)) ids.Add(e.Id);
                    }
                }
            }

            if (ids.Count == 0)
            {
                TaskDialog.Show("Select by Circuit", "That element is not on any circuit.");
                return Result.Cancelled;
            }

            ctx.UiDoc.Selection.SetElementIds(ids);
            return Result.Succeeded;
        }
    }
}
