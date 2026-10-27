using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Removes the view overrides from every device on a circuit in the active view.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ClearColorsCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is ViewSchedule || view is ViewSheet)
            {
                TaskDialog.Show("Clear Colors", "Open a plan, section, elevation or 3D view first.");
                return Result.Cancelled;
            }

            List<FamilyInstance> devices = CircuitDevices.InView(ctx.Doc, view);
            if (devices.Count == 0)
            {
                TaskDialog.Show("Clear Colors", "No devices on a circuit were found in this view.");
                return Result.Cancelled;
            }

            int cleared = 0, blocked = 0;
            ctx.Transact("Clear colors", () =>
            {
                foreach (FamilyInstance device in devices)
                {
                    try
                    {
                        view.SetElementOverrides(device.Id, new OverrideGraphicSettings());
                        cleared++;
                    }
                    catch (Exception ex)
                    {
                        if (blocked == 0) Log.Error(ex);
                        blocked++;
                    }
                }
            });

            TaskDialog.Show("Clear Colors", cleared + " device(s) reset." + (blocked > 0 ? "\n" + blocked + " could not be reset (view template?)." : ""));
            return Result.Succeeded;
        }
    }
}
