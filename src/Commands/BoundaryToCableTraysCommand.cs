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
    /// <summary>Converts connected plan detail-line chains into cable tray runs with required elbows.</summary>
    [Transaction(TransactionMode.Manual)]
    public class BoundaryToCableTraysCommand : ElecCommand
    {
        private const double JoinToleranceFeet = 0.003;
        private const double MinLegFeet = 0.01;
        protected override Result Run(ToolContext ctx, ref string message)
        {
            ViewPlan plan = ctx.Doc.ActiveView as ViewPlan;
            if (plan == null || plan.GenLevel == null)
            {
                TaskDialog.Show("Boundary to Cable Trays", "Open a plan view first.");
                return Result.Cancelled;
            }
            var sources = new List<DetailCurve>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                DetailCurve curve = ctx.Doc.GetElement(id) as DetailCurve;
                if (curve != null && curve.GeometryCurve is Line) sources.Add(curve);
            }
            if (sources.Count == 0)
            {
                try
                {
                    foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                        new PredicateFilter(e => e is DetailCurve && ((DetailCurve)e).GeometryCurve is Line),
                        "Select connected boundary detail lines, then click Finish"))
                    {
                        DetailCurve curve = ctx.Doc.GetElement(r) as DetailCurve;
                        if (curve != null) sources.Add(curve);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            }
            if (sources.Count == 0) return Result.Cancelled;
            List<CableTrayType> types = new FilteredElementCollector(ctx.Doc).OfClass(typeof(CableTrayType))
                .Cast<CableTrayType>().OrderBy(t => t.Name).ToList();
            if (types.Count == 0) { TaskDialog.Show("Boundary to Cable Trays", "No cable tray types exist in this project."); return Result.Cancelled; }
            int typeIndex = Dialogs.Choose("Boundary to Cable Trays", "Cable tray type:", types.Select(t => t.Name).ToList(), 0);
            if (typeIndex < 0 || typeIndex >= types.Count) return Result.Cancelled;
            double offsetMm;
            string answer = Dialogs.Ask("Boundary to Cable Trays", "Height above plan level, in millimetres:",
                UnitConv.Format(ElecSettings.Current.EffectiveConduitOffsetMm));
            if (answer == null) return Result.Cancelled;
            if (!UnitConv.TryParseNumber(answer, out offsetMm) || double.IsNaN(offsetMm) || double.IsInfinity(offsetMm))
            {
                TaskDialog.Show("Boundary to Cable Trays", "Enter a finite numeric height in millimetres.");
                return Result.Cancelled;
            }
            CableTrayType trayType = types[typeIndex];
            Level level = plan.GenLevel;
            double z = level.Elevation + UnitConv.MmToFeet(offsetMm);
            List<List<XYZ>> chains = LineChains.Build(sources.Select(s => (Line)s.GeometryCurve).ToList(), JoinToleranceFeet);
            int runs = 0, trays = 0, elbows = 0, failedRuns = 0, failedFittings = 0;
            var failures = new List<string>();
            ctx.Transact("Boundary to cable trays", () =>
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
                            var made = new List<CableTray>();
                            for (int i = 0; i < segments; i++)
                            {
                                XYZ a = Lift(points[i], z), b = Lift(points[(i + 1) % count], z);
                                if (a.DistanceTo(b) < MinLegFeet) throw new InvalidOperationException("A segment is too short.");
                                made.Add(CableTray.Create(ctx.Doc, trayType.Id, a, b, level.Id));
                            }
                            int runElbows = 0;
                            for (int i = 1; i < made.Count; i++)
                            {
                                if (!TryElbow(ctx.Doc, made[i - 1], made[i], Lift(points[i], z)))
                                    throw new InvalidOperationException("Required elbow failed at vertex " + i + "; the run was rolled back.");
                                runElbows++;
                            }
                            if (closed && made.Count > 1)
                            {
                                if (!TryElbow(ctx.Doc, made[made.Count - 1], made[0], Lift(points[0], z)))
                                    throw new InvalidOperationException("Closing elbow failed; the closed run was rolled back.");
                                runElbows++;
                            }
                            sub.Commit();
                            runs++; trays += made.Count; elbows += runElbows;
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            failedRuns++; failedFittings++;
                            if (failures.Count < 8) failures.Add(ex.Message);
                            Log.Error(ex);
                        }
                    }
                }
            });
            string summary = "Runs created: " + runs + "\nCable tray segments: " + trays + "\nElbow fittings: " + elbows +
                "\nFailed/rolled-back runs: " + failedRuns;
            if (failures.Count > 0) summary += "\n\nFirst failures:\n" + string.Join("\n", failures);
            summary += "\n\nThis command uses the cable tray type's available routing preferences. Verify dimensions, elevations, clearances and connector continuity in Revit.";
            TaskDialog.Show("Boundary to Cable Trays", summary);
            return failedRuns > 0 && runs == 0 ? Result.Failed : Result.Succeeded;
        }
        private static XYZ Lift(XYZ point, double z) { return new XYZ(point.X, point.Y, z); }
        private static bool TryElbow(Document doc, CableTray first, CableTray second, XYZ corner)
        {
            Connector a = Nearest(first, corner), b = Nearest(second, corner);
            if (a == null || b == null) return false;
            try { doc.Create.NewElbowFitting(a, b); return true; }
            catch (Exception ex) { Log.Error(ex); return false; }
        }
        private static Connector Nearest(CableTray tray, XYZ point)
        {
            Connector best = null; double distance = double.MaxValue;
            foreach (Connector connector in tray.ConnectorManager.Connectors)
            {
                double d = connector.Origin.DistanceTo(point);
                if (d < distance) { distance = d; best = connector; }
            }
            return best;
        }
    }
}