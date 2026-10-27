using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ElecKit.Core;

namespace ElecKit.Commands
{
    /// <summary>
    /// Creates a section view that runs along a straight conduit, tray or line, so the element is seen
    /// over its whole length. The view looks to the left of the element's direction; flip it in Revit if needed.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SectionAlongElementCommand : ElecCommand
    {
        private const double PaddingFeet = 1.0;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            Element picked = ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element,
                new PredicateFilter(e => LineOf(e) != null), "Pick a straight conduit, cable tray or line"));
            Line line = LineOf(picked);

            XYZ a = line.GetEndPoint(0);
            XYZ b = line.GetEndPoint(1);
            var along = new XYZ(b.X - a.X, b.Y - a.Y, 0.0);
            double length = along.GetLength();
            if (length < 0.1)
            {
                TaskDialog.Show("Section Along Element", "The element is vertical or too short to give a section direction.");
                return Result.Cancelled;
            }
            along = along.Normalize();

            double? depth = Dialogs.AskNumber("Section Along Element", "How far the section looks into the model:", 3000, true, "mm");
            if (depth == null) return Result.Cancelled;
            double depthMm = depth.Value;

            ViewFamilyType sectionType = new FilteredElementCollector(ctx.Doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Section);
            if (sectionType == null)
            {
                TaskDialog.Show("Section Along Element", "This project has no section view type.");
                return Result.Cancelled;
            }

            Transform transform = Transform.Identity;
            transform.Origin = a.Add(b).Multiply(0.5);
            transform.BasisX = along;
            transform.BasisY = XYZ.BasisZ;
            transform.BasisZ = along.CrossProduct(XYZ.BasisZ);

            double halfHeight = UnitConv.MmToFeet(2000.0);
            var box = new BoundingBoxXYZ();
            box.Transform = transform;
            box.Min = new XYZ(-length / 2.0 - PaddingFeet, -halfHeight, 0.0);
            box.Max = new XYZ(length / 2.0 + PaddingFeet, halfHeight, UnitConv.MmToFeet(depthMm));

            ViewSection section = null;
            ctx.Transact("Section along element", () =>
            {
                section = ViewSection.CreateSection(ctx.Doc, sectionType.Id, box);
                section.Name = "Section along " + picked.Name + " " + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            });

            ctx.UiDoc.ActiveView = section;
            return Result.Succeeded;
        }

        private static Line LineOf(Element element)
        {
            var curveElement = element as CurveElement;
            if (curveElement != null) return curveElement.GeometryCurve as Line;
            var location = element.Location as LocationCurve;
            return location != null ? location.Curve as Line : null;
        }
    }
}
