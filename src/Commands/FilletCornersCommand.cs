using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Rounds every corner where two straight detail lines meet (or nearly meet).
    /// Works on the selection, or the whole active view if nothing is selected.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class FilletCornersCommand : ElecCommand
    {
        private const double GapFeet = 0.05;   // line ends this close to the corner count as meeting (about 15 mm)
        private const int MaxLines = 2000;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is ViewSchedule || view is View3D)
            {
                TaskDialog.Show("Fillet Corners", "Open a plan, section or drafting view first.");
                return Result.Cancelled;
            }

            var selected = ctx.UiDoc.Selection.GetElementIds();
            IEnumerable<DetailCurve> source;
            if (selected.Count > 0)
                source = selected.Select(id => ctx.Doc.GetElement(id)).OfType<DetailCurve>();
            else
                source = new FilteredElementCollector(ctx.Doc, view.Id).OfClass(typeof(CurveElement)).OfType<DetailCurve>();

            var elements = source.Where(c => c.GeometryCurve is Line).ToList();
            if (elements.Count < 2)
            {
                TaskDialog.Show("Fillet Corners", "Select at least two straight detail lines, or run this in a view that has them.");
                return Result.Cancelled;
            }
            if (elements.Count > MaxLines)
            {
                TaskDialog.Show("Fillet Corners", "There are " + elements.Count + " lines in scope. Select fewer than " + MaxLines + " and try again.");
                return Result.Cancelled;
            }

            double radius = Units.MmToFeet(Settings.Current.EffectiveFilletRadiusMm);
            var geometry = elements.Select(c => (Line)c.GeometryCurve).ToArray();
            var usedEnds = new HashSet<string>();
            var problems = new Dictionary<string, int>();
            int made = 0;

            ctx.Transact("Fillet corners", () =>
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    for (int j = i + 1; j < elements.Count; j++)
                    {
                        XYZ corner;
                        if (!FilletMath.TryIntersect(geometry[i], geometry[j], out corner)) continue;

                        int near1 = FilletMath.NearEnd(geometry[i], corner);
                        int near2 = FilletMath.NearEnd(geometry[j], corner);
                        if (geometry[i].GetEndPoint(near1).DistanceTo(corner) > GapFeet) continue;
                        if (geometry[j].GetEndPoint(near2).DistanceTo(corner) > GapFeet) continue;

                        string key1 = i + ":" + near1;
                        string key2 = j + ":" + near2;
                        if (usedEnds.Contains(key1) || usedEnds.Contains(key2)) continue;

                        Line new1, new2;
                        string error;
                        if (FilletOps.Apply(ctx.Doc, view, elements[i], null, elements[j], null, radius,
                                out new1, out new2, out error))
                        {
                            geometry[i] = new1;
                            geometry[j] = new2;
                            usedEnds.Add(key1);
                            usedEnds.Add(key2);
                            made++;
                        }
                        else
                        {
                            int count;
                            problems.TryGetValue(error, out count);
                            problems[error] = count + 1;
                        }
                    }
                }
            });

            string text = made + " corner(s) filleted at " + Units.Format(Settings.Current.EffectiveFilletRadiusMm) + " mm.";
            if (problems.Count > 0)
                text += "\n\nSkipped:\n" + string.Join("\n", problems.Select(p => "- " + p.Key + " (" + p.Value + ")"));
            TaskDialog.Show("Fillet Corners", text);
            return Result.Succeeded;
        }
    }
}
