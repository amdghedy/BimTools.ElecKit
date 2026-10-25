using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Lists the types available for the selected element (the other types of its family, or of its category
    /// for system families) and switches all selected matching elements to the one you pick.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class FamilyTypeManagerCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            var elements = new List<Element>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                Element e = ctx.Doc.GetElement(id);
                if (HasType(e)) elements.Add(e);
            }
            if (elements.Count == 0)
            {
                elements.Add(ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element,
                    new PredicateFilter(HasType), "Pick an element to change the type of")));
            }

            Element sample = elements[0];
            List<ElementType> options = TypesFor(ctx.Doc, sample);
            if (options.Count == 0)
            {
                TaskDialog.Show("Family Type Manager", "No other types were found for this element.");
                return Result.Cancelled;
            }

            int currentId = sample.GetTypeId().Val();
            var labels = options.Select(t => t.Name + (t.Id.Val() == currentId ? "   (current)" : "")).ToList();
            int index = ListPicker.Pick("Family Type Manager",
                options.Count + " type(s) available. Pick the type to switch to:", labels);
            if (index < 0) return Result.Cancelled;
            ElementType chosen = options[index];

            int changed = 0, skipped = 0;
            ctx.Transact("Family type manager", () =>
            {
                var symbol = chosen as FamilySymbol;
                if (symbol != null && !symbol.IsActive) symbol.Activate();

                foreach (Element element in elements)
                {
                    if (!SameGroup(sample, element)) { skipped++; continue; }
                    if (element.GetTypeId().Val() == chosen.Id.Val()) continue;
                    element.ChangeTypeId(chosen.Id);
                    changed++;
                }
            });

            string text = changed + " element(s) switched to \"" + chosen.Name + "\".";
            if (skipped > 0) text += "\n" + skipped + " selected element(s) skipped: they belong to a different family or category.";
            TaskDialog.Show("Family Type Manager", text);
            return Result.Succeeded;
        }

        private static bool HasType(Element e)
        {
            return e != null && e.Category != null && e.GetTypeId() != null &&
                   e.GetTypeId().Val() != ElementId.InvalidElementId.Val();
        }

        private static bool SameGroup(Element sample, Element other)
        {
            var a = sample as FamilyInstance;
            var b = other as FamilyInstance;
            if (a != null && b != null) return a.Symbol.Family.Id.Val() == b.Symbol.Family.Id.Val();
            if (a != null || b != null) return false;
            return sample.Category.Id.Val() == other.Category.Id.Val();
        }

        private static List<ElementType> TypesFor(Document doc, Element sample)
        {
            var instance = sample as FamilyInstance;
            if (instance != null && instance.Symbol != null && instance.Symbol.Family != null)
            {
                return instance.Symbol.Family.GetFamilySymbolIds()
                    .Select(id => doc.GetElement(id) as ElementType)
                    .Where(t => t != null)
                    .OrderBy(t => t.Name)
                    .ToList();
            }

            return new FilteredElementCollector(doc)
                .OfCategoryId(sample.Category.Id)
                .WhereElementIsElementType()
                .Cast<ElementType>()
                .OrderBy(t => t.Name)
                .ToList();
        }
    }
}
