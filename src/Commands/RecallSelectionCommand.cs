using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Selects the elements of a saved selection again (or deletes the saved selection).</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class RecallSelectionCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            List<SelectionSet> all = SelectionSetStore.Load();
            List<SelectionSet> mine = all
                .Where(s => string.Equals(s.Project, ctx.Doc.Title, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (mine.Count == 0)
            {
                TaskDialog.Show("Recall Selection", "No selections are saved for this project. Use Save Selection first.");
                return Result.Cancelled;
            }

            int index = ListPicker.Pick("Recall Selection", "Saved selections for this project:",
                mine.Select(s => s.Name + "   (" + (s.UniqueIds != null ? s.UniqueIds.Count : 0) + " element(s))").ToList());
            if (index < 0) return Result.Cancelled;
            SelectionSet chosen = mine[index];

            int action = Dialogs.Choose("Recall Selection", "\"" + chosen.Name + "\":", new List<string>
            {
                "Select these elements",
                "Delete this saved selection"
            }, 0);
            if (action < 0) return Result.Cancelled;

            if (action == 1)
            {
                all.Remove(chosen);
                SelectionSetStore.Save(all);
                TaskDialog.Show("Recall Selection", "Deleted \"" + chosen.Name + "\".");
                return Result.Succeeded;
            }

            var ids = new List<ElementId>();
            int missing = 0;
            foreach (string uniqueId in chosen.UniqueIds ?? new List<string>())
            {
                Element element = ctx.Doc.GetElement(uniqueId);
                if (element != null) ids.Add(element.Id);
                else missing++;
            }
            if (ids.Count == 0)
            {
                TaskDialog.Show("Recall Selection", "None of the saved elements exist any more.");
                return Result.Cancelled;
            }

            ctx.UiDoc.Selection.SetElementIds(ids);
            if (missing > 0) TaskDialog.Show("Recall Selection", ids.Count + " element(s) selected. " + missing + " no longer exist.");
            return Result.Succeeded;
        }
    }
}
