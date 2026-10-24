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
    /// Finds elements in the active view by category, type name and a parameter value,
    /// then selects them and optionally isolates them (temporary isolate).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SmartFilterCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is ViewSchedule)
            {
                TaskDialog.Show("Smart Filter", "Open a plan, section or 3D view first.");
                return Result.Cancelled;
            }

            List<Element> all = new FilteredElementCollector(ctx.Doc, view.Id)
                .WhereElementIsNotElementType()
                .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                .ToList();
            if (all.Count == 0)
            {
                TaskDialog.Show("Smart Filter", "There are no model elements in this view.");
                return Result.Cancelled;
            }

            var groups = all.GroupBy(e => e.Category.Name).OrderBy(g => g.Key).ToList();
            var names = new List<string> { "(all categories)" };
            names.AddRange(groups.Select(g => g.Key + " (" + g.Count() + ")"));

            int categoryIndex = ListPicker.Pick("Smart Filter", "Category:", names);
            if (categoryIndex < 0) return Result.Cancelled;

            List<Element> pool = categoryIndex == 0 ? all : groups[categoryIndex - 1].ToList();

            string typeText = Dialogs.Ask("Smart Filter", "Family or type name contains (leave empty for any):", "");
            if (typeText == null) return Result.Cancelled;
            typeText = typeText.Trim();

            string paramText = Dialogs.Ask("Smart Filter",
                "Parameter filter as Name=Value, matched as \"contains\" (leave empty for none). Example: Mark=A1",
                "");
            if (paramText == null) return Result.Cancelled;
            paramText = paramText.Trim();

            string paramName = null, paramValue = null;
            if (paramText.Length > 0)
            {
                int equals = paramText.IndexOf('=');
                if (equals <= 0)
                {
                    TaskDialog.Show("Smart Filter", "Use the form Name=Value, for example Mark=A1.");
                    return Result.Cancelled;
                }
                paramName = paramText.Substring(0, equals).Trim();
                paramValue = paramText.Substring(equals + 1).Trim();
            }

            var matches = new List<ElementId>();
            foreach (Element element in pool)
            {
                if (typeText.Length > 0)
                {
                    var type = ctx.Doc.GetElement(element.GetTypeId()) as ElementType;
                    string label = type != null ? type.FamilyName + " " + type.Name : element.Name;
                    if (label.IndexOf(typeText, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                if (paramName != null)
                {
                    string value = ValueOf(ctx.Doc, element, paramName);
                    if (value == null || value.IndexOf(paramValue, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                matches.Add(element.Id);
            }

            if (matches.Count == 0)
            {
                TaskDialog.Show("Smart Filter", "Nothing matches that filter in this view.");
                return Result.Cancelled;
            }

            int action = Dialogs.Choose("Smart Filter", matches.Count + " element(s) match. What now?", new List<string>
            {
                "Select them",
                "Select them and isolate them in this view (temporary)"
            }, 0);
            if (action < 0) return Result.Cancelled;

            ctx.UiDoc.Selection.SetElementIds(matches);
            if (action == 1)
                ctx.Transact("Smart filter isolate", () => view.IsolateElementsTemporary(matches));
            return Result.Succeeded;
        }

        private static string ValueOf(Document doc, Element element, string name)
        {
            Parameter p = element.LookupParameter(name);
            if (p == null)
            {
                Element type = doc.GetElement(element.GetTypeId());
                if (type != null) p = type.LookupParameter(name);
            }
            if (p == null) return null;
            return p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
        }
    }
}
