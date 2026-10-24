using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SelectHostsCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var current = ctx.UiDoc.Selection.GetElementIds();
            if (current.Count == 0)
            {
                TaskDialog.Show("Select Hosts", "Select one or more elements first.");
                return Result.Cancelled;
            }

            var hosts = new List<ElementId>();
            var seen = new HashSet<ElementId>();
            foreach (ElementId id in current)
            {
                var instance = ctx.Doc.GetElement(id) as FamilyInstance;
                if (instance == null || instance.Host == null) continue;
                if (seen.Add(instance.Host.Id)) hosts.Add(instance.Host.Id);
            }

            if (hosts.Count == 0)
            {
                TaskDialog.Show("Select Hosts", "None of the selected elements has a host.");
                return Result.Cancelled;
            }

            ctx.UiDoc.Selection.SetElementIds(hosts);
            ctx.UiDoc.ShowElements(hosts);
            return Result.Succeeded;
        }
    }
}
