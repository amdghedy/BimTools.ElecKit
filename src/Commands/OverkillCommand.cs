using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Deletes detail lines that duplicate, or lie completely on top of, a longer detail line
    /// with the same line style. Works on the current selection, or the whole active view if
    /// nothing is selected.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class OverkillCommand : ElecCommand
    {
        private const double ToleranceFeet = 0.0005;   // about 0.15 mm
        private const int MaxLines = 4000;             // pairwise check, keep it responsive

        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is ViewSchedule)
            {
                TaskDialog.Show("Clean Duplicate Lines", "Open a plan, section or drafting view first.");
                return Result.Cancelled;
            }

            var selected = ctx.UiDoc.Selection.GetElementIds();
            IEnumerable<DetailCurve> curves;
            if (selected.Count > 0)
            {
                curves = selected.Select(id => ctx.Doc.GetElement(id)).OfType<DetailCurve>();
            }
            else
            {
                curves = new FilteredElementCollector(ctx.Doc, view.Id)
                    .OfClass(typeof(CurveElement))
                    .OfType<DetailCurve>();
            }

            var items = curves
                .Select(c => new { Id = c.Id, Line = c.GeometryCurve as Line, Style = StyleKey(c) })
                .Where(x => x.Line != null && x.Line.IsBound)
                .OrderByDescending(x => x.Line.Length)
                .ToList();

            if (items.Count > MaxLines)
            {
                TaskDialog.Show("Clean Duplicate Lines",
                    "There are " + items.Count + " detail lines in scope. Select fewer than " + MaxLines + " and try again.");
                return Result.Cancelled;
            }

            var removed = new HashSet<int>();
            var toDelete = new List<ElementId>();
            for (int i = 0; i < items.Count; i++)
            {
                if (removed.Contains(i)) continue;
                Line keep = items[i].Line;

                for (int j = i + 1; j < items.Count; j++)
                {
                    if (removed.Contains(j) || items[j].Style != items[i].Style) continue;
                    Line other = items[j].Line;
                    if (keep.Distance(other.GetEndPoint(0)) < ToleranceFeet &&
                        keep.Distance(other.GetEndPoint(1)) < ToleranceFeet)
                    {
                        removed.Add(j);
                        toDelete.Add(items[j].Id);
                    }
                }
            }

            if (toDelete.Count == 0)
            {
                TaskDialog.Show("Clean Duplicate Lines", "No duplicate or overlapping detail lines were found.");
                return Result.Succeeded;
            }

            ctx.Transact("Clean duplicate lines", () => ctx.Doc.Delete(toDelete));
            TaskDialog.Show("Clean Duplicate Lines", toDelete.Count + " duplicate/overlapping detail line(s) deleted.");
            return Result.Succeeded;
        }

        private static int StyleKey(CurveElement c)
        {
            Element style = c.LineStyle;
            return style == null ? -1 : style.Id.IntegerValue;
        }
    }
}
