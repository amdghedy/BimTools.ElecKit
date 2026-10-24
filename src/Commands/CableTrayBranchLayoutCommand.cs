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
    /// <summary>Builds a T branch from three picked anchors and creates a tee (and branch elbow if required).</summary>
    [Transaction(TransactionMode.Manual)]
    public class CableTrayBranchLayoutCommand : ElecCommand
    {
        private const double MinLegFeet = 0.01;
        private const double AxisToleranceFeet = 0.0328084; // 10 mm

        protected override Result Run(ToolContext ctx, ref string message)
        {
            ViewPlan plan = ctx.Doc.ActiveView as ViewPlan;
            if (plan == null || plan.GenLevel == null)
            {
                TaskDialog.Show("Cable Tray Branch Layout", "Open a plan view first.");
                return Result.Cancelled;
            }
            IList<Reference> picked;
            try
            {
                picked = ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                    new PredicateFilter(e => Anchors.Of(e) != null),
                    "Pick main-run start, main-run end, then branch endpoint, in that order");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            if (picked == null || picked.Count != 3)
            {
                TaskDialog.Show("Cable Tray Branch Layout", "Pick exactly three anchors in order: main start, main end, branch endpoint.");
                return Result.Cancelled;
            }

            var points = new List<XYZ>();
            foreach (Reference r in picked)
            {
                XYZ p = Anchors.Of(ctx.Doc.GetElement(r));
                if (p == null) return Result.Cancelled;
                points.Add(p);
            }
            Level level = plan.GenLevel;
            double z = level.Elevation;
            XYZ mainStart = new XYZ(points[0].X, points[0].Y, z);
            XYZ mainEnd = new XYZ(points[1].X, points[1].Y, z);
            XYZ branchEnd = new XYZ(points[2].X, points[2].Y, z);
            bool horizontalMain = Math.Abs(mainStart.Y - mainEnd.Y) <= AxisToleranceFeet;
            bool verticalMain = Math.Abs(mainStart.X - mainEnd.X) <= AxisToleranceFeet;
            if (horizontalMain == verticalMain)
            {
                TaskDialog.Show("Cable Tray Branch Layout", "The main run must be a non-zero, axis-aligned straight segment (horizontal or vertical).");
                return Result.Cancelled;
            }
            XYZ tee = horizontalMain
                ? new XYZ(branchEnd.X, (mainStart.Y + mainEnd.Y) / 2.0, z)
                : new XYZ((mainStart.X + mainEnd.X) / 2.0, branchEnd.Y, z);
            double mainLength = mainStart.DistanceTo(mainEnd);
            if (mainLength < MinLegFeet || tee.DistanceTo(mainStart) < MinLegFeet ||
                tee.DistanceTo(mainEnd) < MinLegFeet || tee.DistanceTo(branchEnd) < MinLegFeet)
            {
                TaskDialog.Show("Cable Tray Branch Layout", "The tee point must fall strictly inside the main run and all branch legs must be non-zero.");
                return Result.Cancelled;
            }

            var types = new FilteredElementCollector(ctx.Doc).OfClass(typeof(CableTrayType))
                .Cast<CableTrayType>().OrderBy(t => t.Name).ToList();
            if (types.Count == 0) { TaskDialog.Show("Cable Tray Branch Layout", "No cable tray types exist in this project."); return Result.Cancelled; }
            int typeIndex = Dialogs.Choose("Cable Tray Branch Layout", "Cable tray type:", types.Select(t => t.Name).ToList(), 0);
            if (typeIndex < 0 || typeIndex >= types.Count) return Result.Cancelled;
            double offsetMm;
            string offsetText = Dialogs.Ask("Cable Tray Branch Layout", "Height above plan level, in millimetres:",
                UnitConv.Format(ElecSettings.Current.EffectiveConduitOffsetMm));
            if (offsetText == null) return Result.Cancelled;
            if (!UnitConv.TryParseNumber(offsetText, out offsetMm) || double.IsNaN(offsetMm) ||
                double.IsInfinity(offsetMm))
            {
                TaskDialog.Show("Cable Tray Branch Layout", "Enter a finite numeric height in millimetres.");
                return Result.Cancelled;
            }
            double lift = UnitConv.MmToFeet(offsetMm);
            mainStart = new XYZ(mainStart.X, mainStart.Y, z + lift);
            mainEnd = new XYZ(mainEnd.X, mainEnd.Y, z + lift);
            branchEnd = new XYZ(branchEnd.X, branchEnd.Y, z + lift);
            tee = new XYZ(tee.X, tee.Y, z + lift);
            string failure = null;
            ctx.Transact("Create cable tray branch layout", () =>
            {
                using (var sub = new SubTransaction(ctx.Doc))
                {
                    sub.Start();
                    try
                    {
                        CableTray mainA = CableTray.Create(ctx.Doc, types[typeIndex].Id, mainStart, tee, level.Id);
                        CableTray mainB = CableTray.Create(ctx.Doc, types[typeIndex].Id, tee, mainEnd, level.Id);
                        CableTray branchLast = CableTray.Create(ctx.Doc, types[typeIndex].Id, branchEnd, tee, level.Id);
                        Connector mainConnectorA = Nearest(mainA, tee);
                        Connector mainConnectorB = Nearest(mainB, tee);
                        Connector branchConnector = Nearest(branchLast, tee);
                        if (mainConnectorA == null || mainConnectorB == null || branchConnector == null)
                            throw new InvalidOperationException("Tee connectors were not found at the branch junction.");
                        ctx.Doc.Create.NewTeeFitting(mainConnectorA, mainConnectorB, branchConnector);
                        sub.Commit();
                    }
                    catch (Exception ex)
                    {
                        if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                        failure = ex.Message;
                        Log.Error(ex);
                    }
                }
            });
            if (failure != null)
            {
                TaskDialog.Show("Cable Tray Branch Layout", "The complete branch layout was rolled back.\n\n" + failure +
                    "\n\nCheck routing preferences, connector positions and tray type.");
                return Result.Failed;
            }
            TaskDialog.Show("Cable Tray Branch Layout", "Straight main run, perpendicular branch and tee fitting created. Verify route clearances, connector continuity and tray sizing in Revit.");
            return Result.Succeeded;
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