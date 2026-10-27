using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Writes the room number and/or name each electrical device is in into a text parameter.</summary>
    [Transaction(TransactionMode.Manual)]
    public class RoomToParameterCommand : ElecCommand
    {
        private static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices,
            BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_DataDevices, BuiltInCategory.OST_FireAlarmDevices
        };

        protected override Result Run(ToolContext ctx, ref string message)
        {
            var devices = new List<FamilyInstance>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                var instance = ctx.Doc.GetElement(id) as FamilyInstance;
                if (instance != null) devices.Add(instance);
            }
            if (devices.Count == 0)
            {
                int scope = Dialogs.Choose("Room to Parameter", "No selection. Use:", new List<string>
                {
                    "All electrical devices in the active view",
                    "All electrical devices in the model"
                }, 0);
                if (scope < 0) return Result.Cancelled;
                if (scope == 0 && ctx.Doc.ActiveView is ViewSchedule)
                {
                    TaskDialog.Show("Room to Parameter", "Open a plan or 3D view, or choose the whole model.");
                    return Result.Cancelled;
                }

                var collector = scope == 0
                    ? new FilteredElementCollector(ctx.Doc, ctx.Doc.ActiveView.Id)
                    : new FilteredElementCollector(ctx.Doc);
                devices = collector.WherePasses(new ElementMulticategoryFilter(Categories.ToList()))
                    .WhereElementIsNotElementType().OfType<FamilyInstance>().ToList();
            }
            if (devices.Count == 0)
            {
                TaskDialog.Show("Room to Parameter", "No devices were found.");
                return Result.Cancelled;
            }

            int value = Dialogs.Choose("Room to Parameter", "Write:", new List<string>
            {
                "Room number", "Room name", "Room number and name"
            }, 0);
            if (value < 0) return Result.Cancelled;

            string parameterName = ParameterChooser.Choose(devices[0], "Room to Parameter", ElecSettings.Current.LastLabelParam);
            if (parameterName == null) return Result.Cancelled;

            int written = 0, noRoom = 0, noParameter = 0;
            ctx.Transact("Room to parameter", () =>
            {
                foreach (FamilyInstance device in devices)
                {
                    Room room = RoomLookup.Of(ctx.Doc, device);
                    if (room == null) { noRoom++; continue; }

                    Parameter p = device.LookupParameter(parameterName);
                    if (p == null || p.IsReadOnly || p.StorageType != StorageType.String) { noParameter++; continue; }

                    string number = room.Number ?? "";
                    string name = RoomLookup.NameOf(room);
                    p.Set(value == 0 ? number : value == 1 ? name : (number + " " + name).Trim());
                    written++;
                }
            });

            string text = written + " device(s) updated (" + parameterName + ").";
            if (noRoom > 0) text += "\n" + noRoom + " device(s) are not in a room.";
            if (noParameter > 0) text += "\n" + noParameter + " device(s) have no writable text parameter with that name.";
            TaskDialog.Show("Room to Parameter", text);
            return Result.Succeeded;
        }
    }
}
