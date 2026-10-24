using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Reports the selected conduits (or every conduit in the active view): type, size, length, level and comments,
    /// plus totals by size. Writes a CSV and shows the summary.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ConduitRunReportCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            var selected = ctx.UiDoc.Selection.GetElementIds();

            List<Conduit> conduits;
            if (selected.Count > 0)
                conduits = selected.Select(id => ctx.Doc.GetElement(id)).OfType<Conduit>().ToList();
            else if (view is ViewSchedule)
                conduits = new FilteredElementCollector(ctx.Doc).OfClass(typeof(Conduit)).Cast<Conduit>().ToList();
            else
                conduits = new FilteredElementCollector(ctx.Doc, view.Id).OfClass(typeof(Conduit)).Cast<Conduit>().ToList();

            if (conduits.Count == 0)
            {
                TaskDialog.Show("Conduit Run Report", "No conduits were found in the selection or in this view.");
                return Result.Cancelled;
            }

            var csv = new StringBuilder();
            csv.AppendLine("Id,Type,Size,Length,Length (m),Level,Comments");
            var bySize = new SortedDictionary<string, KeyValuePair<int, double>>(StringComparer.OrdinalIgnoreCase);
            double totalFeet = 0;

            foreach (Conduit conduit in conduits)
            {
                string typeName = Name(ctx.Doc.GetElement(conduit.GetTypeId()));
                string size = Value(conduit, BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                string length = Value(conduit, BuiltInParameter.CURVE_ELEM_LENGTH);
                Parameter lengthParameter = conduit.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                double feet = lengthParameter != null ? lengthParameter.AsDouble() : 0.0;
                string level = Name(ctx.Doc.GetElement(conduit.LevelId));
                string comments = Value(conduit, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);

                csv.AppendLine(string.Join(",", conduit.Id.IntegerValue, Csv.Escape(typeName), Csv.Escape(size),
                    Csv.Escape(length), Meters(feet), Csv.Escape(level), Csv.Escape(comments)));

                string key = size.Length > 0 ? size : "(no size)";
                KeyValuePair<int, double> current;
                bySize.TryGetValue(key, out current);
                bySize[key] = new KeyValuePair<int, double>(current.Key + 1, current.Value + feet);
                totalFeet += feet;
            }

            string path = Reports.PathFor("ConduitRunReport", ".csv");
            System.IO.File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));

            var text = new StringBuilder();
            text.AppendLine("CONDUIT RUN REPORT  (" + (selected.Count > 0 ? "selection" : "view: " + view.Name) + ")");
            text.AppendLine();
            text.AppendLine("Size".PadRight(16) + "Count".PadLeft(7) + "   Length (m)");
            text.AppendLine(new string('-', 38));
            foreach (var entry in bySize)
                text.AppendLine(entry.Key.PadRight(16) + entry.Value.Key.ToString().PadLeft(7) + "   " + Meters(entry.Value.Value));
            text.AppendLine(new string('-', 38));
            text.AppendLine("Total".PadRight(16) + conduits.Count.ToString().PadLeft(7) + "   " + Meters(totalFeet));
            text.AppendLine();
            text.AppendLine("CSV: " + path);

            Dialogs.ShowText("Conduit Run Report", text.ToString());
            return Result.Succeeded;
        }

        private static string Name(Element element)
        {
            return element != null ? element.Name : "";
        }

        private static string Value(Element element, BuiltInParameter id)
        {
            Parameter p = element.get_Parameter(id);
            if (p == null) return "";
            string value = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            return value ?? "";
        }

        private static string Meters(double feet)
        {
            return (feet * 0.3048).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
