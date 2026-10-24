using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Splits straight detail lines wherever another detail line crosses or touches them.
    /// Works on the selection, or the whole active view if nothing is selected.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SplitLinesCommand : ElecCommand
    {
        private const double Tolerance = 0.003;   // feet
        private const int MaxLines = 1500;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is ViewSchedule || view is View3D)
            {
                TaskDialog.Show("Split Lines", "Open a plan, section or drafting view first.");
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
                TaskDialog.Show("Split Lines", "Select at least two straight detail lines, or run this in a view that has them.");
                return Result.Cancelled;
            }
            if (elements.Count > MaxLines)
            {
                TaskDialog.Show("Split Lines", "There are " + elements.Count + " lines in scope. Select fewer than " + MaxLines + " and try again.");
                return Result.Cancelled;
            }

            var lines = elements.Select(c => (Line)c.GeometryCurve).ToArray();
            var cuts = new List<double>[lines.Length];
            for (int i = 0; i < cuts.Length; i++) cuts[i] = new List<double>();

            for (int i = 0; i < lines.Length; i++)
            {
                for (int j = i + 1; j < lines.Length; j++)
                {
                    XYZ point;
                    if (!FilletMath.TryIntersect(lines[i], lines[j], out point)) continue;

                    double ti = FilletMath.Param(lines[i], point);
                    double tj = FilletMath.Param(lines[j], point);
                    double li = lines[i].Length;
                    double lj = lines[j].Length;
                    if (ti < -Tolerance || ti > li + Tolerance || tj < -Tolerance || tj > lj + Tolerance) continue;

                    if (ti > FilletMath.ShortCurveFeet && ti < li - FilletMath.ShortCurveFeet) cuts[i].Add(ti);
                    if (tj > FilletMath.ShortCurveFeet && tj < lj - FilletMath.ShortCurveFeet) cuts[j].Add(tj);
                }
            }

            int linesSplit = 0;
            int segmentsAdded = 0;

            ctx.Transact("Split lines", () =>
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    List<double> positions = CleanCuts(cuts[i], lines[i].Length);
                    if (positions.Count == 0) continue;

                    Line line = lines[i];
                    XYZ start = line.GetEndPoint(0);
                    XYZ direction = line.Direction;

                    var stops = new List<double> { 0.0 };
                    stops.AddRange(positions);
                    stops.Add(line.Length);

                    for (int k = 0; k < stops.Count - 1; k++)
                    {
                        XYZ a = start.Add(direction.Multiply(stops[k]));
                        XYZ b = start.Add(direction.Multiply(stops[k + 1]));
                        Line segment = Line.CreateBound(a, b);

                        if (k == 0)
                        {
                            elements[i].SetGeometryCurve(segment, true);
                        }
                        else
                        {
                            DetailCurve created = ctx.Doc.Create.NewDetailCurve(view, segment);
                            created.LineStyle = elements[i].LineStyle;
                            segmentsAdded++;
                        }
                    }
                    linesSplit++;
                }
            });

            TaskDialog.Show("Split Lines", linesSplit == 0
                ? "No crossings were found, so nothing was split."
                : linesSplit + " line(s) split, " + segmentsAdded + " new segment(s) created.");
            return Result.Succeeded;
        }

        /// <summary>Sorted cut positions, dropping ones that would leave a segment too short.</summary>
        private static List<double> CleanCuts(List<double> raw, double length)
        {
            var result = new List<double>();
            double last = 0.0;
            foreach (double t in raw.OrderBy(x => x))
            {
                if (t - last < FilletMath.ShortCurveFeet * 2) continue;
                if (length - t < FilletMath.ShortCurveFeet) continue;
                result.Add(t);
                last = t;
            }
            return result;
        }
    }
}
