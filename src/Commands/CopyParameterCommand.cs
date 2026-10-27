using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Copies the value of one parameter into a text parameter, for every selected element.</summary>
    [Transaction(TransactionMode.Manual)]
    public class CopyParameterCommand : ElecCommand
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
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element, "Pick the elements, then click Finish"))
                    elements.Add(ctx.Doc.GetElement(r));
            }
            if (elements.Count == 0) return Result.Cancelled;

            string source = ParameterChooser.Pick(elements[0], "Copy Parameter", "Copy the value FROM this parameter:", false);
            if (source == null) return Result.Cancelled;
            string target = ParameterChooser.Pick(elements[0], "Copy Parameter", "Copy it INTO this text parameter:", true);
            if (target == null) return Result.Cancelled;
            if (source == target)
            {
                TaskDialog.Show("Copy Parameter", "Choose two different parameters.");
                return Result.Cancelled;
            }

            int copied = 0, skipped = 0;
            ctx.Transact("Copy parameter", () =>
            {
                foreach (Element element in elements)
                {
                    Parameter sourceParameter = element.LookupParameter(source);
                    Parameter targetParameter = element.LookupParameter(target);
                    if (sourceParameter == null || targetParameter == null || targetParameter.IsReadOnly || targetParameter.StorageType != StorageType.String) { skipped++; continue; }

                    string value = sourceParameter.StorageType == StorageType.String ? sourceParameter.AsString() : sourceParameter.AsValueString();
                    targetParameter.Set(value ?? "");
                    copied++;
                }
            });

            TaskDialog.Show("Copy Parameter", copied + " value(s) copied from \"" + source + "\" to \"" + target + "\"." +
                (skipped > 0 ? "\n" + skipped + " element(s) skipped (a parameter is missing or read-only)." : ""));
            return Result.Succeeded;
        }
    }
}
