using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;

namespace ElecKit.Core
{
    public static class RibbonBuilder
    {
        public const string TabName = "ElecKit";

        /// <summary>
        /// One panel per group of tools. Buttons are stacked three high (small icon with the name beside it)
        /// to save space; a group that would leave a single button over is split 2 + 2 instead, and a panel
        /// with only one tool shows it as a large button.
        /// </summary>
        public static void Build(UIControlledApplication app)
        {
            try { app.CreateRibbonTab(TabName); }
            catch { /* tab already exists */ }

            string assemblyPath = typeof(RibbonBuilder).Assembly.Location;

            foreach (IGrouping<string, ToolInfo> group in ToolRegistry.Tools.GroupBy(t => t.Panel))
            {
                RibbonPanel panel = app.CreateRibbonPanel(TabName, group.Key);
                List<PushButtonData> buttons = group.Select(t => MakeButton(t, assemblyPath)).ToList();

                int index = 0;
                while (index < buttons.Count)
                {
                    int remaining = buttons.Count - index;
                    if (remaining == 1)
                    {
                        panel.AddItem(buttons[index]);
                        index++;
                        continue;
                    }

                    int take = remaining == 4 ? 2 : Math.Min(3, remaining);
                    if (take == 3) panel.AddStackedItems(buttons[index], buttons[index + 1], buttons[index + 2]);
                    else panel.AddStackedItems(buttons[index], buttons[index + 1]);
                    index += take;
                }
            }
        }

        private static PushButtonData MakeButton(ToolInfo tool, string assemblyPath)
        {
            // stacked buttons show the name on one line, so the two-line titles become one line
            string name = tool.Title.Replace("\n", " ");
            var data = new PushButtonData(tool.Id, name, assemblyPath, tool.Command.FullName);
            data.ToolTip = tool.Panel + ": " + name;
            data.LongDescription = tool.Tooltip;
            try
            {
                data.Image = IconFactory.Small(tool);
                data.LargeImage = IconFactory.For(tool);
                data.ToolTipImage = IconFactory.For(tool);
            }
            catch (Exception ex)
            {
                Log.Error(ex);   // icons are optional
            }
            return data;
        }
    }
}
