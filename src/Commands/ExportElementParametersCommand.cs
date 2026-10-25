using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;
using Microsoft.Win32;

namespace ElecKit.Commands
{
    /// <summary>
    /// Exports selected instance parameters in the same keyed CSV shape accepted by ImportScheduleValuesCommand.
    /// ElementId is the primary key; Mark is included for human review, not used as a fallback when ElementId exists.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportElementParametersCommand : ElecCommand
    {
        private sealed class Column
        {
            public string Name;
            public StorageType Storage;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            IList<Reference> picked;
            try
            {
                picked = ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                    "Select model instances to export. Press Finish in the selection bar when done.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            var elements = new List<Element>();
            var seen = new HashSet<long>();
            foreach (Reference reference in picked)
            {
                Element e = ctx.Doc.GetElement(reference);
                if (e != null && !(e is ElementType) && seen.Add(RevitApiCompat.ElementIdValue(e.Id))) elements.Add(e);
            }
            if (elements.Count == 0)
            {
                TaskDialog.Show("Export Element Parameters", "No model instances were selected.");
                return Result.Cancelled;
            }

            // Include only parameter names that are unambiguous on every selected element and share one storage type.
            var candidates = new Dictionary<string, StorageType>(StringComparer.OrdinalIgnoreCase);
            var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Element e in elements)
            {
                var perElement = new Dictionary<string, StorageType>(StringComparer.OrdinalIgnoreCase);
                foreach (Parameter p in e.Parameters)
                {
                    if (p == null || p.Definition == null || p.IsReadOnly ||
                        p.StorageType == StorageType.None || string.IsNullOrWhiteSpace(p.Definition.Name)) continue;
                    string name = p.Definition.Name.Trim();
                    if (string.Equals(name, "ElementId", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Element ID", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Mark", StringComparison.OrdinalIgnoreCase)) continue;
                    if (perElement.ContainsKey(name)) { ambiguous.Add(name); continue; }
                    perElement[name] = p.StorageType;
                }

                foreach (var item in perElement)
                {
                    StorageType existing;
                    if (!candidates.TryGetValue(item.Key, out existing)) candidates[item.Key] = item.Value;
                    else if (existing != item.Value) ambiguous.Add(item.Key);
                }
            }

            var columns = candidates
                .Where(kv => !ambiguous.Contains(kv.Key))
                .Where(kv => elements.All(e => e.LookupParameter(kv.Key) != null &&
                    !e.LookupParameter(kv.Key).IsReadOnly && e.LookupParameter(kv.Key).StorageType == kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new Column { Name = kv.Key, Storage = kv.Value })
                .ToList();

            if (columns.Count == 0)
            {
                TaskDialog.Show("Export Element Parameters",
                    "No common, writable, unambiguous instance parameters were found across the selection.");
                return Result.Cancelled;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Export Keyed Element Parameter CSV",
                Filter = "CSV (*.csv)|*.csv",
                FileName = "ElecKit_ElementParameters_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
                AddExtension = true,
                DefaultExt = ".csv"
            };
            if (dlg.ShowDialog() != true) return Result.Cancelled;

            var csv = new StringBuilder();
            csv.Append("ElementId,Mark");
            foreach (Column column in columns) csv.Append(",").Append(Csv.Escape(column.Name));
            csv.AppendLine();

            foreach (Element e in elements.OrderBy(x => RevitApiCompat.ElementIdValue(x.Id)))
            {
                Parameter markParameter = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                string mark = markParameter != null && markParameter.StorageType == StorageType.String
                    ? markParameter.AsString() : "";
                csv.Append(RevitApiCompat.ElementIdValue(e.Id).ToString(CultureInfo.InvariantCulture))
                    .Append(",").Append(Csv.Escape(mark));
                foreach (Column column in columns)
                {
                    Parameter p = e.LookupParameter(column.Name);
                    string value = "";
                    if (p != null)
                    {
                        if (p.StorageType == StorageType.String) value = p.AsString() ?? "";
                        else if (p.StorageType == StorageType.Integer)
                            value = p.AsInteger().ToString(CultureInfo.InvariantCulture);
                        else if (p.StorageType == StorageType.Double)
                            value = p.AsValueString() ?? "";
                        // ElementId-valued parameters are deliberately excluded from the import contract.
                    }
                    csv.Append(",").Append(Csv.Escape(value));
                }
                csv.AppendLine();
            }

            File.WriteAllText(dlg.FileName, csv.ToString(), new UTF8Encoding(true));
            TaskDialog.Show("Export Element Parameters",
                elements.Count + " elements and " + columns.Count +
                " common writable parameter columns exported. The ElementId column is the primary import key. " +
                "Review the CSV before importing; blank values are intentionally ignored by the importer.\n\n" + dlg.FileName);
            if (!ElecSettings.Current.DoNotOpenReports)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            return Result.Succeeded;
        }
    }
}