using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Saves the current selection under a name so you can select the same elements again later.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class SaveSelectionCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var ids = ctx.UiDoc.Selection.GetElementIds();
            if (ids.Count == 0)
            {
                TaskDialog.Show("Save Selection", "Select some elements first.");
                return Result.Cancelled;
            }

            string name = Dialogs.Ask("Save Selection", ids.Count + " element(s) selected. Name for this selection:", "");
            if (name == null) return Result.Cancelled;
            name = name.Trim();
            if (name.Length == 0)
            {
                TaskDialog.Show("Save Selection", "Give the selection a name.");
                return Result.Cancelled;
            }

            var sets = SelectionSetStore.Load();
            string project = ctx.Doc.Title;
            sets.RemoveAll(s => string.Equals(s.Project, project, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            sets.Add(new SelectionSet
            {
                Project = project,
                Name = name,
                UniqueIds = ids.Select(id => ctx.Doc.GetElement(id)).Where(e => e != null).Select(e => e.UniqueId).ToList()
            });
            SelectionSetStore.Save(sets);

            TaskDialog.Show("Save Selection", "Saved \"" + name + "\" (" + ids.Count + " element(s)). Use Recall Selection to select them again.");
            return Result.Succeeded;
        }
    }
}
