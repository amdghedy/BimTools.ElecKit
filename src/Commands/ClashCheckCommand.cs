using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Finds elements of one set (selected elements, or a category) that intersect elements of a category.
    /// Writes the pairs to a CSV report and selects every element involved.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class ClashCheckCommand : ElecCommand
    {
        private const int MaxFirstSet = 2000;

        private static readonly string[] FirstNames =
        {
            "Selected elements", "Conduits", "Cable trays", "Electrical equipment", "Electrical fixtures",
            "Lighting fixtures", "Lighting devices"
        };
        private static readonly BuiltInCategory[] FirstCategories =
        {
            BuiltInCategory.OST_Conduit, BuiltInCategory.OST_CableTray, BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices
        };

        private static readonly string[] SecondNames =
        {
            "Ducts", "Pipes", "Cable trays", "Conduits", "Structural framing", "Structural columns",
            "Walls", "Floors", "Ceilings", "Mechanical equipment", "Electrical equipment"
        };
        private static readonly BuiltInCategory[] SecondCategories =
        {
            BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_CableTray,
            BuiltInCategory.OST_Conduit, BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_ElectricalEquipment
        };

        protected override Result Run(ToolContext ctx, ref string message)
        {
            int first = Dialogs.Choose("Clash Check", "Check these elements:", FirstNames, 0);
            if (first < 0) return Result.Cancelled;
            int second = Dialogs.Choose("Clash Check", "against this category:", SecondNames, 0);
            if (second < 0) return Result.Cancelled;
            BuiltInCategory against = SecondCategories[second];

            List<Element> setA;
            if (first == 0)
            {
                setA = new List<Element>();
                foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
                {
                    Element e = ctx.Doc.GetElement(id);
                    if (e != null) setA.Add(e);
                }
                if (setA.Count == 0)
                {
                    TaskDialog.Show("Clash Check", "Select the elements to check first, or choose a category.");
                    return Result.Cancelled;
                }
            }
            else
            {
                setA = new FilteredElementCollector(ctx.Doc)
                    .OfCategory(FirstCategories[first - 1])
                    .WhereElementIsNotElementType()
                    .ToList();
                if (setA.Count == 0)
                {
                    TaskDialog.Show("Clash Check", "There are no " + FirstNames[first].ToLowerInvariant() + " in this model.");
                    return Result.Cancelled;
                }
            }
            if (setA.Count > MaxFirstSet)
            {
                TaskDialog.Show("Clash Check", "That is " + setA.Count + " elements. Select fewer than " + MaxFirstSet + " and try again.");
                return Result.Cancelled;
            }

            Busy.On();
            var pairs = new List<KeyValuePair<Element, Element>>();
            var seen = new HashSet<string>();
            int skipped = 0;

            foreach (Element a in setA)
            {
                BoundingBoxXYZ box = a.get_BoundingBox(null);
                if (box == null) { skipped++; continue; }

                try
                {
                    var hits = new FilteredElementCollector(ctx.Doc)
                        .OfCategory(against)
                        .WhereElementIsNotElementType()
                        .WherePasses(new BoundingBoxIntersectsFilter(new Outline(box.Min, box.Max)))
                        .WherePasses(new ElementIntersectsElementFilter(a));

                    foreach (Element b in hits)
                    {
                        int idA = a.Id.Val(), idB = b.Id.Val();
                        if (idA == idB) continue;
                        string key = Math.Min(idA, idB) + "-" + Math.Max(idA, idB);
                        if (seen.Add(key)) pairs.Add(new KeyValuePair<Element, Element>(a, b));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    skipped++;
                }
            }

            Busy.Off();
            if (pairs.Count == 0)
            {
                TaskDialog.Show("Clash Check", "No clashes found." +
                    (skipped > 0 ? "\n" + skipped + " element(s) could not be tested (no solid geometry)." : ""));
                return Result.Succeeded;
            }

            var csv = new StringBuilder();
            csv.AppendLine("Element A Id,Category A,Name A,Element B Id,Category B,Name B");
            foreach (var pair in pairs)
            {
                csv.AppendLine(string.Join(",",
                    pair.Key.Id.Val(), Csv.Escape(CategoryName(pair.Key)), Csv.Escape(pair.Key.Name),
                    pair.Value.Id.Val(), Csv.Escape(CategoryName(pair.Value)), Csv.Escape(pair.Value.Name)));
            }
            string path = Reports.PathFor("ClashCheck", ".csv");
            System.IO.File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));

            var involved = new HashSet<ElementId>();
            foreach (var pair in pairs) { involved.Add(pair.Key.Id); involved.Add(pair.Value.Id); }
            ctx.UiDoc.Selection.SetElementIds(involved.ToList());

            var notes = new List<string> { "All " + involved.Count + " elements involved are now selected." };
            if (skipped > 0) notes.Add(skipped + " element(s) could not be tested (no solid geometry).");
            Reports.Finish("Clash Check", pairs.Count + " clash(es) found.", notes, path);
            return Result.Succeeded;
        }

        private static string CategoryName(Element e)
        {
            return e.Category != null ? e.Category.Name : "";
        }
    }
}
