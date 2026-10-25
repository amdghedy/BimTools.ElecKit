using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Turns a boundary or path drawn with detail lines into one continuous conduit run:
    /// lines that share end points are joined, straight stretches become one conduit,
    /// and elbows are added at the corners (including the closing corner of a loop).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BoundaryToConduitsCommand : ElecCommand
    {
        private const double JoinToleranceFeet = 0.003;
        private const double MinLegFeet = 0.01;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            var plan = ctx.Doc.ActiveView as ViewPlan;
            if (plan == null || plan.GenLevel == null)
            {
                TaskDialog.Show("Boundary to Conduits", "Open a plan view first.");
                return Result.Cancelled;
            }

            var sources = new List<DetailCurve>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                var curve = ctx.Doc.GetElement(id) as DetailCurve;
                if (curve != null && curve.GeometryCurve is Line) sources.Add(curve);
            }
            if (sources.Count == 0)
            {
                var filter = new PredicateFilter(e =>
                {
                    var c = e as DetailCurve;
                    return c != null && c.GeometryCurve is Line;
                });
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element, filter,
                    "Select the boundary detail lines, then click Finish"))
                {
                    var curve = ctx.Doc.GetElement(r) as DetailCurve;
                    if (curve != null) sources.Add(curve);
                }
            }
            if (sources.Count == 0) return Result.Cancelled;

            ConduitType type;
            double offsetMm;
            if (!ConduitPrompt.TryAsk(ctx.Doc, "Boundary to Conduits", out type, out offsetMm)) return Result.Cancelled;

            Level level = plan.GenLevel;
            double z = level.Elevation + UnitConv.MmToFeet(offsetMm);

            var lines = sources.Select(s => (Line)s.GeometryCurve).ToList();
            List<List<XYZ>> chains = LineChains.Build(lines, JoinToleranceFeet);

            int conduits = 0, elbows = 0, elbowsFailed = 0, failedRuns = 0, runs = 0;

            ctx.Transact("Boundary to conduits", () =>
            {
                foreach (List<XYZ> chain in chains)
                {
                    bool closed = chain.Count >= 4 && chain[0].DistanceTo(chain[chain.Count - 1]) <= JoinToleranceFeet;
                    List<XYZ> points = LineChains.RemoveStraightVertices(chain);
                    if (closed) points.RemoveAt(points.Count - 1);

                    int count = points.Count;
                    int segments = closed ? count : count - 1;
                    if (segments < 1 || (closed && count < 3)) continue;

                    using (var sub = new SubTransaction(ctx.Doc))
                    {
                        sub.Start();
                        try
                        {
                            var made = new List<Conduit>();
                            for (int i = 0; i < segments; i++)
                            {
                                XYZ a = Lift(points[i], z);
                                XYZ b = Lift(points[(i + 1) % count], z);
                                if (a.DistanceTo(b) < MinLegFeet)
                                    throw new InvalidOperationException("A segment is shorter than " + MinLegFeet + " ft.");
                                made.Add(Conduit.Create(ctx.Doc, type.Id, a, b, level.Id));
                            }

                            int runElbows = 0, runElbowsFailed = 0;
                            for (int i = 1; i < made.Count; i++)
                                Tally(ConduitFittings.TryElbow(ctx.Doc, made[i - 1], made[i], Lift(points[i], z)),
                                    ref runElbows, ref runElbowsFailed);
                            if (closed && made.Count > 1)
                                Tally(ConduitFittings.TryElbow(ctx.Doc, made[made.Count - 1], made[0], Lift(points[0], z)),
                                    ref runElbows, ref runElbowsFailed);

                            sub.Commit();
                            runs++;
                            conduits += made.Count;
                            elbows += runElbows;
                            elbowsFailed += runElbowsFailed;
                        }
                        catch (Exception ex)
                        {
                            sub.RollBack();
                            Log.Error(ex);
                            failedRuns++;
                        }
                    }
                }
            });

            string text = runs + " run(s): " + conduits + " conduit(s) and " + elbows + " elbow(s) created (" + type.Name + ").";
            if (elbowsFailed > 0) text += "\n" + elbowsFailed + " corner(s) have no elbow: check the conduit type's routing preferences.";
            if (failedRuns > 0) text += "\n" + failedRuns + " run(s) failed and were undone (see log).";
            TaskDialog.Show("Boundary to Conduits", text);
            return Result.Succeeded;
        }

        private static XYZ Lift(XYZ point, double z)
        {
            return new XYZ(point.X, point.Y, z);
        }

        private static void Tally(ElbowResult result, ref int made, ref int failed)
        {
            if (result == ElbowResult.Made) made++;
            else if (result == ElbowResult.Failed) failed++;
        }
    }
}
