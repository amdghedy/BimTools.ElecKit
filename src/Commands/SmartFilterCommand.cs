using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class SmartFilterCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            Reference reference;
            try { reference = ctx.UiDoc.Selection.PickObject(ObjectType.Element, "Pick a reference element to filter the active view"); }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            Element source = ctx.Doc.GetElement(reference);
            if (source == null || source.Category == null)
            { TaskDialog.Show("Smart Filter", "The picked element has no category."); return Result.Cancelled; }

            var choice = new TaskDialog("Smart Filter");
            choice.MainInstruction = "Choose the matching rule";
            choice.MainContent = "Scope: active view only. This command selects elements and does not modify the model.";
            choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Same type as picked element");
            choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Same category as picked element");
            choice.CommonButtons = TaskDialogCommonButtons.Cancel;
            TaskDialogResult result = choice.Show();
            if (result == TaskDialogResult.Cancel) return Result.Cancelled;

            bool byType = result == TaskDialogResult.CommandLink1;
            ElementId typeId = source.GetTypeId();
            int categoryId = source.Category.Id.IntegerValue;
            var ids = new List<ElementId>();
            foreach (Element candidate in new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id).WhereElementIsNotElementType())
            {
                if (candidate.Category == null) continue;
                bool match = byType
                    ? candidate.GetTypeId() == typeId
                    : candidate.Category.Id.IntegerValue == categoryId;
                if (match) ids.Add(candidate.Id);
            }

            if (ids.Count == 0)
            { TaskDialog.Show("Smart Filter", "No matching elements were found in the active view."); return Result.Succeeded; }
            ctx.UiDoc.Selection.SetElementIds(ids);
            TaskDialog.Show("Smart Filter", ids.Count + " element(s) selected in the active view.");
            return Result.Succeeded;
        }
    }
}