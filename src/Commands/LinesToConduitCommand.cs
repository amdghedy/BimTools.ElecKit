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
    /// Converts straight detail or model lines into conduits.
    /// Detail lines need a plan view; they are placed at the view's level plus the height you enter.
    /// Model lines keep their own 3D position.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LinesToConduitCommand : ElecCommand
    {
        private const double MinLengthFeet = 0.01;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            var curves = new List<CurveElement>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                var curve = ctx.Doc.GetElement(id) as CurveElement;
                if (curve != null && (curve is DetailCurve || curve is ModelCurve)) curves.Add(curve);
            }
            if (curves.Count == 0)
            {
                var filter = new PredicateFilter(e => e is DetailCurve || e is ModelCurve);
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element, filter,
                    "Select detail or model lines to convert, then click Finish"))
                {
                    var curve = ctx.Doc.GetElement(r) as CurveElement;
                    if (curve != null) curves.Add(curve);
                }
            }
            if (curves.Count == 0) return Result.Cancelled;

            ConduitType conduitType;
            double offsetMm;
            if (!ConduitPrompt.TryAsk(ctx.Doc, "Lines to Conduit", out conduitType, out offsetMm))
                return Result.Cancelled;

            var ask = new TaskDialog("Lines to Conduit");
            ask.MainInstruction = "Delete the source lines after converting?";
            ask.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No | TaskDialogCommonButtons.Cancel;
            ask.DefaultButton = TaskDialogResult.No;
            TaskDialogResult choice = ask.Show();
            if (choice == TaskDialogResult.Cancel) return Result.Cancelled;
            bool deleteSources = choice == TaskDialogResult.Yes;

            var plan = ctx.Doc.ActiveView as ViewPlan;
            Level planLevel = plan != null ? plan.GenLevel : null;
            double offsetFeet = UnitConv.MmToFeet(offsetMm);

            int created = 0, arcs = 0, tooShort = 0, needsPlan = 0, failed = 0;
            var converted = new List<ElementId>();

            ctx.Transact("Lines to conduit", () =>
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

                    try
                    {
                        Conduit.Create(ctx.Doc, conduitType.Id, start, end, level.Id);
                        converted.Add(source.Id);
                        created++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        failed++;
                    }
                }

                if (deleteSources && converted.Count > 0) ctx.Doc.Delete(converted);
            });

            string text = created + " conduit(s) created (" + conduitType.Name + ").";
            if (arcs > 0) text += "\n" + arcs + " arc(s) skipped: only straight lines convert.";
            if (needsPlan > 0) text += "\n" + needsPlan + " detail line(s) skipped: run this in a plan view.";
            if (tooShort > 0) text += "\n" + tooShort + " line(s) skipped: too short or no level found.";
            if (failed > 0) text += "\n" + failed + " line(s) failed (see log).";
            TaskDialog.Show("Lines to Conduit", text);
            return Result.Succeeded;
        }
    }
}
