using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Exports the open schedule, or every schedule (one sheet each), to an Excel .xlsx workbook.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportSchedulesExcelCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            int mode = Dialogs.Choose("Export Schedules to Excel", "Export:", new List<string>
            {
                "The open schedule",
                "All schedules (one sheet each)"
            }, ctx.Doc.ActiveView is ViewSchedule ? 0 : 1);
            if (mode < 0) return Result.Cancelled;

            List<ViewSchedule> schedules;
            if (mode == 0)
            {
                var active = ctx.Doc.ActiveView as ViewSchedule;
                if (active == null)
                {
                    TaskDialog.Show("Export Schedules to Excel", "Open a schedule view first, or choose all schedules.");
                    return Result.Cancelled;
                }
                schedules = new List<ViewSchedule> { active };
            }
            else
            {
                schedules = new FilteredElementCollector(ctx.Doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(v => !v.IsTemplate)
                    .OrderBy(v => v.Name)
                    .ToList();
            }
            if (schedules.Count == 0)
            {
                TaskDialog.Show("Export Schedules to Excel", "This model has no schedules.");
                return Result.Cancelled;
            }

            var sheets = new List<KeyValuePair<string, List<string[]>>>();
            var failed = new List<string>();
            foreach (ViewSchedule schedule in schedules)
            {
                try
                {
                    sheets.Add(new KeyValuePair<string, List<string[]>>(schedule.Name, Rows(schedule)));
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    failed.Add(schedule.Name);
                }
            }
            if (sheets.Count == 0)
            {
                TaskDialog.Show("Export Schedules to Excel", "None of the schedules could be read (see log).");
                return Result.Failed;
            }

            string path = Reports.PathFor(mode == 0 ? schedules[0].Name : "Schedules", ".xlsx");
            XlsxWriter.Write(path, sheets);

            string text = sheets.Count + " schedule(s) exported to:\n" + path;
            if (failed.Count > 0) text += "\n\nSkipped (see log):\n" + string.Join("\n", failed);
            TaskDialog.Show("Export Schedules to Excel", text);
            Reports.OpenIfWanted(path);
            return Result.Succeeded;
        }

        private static List<string[]> Rows(ViewSchedule schedule)
        {
            TableSectionData body = schedule.GetTableData().GetSectionData(SectionType.Body);
            var rows = new List<string[]>();
            for (int r = body.FirstRowNumber; r <= body.LastRowNumber; r++)
            {
                var cells = new List<string>();
                for (int c = body.FirstColumnNumber; c <= body.LastColumnNumber; c++)
                    cells.Add(body.GetCellText(r, c));
                rows.Add(cells.ToArray());
            }
            return rows;
        }
    }
}
