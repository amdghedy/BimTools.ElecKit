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
    /// Creates a new drafting view holding a text-note summary of cable trays (count and length by type and size).
    /// Uses the selected trays, or every tray in the project.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class TraySummaryViewCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var selected = ctx.UiDoc.Selection.GetElementIds();
            List<CableTray> trays = selected.Count > 0
                ? selected.Select(id => ctx.Doc.GetElement(id)).OfType<CableTray>().ToList()
                : new FilteredElementCollector(ctx.Doc).OfClass(typeof(CableTray)).Cast<CableTray>().ToList();

            if (trays.Count == 0)
            {
                TaskDialog.Show("Tray Summary View", "No cable trays were found.");
                return Result.Cancelled;
            }

            var groups = new SortedDictionary<string, KeyValuePair<int, double>>(StringComparer.OrdinalIgnoreCase);
            double totalFeet = 0;
            foreach (CableTray tray in trays)
            {
                Element type = ctx.Doc.GetElement(tray.GetTypeId());
                string typeName = type != null ? type.Name : "Cable tray";
                string width = Value(tray, BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                string height = Value(tray, BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
                string key = typeName + "   " + width + " x " + height;

                Parameter lengthParameter = tray.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                double feet = lengthParameter != null ? lengthParameter.AsDouble() : 0.0;

                KeyValuePair<int, double> current;
                groups.TryGetValue(key, out current);
                groups[key] = new KeyValuePair<int, double>(current.Key + 1, current.Value + feet);
                totalFeet += feet;
            }

            var text = new StringBuilder();
            text.Append("CABLE TRAY SUMMARY\r\r");
            text.Append("Type   Width x Height   |   Count   |   Length (m)\r");
            foreach (var entry in groups)
                text.Append(entry.Key + "   |   " + entry.Value.Key + "   |   " + Meters(entry.Value.Value) + "\r");
            text.Append("\rTotal: " + trays.Count + " tray(s), " + Meters(totalFeet) + " m");

            ViewFamilyType draftingType = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Drafting);
            if (draftingType == null)
            {
                TaskDialog.Show("Tray Summary View", "This project has no drafting view type.");
                return Result.Cancelled;
            }

            ViewDrafting view = null;
            ctx.Transact("Tray summary view", () =>
            {
                view = ViewDrafting.Create(ctx.Doc, draftingType.Id);
                view.Name = "Cable Tray Summary " + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                ElementId textType = ctx.Doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                TextNote.Create(ctx.Doc, view.Id, XYZ.Zero, text.ToString(), textType);
            });

            ctx.UiDoc.ActiveView = view;
            return Result.Succeeded;
        }

        private static string Value(Element element, BuiltInParameter id)
        {
            Parameter p = element.get_Parameter(id);
            if (p == null) return "?";
            string value = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            return value ?? "?";
        }

        private static string Meters(double feet)
        {
            return (feet * 0.3048).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
