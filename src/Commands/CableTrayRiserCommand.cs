using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Creates a vertical cable tray riser between two points picked in a 3D view.</summary>
    [Transaction(TransactionMode.Manual)]
    public class CableTrayRiserCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            if (!(ctx.Doc.ActiveView is View3D))
            {
                TaskDialog.Show("Cable Tray Riser", "Open a 3D view and pick the lower and upper riser endpoints.");
                return Result.Cancelled;
            }
            XYZ lower, upper;
            try
            {
                lower = ctx.UiDoc.Selection.PickPoint("Pick the lower endpoint of the vertical cable-tray riser");
                upper = ctx.UiDoc.Selection.PickPoint("Pick the upper endpoint of the vertical cable-tray riser");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            double horizontalOffset = Math.Sqrt(Math.Pow(upper.X - lower.X, 2) + Math.Pow(upper.Y - lower.Y, 2));
            if (horizontalOffset > UnitConv.MmToFeet(25.0) || Math.Abs(upper.Z - lower.Z) < UnitConv.MmToFeet(100.0))
            {
                TaskDialog.Show("Cable Tray Riser", "The endpoints must be vertically aligned within 25 mm and separated vertically by at least 100 mm.");
                return Result.Cancelled;
            }
            var types = new FilteredElementCollector(ctx.Doc).OfClass(typeof(CableTrayType))
                .Cast<CableTrayType>().OrderBy(t => t.Name).ToList();
            if (types.Count == 0) { TaskDialog.Show("Cable Tray Riser", "No cable tray types exist in this project."); return Result.Cancelled; }
            int index = Dialogs.Choose("Cable Tray Riser", "Cable tray type:", types.Select(t => t.Name).ToList(), 0);
            if (index < 0 || index >= types.Count) return Result.Cancelled;
            XYZ start = lower.Z <= upper.Z ? lower : upper;
            XYZ end = lower.Z <= upper.Z ? upper : lower;
            Level level = Levels.LevelBelow(ctx.Doc, start.Z);
            if (level == null) { TaskDialog.Show("Cable Tray Riser", "No Revit level was found for the riser."); return Result.Cancelled; }
            string failure = null;
            ctx.Transact("Create cable tray riser", () =>
            {
                using (var sub = new SubTransaction(ctx.Doc))
                {
                    sub.Start();
                    try
                    {
                        CableTray.Create(ctx.Doc, types[index].Id, start, end, level.Id);
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
                TaskDialog.Show("Cable Tray Riser", "Revit rejected the vertical tray segment; changes were rolled back.\n\n" + failure);
                return Result.Failed;
            }
            TaskDialog.Show("Cable Tray Riser", "Vertical cable-tray riser created. Verify connector alignment and connect the upper/lower ends to horizontal runs as required.");
            return Result.Succeeded;
        }
    }
}