using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Counts the selected elements (or every model element in the active view) by category, family and type,
    /// adding up lengths for linear elements. Writes a CSV report and shows the table.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class QuantityTakeoffCommand : ElecCommand
    {
        private sealed class Row
        {
            public string Category, Family, Type;
            public int Count;
            public double LengthFeet;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            IEnumerable<Element> source;
            var selected = ctx.UiDoc.Selection.GetElementIds();
            if (selected.Count > 0)
            {
                source = selected.Select(id => ctx.Doc.GetElement(id)).Where(e => e != null).ToList();
            }
            else
            {
                if (view is ViewSchedule)
                {
                    TaskDialog.Show("Quantity Takeoff", "Select elements, or open a plan, section or 3D view.");
                    return Result.Cancelled;
                }
                source = new FilteredElementCollector(ctx.Doc, view.Id).WhereElementIsNotElementType().ToList();
            }

            Busy.On();
            var rows = new Dictionary<string, Row>();
            foreach (Element element in source)
            {
                if (element.Category == null || element.Category.CategoryType != CategoryType.Model) continue;

                var type = ctx.Doc.GetElement(element.GetTypeId()) as ElementType;
                string family = type != null ? type.FamilyName : "";
                string typeName = type != null ? type.Name : element.Name;
                string category = element.Category.Name;

                string key = category + "|" + family + "|" + typeName;
                Row row;
                if (!rows.TryGetValue(key, out row))
                {
                    row = new Row { Category = category, Family = family, Type = typeName };
                    rows[key] = row;
                }
                row.Count++;

                var location = element.Location as LocationCurve;
                if (location != null) row.LengthFeet += location.Curve.Length;
            }

            Busy.Off();
            if (rows.Count == 0)
            {
                TaskDialog.Show("Quantity Takeoff", "No model elements were found in the selection or in this view.");
                return Result.Cancelled;
            }

            List<Row> sorted = rows.Values.OrderBy(r => r.Category).ThenBy(r => r.Family).ThenBy(r => r.Type).ToList();

            var csv = new StringBuilder();
            csv.AppendLine("Category,Family,Type,Count,Total length (m)");
            foreach (Row r in sorted)
                csv.AppendLine(string.Join(",", Csv.Escape(r.Category), Csv.Escape(r.Family), Csv.Escape(r.Type),
                    r.Count, r.LengthFeet > 0 ? Meters(r.LengthFeet) : ""));
            string path = Reports.PathFor("QuantityTakeoff", ".csv");
            System.IO.File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));

            int wCat = Math.Max(8, sorted.Max(r => r.Category.Length));
            int wFam = Math.Max(6, sorted.Max(r => r.Family.Length));
            int wTyp = Math.Max(4, sorted.Max(r => r.Type.Length));
            var text = new StringBuilder();
            text.AppendLine("QUANTITY TAKEOFF  (" + (selected.Count > 0 ? "selection" : "view: " + view.Name) + ")");
            text.AppendLine();
            text.AppendLine("Category".PadRight(wCat) + "  " + "Family".PadRight(wFam) + "  " + "Type".PadRight(wTyp) + "  " + "Count".PadLeft(6) + "  Length (m)");
            text.AppendLine(new string('-', wCat + wFam + wTyp + 28));
            foreach (Row r in sorted)
                text.AppendLine(r.Category.PadRight(wCat) + "  " + r.Family.PadRight(wFam) + "  " + r.Type.PadRight(wTyp) + "  " +
                                r.Count.ToString().PadLeft(6) + "  " + (r.LengthFeet > 0 ? Meters(r.LengthFeet) : ""));
            text.AppendLine();
            text.AppendLine("Total elements: " + sorted.Sum(r => r.Count));
            text.AppendLine("CSV: " + path);

            Dialogs.ShowText("Quantity Takeoff", text.ToString());
            return Result.Succeeded;
        }

        private static string Meters(double feet)
        {
            return (feet * 0.3048).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
