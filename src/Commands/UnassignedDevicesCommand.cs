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
    /// Finds electrical devices that are not on any circuit, selects them, and saves a CSV list.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class UnassignedDevicesCommand : ElecCommand
    {
        private static readonly string[] Names =
        {
            "Electrical fixtures", "Lighting fixtures", "Lighting devices (switches)", "Electrical equipment",
            "Data devices", "Fire alarm devices", "All of the above"
        };

        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_DataDevices, BuiltInCategory.OST_FireAlarmDevices
        };

        protected override Result Run(ToolContext ctx, ref string message)
        {
            int category = Dialogs.Choose("Unassigned Devices", "Check these devices:", Names, 0);
            if (category < 0) return Result.Cancelled;

            int scope = Dialogs.Choose("Unassigned Devices", "Look in:", new List<string> { "The whole model", "The active view only" }, 0);
            if (scope < 0) return Result.Cancelled;
            if (scope == 1 && ctx.Doc.ActiveView is ViewSchedule)
            {
                TaskDialog.Show("Unassigned Devices", "Open a plan, section or 3D view, or choose the whole model.");
                return Result.Cancelled;
            }

            List<BuiltInCategory> checkedCategories = category == Categories.Length
                ? Categories.ToList()
                : new List<BuiltInCategory> { Categories[category] };

            var collector = scope == 0
                ? new FilteredElementCollector(ctx.Doc)
                : new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id);

            List<FamilyInstance> devices = collector
                .WherePasses(new ElementMulticategoryFilter(checkedCategories))
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .ToList();

            if (devices.Count == 0)
            {
                TaskDialog.Show("Unassigned Devices", "No devices of that kind were found.");
                return Result.Cancelled;
            }

            List<FamilyInstance> unassigned = devices.Where(d => CircuitInfo.Of(d) == null).ToList();
            if (unassigned.Count == 0)
            {
                TaskDialog.Show("Unassigned Devices", "All " + devices.Count + " device(s) are on a circuit.");
                return Result.Succeeded;
            }

            var csv = new StringBuilder();
            csv.AppendLine("Id,Category,Family,Type,Level,Mark");
            foreach (FamilyInstance d in unassigned)
            {
                Element level = ctx.Doc.GetElement(d.LevelId);
                Parameter mark = d.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                csv.AppendLine(string.Join(",", d.Id.Val(),
                    Csv.Escape(d.Category != null ? d.Category.Name : ""),
                    Csv.Escape(d.Symbol != null ? d.Symbol.FamilyName : ""),
                    Csv.Escape(d.Name),
                    Csv.Escape(level != null ? level.Name : ""),
                    Csv.Escape(mark != null ? mark.AsString() : "")));
            }
            string path = Reports.PathFor("UnassignedDevices", ".csv");
            System.IO.File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));

            ctx.UiDoc.Selection.SetElementIds(unassigned.Select(d => d.Id).ToList());
            TaskDialog.Show("Unassigned Devices",
                unassigned.Count + " of " + devices.Count + " device(s) are not on any circuit. They are now selected.\nList: " + path);
            Reports.OpenIfWanted(path);
            return Result.Succeeded;
        }
    }
}
