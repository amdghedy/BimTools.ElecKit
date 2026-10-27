using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportActiveScheduleCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var schedule = ctx.Doc.ActiveView as ViewSchedule;
            if (schedule == null)
            {
                TaskDialog.Show("Export Active Schedule", "Open a schedule view first, then run this tool.");
                return Result.Cancelled;
            }

            string folder = ElecSettings.Current.ResolveReportFolder();
            string fileName = FileNames.Safe(schedule.Name) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            schedule.Export(folder, fileName, ScheduleExport.Options());

            string path = Path.Combine(folder, fileName);
            Reports.Finish("Export Active Schedule", "Schedule exported.", null, path);
            return Result.Succeeded;
        }
    }
}
