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
    /// Reads a CSV file and writes its values onto elements, matching rows to elements by Mark.
    /// The first row must hold parameter names; one column must be called Mark.
    /// Shows how many cells will change and asks before applying.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportValuesByMarkCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            string file = Dialogs.PickFile("Choose the CSV file to import", "CSV files (*.csv)|*.csv|All files (*.*)|*.*");
            if (file == null) return Result.Cancelled;

            List<string[]> rows = Csv.Parse(File.ReadAllText(file, Encoding.UTF8));
            if (rows.Count < 2)
            {
                TaskDialog.Show("Import Values by Mark", "The file needs a header row and at least one data row.");
                return Result.Cancelled;
            }

            string[] header = rows[0];
            int markColumn = Array.FindIndex(header, h => string.Equals(h.Trim(), "Mark", StringComparison.OrdinalIgnoreCase));
            if (markColumn < 0)
            {
                TaskDialog.Show("Import Values by Mark", "No column is called \"Mark\" in the first row.");
                return Result.Cancelled;
            }

            Busy.On();
            var byMark = new Dictionary<string, List<Element>>(StringComparer.OrdinalIgnoreCase);
            foreach (Element element in new FilteredElementCollector(ctx.Doc).WhereElementIsNotElementType())
            {
                if (element.Category == null || element.Category.CategoryType != CategoryType.Model) continue;
                Parameter markParameter = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                string mark = markParameter != null ? markParameter.AsString() : null;
                if (string.IsNullOrWhiteSpace(mark)) continue;

                List<Element> list;
                if (!byMark.TryGetValue(mark.Trim(), out list))
                {
                    list = new List<Element>();
                    byMark[mark.Trim()] = list;
                }
                list.Add(element);
            }

            var updates = new List<KeyValuePair<Parameter, string>>();
            var unmatched = new List<string>();
            int matchedRows = 0, unknownColumns = 0;
            var missingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string mark = markColumn < row.Length ? row[markColumn].Trim() : "";
                if (mark.Length == 0) continue;

                List<Element> targets;
                if (!byMark.TryGetValue(mark, out targets)) { unmatched.Add(mark); continue; }
                matchedRows++;

                for (int c = 0; c < header.Length && c < row.Length; c++)
                {
                    string name = header[c].Trim();
                    if (c == markColumn || name.Length == 0) continue;

                    foreach (Element element in targets)
                    {
                        Parameter p = element.LookupParameter(name);
                        if (p == null) { if (missingColumns.Add(name)) unknownColumns++; continue; }
                        if (p.IsReadOnly) continue;
                        if (!string.Equals(Current(p), row[c], StringComparison.Ordinal)) updates.Add(new KeyValuePair<Parameter, string>(p, row[c]));
                    }
                }
            }

            Busy.Off();
            string preview = matchedRows + " row(s) matched an element by Mark; " + updates.Count + " value(s) will change.";
            if (unmatched.Count > 0)
                preview += "\n\nNo element has these marks (" + unmatched.Count + "): " + string.Join(", ", unmatched.Take(10)) + (unmatched.Count > 10 ? " ..." : "");
            if (unknownColumns > 0)
                preview += "\n\nColumns with no matching parameter: " + string.Join(", ", missingColumns);

            if (updates.Count == 0)
            {
                TaskDialog.Show("Import Values by Mark", preview + "\n\nNothing to change.");
                return Result.Cancelled;
            }

            var confirm = new TaskDialog("Import Values by Mark");
            confirm.MainInstruction = "Apply these changes?";
            confirm.MainContent = preview;
            confirm.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            confirm.DefaultButton = TaskDialogResult.No;
            if (confirm.Show() != TaskDialogResult.Yes) return Result.Cancelled;

            int applied = 0, rejected = 0;
            ctx.Transact("Import values by mark", () =>
            {
                foreach (var update in updates)
                {
                    if (Apply(update.Key, update.Value)) applied++;
                    else rejected++;
                }
            });

            string text = applied + " value(s) written.";
            if (rejected > 0) text += "\n" + rejected + " value(s) were not accepted by Revit (wrong format or units).";
            TaskDialog.Show("Import Values by Mark", text);
            return Result.Succeeded;
        }

        private static string Current(Parameter p)
        {
            string value = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            return value ?? "";
        }

        private static bool Apply(Parameter p, string value)
        {
            try
            {
                if (p.StorageType == StorageType.String) return p.Set(value);
                if (p.SetValueString(value)) return true;

                double number;
                int whole;
                if (p.StorageType == StorageType.Integer && int.TryParse(value, out whole)) return p.Set(whole);
                if (p.StorageType == StorageType.Double && UnitConv.TryParseNumber(value, out number)) return p.Set(number);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return false;
        }
    }
}
