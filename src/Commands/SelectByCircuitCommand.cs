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
            int pickedId = ctx.Doc.GetElement(picked).Id.Val();

            var ids = new List<ElementId>();
            var seen = new HashSet<int>();
            bool onCircuit = false;

            foreach (Element item in new FilteredElementCollector(ctx.Doc).OfClass(typeof(ElectricalSystem)))
            {
                var system = item as ElectricalSystem;
                if (system == null) continue;

                bool contains = false;
                foreach (Element member in system.Elements)
                {
                    if (member.Id.Val() == pickedId) { contains = true; break; }
                }
                if (!contains) continue;

                onCircuit = true;
                foreach (Element member in system.Elements)
                {
                    if (seen.Add(member.Id.Val())) ids.Add(member.Id);
                }
            }

            if (!onCircuit)
            {
                TaskDialog.Show("Select by Circuit", "That element is not on any circuit.");
                return Result.Cancelled;
            }

            ctx.UiDoc.Selection.SetElementIds(ids);
            return Result.Succeeded;
        }
    }
}
