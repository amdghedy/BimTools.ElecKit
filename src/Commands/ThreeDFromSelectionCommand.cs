using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ElecKit.Core;

namespace ElecKit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ThreeDFromSelectionCommand : ElecCommand
    {
        private const double PaddingFeet = 1.0;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            var selected = ctx.UiDoc.Selection.GetElementIds();
            if (selected.Count == 0)
            {
                TaskDialog.Show("3D View from Selection", "Select one or more elements first.");
                return Result.Cancelled;
            }

            XYZ min = null;
            XYZ max = null;
            foreach (ElementId id in selected)
            {
                Element element = ctx.Doc.GetElement(id);
                if (element == null) continue;
                BoundingBoxXYZ bb = element.get_BoundingBox(null);
                if (bb == null) continue;

                if (min == null)
                {
                    min = bb.Min;
                    max = bb.Max;
                }
                else
                {
                    min = new XYZ(Math.Min(min.X, bb.Min.X), Math.Min(min.Y, bb.Min.Y), Math.Min(min.Z, bb.Min.Z));
                    max = new XYZ(Math.Max(max.X, bb.Max.X), Math.Max(max.Y, bb.Max.Y), Math.Max(max.Z, bb.Max.Z));
                }
            }

            if (min == null)
            {
                TaskDialog.Show("3D View from Selection", "None of the selected elements has a 3D extent.");
                return Result.Cancelled;
            }

            var box = new BoundingBoxXYZ();
            box.Min = new XYZ(min.X - PaddingFeet, min.Y - PaddingFeet, min.Z - PaddingFeet);
            box.Max = new XYZ(max.X + PaddingFeet, max.Y + PaddingFeet, max.Z + PaddingFeet);

            ViewFamilyType viewType = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);
            if (viewType == null)
            {
                TaskDialog.Show("3D View from Selection", "This project has no 3D view type.");
                return Result.Cancelled;
            }

            View3D view = null;
            ctx.Transact("3D view from selection", () =>
            {
                view = View3D.CreateIsometric(ctx.Doc, viewType.Id);
                view.Name = "3D - Selection " + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                view.SetSectionBox(box);
            });

            ctx.UiDoc.ActiveView = view;
            return Result.Succeeded;
        }
    }
}
