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
    /// Places a tag with a leader on each selected element, with the tag head offset by a set
    /// distance (measured on the sheet) in the chosen direction. Uses the default tag for each category.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class TagsWithLeadersCommand : ElecCommand
    {
        protected override Result Run(ToolContext ctx, ref string message)
        {
            View view = ctx.Doc.ActiveView;
            if (view is View3D || view is ViewSchedule || view is ViewDrafting)
            {
                TaskDialog.Show("Tags with Leaders", "Open a plan, section or elevation view first.");
                return Result.Cancelled;
            }

            var elements = new List<Element>();
            foreach (ElementId id in ctx.UiDoc.Selection.GetElementIds())
            {
                Element e = ctx.Doc.GetElement(id);
                if (IsTaggable(e)) elements.Add(e);
            }
            if (elements.Count == 0)
            {
                foreach (Reference r in ctx.UiDoc.Selection.PickObjects(ObjectType.Element,
                    new PredicateFilter(IsTaggable), "Pick the elements to tag, then click Finish"))
                    elements.Add(ctx.Doc.GetElement(r));
            }
            if (elements.Count == 0) return Result.Cancelled;

            int direction = Dialogs.Choose("Tags with Leaders", "Place the tag heads:", new List<string>
            {
                "Upper right", "Upper left", "Lower right", "Lower left"
            }, 0);
            if (direction < 0) return Result.Cancelled;

            double? paper = Dialogs.AskNumber("Tags with Leaders", "Distance from the element to the tag head, measured on the printed sheet:",
                ElecSettings.Current.TagOffsetMm ?? 15.0, true, "mm");
            if (paper == null) return Result.Cancelled;
            double paperMm = paper.Value;
            ElecSettings.Current.TagOffsetMm = paperMm;
            ElecSettings.Current.Save();

            double distance = UnitConv.MmToFeet(paperMm) * view.Scale;
            double sx = (direction == 1 || direction == 3) ? -1.0 : 1.0;
            double sy = direction >= 2 ? -1.0 : 1.0;
            XYZ right = view.RightDirection;
            XYZ up = view.UpDirection;

            int created = 0, failed = 0;
            string firstError = null;
            ctx.Transact("Tags with leaders", () =>
            {
                foreach (Element element in elements)
                {
                    XYZ anchor = Anchors.Of(element);
                    if (anchor == null) { failed++; continue; }

                    XYZ head = anchor.Add(right.Multiply(sx * distance)).Add(up.Multiply(sy * distance));
                    try
                    {
                        IndependentTag.Create(ctx.Doc, view.Id, new Reference(element), true,
                            TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, head);
                        created++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        if (firstError == null) firstError = ex.Message;
                        failed++;
                    }
                }
            });

            string text = created + " tag(s) placed.";
            if (failed > 0) text += "\n" + failed + " element(s) not tagged: " + (firstError ?? "no location") +
                                    "\nCheck that a tag family is loaded for each category.";
            TaskDialog.Show("Tags with Leaders", text);
            return Result.Succeeded;
        }

        private static bool IsTaggable(Element e)
        {
            return e != null && e.Category != null && e.Category.CategoryType == CategoryType.Model;
        }
    }
}
