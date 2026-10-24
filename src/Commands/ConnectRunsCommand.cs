using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Connects picked elements, in pick order, with conduit runs at a set height above the plan level.
    /// Each pair gets its own run (direct, or an L shape with an elbow at the corner).
    /// Runs start and end at each element's location point.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ConnectRunsCommand : ElecCommand
    {
        private const double MinLegFeet = 0.01;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            var plan = ctx.Doc.ActiveView as ViewPlan;
            if (plan == null || plan.GenLevel == null)
            {
                TaskDialog.Show("Connect Runs", "Open a plan view first.");
                return Result.Cancelled;
            }

            IList<Reference> picked = ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                new PredicateFilter(e => Anchors.Of(e) != null),
                "Pick the elements in the order they should be connected, then click Finish");
            if (picked.Count < 2)
            {
                TaskDialog.Show("Connect Runs", "Pick at least two elements.");
                return Result.Cancelled;
            }

            ConduitType type;
            double offsetMm;
            if (!ConduitPrompt.TryAsk(ctx.Doc, "Connect Runs", out type, out offsetMm)) return Result.Cancelled;

            int shape = Dialogs.Choose("Connect Runs", "Route shape:", new List<string>
            {
                "Direct (one straight conduit)",
                "L shape: along X first, then Y",
                "L shape: along Y first, then X"
            }, 1);
            if (shape < 0) return Result.Cancelled;

            Level level = plan.GenLevel;
            double z = level.Elevation + Units.MmToFeet(offsetMm);
            var points = new List<XYZ>();
            foreach (Reference r in picked)
            {
                XYZ a = Anchors.Of(ctx.Doc.GetElement(r));
                points.Add(new XYZ(a.X, a.Y, z));
            }

            int conduits = 0, elbows = 0, elbowsFailed = 0, skippedPairs = 0, failedPairs = 0;

            ctx.Transact("Connect runs", () =>
            {
                for (int i = 0; i < points.Count - 1; i++)
                {
                    XYZ a = points[i];
                    XYZ b = points[i + 1];
                    double dx = Math.Abs(b.X - a.X);
                    double dy = Math.Abs(b.Y - a.Y);

                    if (dx < MinLegFeet && dy < MinLegFeet) { skippedPairs++; continue; }

                    using (var sub = new SubTransaction(ctx.Doc))
                    {
                        sub.Start();
                        try
                        {
                            bool direct = shape == 0 || dx < MinLegFeet || dy < MinLegFeet;
                            if (direct)
                            {
                                Conduit.Create(ctx.Doc, type.Id, a, b, level.Id);
                                conduits++;
                            }
                            else
                            {
                                XYZ corner = shape == 1 ? new XYZ(b.X, a.Y, z) : new XYZ(a.X, b.Y, z);
                                Conduit first = Conduit.Create(ctx.Doc, type.Id, a, corner, level.Id);
                                Conduit second = Conduit.Create(ctx.Doc, type.Id, corner, b, level.Id);
                                conduits += 2;

                                try
                                {
                                    Connector c1 = Nearest(first, corner);
                                    Connector c2 = Nearest(second, corner);
                                    if (c1 != null && c2 != null)
                                    {
                                        ctx.Doc.Create.NewElbowFitting(c1, c2);
                                        elbows++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex);
                                    elbowsFailed++;
                                }
                            }
                            sub.Commit();
                        }
                        catch (Exception ex)
                        {
                            sub.RollBack();
                            Log.Error(ex);
                            failedPairs++;
                        }
                    }
                }
            });

            string text = conduits + " conduit(s) and " + elbows + " elbow(s) created (" + type.Name + ").";
            if (elbowsFailed > 0) text += "\n" + elbowsFailed + " corner(s) have no elbow: check the conduit type's routing preferences.";
            if (skippedPairs > 0) text += "\n" + skippedPairs + " pair(s) skipped: the elements are at the same spot.";
            if (failedPairs > 0) text += "\n" + failedPairs + " pair(s) failed (see log).";
            TaskDialog.Show("Connect Runs", text);
            return Result.Succeeded;
        }

        private static Connector Nearest(Conduit conduit, XYZ point)
        {
            Connector best = null;
            double bestDistance = double.MaxValue;
            foreach (Connector connector in conduit.ConnectorManager.Connectors)
            {
                double distance = connector.Origin.DistanceTo(point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = connector;
                }
            }
            return best;
        }
    }
}
