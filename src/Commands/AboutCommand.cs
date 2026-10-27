using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Version, quick start, where files are kept, and the full list of tools with what each one does.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class AboutCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            var text = new StringBuilder();
            text.AppendLine("ELECKIT " + version.Major + "." + version.Minor + "." + version.Build);
            text.AppendLine("Running in Revit " + ctx.UiApp.Application.VersionNumber + "   (" + ToolRegistry.Tools.Count + " tools)");
            text.AppendLine();

            text.AppendLine("GETTING STARTED");
            text.AppendLine("  - Start with Tool Finder (Tools panel): type a few letters to find any tool and run it.");
            text.AppendLine("  - Most tools use your selection if you made one; otherwise they ask you to pick.");
            text.AppendLine("  - Tools that change the model run as one step: Ctrl+Z undoes a whole run.");
            text.AppendLine("  - Reports are saved to the folder set in Options (the Desktop by default) and have an Open button.");
            text.AppendLine("  - Project Health gives a quick check of the electrical model and what to fix first.");
            text.AppendLine("  - Tools remember your last choices. Options can forget them.");
            text.AppendLine();

            text.AppendLine("WHERE THINGS ARE KEPT");
            text.AppendLine("  Settings:         " + ElecSettings.FilePath);
            text.AppendLine("  Cable catalog:    " + CableCatalogStore.FilePath);
            text.AppendLine("  Saved selections: " + SelectionSetStore.FilePath);
            text.AppendLine("  Log (errors):     " + Log.FilePath);
            text.AppendLine();

            text.AppendLine("ALL TOOLS");
            foreach (var panel in ToolRegistry.Tools.GroupBy(t => t.Panel))
            {
                text.AppendLine();
                text.AppendLine("  " + panel.Key.ToUpperInvariant());
                foreach (ToolInfo tool in panel)
                    text.AppendLine("    " + tool.Title.Replace("\n", " ").PadRight(26) + FirstSentence(tool.Tooltip));
            }

            Dialogs.ShowText("About and Help", text.ToString());
            return Result.Succeeded;
        }

        private static string FirstSentence(string text)
        {
            int end = text.IndexOf(". ", StringComparison.Ordinal);
            return end > 0 ? text.Substring(0, end + 1) : text;
        }
    }
}
