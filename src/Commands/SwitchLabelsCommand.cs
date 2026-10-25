using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Writes switch labels (a, b, c / A, B, C / 1, 2, 3 or one fixed text) onto lighting devices.</summary>
    [Transaction(TransactionMode.Manual)]
    public class SwitchLabelsCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            int mode = Dialogs.Choose("Switch Labels", "Label style:", new List<string>
            {
                "Letters a, b, c ... in pick order",
                "Capital letters A, B, C ... in pick order",
                "Numbers 1, 2, 3 ... in pick order",
                "The same text on every switch"
            }, 0);
            if (mode < 0) return Result.Cancelled;

            var filter = new PredicateFilter(IsSwitch);
            var switches = new List<Element>();

            if (mode == 3)
            {
                foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
                {
                    Element e = ctx.Doc.GetElement(id);
                    if (e != null && IsSwitch(e)) switches.Add(e);
                }
            }
            if (switches.Count == 0)
            {
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element, filter,
                    "Pick the switches in labelling order, then click Finish"))
                    switches.Add(ctx.Doc.GetElement(r));
            }
            if (switches.Count == 0) return Result.Cancelled;

            string fixedText = null;
            if (mode == 3)
            {
                fixedText = Dialogs.Ask("Switch Labels", "Label text for all selected switches:", "");
                if (fixedText == null) return Result.Cancelled;
            }

            string parameterName = ParameterChooser.Choose(switches[0], "Switch Labels", ElecSettings.Current.LastLabelParam);
            if (parameterName == null) return Result.Cancelled;
            ElecSettings.Current.LastLabelParam = parameterName;
            ElecSettings.Current.Save();

            int written = 0, skipped = 0;
            ctx.Transact("Switch labels", () =>
            {
                for (int i = 0; i < switches.Count; i++)
                {
                    string text = mode == 0 ? Letters(i, false)
                                : mode == 1 ? Letters(i, true)
                                : mode == 2 ? (i + 1).ToString()
                                : fixedText;

                    Parameter p = switches[i].LookupParameter(parameterName);
                    if (p == null || p.IsReadOnly || p.StorageType != StorageType.String) { skipped++; continue; }
                    p.Set(text);
                    written++;
                }
            });

            string result = written + " switch label(s) written to \"" + parameterName + "\".";
            if (skipped > 0) result += "\n" + skipped + " element(s) skipped: no writable text parameter with that name.";
            TaskDialog.Show("Switch Labels", result);
            return Result.Succeeded;
        }

        private static bool IsSwitch(Element e)
        {
            return e is FamilyInstance && e.Category != null &&
                   e.Category.Id.Val() == (int)BuiltInCategory.OST_LightingDevices;
        }

        /// <summary>0 -> a, 25 -> z, 26 -> aa ...</summary>
        private static string Letters(int index, bool upper)
        {
            string s = "";
            int n = index;
            do
            {
                s = (char)((upper ? 'A' : 'a') + n % 26) + s;
                n = n / 26 - 1;
            } while (n >= 0);
            return s;
        }
    }
}
