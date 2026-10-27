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
    /// <summary>A one-page health check of the electrical model: devices and circuits, panels, conduit and tray lengths, warnings.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ProjectHealthCommand : ElecCommand
    {
        private static readonly string[] Names =
        {
            "Electrical fixtures", "Lighting fixtures", "Lighting devices", "Electrical equipment", "Data devices", "Fire alarm devices"
        };

        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_DataDevices, BuiltInCategory.OST_FireAlarmDevices
        };

        protected override Result Run(ToolContext ctx, ref string message)
        {
            Busy.On();
            var text = new StringBuilder();
            text.AppendLine("ELECTRICAL MODEL HEALTH CHECK: " + ctx.Doc.Title);
            text.AppendLine();

            text.AppendLine("DEVICES".PadRight(24) + "Total".PadLeft(7) + "On circuit".PadLeft(12) + "No circuit".PadLeft(12));
            int totalDevices = 0, totalUnassigned = 0;
            for (int i = 0; i < Categories.Length; i++)
            {
                List<FamilyInstance> devices = new FilteredElementCollector(ctx.Doc)
                    .OfCategory(Categories[i]).WhereElementIsNotElementType().OfType<FamilyInstance>().ToList();
                int unassigned = devices.Count(d => CircuitInfo.Of(d) == null);
                totalDevices += devices.Count;
                totalUnassigned += unassigned;
                text.AppendLine(("  " + Names[i]).PadRight(24) + devices.Count.ToString().PadLeft(7) +
                                (devices.Count - unassigned).ToString().PadLeft(12) + unassigned.ToString().PadLeft(12));
            }
            text.AppendLine();

            List<ElectricalSystem> systems = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(ElectricalSystem)).Cast<ElectricalSystem>().ToList();
            int noPanel = systems.Count(s => s.BaseEquipment == null);
            int empty = systems.Count(s => s.Elements.Size == 0);
            text.AppendLine("CIRCUITS");
            text.AppendLine("  Circuits in the model:        " + systems.Count);
            text.AppendLine("  Not connected to a panel:     " + noPanel);
            text.AppendLine("  With no elements:             " + empty);
            text.AppendLine();

            double conduitFeet = 0, trayFeet = 0;
            int conduits = 0, trays = 0;
            foreach (Element e in new FilteredElementCollector(ctx.Doc).OfClass(typeof(Conduit)))
            {
                conduits++;
                Parameter p = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (p != null) conduitFeet += p.AsDouble();
            }
            foreach (Element e in new FilteredElementCollector(ctx.Doc).OfClass(typeof(CableTray)))
            {
                trays++;
                Parameter p = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (p != null) trayFeet += p.AsDouble();
            }
            text.AppendLine("RACEWAYS");
            text.AppendLine("  Conduits: " + conduits + "   total " + (conduitFeet * 0.3048).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m");
            text.AppendLine("  Cable trays: " + trays + "   total " + (trayFeet * 0.3048).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m");
            text.AppendLine();

            int warnings = ctx.Doc.GetWarnings().Count;
            text.AppendLine("MODEL");
            text.AppendLine("  Warnings in the model: " + warnings + (warnings > 0 ? "   (use Model Warnings for the list)" : ""));
            text.AppendLine();

            var notes = new List<string>();
            if (totalUnassigned > 0) notes.Add(totalUnassigned + " device(s) are on no circuit: use Unassigned Devices to find them.");
            if (noPanel > 0) notes.Add(noPanel + " circuit(s) have no panel: use Create Circuits or edit them in Revit.");
            if (empty > 0) notes.Add(empty + " circuit(s) have no elements and can be deleted.");
            if (warnings > 100) notes.Add("The model has many warnings; resolve them before issuing drawings.");
            text.AppendLine(notes.Count == 0 ? "Nothing needs attention." : "TO DO");
            foreach (string note in notes) text.AppendLine("  - " + note);

            Busy.Off();
            string path = Reports.PathFor("ElectricalHealth", ".txt");
            System.IO.File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
            text.AppendLine();
            text.AppendLine("Saved to: " + path);

            Dialogs.ShowText("Electrical Model Health Check", text.ToString());
            return Result.Succeeded;
        }
    }
}
