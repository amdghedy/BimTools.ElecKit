using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportAllSchedulesCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var schedules = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(v => !v.IsTemplate)
                .ToList();

            if (schedules.Count == 0)
            {
                TaskDialog.Show("Export All Schedules", "This model has no schedules.");
                return Result.Succeeded;
            }

            string folder = Path.Combine(
                ElecSettings.Current.ResolveReportFolder(),
                "Schedules_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);

            int exported = 0;
            var failed = new List<string>();
            foreach (ViewSchedule schedule in schedules)
            {
                try
                {
                    schedule.Export(folder, FileNames.Safe(schedule.Name) + ".csv", ScheduleExport.Options());
                    exported++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    failed.Add(schedule.Name);
                }
            }

            var notes = new List<string>();
            if (failed.Count > 0) notes.Add("Skipped (see log): " + string.Join(", ", failed));
            Reports.Finish("Export All Schedules", exported + " schedule(s) exported.", notes, folder);
            return Result.Succeeded;
        }
    }
}
