using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>Assigns selected model instances to an existing user workset in a workshared document.</summary>
    [Transaction(TransactionMode.Manual)]
    public class AssignWorksetCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            if (!ctx.Doc.IsWorkshared)
            {
                TaskDialog.Show("Assign Workset", "This document is not workshared. Create/enable worksharing before assigning user worksets.");
                return Result.Cancelled;
            }

            IList<Reference> picked;
            try
            {
                picked = ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                    "Select elements to assign to a user workset. Press Finish when done.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            var elements = new List<Element>();
            var seen = new HashSet<long>();
            foreach (Reference reference in picked)
            {
                Element e = ctx.Doc.GetElement(reference);
                if (e != null && !(e is ElementType) && seen.Add(RevitApiCompat.ElementIdValue(e.Id))) elements.Add(e);
            }
            if (elements.Count == 0)
            {
                TaskDialog.Show("Assign Workset", "No model instances were selected.");
                return Result.Cancelled;
            }

            var worksets = new List<Workset>();
            foreach (Workset workset in new FilteredWorksetCollector(ctx.Doc).OfKind(WorksetKind.UserWorkset).ToWorksets())
                if (workset != null) worksets.Add(workset);

            if (worksets.Count == 0)
            {
                TaskDialog.Show("Assign Workset", "No user worksets are available in this document.");
                return Result.Cancelled;
            }

            var labels = new List<string>();
            foreach (Workset workset in worksets) labels.Add(workset.Name + " (ID " + RevitApiCompat.ElementIdValue(workset.Id) + ")");
            int chosen = Dialogs.Choose("Assign Workset",
                "Selected elements: " + elements.Count + "\nChoose the destination user workset. Elements that cannot be changed will be reported.",
                labels, 0);
            if (chosen < 0 || chosen >= worksets.Count) return Result.Cancelled;

            Workset target = worksets[chosen];
            int changed = 0, unchanged = 0, failed = 0;
            var failures = new List<string>();
            ctx.Transact("Assign selected elements to workset", delegate
            {
                foreach (Element e in elements)
                {
                    try
                    {
                        Parameter p = e.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        if (p == null || p.IsReadOnly || p.StorageType != StorageType.ElementId)
                        {
                            failed++;
                            if (failures.Count < 10) failures.Add(RevitApiCompat.ElementIdValue(e.Id) + ": workset parameter unavailable/read-only");
                            continue;
                        }
                        if (RevitApiCompat.ElementIdValue(p.AsElementId()) == RevitApiCompat.ElementIdValue(target.Id)) { unchanged++; continue; }
                        if (!p.Set(RevitApiCompat.CreateElementId(RevitApiCompat.ElementIdValue(target.Id))))
                        {
                            failed++;
                            if (failures.Count < 10) failures.Add(RevitApiCompat.ElementIdValue(e.Id) + ": Revit rejected assignment");
                            continue;
                        }
                        changed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log.Error(ex);
                        if (failures.Count < 10) failures.Add(RevitApiCompat.ElementIdValue(e.Id) + ": " + ex.Message);
                    }
                }
            });

            string report = "Target workset: " + target.Name + "\nChanged: " + changed +
                "\nAlready on target: " + unchanged + "\nFailed: " + failed;
            if (failures.Count > 0) report += "\n\nFirst failures:\n" + string.Join("\n", failures);
            TaskDialog.Show("Assign Workset", report);
            return Result.Succeeded;
        }
    }
}