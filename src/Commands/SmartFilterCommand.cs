using System;
using System.Collections.Generic;
using System.Globalization;
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
        private enum RuleKind { Type, Category, Parameter }

        private sealed class FilterRule
        {
            public RuleKind Kind;
            public ElementId TypeId;
            public long CategoryId;
            public string ParameterName;
            public string ParameterValue;
            public StorageType StorageType;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            Reference reference;
            try { reference = ctx.UiDoc.Selection.PickObject(ObjectType.Element, "Pick a reference element for Smart Filter"); }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            Element source = ctx.Doc.GetElement(reference);
            if (source == null || source.Category == null)
            {
                TaskDialog.Show("Smart Filter", "The picked element has no category.");
                return Result.Cancelled;
            }

            var rules = new List<FilterRule>();
            bool addMore = true;
            while (addMore)
            {
                var picker = new TaskDialog("Smart Filter - Add Rule");
                picker.MainInstruction = "Choose a rule";
                picker.MainContent = "Build a compound filter. Each rule compares elements in the active view against the picked reference element.";
                picker.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Same type as reference");
                picker.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Same category as reference");
                picker.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Parameter equals a value");
                picker.CommonButtons = TaskDialogCommonButtons.Cancel;
                TaskDialogResult pickedRule = picker.Show();

                var rule = new FilterRule();
                if (pickedRule == TaskDialogResult.CommandLink1)
                {
                    rule.Kind = RuleKind.Type;
                    rule.TypeId = source.GetTypeId();
                }
                else if (pickedRule == TaskDialogResult.CommandLink2)
                {
                    rule.Kind = RuleKind.Category;
                    rule.CategoryId = source.Category.Id.IntegerValue;
                }
                else if (pickedRule == TaskDialogResult.CommandLink3)
                {
                    string name = Dialogs.Ask("Smart Filter", "Enter the exact parameter name on the reference element:", "Mark");
                    if (string.IsNullOrWhiteSpace(name)) return Result.Cancelled;
                    Parameter parameter = source.LookupParameter(name.Trim());
                    if (parameter == null)
                    {
                        TaskDialog.Show("Smart Filter", "The reference element does not contain parameter '" + name + "'.");
                        return Result.Cancelled;
                    }

                    string value = Dialogs.Ask("Smart Filter", "Enter the exact displayed value to match for '" + name.Trim() + "':", ParameterText(parameter));
                    if (value == null) return Result.Cancelled;
                    rule.Kind = RuleKind.Parameter;
                    rule.ParameterName = name.Trim();
                    rule.ParameterValue = value;
                    rule.StorageType = parameter.StorageType;
                }
                else return Result.Cancelled;

                rules.Add(rule);
                if (rules.Count >= 12) break;

                var more = new TaskDialog("Smart Filter - Compound Rules");
                more.MainInstruction = rules.Count + " rule(s) configured";
                more.MainContent = "Add another rule? You can combine the rules with AND (every rule must match) or OR (any rule may match).";
                more.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No | TaskDialogCommonButtons.Cancel;
                TaskDialogResult moreResult = more.Show();
                if (moreResult == TaskDialogResult.Cancel) return Result.Cancelled;
                addMore = moreResult == TaskDialogResult.Yes;
            }

            bool useAnd = true;
            if (rules.Count > 1)
            {
                var combine = new TaskDialog("Smart Filter - Combine Rules");
                combine.MainInstruction = "How should rules be combined?";
                combine.MainContent = "AND returns elements matching every rule. OR returns elements matching at least one rule.";
                combine.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "AND - all rules must match");
                combine.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "OR - any rule may match");
                combine.CommonButtons = TaskDialogCommonButtons.Cancel;
                TaskDialogResult combineResult = combine.Show();
                if (combineResult == TaskDialogResult.CommandLink1) useAnd = true;
                else if (combineResult == TaskDialogResult.CommandLink2) useAnd = false;
                else return Result.Cancelled;
            }

            var ids = new List<ElementId>();
            foreach (Element candidate in new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id).WhereElementIsNotElementType())
            {
                if (candidate.Category == null) continue;
                var ruleResults = new List<bool>(rules.Count);
                foreach (FilterRule rule in rules)
                    ruleResults.Add(Matches(candidate, rule));

                if (SmartFilterLogic.Matches(ruleResults, useAnd))
                    ids.Add(candidate.Id);
            }

            if (ids.Count == 0)
            {
                TaskDialog.Show("Smart Filter", "No elements matched the " + rules.Count + "-rule filter in the active view.");
                return Result.Succeeded;
            }

            ctx.UiDoc.Selection.SetElementIds(ids);
            var action = new TaskDialog("Smart Filter");
            action.MainInstruction = ids.Count + " matching element(s) found.";
            action.MainContent = "Rules: " + rules.Count + " | Combination: " + (rules.Count == 1 ? "single rule" : useAnd ? "AND" : "OR") + ". Choose selection only or temporary isolation.";
            action.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Select matches only");
            action.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Temporarily isolate matches");
            action.CommonButtons = TaskDialogCommonButtons.Cancel;
            TaskDialogResult actionResult = action.Show();

            if (actionResult == TaskDialogResult.CommandLink2)
            {
                try
                {
                    ctx.Transact("Temporarily isolate Smart Filter matches", delegate { ctx.Doc.ActiveView.IsolateElementsTemporary(ids); });
                    TaskDialog.Show("Smart Filter", ids.Count + " element(s) isolated temporarily. Use Revit's Reset Temporary Hide/Isolate control to restore the view.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    TaskDialog.Show("Smart Filter", "Temporary isolation failed. Matching elements remain selected.\n" + ex.Message);
                }
            }
            else if (actionResult == TaskDialogResult.CommandLink1)
                TaskDialog.Show("Smart Filter", ids.Count + " element(s) selected in the active view.");
            else return Result.Cancelled;

            return Result.Succeeded;
        }

        private static bool Matches(Element candidate, FilterRule rule)
        {
            if (rule.Kind == RuleKind.Type) return candidate.GetTypeId() == rule.TypeId;
            if (rule.Kind == RuleKind.Category)
                return candidate.Category != null && candidate.Category.Id.IntegerValue == rule.CategoryId;

            Parameter parameter = candidate.LookupParameter(rule.ParameterName);
            return parameter != null && parameter.StorageType == rule.StorageType &&
                SmartFilterLogic.ValuesEqual(ParameterText(parameter), rule.ParameterValue);
        }

        private static string ParameterText(Parameter parameter)
        {
            if (parameter == null) return string.Empty;
            switch (parameter.StorageType)
            {
                case StorageType.String: return parameter.AsString() ?? string.Empty;
                case StorageType.Integer:
                    return parameter.AsValueString() ?? parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
                case StorageType.Double: return parameter.AsValueString() ?? string.Empty;
                case StorageType.ElementId:
                    return parameter.AsValueString() ?? parameter.AsElementId().IntegerValue.ToString(CultureInfo.InvariantCulture);
                default: return string.Empty;
            }
        }
    }
}