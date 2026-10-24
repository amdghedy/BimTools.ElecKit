using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Lists every printable view and sheet in a searchable list, and opens the one you pick.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ViewManagerCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            List<View> views = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted &&
                            v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser)
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name)
                .ToList();

            if (views.Count == 0)
            {
                TaskDialog.Show("View Manager", "No printable views were found.");
                return Result.Cancelled;
            }

            List<string> labels = views.Select(v => v.ViewType + "   |   " + v.Name).ToList();
            int index = ListPicker.Pick("View Manager", views.Count + " printable view(s). Type to search, double-click to open:", labels);
            if (index < 0) return Result.Cancelled;

            ctx.UiDoc.ActiveView = views[index];
            return Result.Succeeded;
        }
    }
}
