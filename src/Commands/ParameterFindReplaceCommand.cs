using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Changes a text parameter on the selected elements: replace text, add a prefix, or add a suffix. Shows the count first.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ParameterFindReplaceCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var elements = new List<Element>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                Element e = ctx.Doc.GetElement(id);
                if (e != null) elements.Add(e);
            }
            if (elements.Count == 0)
            {
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element, "Pick the elements to change, then click Finish"))
                    elements.Add(ctx.Doc.GetElement(r));
            }
            if (elements.Count == 0) return Result.Cancelled;

            string parameterName = ParameterChooser.Pick(elements[0], "Parameter Find and Replace",
                "Text parameter to change:", true);
            if (parameterName == null) return Result.Cancelled;

            int mode = Dialogs.Choose("Parameter Find and Replace", "Action:", new List<string>
            {
                "Replace text (ignoring upper and lower case)",
                "Add a prefix to the value",
                "Add a suffix to the value"
            }, 0);
            if (mode < 0) return Result.Cancelled;

            string find = "";
            if (mode == 0)
            {
                find = Dialogs.Ask("Parameter Find and Replace", "Find:", "");
                if (string.IsNullOrEmpty(find)) return Result.Cancelled;
            }
            string replacement = Dialogs.Ask("Parameter Find and Replace",
                mode == 0 ? "Replace with (can be empty to delete the found text):" : "Text to add:", "");
            if (replacement == null) return Result.Cancelled;
            if (mode != 0 && replacement.Length == 0) return Result.Cancelled;

            var changes = new List<KeyValuePair<Parameter, string>>();
            foreach (Element element in elements)
            {
                Parameter p = element.LookupParameter(parameterName);
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.String) continue;

                string current = p.AsString() ?? "";
                string updated = mode == 0 ? Regex.Replace(current, Regex.Escape(find), replacement.Replace("$", "$$"), RegexOptions.IgnoreCase)
                               : mode == 1 ? replacement + current
                               : current + replacement;
                if (!string.Equals(current, updated, StringComparison.Ordinal))
                    changes.Add(new KeyValuePair<Parameter, string>(p, updated));
            }

            if (changes.Count == 0)
            {
                TaskDialog.Show("Parameter Find and Replace", "Nothing would change on the " + elements.Count + " element(s).");
                return Result.Cancelled;
            }

            var confirm = new TaskDialog("Parameter Find and Replace");
            confirm.MainInstruction = "Change " + changes.Count + " of " + elements.Count + " element(s)?";
            confirm.MainContent = "Example: \"" + (changes[0].Key.AsString() ?? "") + "\"  becomes  \"" + changes[0].Value + "\"";
            confirm.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            confirm.DefaultButton = TaskDialogResult.No;
            if (confirm.Show() != TaskDialogResult.Yes) return Result.Cancelled;

            ctx.Transact("Parameter find and replace", () =>
            {
                foreach (var change in changes) change.Key.Set(change.Value);
            });
            TaskDialog.Show("Parameter Find and Replace", changes.Count + " value(s) changed.");
            return Result.Succeeded;
        }
    }
}
