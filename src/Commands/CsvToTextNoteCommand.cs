using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Places a CSV table in the active view as one text note, with columns padded to line up.
    /// Choose a text type with a fixed-width font for the neatest result.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CsvToTextNoteCommand : ElecCommand
    {
        private const int MaxRows = 200;
        private const int MaxColumns = 20;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is View3D || view is ViewSchedule)
            {
                TaskDialog.Show("CSV to Text Note", "Open a plan, section, drafting or legend view first.");
                return Result.Cancelled;
            }

            string file = Dialogs.PickFile("Choose the CSV file", "CSV files (*.csv)|*.csv|All files (*.*)|*.*");
            if (file == null) return Result.Cancelled;

            List<string[]> rows = Csv.Parse(File.ReadAllText(file, Encoding.UTF8));
            if (rows.Count == 0)
            {
                TaskDialog.Show("CSV to Text Note", "The file is empty.");
                return Result.Cancelled;
            }
            if (rows.Count > MaxRows) rows = rows.Take(MaxRows).ToList();

            int columns = Math.Min(MaxColumns, rows.Max(r => r.Length));
            var widths = new int[columns];
            foreach (string[] row in rows)
                for (int c = 0; c < columns && c < row.Length; c++)
                    widths[c] = Math.Max(widths[c], Clean(row[c]).Length);

            var lines = new List<string>();
            for (int r = 0; r < rows.Count; r++)
            {
                var cells = new List<string>();
                for (int c = 0; c < columns; c++)
                    cells.Add((c < rows[r].Length ? Clean(rows[r][c]) : "").PadRight(widths[c]));
                lines.Add(string.Join("  ", cells).TrimEnd());

                if (r == 0)
                    lines.Add(new string('-', Math.Max(1, widths.Sum() + 2 * (columns - 1))));
            }
            string text = string.Join("\r", lines);

            var types = new FilteredElementCollector(ctx.Doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().OrderBy(t => t.Name).ToList();
            if (types.Count == 0)
            {
                TaskDialog.Show("CSV to Text Note", "This project has no text note types.");
                return Result.Cancelled;
            }
            int typeIndex = Dialogs.Choose("CSV to Text Note", "Text type:", types.Select(t => t.Name).ToList(), 0);
            if (typeIndex < 0) return Result.Cancelled;

            XYZ point = ctx.UiDoc.Selection.PickPoint("Pick the top-left corner for the table");

            ctx.Transact("CSV to text note", () =>
            {
                TextNote.Create(ctx.Doc, view.Id, point, text, types[typeIndex].Id);
            });

            TaskDialog.Show("CSV to Text Note", rows.Count + " row(s) placed.");
            return Result.Succeeded;
        }

        private static string Clean(string value)
        {
            return (value ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        }
    }
}
