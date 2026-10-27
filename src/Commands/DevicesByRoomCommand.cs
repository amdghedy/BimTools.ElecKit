using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Counts electrical devices in each room, by category and type. Saves a CSV and shows the totals per room.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class DevicesByRoomCommand : ElecCommand
    {
        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_DataDevices, BuiltInCategory.OST_FireAlarmDevices
        };

        protected override Result Run(ToolContext ctx, ref string message)
        {
            List<FamilyInstance> devices = new FilteredElementCollector(ctx.Doc)
                .WherePasses(new ElementMulticategoryFilter(Categories.ToList()))
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .ToList();
            if (devices.Count == 0)
            {
                TaskDialog.Show("Devices by Room", "No electrical devices were found.");
                return Result.Cancelled;
            }

            Busy.On();
            var rows = new Dictionary<string, int>();
            var perRoom = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int outside = 0;

            foreach (FamilyInstance device in devices)
            {
                Room room = RoomLookup.Of(ctx.Doc, device);
                string roomLabel;
                string number = "", name = "(not in a room)";
                if (room != null)
                {
                    number = room.Number ?? "";
                    name = RoomLookup.NameOf(room);
                }
                else outside++;
                roomLabel = (number + "|" + name);

                string category = device.Category != null ? device.Category.Name : "";
                string family = device.Symbol != null ? device.Symbol.FamilyName : "";
                string key = roomLabel + "|" + category + "|" + family + "|" + device.Name;

                int count;
                rows.TryGetValue(key, out count);
                rows[key] = count + 1;

                int roomCount;
                string roomDisplay = (number + " " + name).Trim();
                perRoom.TryGetValue(roomDisplay, out roomCount);
                perRoom[roomDisplay] = roomCount + 1;
            }

            Busy.Off();
            var csv = new StringBuilder();
            csv.AppendLine("Room number,Room name,Category,Family,Type,Count");
            foreach (var pair in rows.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                string[] parts = pair.Key.Split('|');
                csv.AppendLine(string.Join(",", Csv.Escape(parts[0]), Csv.Escape(parts[1]), Csv.Escape(parts[2]),
                    Csv.Escape(parts[3]), Csv.Escape(parts[4]), pair.Value));
            }
            string path = Reports.PathFor("DevicesByRoom", ".csv");
            System.IO.File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));

            var text = new StringBuilder();
            text.AppendLine("DEVICES BY ROOM: " + devices.Count + " device(s) in " + perRoom.Count + " room group(s)");
            text.AppendLine();
            foreach (var room in perRoom) text.AppendLine("  " + room.Value.ToString().PadLeft(5) + "   " + room.Key);
            if (outside > 0) text.AppendLine("\n" + outside + " device(s) are not inside any room.");
            text.AppendLine("\nFull table by category and type: " + path);
            Dialogs.ShowText("Devices by Room", text.ToString());
            return Result.Succeeded;
        }
    }
}
