using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class ScheduleExport
    {
        public static ViewScheduleExportOptions Options()
        {
            var options = new ViewScheduleExportOptions();
            options.FieldDelimiter = ",";
            options.TextQualifier = ExportTextQualifier.DoubleQuote;
            options.Title = true;
            options.HeadersFootersBlanks = true;
            options.ColumnHeaders = ExportColumnHeaders.OneRow;
            return options;
        }
    }
}
