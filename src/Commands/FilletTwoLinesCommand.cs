using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Pick two detail lines (click the part of each you want to keep) and round the corner. Repeats until Esc.</summary>
    [Transaction(TransactionMode.Manual)]
    public class FilletTwoLinesCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is ViewSchedule || view is View3D)
            {
                TaskDialog.Show("Fillet Two Lines", "Open a plan, section or drafting view first.");
                return Result.Cancelled;
            }

            double radius = UnitConv.MmToFeet(ElecSettings.Current.EffectiveFilletRadiusMm);
            var filter = new PredicateFilter(e =>
            {
                var curve = e as DetailCurve;
                return curve != null && curve.GeometryCurve is Line;
            });

            int done = 0;
            try
            {
                while (true)
                {
                    Reference r1 = ctx.UiDoc.Selection.PickObject(ObjectType.Element, filter,
                        "Pick the first detail line (click the part to keep). Esc to finish.");
                    Reference r2 = ctx.UiDoc.Selection.PickObject(ObjectType.Element, filter,
                        "Pick the second detail line (click the part to keep).");

                    if (r1.ElementId.Val() == r2.ElementId.Val())
                    {
                        TaskDialog.Show("Fillet Two Lines", "Pick two different lines.");
                        continue;
                    }

                    var c1 = (DetailCurve)ctx.Doc.GetElement(r1);
                    var c2 = (DetailCurve)ctx.Doc.GetElement(r2);
                    XYZ p1 = r1.GlobalPoint;
                    XYZ p2 = r2.GlobalPoint;

                    string error = null;
                    bool ok = ctx.TryTransact("Fillet two lines", () =>
                    {
                        Line n1, n2;
                        return FilletOps.Apply(ctx.Doc, view, c1, p1, c2, p2, radius, out n1, out n2, out error);
                    });

                    if (ok) done++;
                    else TaskDialog.Show("Fillet Two Lines", error ?? "The fillet could not be made.");
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Esc ends the loop
            }

            return done > 0 ? Result.Succeeded : Result.Cancelled;
        }
    }
}
