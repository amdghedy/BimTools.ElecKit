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
using ElecKit.Core;
using Microsoft.Win32;

namespace ElecKit.Commands
{
    /// <summary>
    /// Exports itemized native schedule rows with stable ElementId keys and parameter-id-qualified
    /// columns so the same CSV can be imported without relying on display-name uniqueness.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportScheduleRoundTripCommand : ElecCommand
    {
        private sealed class Column
        {
            public int ParameterId;
            public string Name;
            public bool IsTypeParameter;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            ViewSchedule schedule = ctx.Doc.ActiveView as ViewSchedule;
            if (schedule == null || schedule.IsTemplate)
            {
                TaskDialog.Show("Export Schedule Round-Trip", "Open a non-template schedule view first.");
                return Result.Cancelled;
            }
            if (schedule.Definition.CategoryId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("Export Schedule Round-Trip", "Multi-category schedules are not supported in this round-trip mode. Use a single-category itemized schedule.");
                return Result.Cancelled;
            }
            if (!schedule.Definition.IsItemized)
            {
                TaskDialog.Show("Export Schedule Round-Trip",
                    "Grouped schedules cannot be round-tripped safely because one visible row may represent multiple elements. Enable Itemize every instance and retry.");
                return Result.Cancelled;
            }
            if (schedule.Definition.GetFilters().Count > 0)
            {
                TaskDialog.Show("Export Schedule Round-Trip",
                    "This first round-trip mode accepts schedules without schedule filters. Remove the filters or use the normal schedule export to avoid exporting rows outside the displayed schedule.");
                return Result.Cancelled;
            }

            var columns = new List<Column>();
            var seenParameterIds = new HashSet<int>();
            foreach (ScheduleFieldId fieldId in schedule.Definition.GetFieldOrder())
            {
                ScheduleField field = schedule.Definition.GetField(fieldId);
                if (field.IsHidden) continue;
                if (field.IsCalculatedField || field.IsCombinedParameterField)
                {
                    TaskDialog.Show("Export Schedule Round-Trip",
                        "The schedule contains a calculated or combined field ('" + field.GetName() +
                        "'). This field cannot be imported as a direct element parameter. Remove it from the round-trip export and retry.");
                    return Result.Cancelled;
                }

                ElementId parameterId;
                ScheduleFieldType fieldType;
                try
                {
                    SchedulableField schedulable = field.GetSchedulableField();
                    parameterId = schedulable.ParameterId;
                    fieldType = schedulable.FieldType;
                }
                catch (Exception)
                {
                    TaskDialog.Show("Export Schedule Round-Trip",
                        "The schedule field '" + field.GetName() + "' is not directly parameter-backed and cannot be round-tripped.");
                    return Result.Cancelled;
                }
                if (fieldType != ScheduleFieldType.Instance && fieldType != ScheduleFieldType.ElementType)
                {
                    TaskDialog.Show("Export Schedule Round-Trip",
                        "The schedule field '" + field.GetName() + "' is not a direct instance/type parameter field.");
                    return Result.Cancelled;
                }
                if (parameterId == null || parameterId == ElementId.InvalidElementId ||
                    !seenParameterIds.Add(parameterId.IntegerValue))
                {
                    TaskDialog.Show("Export Schedule Round-Trip",
                        "The schedule contains an invalid or repeated parameter field ('" + field.GetName() +
                        "'). Remove duplicate/unsupported fields and retry.");
                    return Result.Cancelled;
                }
                columns.Add(new Column
                {
                    ParameterId = parameterId.IntegerValue,
                    Name = field.GetName(),
                    IsTypeParameter = fieldType == ScheduleFieldType.ElementType
                });
            }
            if (columns.Count == 0)
            {
                TaskDialog.Show("Export Schedule Round-Trip", "No visible direct parameter fields were found.");
                return Result.Cancelled;
            }

            IList<Element> elements;
            try
            {
                elements = new FilteredElementCollector(ctx.Doc, schedule.Id)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                TaskDialog.Show("Export Schedule Round-Trip",
                    "Revit could not enumerate the elements represented by this schedule. No CSV was written.\n\n" + ex.Message);
                return Result.Failed;
            }
            elements = elements
                .Where(e => e.Category != null && e.Category.Id.IntegerValue == schedule.Definition.CategoryId.IntegerValue)
                .OrderBy(e => e.Id.IntegerValue)
                .ToList();
            if (elements.Count == 0)
            {
                TaskDialog.Show("Export Schedule Round-Trip",
                    "The active schedule returned no model instances. No CSV was written.");
                return Result.Cancelled;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Export Native Schedule Rows for Round-Trip",
                Filter = "CSV (*.csv)|*.csv",
                FileName = FileNames.Safe(schedule.Name) + "_RoundTrip_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
                AddExtension = true,
                DefaultExt = ".csv"
            };
            if (dlg.ShowDialog() != true) return Result.Cancelled;

            var csv = new StringBuilder();
            csv.Append("__ElementId");
            foreach (Column column in columns)
                csv.Append(",").Append(Csv.Escape("__PARAMID_" + column.ParameterId.ToString(CultureInfo.InvariantCulture) +
                    (column.IsTypeParameter ? "__TYPE__" : "__INSTANCE__") + column.Name));
            csv.AppendLine();

            foreach (Element element in elements)
            {
                csv.Append(element.Id.IntegerValue.ToString(CultureInfo.InvariantCulture));
                foreach (Column column in columns)
                {
                    Element parameterOwner = column.IsTypeParameter
                        ? ctx.Doc.GetElement(element.GetTypeId())
                        : element;
                    Parameter parameter = parameterOwner == null ? null : parameterOwner.get_Parameter(new ElementId(column.ParameterId));
                    string value = "";
                    if (parameter != null)
                    {
                        switch (parameter.StorageType)
                        {
                            case StorageType.String:
                                value = parameter.AsString() ?? "";
                                break;
                            case StorageType.Integer:
                                value = parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
                                break;
                            case StorageType.Double:
                                value = parameter.AsDouble().ToString("R", CultureInfo.InvariantCulture);
                                break;
                            case StorageType.ElementId:
                                value = parameter.AsElementId().IntegerValue.ToString(CultureInfo.InvariantCulture);
                                break;
                        }
                    }
                    csv.Append(",").Append(Csv.Escape(value));
                }
                csv.AppendLine();
            }

            File.WriteAllText(dlg.FileName, csv.ToString(), new UTF8Encoding(true));
            TaskDialog.Show("Export Schedule Round-Trip",
                "Exported " + elements.Count + " schedule element rows and " + columns.Count +
                " direct parameter columns. Numeric values use invariant raw values (double values are Revit internal units). " +
                "The first column is the element identity key. Review the file before importing.\n\n" + dlg.FileName);
            if (!ElecSettings.Current.DoNotOpenReports)
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            return Result.Succeeded;
        }
    }
}