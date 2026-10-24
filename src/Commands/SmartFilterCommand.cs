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
    [Transaction(TransactionMode.Manual)]
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
            choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Same parameter value");
            choice.CommonButtons = TaskDialogCommonButtons.Cancel;
            TaskDialogResult result = choice.Show();
            if (result == TaskDialogResult.Cancel) return Result.Cancelled;

            bool byType = result == TaskDialogResult.CommandLink1;
            bool byParameter = result == TaskDialogResult.CommandLink3;
            ElementId typeId = source.GetTypeId();
            int categoryId = source.Category.Id.IntegerValue;
            string parameterName = null, parameterValue = null;
            Parameter sourceParameter = null;
            if (byParameter)
            {
                parameterName = Dialogs.Ask("Smart Filter", "Enter the exact parameter name on the picked element:", "Mark");
                if (string.IsNullOrWhiteSpace(parameterName)) return Result.Cancelled;
                sourceParameter = source.LookupParameter(parameterName.Trim());
                if (sourceParameter == null)
                { TaskDialog.Show("Smart Filter", "The picked element does not contain a parameter named \"" + parameterName + "\"."); return Result.Cancelled; }
                parameterValue = ParameterText(sourceParameter);
                parameterValue = Dialogs.Ask("Smart Filter", "Enter the exact displayed value to match for parameter \"" + parameterName + "\".\nUse an empty value to match blank string parameters.", parameterValue);
                if (parameterValue == null) return Result.Cancelled;
            }
            var ids = new List<ElementId>();
            foreach (Element candidate in new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id).WhereElementIsNotElementType())
            {
                if (candidate.Category == null) continue;
                bool match;
                if (byParameter)
                {
                    Parameter candidateParameter = candidate.LookupParameter(parameterName);
                    match = candidateParameter != null && candidateParameter.StorageType == sourceParameter.StorageType &&
                        string.Equals(ParameterText(candidateParameter), parameterValue, StringComparison.OrdinalIgnoreCase);
                }
                else if (byType) match = candidate.GetTypeId() == typeId;
                else match = candidate.Category.Id.IntegerValue == categoryId;
                if (match) ids.Add(candidate.Id);
            }

            if (ids.Count == 0)
            { TaskDialog.Show("Smart Filter", "No matching elements were found in the active view."); return Result.Succeeded; }
            ctx.UiDoc.Selection.SetElementIds(ids);

            var action = new TaskDialog("Smart Filter");
            action.MainInstruction = ids.Count + " matching element(s) found.";
            action.MainContent = "Choose whether to keep the selection only or temporarily isolate the matches in this view.";
            action.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Select matches only");
            action.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Temporarily isolate matches");
            action.CommonButtons = TaskDialogCommonButtons.Cancel;
            TaskDialogResult actionResult = action.Show();
            if (actionResult == TaskDialogResult.CommandLink2)
            {
                try
                {
                    ctx.Transact("Temporarily isolate Smart Filter matches", delegate
                    {
                        ctx.Doc.ActiveView.IsolateElementsTemporary(ids);
                    });
                    TaskDialog.Show("Smart Filter", ids.Count + " element(s) isolated temporarily in the active view. Use Revit's Reset Temporary Hide/Isolate control to restore the view.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    TaskDialog.Show("Smart Filter", "Temporary isolation failed. The matching elements remain selected.\\n" + ex.Message);
                }
            }
            else if (actionResult == TaskDialogResult.CommandLink1)
            {
                TaskDialog.Show("Smart Filter", ids.Count + " element(s) selected in the active view.");
            }
            else return Result.Cancelled;
            return Result.Succeeded;
        }

        private static string ParameterText(Parameter parameter)
        {
            if (parameter == null) return "";
            switch (parameter.StorageType)
            {
                case StorageType.String: return parameter.AsString() ?? "";
                case StorageType.Integer: return parameter.AsValueString() ?? parameter.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture);
                case StorageType.Double: return parameter.AsValueString() ?? "";
                case StorageType.ElementId: return parameter.AsValueString() ?? parameter.AsElementId().IntegerValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default: return "";
            }
        }
    }
}