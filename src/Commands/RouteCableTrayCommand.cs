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
    /// <summary>Routes cable tray between picked anchors in order; each L-turn/fitting is isolated in a subtransaction.</summary>
    [Transaction(TransactionMode.Manual)]
    public class RouteCableTrayCommand : ElecCommand
    {
        private const double MinLegFeet = 0.01;
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var plan = ctx.Doc.ActiveView as ViewPlan;
            if (plan == null || plan.GenLevel == null)
            {
                TaskDialog.Show("Route Cable Tray", "Open a plan view first so the route level is explicit.");
                return Result.Cancelled;
            }
            IList<Reference> picked;
            try
            {
                picked = ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                    new PredicateFilter(e => Anchors.Of(e) != null), "Pick route anchors in order, then click Finish");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            if (picked == null || picked.Count < 2)
            {
                TaskDialog.Show("Route Cable Tray", "Pick at least two elements with a location or bounding box.");
                return Result.Cancelled;
            }
            List<CableTrayType> types = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(CableTrayType)).Cast<CableTrayType>().OrderBy(t => t.Name).ToList();
            if (types.Count == 0)
            {
                TaskDialog.Show("Route Cable Tray", "No cable tray types exist. Load or create one first.");
                return Result.Cancelled;
            }
            int typeIndex = Dialogs.Choose("Route Cable Tray", "Cable tray type:", types.Select(t => t.Name).ToList(), 0);
            if (typeIndex < 0 || typeIndex >= types.Count) return Result.Cancelled;
            string offsetText = Dialogs.Ask("Route Cable Tray", "Height above plan level, in millimetres:",
                UnitConv.Format(ElecSettings.Current.EffectiveConduitOffsetMm));
            double offsetMm;
            if (offsetText == null) return Result.Cancelled;
            if (!UnitConv.TryParseNumber(offsetText, out offsetMm) || double.IsNaN(offsetMm) || double.IsInfinity(offsetMm))
            {
                TaskDialog.Show("Route Cable Tray", "Enter a finite numeric height in millimetres.");
                return Result.Cancelled;
            }
            int shape = Dialogs.Choose("Route Cable Tray", "Route shape:", new List<string>
            {
                "Direct (one straight tray segment)", "L shape: along X first, then Y", "L shape: along Y first, then X", "Multi-segment path through picked anchors (elbows at turns)", "Avoid active-view obstacles (automatic orthogonal route)"
            }, 1);
            if (shape < 0) return Result.Cancelled;
            CableTrayType trayType = types[typeIndex];
            Level level = plan.GenLevel;
            double z = level.Elevation + UnitConv.MmToFeet(offsetMm);
            var points = new List<XYZ>();
            foreach (Reference reference in picked)
            {
                XYZ anchor = Anchors.Of(ctx.Doc.GetElement(reference));
                if (anchor != null) points.Add(new XYZ(anchor.X, anchor.Y, z));
            }
            if (points.Count < 2) return Result.Cancelled;
            double clearanceFeet = 0.0;
            if (shape == 4)
            {
                string clearanceText = Dialogs.Ask("Route Cable Tray", "Minimum horizontal clearance from obstacles, in millimetres:", "150");
                double clearanceMm;
                if (clearanceText == null) return Result.Cancelled;
                if (!UnitConv.TryParseNumber(clearanceText, out clearanceMm) || double.IsNaN(clearanceMm) ||
                    double.IsInfinity(clearanceMm) || clearanceMm < 0)
                {
                    TaskDialog.Show("Route Cable Tray", "Clearance must be a finite, non-negative number.");
                    return Result.Cancelled;
                }
                clearanceFeet = UnitConv.MmToFeet(clearanceMm);
            }

            if (shape == 4)
                return RouteAroundObstacles(ctx, plan, points, picked, trayType, level, clearanceFeet);
            if (shape == 3)
            {
                int pathTrays = 0, pathElbows = 0;
                string pathFailure = null;
                ctx.Transact("Route cable tray polyline", () =>
                {
                    using (var sub = new SubTransaction(ctx.Doc))
                    {
                        sub.Start();
                        try
                        {
                            var made = new List<CableTray>();
                            for (int i = 0; i < points.Count - 1; i++)
                            {
                                if (points[i].DistanceTo(points[i + 1]) < MinLegFeet)
                                    throw new InvalidOperationException("Route anchors " + (i + 1) + " and " + (i + 2) + " are coincident.");
                                made.Add(CableTray.Create(ctx.Doc, trayType.Id, points[i], points[i + 1], level.Id));
                            }
                            for (int i = 1; i < made.Count; i++)
                            {
                                XYZ corner = points[i];
                                XYZ incoming = corner.Subtract(points[i - 1]).Normalize();
                                XYZ outgoing = points[i + 1].Subtract(corner).Normalize();
                                double dot = incoming.DotProduct(outgoing);
                                if (dot > 0.99999) continue;
                                if (dot < -0.99999)
                                    throw new InvalidOperationException("Route doubles back at anchor " + (i + 1) + "; split the route into separate runs.");
                                Connector c1 = Nearest(made[i - 1], corner);
                                Connector c2 = Nearest(made[i], corner);
                                if (c1 == null || c2 == null ||
                                    c1.Origin.DistanceTo(corner) > 0.1 || c2.Origin.DistanceTo(corner) > 0.1)
                                    throw new InvalidOperationException("Revit connectors are not within 0.1 ft of turning anchor " + (i + 1) + ".");
                                ctx.Doc.Create.NewElbowFitting(c1, c2);
                                pathElbows++;
                            }
                            sub.Commit();
                            pathTrays = made.Count;
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            pathFailure = ex.Message;
                            Log.Error(ex);
                        }
                    }
                });
                string pathSummary = pathFailure == null
                    ? "Multi-segment cable tray path created.\nSegments: " + pathTrays + "\nElbows: " + pathElbows
                    : "The complete cable tray path was rolled back.\nReason: " + pathFailure;
                pathSummary += "\n\nVerify connector continuity, dimensions, clearance and tray routing preferences in Revit.";
                TaskDialog.Show("Route Cable Tray", pathSummary);
                return pathFailure == null ? Result.Succeeded : Result.Failed;
            }
            int trays = 0, elbows = 0, failedPairs = 0, skippedPairs = 0;
            var failures = new List<string>();
            ctx.Transact("Route cable tray", () =>
            {
                for (int i = 0; i < points.Count - 1; i++)
                {
                    XYZ a = points[i], b = points[i + 1];
                    double dx = Math.Abs(b.X - a.X), dy = Math.Abs(b.Y - a.Y);
                    if (a.DistanceTo(b) < MinLegFeet) { skippedPairs++; continue; }
                    using (var sub = new SubTransaction(ctx.Doc))
                    {
                        sub.Start();
                        try
                        {
                            if (shape == 0 || dx < MinLegFeet || dy < MinLegFeet)
                            {
                                CableTray.Create(ctx.Doc, trayType.Id, a, b, level.Id);
                                trays++;
                            }
                            else
                            {
                                XYZ corner = shape == 1 ? new XYZ(b.X, a.Y, z) : new XYZ(a.X, b.Y, z);
                                if (a.DistanceTo(corner) < MinLegFeet || corner.DistanceTo(b) < MinLegFeet)
                                    throw new InvalidOperationException("The route produces a zero-length leg.");
                                CableTray first = CableTray.Create(ctx.Doc, trayType.Id, a, corner, level.Id);
                                CableTray second = CableTray.Create(ctx.Doc, trayType.Id, corner, b, level.Id);
                                Connector c1 = Nearest(first, corner), c2 = Nearest(second, corner);
                                if (c1 == null || c2 == null) throw new InvalidOperationException("Revit did not expose connectors at the corner.");
                                ctx.Doc.Create.NewElbowFitting(c1, c2);
                                trays += 2;
                                elbows++;
                            }
                            sub.Commit();
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            failedPairs++;
                            if (failures.Count < 8) failures.Add("Pair " + (i + 1) + ": " + ex.Message);
                            Log.Error(ex);
                        }
                    }
                }
            });
            string summary = "Cable trays created: " + trays + "\nElbow fittings created: " + elbows +
                "\nFailed route pairs (rolled back): " + failedPairs + "\nSkipped zero-length pairs: " + skippedPairs;
            if (failures.Count > 0) summary += "\n\nFirst failures:\n" + string.Join("\n", failures);
            summary += "\n\nFitting creation depends on tray routing preferences. Verify elevations, sizes, clearances and connectivity in Revit.";
            TaskDialog.Show("Route Cable Tray", summary);
            return failedPairs > 0 && trays == 0 ? Result.Failed : Result.Succeeded;
        }

        private static Result RouteAroundObstacles(ToolContext ctx, ViewPlan plan, IList<XYZ> points,
            IList<Reference> picked, CableTrayType trayType, Level level, double clearanceFeet)
        {
            var excluded = new HashSet<long>(picked.Select(r => (long)r.ElementId.IntegerValue));
            double minX = points.Min(p => p.X) - UnitConv.MmToFeet(5000);
            double maxX = points.Max(p => p.X) + UnitConv.MmToFeet(5000);
            double minY = points.Min(p => p.Y) - UnitConv.MmToFeet(5000);
            double maxY = points.Max(p => p.Y) + UnitConv.MmToFeet(5000);
            double z = points[0].Z;
            var categories = new[]
            {
                BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors, BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_ElectricalEquipment,
                BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingFixtures,
                BuiltInCategory.OST_LightingDevices, BuiltInCategory.OST_Conduit, BuiltInCategory.OST_ConduitFitting,
                BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayFitting, BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_MechanicalEquipment
            };
            var obstacles = new List<RouteObstacle2D>();
            foreach (BuiltInCategory category in categories)
            {
                foreach (Element e in new FilteredElementCollector(ctx.Doc, plan.Id).OfCategory(category).WhereElementIsNotElementType())
                {
                    if (excluded.Contains(e.Id.IntegerValue)) continue;
                    BoundingBoxXYZ box;
                    try { box = e.get_BoundingBox(plan); } catch { continue; }
                    if (box == null) continue;
                    XYZ boxMin, boxMax;
                    AxisAlignedBounds(box, out boxMin, out boxMax);
                    if (boxMax.Z < z - UnitConv.MmToFeet(50) || boxMin.Z > z + UnitConv.MmToFeet(50)) continue;
                    if (boxMax.X < minX || boxMin.X > maxX || boxMax.Y < minY || boxMin.Y > maxY) continue;
                    obstacles.Add(new RouteObstacle2D(boxMin.X, boxMin.Y, boxMax.X, boxMax.Y));
                    if (obstacles.Count > 100)
                    {
                        TaskDialog.Show("Route Cable Tray", "More than 100 relevant obstacles were found within the routing window. Narrow the active view or split the route, then retry; obstacles are never silently ignored.");
                        return Result.Cancelled;
                    }
                }
            }

            int created = 0, elbows = 0, failed = 0;
            var failures = new List<string>();
            ctx.Transact("Route cable tray avoiding obstacles", () =>
            {
                for (int pair = 0; pair < points.Count - 1; pair++)
                {
                    XYZ a = points[pair], b = points[pair + 1];
                    IList<RoutePoint2D> path = OrthogonalRoutePlanner.Plan(a.X, a.Y, b.X, b.Y, obstacles, clearanceFeet);
                    if (path == null || path.Count < 2)
                    {
                        failed++;
                        if (failures.Count < 8) failures.Add("Pair " + (pair + 1) + ": no clear orthogonal route found.");
                        continue;
                    }
                    using (var sub = new SubTransaction(ctx.Doc))
                    {
                        sub.Start();
                        try
                        {
                            var made = new List<CableTray>();
                            for (int i = 0; i < path.Count - 1; i++)
                            {
                                XYZ start = new XYZ(path[i].X, path[i].Y, a.Z);
                                XYZ end = new XYZ(path[i + 1].X, path[i + 1].Y, a.Z);
                                if (start.DistanceTo(end) < MinLegFeet)
                                    throw new InvalidOperationException("Planner returned a zero-length segment.");
                                made.Add(CableTray.Create(ctx.Doc, trayType.Id, start, end, level.Id));
                            }
                            int pairElbows = 0;
                            for (int i = 1; i < made.Count; i++)
                            {
                                XYZ corner = new XYZ(path[i].X, path[i].Y, a.Z);
                                Connector c1 = Nearest(made[i - 1], corner), c2 = Nearest(made[i], corner);
                                if (c1 == null || c2 == null || c1.Origin.DistanceTo(corner) > 0.1 || c2.Origin.DistanceTo(corner) > 0.1)
                                    throw new InvalidOperationException("Tray connectors do not meet at an automatic route corner.");
                                ctx.Doc.Create.NewElbowFitting(c1, c2);
                                pairElbows++;
                            }
                            sub.Commit();
                            created += made.Count;
                            elbows += pairElbows;
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            failed++;
                            if (failures.Count < 8) failures.Add("Pair " + (pair + 1) + ": " + ex.Message);
                            Log.Error(ex);
                        }
                    }
                }
            });
            TaskDialog.Show("Route Cable Tray", "Obstacle-aware routing finished.\nSegments created: " + created +
                "\nElbows created: " + elbows + "\nPairs failed/rolled back: " + failed +
                "\nClearance requested: " + UnitConv.FeetToMm(clearanceFeet).ToString("0.##") + " mm" +
                (failures.Count > 0 ? "\n\nFirst failures:\n" + string.Join("\n", failures) : "") +
                "\n\nThis is 2D axis-aligned routing from active-view bounding boxes, not exact-solid clearance. Verify the resulting route in Revit.");
            return failed > 0 && created == 0 ? Result.Failed : Result.Succeeded;
        }

        private static void AxisAlignedBounds(BoundingBoxXYZ box, out XYZ min, out XYZ max)
        {
            Transform t = box.Transform ?? Transform.Identity;
            min = new XYZ(double.MaxValue, double.MaxValue, double.MaxValue);
            max = new XYZ(double.MinValue, double.MinValue, double.MinValue);
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                XYZ p = t.OfPoint(new XYZ(x == 0 ? box.Min.X : box.Max.X,
                    y == 0 ? box.Min.Y : box.Max.Y, z == 0 ? box.Min.Z : box.Max.Z));
                min = new XYZ(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new XYZ(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }
        }

        private static Connector Nearest(CableTray tray, XYZ point)
        {
            Connector best = null;
            double bestDistance = double.MaxValue;
            foreach (Connector connector in tray.ConnectorManager.Connectors)
            {
                double distance = connector.Origin.DistanceTo(point);
                if (distance < bestDistance) { bestDistance = distance; best = connector; }
            }
            return best;
        }
    }
}