using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;

namespace ElecKit.Core
{
    public static class RibbonBuilder
    {
        public const string TabName = "ElecKit";

        public static void Build(UIControlledApplication app)
        {
            try { app.CreateRibbonTab(TabName); }
            catch (Autodesk.Revit.Exceptions.ArgumentException ex)
            {
                // Revit reports an ArgumentException when a ribbon tab with this name already exists.
                // Do not hide unrelated startup errors that could leave the ribbon only partially built.
                if (ex.Message == null || ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) < 0)
                    throw;
            }

            string assemblyPath = typeof(RibbonBuilder).Assembly.Location;
            var panels = new Dictionary<string, RibbonPanel>();

            foreach (var tool in ToolRegistry.Tools)
            {
                RibbonPanel panel;
                if (!panels.TryGetValue(tool.Panel, out panel))
                {
                    panel = app.CreateRibbonPanel(TabName, tool.Panel);
                    panels[tool.Panel] = panel;
                }

                var data = new PushButtonData(tool.Id, tool.Title, assemblyPath, tool.Command.FullName);
                data.ToolTip = tool.Tooltip;
                panel.AddItem(data);
            }
        }
    }
}
