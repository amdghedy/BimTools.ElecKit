using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;
using Microsoft.Win32;

namespace ElecKit.Commands
{
    /// <summary>Imports parameter values from an Export Schedule Round-Trip CSV by ElementId and parameter id.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportScheduleRoundTripCommand : ElecCommand
    {
        private sealed class Column
        {
            public int CsvIndex;
            public int ParameterId;
            public string Name;
            public bool IsTypeParameter;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Native Schedule Round-Trip CSV",
                Filter = "CSV (*.csv)|*.csv",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() != true) return Result.Cancelled;

            List<string[]> rows;
            try
            {
                rows = CsvLineParser.ParseRecords(File.ReadAllText(dlg.FileName, Encoding.UTF8))
                    .Where(row => row.Length > 0 && !(row.Length == 1 && string.IsNullOrWhiteSpace(row[0])) &&
                        !row[0].TrimStart().StartsWith("#", StringComparison.Ordinal))
                    .ToList();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
            {
                Log.Error(ex);
                TaskDialog.Show("Import Schedule Round-Trip", "The CSV could not be read or parsed.\n\n" + ex.Message);
                return Result.Cancelled;
            }
            if (rows.Count < 2)
            {
                TaskDialog.Show("Import Schedule Round-Trip", "CSV must contain a header and at least one data row.");
                return Result.Cancelled;
            }

            string[] header = rows[0].Select((value, index) => index == 0 ? value.Trim().TrimStart('\uFEFF') : value.Trim()).ToArray();
            if (!string.Equals(header[0], "__ElementId", StringComparison.OrdinalIgnoreCase))
            {
                TaskDialog.Show("Import Schedule Round-Trip", "This is not a round-trip CSV: the first header must be __ElementId.");
                return Result.Cancelled;
            }

            var columns = new List<Column>();
            var usedParameterIds = new HashSet<int>();
            for (int i = 1; i < header.Length; i++)
            {
                string token = header[i];
                const string prefix = "__PARAMID_";
                if (!token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    TaskDialog.Show("Import Schedule Round-Trip", "Unsupported column header at column " + (i + 1) + ": " + token);
                    return Result.Cancelled;
                }
                int end = token.IndexOf("__", prefix.Length, StringComparison.Ordinal);
                int parameterId;
                if (end < 0 || !int.TryParse(token.Substring(prefix.Length, end - prefix.Length),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out parameterId) ||
                    !usedParameterIds.Add(parameterId))
                {
                    TaskDialog.Show("Import Schedule Round-Trip", "Invalid or duplicate parameter-id column: " + token);
                    return Result.Cancelled;
                }
                string metadata = token.Substring(end + 2);
                bool isTypeParameter;
                string fieldName;
                if (metadata.StartsWith("TYPE__", StringComparison.OrdinalIgnoreCase))
                {
                    isTypeParameter = true;
                    fieldName = metadata.Substring("TYPE__".Length);
                }
                else if (metadata.StartsWith("INSTANCE__", StringComparison.OrdinalIgnoreCase))
                {
                    isTypeParameter = false;
                    fieldName = metadata.Substring("INSTANCE__".Length);
                }
                else
                {
                    TaskDialog.Show("Import Schedule Round-Trip", "Column must declare TYPE or INSTANCE parameter scope: " + token);
                    return Result.Cancelled;
                }
                columns.Add(new Column
                {
                    CsvIndex = i,
                    ParameterId = parameterId,
                    Name = fieldName,
                    IsTypeParameter = isTypeParameter
                });
            }
            if (columns.Count == 0)
            {
                TaskDialog.Show("Import Schedule Round-Trip", "No parameter columns were found.");
                return Result.Cancelled;
            }

            var seenIds = new HashSet<int>();
            for (int r = 1; r < rows.Count; r++)
            {
                if (rows[r].All(string.IsNullOrWhiteSpace)) continue;
                int id;
                if (rows[r].Length == 0 || !int.TryParse(rows[r][0], NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                {
                    TaskDialog.Show("Import Schedule Round-Trip", "Invalid ElementId on CSV row " + (r + 1) + ". No values were imported.");
                    return Result.Cancelled;
                }
                if (!seenIds.Add(id))
                {
                    TaskDialog.Show("Import Schedule Round-Trip", "Duplicate ElementId " + id + " on CSV row " + (r + 1) + ". No values were imported.");
                    return Result.Cancelled;
                }
                if (rows[r].Length != header.Length)
                {
                    TaskDialog.Show("Import Schedule Round-Trip", "Column count mismatch on CSV row " + (r + 1) + ". No values were imported.");
                    return Result.Cancelled;
                }
            }

            // Type parameters are shared by every instance of the same type. Reject conflicting edits
            // before opening a transaction; otherwise the last CSV row would silently win.
            var typeValues = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int r = 1; r < rows.Count; r++)
            {
                string[] cells = rows[r];
                if (cells.All(string.IsNullOrWhiteSpace)) continue;
                int id;
                if (!int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) continue;
                Element instance = ctx.Doc.GetElement(new ElementId(id));
                if (instance == null) continue;
                Element type = ctx.Doc.GetElement(instance.GetTypeId());
                if (type == null) continue;
                foreach (Column column in columns.Where(col => col.IsTypeParameter))
                {
                    string raw = cells[column.CsvIndex];
                    if (string.IsNullOrEmpty(raw)) continue;
                    string key = type.Id.Val().ToString(CultureInfo.InvariantCulture) + ":" +
                        column.ParameterId.ToString(CultureInfo.InvariantCulture);
                    string existing;
                    if (typeValues.TryGetValue(key, out existing) &&
                        !string.Equals(existing, raw, StringComparison.Ordinal))
                    {
                        TaskDialog.Show("Import Schedule Round-Trip",
                            "Conflicting values for type parameter '" + column.Name + "' on type ElementId " +
                            type.Id.Val() + ". Instances sharing one type must have the same value. No values were imported.");
                        return Result.Cancelled;
                    }
                    typeValues[key] = raw;
                }
            }

            var confirm = new TaskDialog("Import Schedule Round-Trip");
            confirm.MainInstruction = "Apply CSV values to the identified model elements?";
            confirm.MainContent = (rows.Count - 1) + " data rows and " + columns.Count +
                " parameter columns. ElementId and parameter-id headers must come from Export Schedule Round-Trip. " +
                "Blank cells are ignored. Double values are raw Revit internal units. This operation cannot restore deleted elements or deleted parameter definitions.";
            confirm.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.Cancel;
            if (confirm.Show() != TaskDialogResult.Yes) return Result.Cancelled;

            int updatedElements = 0, updatedParameters = 0, skippedRows = 0, failedValues = 0;
            var issues = new List<string>();
            var appliedTypeParameters = new HashSet<string>(StringComparer.Ordinal);
            ctx.Transact("Import native schedule rows", () =>
            {
                for (int r = 1; r < rows.Count; r++)
                {
                    string[] cells = rows[r];
                    if (cells.All(string.IsNullOrWhiteSpace)) continue;
                    int id;
                    if (!int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                    {
                        skippedRows++;
                        continue;
                    }
                    Element element = ctx.Doc.GetElement(new ElementId(id));
                    if (element == null)
                    {
                        skippedRows++;
                        if (issues.Count < 50) issues.Add("Row " + (r + 1) + ": ElementId " + id + " no longer exists.");
                        continue;
                    }

                    bool changed = false;
                    foreach (Column column in columns)
                    {
                        string raw = cells[column.CsvIndex];
                        if (string.IsNullOrEmpty(raw)) continue;
                        Element parameterOwner = column.IsTypeParameter
                            ? ctx.Doc.GetElement(element.GetTypeId())
                            : element;
                        Parameter parameter = parameterOwner == null
                            ? null
                            : ResolveParameter(ctx.Doc, parameterOwner, new ElementId(column.ParameterId));
                        string typeParameterKey = column.IsTypeParameter && parameterOwner != null
                            ? parameterOwner.Id.Val().ToString(CultureInfo.InvariantCulture) + ":" +
                                column.ParameterId.ToString(CultureInfo.InvariantCulture)
                            : null;
                        if (column.IsTypeParameter && typeParameterKey != null &&
                            appliedTypeParameters.Contains(typeParameterKey))
                            continue;
                        if (parameter == null || parameter.IsReadOnly)
                        {
                            failedValues++;
                            if (issues.Count < 50) issues.Add("Row " + (r + 1) + ", " + column.Name + ": parameter is missing or read-only.");
                            continue;
                        }
                        try
                        {
                            bool ok = false;
                            switch (parameter.StorageType)
                            {
                                case StorageType.String:
                                    ok = parameter.Set(raw);
                                    break;
                                case StorageType.Integer:
                                    int integerValue;
                                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
                                        ok = parameter.Set(integerValue);
                                    break;
                                case StorageType.Double:
                                    double doubleValue;
                                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue) &&
                                        !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue))
                                        ok = parameter.Set(doubleValue);
                                    break;
                                case StorageType.ElementId:
                                    int referencedId;
                                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out referencedId))
                                    {
                                        ElementId referenceId = new ElementId(referencedId);
                                        if (referencedId < 0 || ctx.Doc.GetElement(referenceId) != null)
                                            ok = parameter.Set(referenceId);
                                    }
                                    break;
                            }
                            if (ok)
                            {
                                updatedParameters++;
                                changed = true;
                                if (typeParameterKey != null) appliedTypeParameters.Add(typeParameterKey);
                            }
                            else
                            {
                                failedValues++;
                                if (issues.Count < 50) issues.Add("Row " + (r + 1) + ", " + column.Name + ": value was rejected.");
                            }
                        }
                        catch (Exception ex)
                        {
                            failedValues++;
                            Log.Error(ex);
                            if (issues.Count < 50) issues.Add("Row " + (r + 1) + ", " + column.Name + ": " + ex.Message);
                        }
                    }
                    if (changed) updatedElements++;
                }
            });

            string summary = "Updated elements: " + updatedElements + "\nUpdated parameters: " + updatedParameters +
                "\nSkipped rows: " + skippedRows + "\nFailed values: " + failedValues;
            if (issues.Count > 0) summary += "\n\nIssues:\n" + string.Join("\n", issues.Take(15));
            TaskDialog.Show("Import Schedule Round-Trip", summary);
            return Result.Succeeded;
        }

        private static Parameter ResolveParameter(Document doc, Element owner, ElementId parameterId)
        {
            if (owner == null || parameterId == null || parameterId == ElementId.InvalidElementId)
                return null;
            if (parameterId.Val() < 0)
                return owner.get_Parameter((BuiltInParameter)parameterId.Val());
            ParameterElement parameterElement = doc.GetElement(parameterId) as ParameterElement;
            return parameterElement == null ? null : owner.get_Parameter(parameterElement.GetDefinition());
        }

    }
}
