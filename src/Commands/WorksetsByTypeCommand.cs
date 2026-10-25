using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Moves the selected elements into worksets named after their category, or their family and type.
    /// Missing worksets are created. The project must already be workshared.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class WorksetsByTypeCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            if (!ctx.Doc.IsWorkshared)
            {
                TaskDialog.Show("Worksets by Type", "This project is not workshared. Enable worksets first (Collaborate > Worksets).");
                return Result.Cancelled;
            }

            var elements = new List<Element>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                Element e = ctx.Doc.GetElement(id);
                if (IsModelElement(e)) elements.Add(e);
            }
            if (elements.Count == 0)
            {
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                    new PredicateFilter(IsModelElement), "Pick the elements to assign, then click Finish"))
                    elements.Add(ctx.Doc.GetElement(r));
            }
            if (elements.Count == 0) return Result.Cancelled;

            int mode = Dialogs.Choose("Worksets by Type", "One workset for each:", new List<string>
            {
                "Category",
                "Family and type"
            }, 0);
            if (mode < 0) return Result.Cancelled;

            string prefix = Dialogs.Ask("Worksets by Type", "Workset name prefix (can be empty):", "E-");
            if (prefix == null) return Result.Cancelled;

            var worksets = new Dictionary<string, Workset>(StringComparer.OrdinalIgnoreCase);
            foreach (Workset w in new FilteredWorksetCollector(ctx.Doc).OfKind(WorksetKind.UserWorkset))
                worksets[w.Name] = w;

            int assigned = 0, created = 0, skipped = 0;
            ctx.Transact("Worksets by type", () =>
            {
                foreach (Element element in elements)
                {
                    string name = NameFor(ctx.Doc, element, mode, prefix);
                    Workset workset;
                    if (!worksets.TryGetValue(name, out workset))
                    {
                        workset = Workset.Create(ctx.Doc, name);
                        worksets[name] = workset;
                        created++;
                    }

                    Parameter parameter = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (parameter == null || parameter.IsReadOnly) { skipped++; continue; }
                    parameter.Set(workset.Id.IntegerValue);
                    assigned++;
                }
            });

            string text = assigned + " element(s) assigned, " + created + " workset(s) created.";
            if (skipped > 0) text += "\n" + skipped + " element(s) skipped: their workset cannot be changed.";
            TaskDialog.Show("Worksets by Type", text);
            return Result.Succeeded;
        }

        private static bool IsModelElement(Element e)
        {
            return e != null && e.Category != null && e.Category.CategoryType == CategoryType.Model;
        }

        private static string NameFor(Document doc, Element element, int mode, string prefix)
        {
            string core = element.Category != null ? element.Category.Name : "Other";
            if (mode == 1)
            {
                var type = doc.GetElement(element.GetTypeId()) as ElementType;
                if (type != null) core = type.FamilyName + " - " + type.Name;
            }
            string name = (prefix + core).Trim();
            return name.Length > 100 ? name.Substring(0, 100) : name;
        }
    }
}
