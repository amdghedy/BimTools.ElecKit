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
    /// Draws cable tray between points you click, at a set height above the plan level, in the chosen size.
    /// Keeps going from the last point until Esc, and adds an elbow at each corner when the tray type allows it.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class DrawCableTrayCommand : ElecCommand
    {
        private const double MinSegmentFeet = 0.05;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            var plan = ctx.Doc.ActiveView as ViewPlan;
            if (plan == null || plan.GenLevel == null)
            {
                TaskDialog.Show("Draw Cable Tray", "Open a plan view first.");
                return Result.Cancelled;
            }

            var types = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(CableTrayType))
                .Cast<CableTrayType>()
                .OrderBy(t => t.Name)
                .ToList();
            if (types.Count == 0)
            {
                TaskDialog.Show("Draw Cable Tray", "This project has no cable tray types. Load or create one first.");
                return Result.Cancelled;
            }

            int typeIndex = 0;
            if (types.Count > 1)
            {
                typeIndex = ListPicker.Pick("Draw Cable Tray", "Cable tray type:", types.Select(t => t.Name).ToList());
                if (typeIndex < 0) return Result.Cancelled;
            }
            CableTrayType type = types[typeIndex];

            string sizeAnswer = Dialogs.Ask("Draw Cable Tray", "Size as width x height in millimetres, for example 300x60:",
                ElecSettings.Current.TraySize ?? "300x60");
            if (sizeAnswer == null) return Result.Cancelled;
            double widthMm, heightMm;
            if (!TryParseSize(sizeAnswer, out widthMm, out heightMm))
            {
                TaskDialog.Show("Draw Cable Tray", "Enter the size like 300x60.");
                return Result.Cancelled;
            }

            double? offset = Dialogs.AskNumber("Draw Cable Tray", "Height above the level:",
                ElecSettings.Current.TrayOffsetMm ?? 3000.0, false, "mm");
            if (offset == null) return Result.Cancelled;
            double offsetMm = offset.Value;

            ElecSettings.Current.LastTrayType = type.Name;
            ElecSettings.Current.TraySize = UnitConv.Format(widthMm) + "x" + UnitConv.Format(heightMm);
            ElecSettings.Current.TrayOffsetMm = offsetMm;
            ElecSettings.Current.Save();

            Level level = plan.GenLevel;
            double z = level.Elevation + UnitConv.MmToFeet(offsetMm);
            double width = UnitConv.MmToFeet(widthMm);
            double height = UnitConv.MmToFeet(heightMm);

            XYZ previous = null;
            CableTray previousTray = null;
            int segments = 0, elbows = 0, elbowsFailed = 0;

            try
            {
                while (true)
                {
                    XYZ picked = ctx.UiDoc.Selection.PickPoint(
                        ObjectSnapTypes.Endpoints | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Nearest,
                        previous == null ? "Pick the start point" : "Pick the next point. Esc to finish.");
                    var point = new XYZ(picked.X, picked.Y, z);

                    if (previous == null) { previous = point; continue; }
                    if (previous.DistanceTo(point) < MinSegmentFeet) continue;

                    CableTray tray = null;
                    ElbowResult elbow = ElbowResult.Skipped;
                    XYZ segmentStart = previous;
                    try
                    {
                        ctx.Transact("Draw cable tray", () =>
                        {
                            tray = CableTray.Create(ctx.Doc, type.Id, segmentStart, point, level.Id);
                            SetSize(tray, width, height);
                            if (previousTray != null)
                            {
                                ctx.Doc.Regenerate();
                                elbow = ConduitFittings.TryElbow(ctx.Doc, previousTray, tray, segmentStart);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        TaskDialog.Show("Draw Cable Tray", "Could not draw the tray: " + ex.Message);
                        break;
                    }

                    segments++;
                    if (elbow == ElbowResult.Made) elbows++;
                    else if (elbow == ElbowResult.Failed) elbowsFailed++;
                    previousTray = tray;
                    previous = point;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Esc ends the run
            }

            if (segments > 0)
            {
                string text = segments + " tray segment(s) drawn, " + elbows + " elbow(s).";
                if (elbowsFailed > 0) text += "\n" + elbowsFailed + " corner(s) have no elbow: check the tray type's routing preferences.";
                TaskDialog.Show("Draw Cable Tray", text);
            }
            return segments > 0 ? Result.Succeeded : Result.Cancelled;
        }

        private static void SetSize(CableTray tray, double width, double height)
        {
            Parameter w = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
            Parameter h = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
            if (w != null && !w.IsReadOnly) w.Set(width);
            if (h != null && !h.IsReadOnly) h.Set(height);
        }

        private static bool TryParseSize(string text, out double widthMm, out double heightMm)
        {
            widthMm = 0;
            heightMm = 0;
            string[] parts = text.ToLowerInvariant().Replace('*', 'x').Replace('\u00D7', 'x').Split('x');
            if (parts.Length != 2) return false;
            return UnitConv.TryParseNumber(parts[0], out widthMm) && UnitConv.TryParseNumber(parts[1], out heightMm)
                   && widthMm > 0 && heightMm > 0;
        }
    }
}
