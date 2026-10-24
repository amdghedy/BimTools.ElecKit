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
    /// <summary>Converts straight detail/model lines to cable tray with per-element failure reporting.</summary>
    [Transaction(TransactionMode.Manual)]
    public class LinesToCableTrayCommand : ElecCommand
    {
        private const double MinLengthFeet = 0.01;
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var curves = new List<CurveElement>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                CurveElement curve = ctx.Doc.GetElement(id) as CurveElement;
                if (curve != null && (curve is DetailCurve || curve is ModelCurve)) curves.Add(curve);
            }
            if (curves.Count == 0)
            {
                try
                {
                    foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                        new PredicateFilter(e => e is DetailCurve || e is ModelCurve),
                        "Select straight detail/model lines to convert, then click Finish"))
                    {
                        CurveElement curve = ctx.Doc.GetElement(r) as CurveElement;
                        if (curve != null) curves.Add(curve);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            }
            if (curves.Count == 0) return Result.Cancelled;

            List<CableTrayType> types = new FilteredElementCollector(ctx.Doc).OfClass(typeof(CableTrayType))
                .Cast<CableTrayType>().OrderBy(t => t.Name).ToList();
            if (types.Count == 0) { TaskDialog.Show("Lines to Cable Tray", "No cable tray types exist in this project."); return Result.Cancelled; }
            int typeIndex = Dialogs.Choose("Lines to Cable Tray", "Cable tray type:", types.Select(t => t.Name).ToList(), 0);
            if (typeIndex < 0 || typeIndex >= types.Count) return Result.Cancelled;
            double offsetMm;
            string answer = Dialogs.Ask("Lines to Cable Tray", "Height above level, in millimetres:",
                UnitConv.Format(ElecSettings.Current.EffectiveConduitOffsetMm));
            if (answer == null) return Result.Cancelled;
            if (!UnitConv.TryParseNumber(answer, out offsetMm) || double.IsNaN(offsetMm) || double.IsInfinity(offsetMm))
            {
                TaskDialog.Show("Lines to Cable Tray", "Enter a finite numeric height in millimetres.");
                return Result.Cancelled;
            }
            var ask = new TaskDialog("Lines to Cable Tray");
            ask.MainInstruction = "Delete source lines after successful conversion?";
            ask.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No | TaskDialogCommonButtons.Cancel;
            ask.DefaultButton = TaskDialogResult.No;
            TaskDialogResult choice = ask.Show();
            if (choice == TaskDialogResult.Cancel) return Result.Cancelled;

            CableTrayType trayType = types[typeIndex];
            ViewPlan plan = ctx.Doc.ActiveView as ViewPlan;
            Level planLevel = plan == null ? null : plan.GenLevel;
            double offsetFeet = UnitConv.MmToFeet(offsetMm);
            int created = 0, arcs = 0, needsPlan = 0, tooShort = 0, failed = 0;
            var converted = new List<ElementId>();
            var details = new List<string>();
            ctx.Transact("Lines to cable tray", () =>
            {
                foreach (CurveElement source in curves)
                {
                    Line line = source.GeometryCurve as Line;
                    if (line == null) { arcs++; continue; }
                    XYZ start, end;
                    Level level;
                    if (source is DetailCurve)
                    {
                        if (planLevel == null) { needsPlan++; continue; }
                        double z = planLevel.Elevation + offsetFeet;
                        start = new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, z);
                        end = new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, z);
                        level = planLevel;
                    }
                    else
                    {
                        start = line.GetEndPoint(0);
                        end = line.GetEndPoint(1);
                        level = Levels.LevelBelow(ctx.Doc, Math.Min(start.Z, end.Z));
                    }
                    if (level == null || start.DistanceTo(end) < MinLengthFeet) { tooShort++; continue; }
                    using (var sub = new SubTransaction(ctx.Doc))
                    {
                        sub.Start();
                        try
                        {
                            CableTray.Create(ctx.Doc, trayType.Id, start, end, level.Id);
                            sub.Commit();
                            converted.Add(source.Id);
                            created++;
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            failed++;
                            if (details.Count < 8) details.Add(source.Id.IntegerValue + ": " + ex.Message);
                            Log.Error(ex);
                        }
                    }
                }
                if (choice == TaskDialogResult.Yes && converted.Count > 0) ctx.Doc.Delete(converted);
            });
            string summary = created + " cable tray segment(s) created (" + trayType.Name + ").";
            if (arcs > 0) summary += "\n" + arcs + " curved line(s) skipped; only straight lines are supported.";
            if (needsPlan > 0) summary += "\n" + needsPlan + " detail line(s) skipped because the active view is not a plan.";
            if (tooShort > 0) summary += "\n" + tooShort + " segment(s) skipped due to missing level or insufficient length.";
            if (failed > 0) summary += "\n" + failed + " segment(s) failed and were rolled back.";
            if (details.Count > 0) summary += "\n\nFirst failures:\n" + string.Join("\n", details);
            TaskDialog.Show("Lines to Cable Tray", summary);
            return failed > 0 && created == 0 ? Result.Failed : Result.Succeeded;
        }
    }
}