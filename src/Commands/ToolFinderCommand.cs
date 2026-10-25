using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Search every ElecKit tool by name or description and run it from one list.
    /// Recently used tools are kept at the top.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ToolFinderCommand : ElecCommand
    {
        private const int MaxRecent = 8;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            List<string> recent = ElecSettings.Current.RecentTools ?? new List<string>();

            List<ToolInfo> ordered = ToolRegistry.Tools
                .Where(t => t.Command != typeof(ToolFinderCommand))
                .Select((tool, position) => new { tool, position })
                .OrderBy(x => { int i = recent.IndexOf(x.tool.Id); return i < 0 ? int.MaxValue : i; })
                .ThenBy(x => x.position)
                .Select(x => x.tool)
                .ToList();

            List<string> labels = ordered.Select(t =>
                (recent.Contains(t.Id) ? "* " : "") + t.Panel + "   >   " + t.Title.Replace("\n", " ") +
                "   -   " + FirstSentence(t.Tooltip)).ToList();

            int index = ListPicker.Pick("ElecKit Tool Finder",
                ordered.Count + " tools. Type to search; * marks recently used tools. Double-click to run:", labels);
            if (index < 0) return Result.Cancelled;

            ToolInfo chosen = ordered[index];
            recent.Remove(chosen.Id);
            recent.Insert(0, chosen.Id);
            if (recent.Count > MaxRecent) recent.RemoveRange(MaxRecent, recent.Count - MaxRecent);
            ElecSettings.Current.RecentTools = recent;
            ElecSettings.Current.Save();

            var command = (IExternalCommand)Activator.CreateInstance(chosen.Command);
            return command.Execute(ctx.Data, ref message, new ElementSet());
        }

        private static string FirstSentence(string text)
        {
            int end = text.IndexOf(". ", StringComparison.Ordinal);
            return end > 0 ? text.Substring(0, end + 1) : text;
        }
    }
}
