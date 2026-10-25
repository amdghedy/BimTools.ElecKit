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
            catch { /* tab already exists */ }

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
                try { data.LargeImage = IconFactory.For(tool); }
                catch (System.Exception ex) { Log.Error(ex); }   // icons are optional
                panel.AddItem(data);
            }
        }
    }
}
