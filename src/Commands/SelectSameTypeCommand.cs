using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SelectSameTypeCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            Reference picked = ctx.UiDoc.Selection.PickObject(
                ObjectType.Element, "Pick an element whose type you want to match");

            Element source = ctx.Doc.GetElement(picked);
            ElementId typeId = source.GetTypeId();
            if (typeId == null || typeId.IntegerValue == ElementId.InvalidElementId.IntegerValue)
            {
                TaskDialog.Show("Select Same Type", "That element has no type.");
                return Result.Cancelled;
            }

            var ids = new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id)
                .WhereElementIsNotElementType()
                .Where(e => e.GetTypeId().IntegerValue == typeId.IntegerValue)
                .Select(e => e.Id)
                .ToList();

            ctx.UiDoc.Selection.SetElementIds(ids);
            TaskDialog.Show("Select Same Type", ids.Count + " element(s) of the same type are now selected in this view.");
            return Result.Succeeded;
        }
    }
}
