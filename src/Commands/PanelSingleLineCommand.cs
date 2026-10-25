using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Builds a text single-line report for a picked panel: its circuits with number, load name,
    /// poles, voltage, apparent load and number of connected elements. Shown in a window and saved as .txt.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class PanelSingleLineCommand : ElecCommand
    {
        private sealed class CircuitLine
        {
            public string Number, Load, Poles, Voltage, Apparent;
            public int Elements;
        }

        protected override Result Run(ToolContext ctx, ref string message)
        {
            Element panel = ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element,
                new PredicateFilter(e => e is FamilyInstance && e.Category != null &&
                                         RevitApiCompat.ElementIdValue(e.Category.Id) == (int)BuiltInCategory.OST_ElectricalEquipment),
                "Pick the panel"));

            string panelName = Text(panel, BuiltInParameter.RBS_ELEC_PANEL_NAME);
            if (panelName.Length == 0) panelName = panel.Name;

            var lines = new List<CircuitLine>();
            foreach (Element item in new FilteredElementCollector(ctx.Doc).OfClass(typeof(ElectricalSystem)))
            {
                var system = item as ElectricalSystem;
                if (system == null) continue;
                FamilyInstance baseEquipment = system.BaseEquipment;
                if (baseEquipment == null || RevitApiCompat.ElementIdValue(baseEquipment.Id) != RevitApiCompat.ElementIdValue(panel.Id)) continue;

                lines.Add(new CircuitLine
                {
                    Number = system.CircuitNumber ?? "",
                    Load = system.LoadName ?? "",
                    Poles = Text(system, BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES),
                    Voltage = Text(system, BuiltInParameter.RBS_ELEC_VOLTAGE),
                    Apparent = Text(system, BuiltInParameter.RBS_ELEC_APPARENT_LOAD),
                    Elements = system.Elements.Size
                });
            }

            if (lines.Count == 0)
            {
                TaskDialog.Show("Panel Single-Line Report", "No circuits are fed from " + panelName + ".");
                return Result.Cancelled;
            }
            lines = lines.OrderBy(l => Sort(l.Number)).ThenBy(l => l.Number).ToList();

            int wLoad = Math.Max(9, lines.Max(l => l.Load.Length));
            int wVolt = Math.Max(7, lines.Max(l => l.Voltage.Length));
            int wApp = Math.Max(8, lines.Max(l => l.Apparent.Length));

            var text = new StringBuilder();
            text.AppendLine("PANEL  " + panelName);
            text.AppendLine("Circuits: " + lines.Count);
            text.AppendLine("  |");
            foreach (CircuitLine l in lines)
            {
                text.AppendLine("  +-- " + l.Number.PadRight(5) + " " + l.Load.PadRight(wLoad) + "  " + l.Poles.PadRight(3) + "P  " +
                                l.Voltage.PadRight(wVolt) + "  " + l.Apparent.PadRight(wApp) + "  " + l.Elements + " element(s)");
            }
            text.AppendLine();
            text.AppendLine("Columns: circuit, load name, poles, voltage, apparent load, connected elements");

            string path = Reports.PathFor("PanelSingleLine_" + panelName, ".txt");
            System.IO.File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
            text.AppendLine("Saved to: " + path);

            Dialogs.ShowText("Panel Single-Line Report: " + panelName, text.ToString());
            return Result.Succeeded;
        }

        private static string Text(Element element, BuiltInParameter id)
        {
            Parameter p = element.get_Parameter(id);
            if (p == null) return "";
            string value = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            return value ?? "";
        }

        /// <summary>Circuit numbers sort numerically when they are numbers.</summary>
        private static int Sort(string number)
        {
            int n;
            return int.TryParse(number, out n) ? n : int.MaxValue;
        }
    }
}
