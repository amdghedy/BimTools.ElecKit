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
    /// <summary>
    /// Writes circuit number text into a text parameter of fixtures and equipment:
    /// the same text on all, each element's own circuit, or a numbered sequence in pick order.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CircuitNumberTextCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            int mode = Dialogs.Choose("Circuit Number Text", "What should be written?", new List<string>
            {
                "The same text on every element",
                "Each element's own circuit (Panel-Circuit)",
                "A numbered sequence in pick order (prefix + number)"
            }, 0);
            if (mode < 0) return Result.Cancelled;

            // elements: the sequence mode needs the pick order, so it always asks for a pick
            var elements = new List<Element>();
            if (mode != 2)
            {
                foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
                {
                    var instance = ctx.Doc.GetElement(id) as FamilyInstance;
                    if (instance != null) elements.Add(instance);
                }
            }
            if (elements.Count == 0)
            {
                var filter = new PredicateFilter(e => e is FamilyInstance);
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element, filter,
                    mode == 2 ? "Pick the fixtures in numbering order, then click Finish"
                              : "Pick fixtures or equipment, then click Finish"))
                {
                    Element element = ctx.Doc.GetElement(r);
                    if (element != null) elements.Add(element);
                }
            }
            if (elements.Count == 0) return Result.Cancelled;

            // target parameter
            string parameterName = ChooseParameter(elements[0]);
            if (parameterName == null) return Result.Cancelled;

            // text inputs
            string fixedText = null;
            string prefix = "";
            int start = 1;
            if (mode == 0)
            {
                fixedText = Dialogs.Ask("Circuit Number Text", "Circuit number text to write on all selected elements:", "");
                if (fixedText == null) return Result.Cancelled;
            }
            else if (mode == 2)
            {
                string prefixAnswer = Dialogs.Ask("Circuit Number Text", "Prefix, for example A- (can be empty):", "");
                if (prefixAnswer == null) return Result.Cancelled;
                prefix = prefixAnswer;

                string startAnswer = Dialogs.Ask("Circuit Number Text", "First number:", "1");
                if (startAnswer == null) return Result.Cancelled;
                if (!int.TryParse(startAnswer.Trim(), out start))
                {
                    TaskDialog.Show("Circuit Number Text", "Enter a whole number, for example 1.");
                    return Result.Cancelled;
                }
            }

            ElecSettings.Current.LastCircuitParam = parameterName;
            ElecSettings.Current.Save();

            int written = 0, noParameter = 0, noCircuit = 0;
            ctx.Transact("Circuit number text", () =>
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    string text;
                    if (mode == 0) text = fixedText;
                    else if (mode == 1) text = CircuitInfo.Of(elements[i]);
                    else text = prefix + (start + i);

                    if (text == null) { noCircuit++; continue; }

                    Parameter parameter = elements[i].LookupParameter(parameterName);
                    if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
                    {
                        noParameter++;
                        continue;
                    }
                    parameter.Set(text);
                    written++;
                }
            });

            string result = written + " element(s) updated (" + parameterName + ").";
            if (noCircuit > 0) result += "\n" + noCircuit + " element(s) skipped: not on any circuit.";
            if (noParameter > 0) result += "\n" + noParameter + " element(s) skipped: no writable \"" + parameterName + "\" text parameter.";
            TaskDialog.Show("Circuit Number Text", result);
            return Result.Succeeded;
        }

        /// <summary>Lists the writable text parameters of the first element. Returns null if cancelled or none exist.</summary>
        private static string ChooseParameter(Element sample)
        {
            var names = new List<string>();
            foreach (Parameter p in sample.Parameters)
            {
                if (p.StorageType == StorageType.String && !p.IsReadOnly && p.Definition != null &&
                    !names.Contains(p.Definition.Name))
                    names.Add(p.Definition.Name);
            }
            if (names.Count == 0)
            {
                TaskDialog.Show("Circuit Number Text", "The first element has no writable text parameter.");
                return null;
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);

            int index = names.IndexOf(ElecSettings.Current.LastCircuitParam ?? "");
            if (index < 0)
            {
                Parameter comments = sample.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comments != null) index = names.IndexOf(comments.Definition.Name);
            }

            int chosen = Dialogs.Choose("Circuit Number Text", "Write into this parameter:", names, Math.Max(index, 0));
            return chosen < 0 ? null : names[chosen];
        }
    }
}
