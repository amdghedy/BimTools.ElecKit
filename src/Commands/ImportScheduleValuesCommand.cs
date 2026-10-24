using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ImportScheduleValuesCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var dlg = new OpenFileDialog { Title = "Import Schedule Values", Filter = "CSV (*.csv)|*.csv", CheckFileExists = true };
            if (dlg.ShowDialog() != true) return Result.Cancelled;
            var rows = File.ReadAllLines(dlg.FileName, Encoding.UTF8)
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#", StringComparison.Ordinal))
                .Select(ParseCsv).ToList();
            if (rows.Count < 2) { TaskDialog.Show("Import Values", "CSV must contain a header and at least one data row."); return Result.Cancelled; }

            string[] header = rows[0].Select(s => s.Trim().TrimStart('\uFEFF')).ToArray();
            var duplicateHeaders = header
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateHeaders.Count > 0)
            {
                TaskDialog.Show("Import Values", "Duplicate CSV headers are not allowed because they make parameter mapping ambiguous:\\n" +
                    string.Join(", ", duplicateHeaders));
                return Result.Cancelled;
            }

            int idCol = HeaderIndex(header, "ElementId", "Element ID", "Id");
            int markCol = HeaderIndex(header, "Mark");
            int idAliases = header.Count(h =>
                string.Equals(h, "ElementId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(h, "Element ID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(h, "Id", StringComparison.OrdinalIgnoreCase));
            if (idAliases > 1)
            {
                TaskDialog.Show("Import Values", "Use only one key column: ElementId, Element ID, or Id.");
                return Result.Cancelled;
            }
            if (idCol < 0 && markCol < 0) { TaskDialog.Show("Import Values", "Include an ElementId or Mark column."); return Result.Cancelled; }
            // Reject duplicate row keys before opening a transaction so one element cannot be updated twice accidentally.
            int keyCol = idCol >= 0 ? idCol : markCol;
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int r = 1; r < rows.Count; r++)
            {
                if (keyCol >= rows[r].Length || string.IsNullOrWhiteSpace(rows[r][keyCol])) continue;
                string key = rows[r][keyCol].Trim();
                if (!seenKeys.Add(key))
                {
                    TaskDialog.Show("Import Values", "Duplicate " + (idCol >= 0 ? "ElementId" : "Mark") +
                        " key found on CSV row " + (r + 1) + ": " + key +
                        ". No values were imported. Remove duplicate rows and try again.");
                    return Result.Cancelled;
                }
            }

            var valueCols = Enumerable.Range(0, header.Length).Where(i => i != idCol && i != markCol && !string.IsNullOrWhiteSpace(header[i])).ToList();
            if (valueCols.Count == 0) { TaskDialog.Show("Import Values", "No parameter columns found."); return Result.Cancelled; }

            var markMap = new Dictionary<string, List<Element>>(StringComparer.OrdinalIgnoreCase);
            if (idCol < 0)
            {
                foreach (Element e in new FilteredElementCollector(ctx.Doc).WhereElementIsNotElementType())
                {
                    Parameter p = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    string mark = p != null && p.StorageType == StorageType.String ? p.AsString() : null;
                    if (string.IsNullOrWhiteSpace(mark)) continue;
                    if (!markMap.ContainsKey(mark)) markMap[mark] = new List<Element>();
                    markMap[mark].Add(e);
                }
            }

            var confirm = new TaskDialog("Import Schedule Values");
            confirm.MainInstruction = "Import parameter values into the active model?";
            confirm.MainContent = rows.Count - 1 + " data rows, " + valueCols.Count + " parameter columns. Match by " +
                (idCol >= 0 ? "ElementId" : "unique Mark") +
                ". Blank cells are ignored. Only writable parameters are updated. Double values must use the document's displayed units.";
            confirm.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.Cancel;
            if (confirm.Show() != TaskDialogResult.Yes) return Result.Cancelled;

            int updated = 0, parameterCount = 0, skipped = 0, failed = 0;
            var issues = new List<string>();
            ctx.Transact("Import schedule values", () =>
            {
                for (int r = 1; r < rows.Count; r++)
                {
                    string[] cells = rows[r];
                    if (cells.All(string.IsNullOrWhiteSpace)) continue;
                    Element el = null;
                    if (idCol >= 0 && idCol < cells.Length)
                    {
                        int id;
                        if (int.TryParse(cells[idCol], NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                            el = ctx.Doc.GetElement(new ElementId(id));
                    }
                    else if (markCol >= 0 && markCol < cells.Length)
                    {
                        List<Element> matches;
                        string mark = cells[markCol].Trim();
                        if (markMap.TryGetValue(mark, out matches) && matches.Count == 1) el = matches[0];
                        else if (!string.IsNullOrWhiteSpace(mark) && issues.Count < 50)
                            issues.Add("Row " + (r + 1) + ": Mark is missing or not unique: " + mark);
                    }
                    if (el == null || el is ElementType) { skipped++; continue; }
                    bool changed = false;
                    foreach (int c in valueCols)
                    {
                        if (c >= cells.Length || string.IsNullOrEmpty(cells[c])) continue;
                        Parameter p = el.LookupParameter(header[c]);
                        if (p == null || p.IsReadOnly || p.StorageType == StorageType.ElementId)
                        { failed++; continue; }
                        try
                        {
                            bool ok = false;
                            if (p.StorageType == StorageType.String) ok = p.Set(cells[c]);
                            else if (p.StorageType == StorageType.Integer)
                            {
                                int n;
                                if (int.TryParse(cells[c], NumberStyles.Integer, CultureInfo.CurrentCulture, out n) ||
                                    int.TryParse(cells[c], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) ok = p.Set(n);
                                else if (cells[c].Equals("Yes", StringComparison.OrdinalIgnoreCase) || cells[c].Equals("No", StringComparison.OrdinalIgnoreCase))
                                    ok = p.Set(cells[c].Equals("Yes", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                            }
                            else if (p.StorageType == StorageType.Double) ok = p.SetValueString(cells[c]);
                            if (ok) { parameterCount++; changed = true; } else failed++;
                        }
                        catch (Exception ex) { failed++; Log.Error(ex); if (issues.Count < 50) issues.Add("Row " + (r + 1) + ", " + header[c] + ": " + ex.Message); }
                    }
                    if (changed) updated++;
                }
            });
            string summary = "Updated elements: " + updated + "\nUpdated parameters: " + parameterCount + "\nSkipped rows: " + skipped + "\nFailed values: " + failed;
            if (issues.Count > 0) summary += "\n\nIssues:\n" + string.Join("\n", issues.Take(15));
            TaskDialog.Show("Import Schedule Values", summary);
            return Result.Succeeded;
        }

        private static int HeaderIndex(string[] headers, params string[] names)
        {
            for (int i = 0; i < headers.Length; i++)
                if (names.Any(n => string.Equals(headers[i], n, StringComparison.OrdinalIgnoreCase))) return i;
            return -1;
        }

        private static string[] ParseCsv(string line)
        {
            var result = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (ch == ',' && !quoted) { result.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(ch);
            }
            result.Add(cell.ToString());
            return result.ToArray();
        }
    }
}