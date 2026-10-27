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
    /// Dimensions between two picked elements along the direction that joins them.
    /// Works with family instances (using their centre reference planes), grids and reference planes.
    /// The two elements must have parallel planes. Plan views only.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AlignedDimensionCommand : ElecCommand
    {
        private const double PaperOffsetMm = 8.0;

        protected override Result Run(ToolContext ctx, ref string message)
        {
            var view = ctx.Doc.ActiveView as ViewPlan;
            if (view == null)
            {
                TaskDialog.Show("Aligned Dimension", "Open a plan view first.");
                return Result.Cancelled;
            }

            var filter = new PredicateFilter(e => e is Grid || e is ReferencePlane || (e is FamilyInstance && Anchors.Of(e) != null));
            Element first = ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element, filter,
                "Pick the first element (family instance, grid or reference plane)"));
            Element second = ctx.Doc.GetElement(ctx.UiDoc.Selection.PickObject(ObjectType.Element, filter,
                "Pick the second element"));
            if (first.Id.Val() == second.Id.Val())
            {
                TaskDialog.Show("Aligned Dimension", "Pick two different elements.");
                return Result.Cancelled;
            }

            XYZ p1 = PointOf(first);
            XYZ p2 = PointOf(second);
            XYZ between = Flat(p2.Subtract(p1));

            List<KeyValuePair<Reference, XYZ>> optionsA = Options(first);
            List<KeyValuePair<Reference, XYZ>> optionsB = Options(second);

            bool found = false;
            double bestScore = -1.0;
            KeyValuePair<Reference, XYZ> bestA = default(KeyValuePair<Reference, XYZ>);
            KeyValuePair<Reference, XYZ> bestB = default(KeyValuePair<Reference, XYZ>);
            foreach (var a in optionsA)
            {
                foreach (var b in optionsB)
                {
                    if (Math.Abs(a.Value.DotProduct(b.Value)) < 0.999) continue;
                    double score = between == null ? 1.0 : Math.Abs(a.Value.DotProduct(between));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestA = a;
                        bestB = b;
                        found = true;
                    }
                }
            }
            if (!found)
            {
                TaskDialog.Show("Aligned Dimension",
                    "These two elements have no parallel reference planes to dimension between. " +
                    "Rotate one to match the other, or pick grids or reference planes.");
                return Result.Cancelled;
            }

            XYZ normal = bestA.Value;
            double gap = p2.Subtract(p1).DotProduct(normal);
            if (gap < 0)
            {
                normal = normal.Negate();
                gap = -gap;
            }
            if (gap < 0.01)
            {
                TaskDialog.Show("Aligned Dimension", "The elements are in the same place along that direction.");
                return Result.Cancelled;
            }

            double offset = UnitConv.MmToFeet(PaperOffsetMm) * view.Scale;
            XYZ perpendicular = new XYZ(-normal.Y, normal.X, 0.0);
            XYZ start = p1.Add(perpendicular.Multiply(offset));
            start = new XYZ(start.X, start.Y, view.Origin.Z);
            XYZ end = start.Add(normal.Multiply(gap));

            var references = new ReferenceArray();
            references.Append(bestA.Key);
            references.Append(bestB.Key);

            ctx.Transact("Aligned dimension", () =>
            {
                ctx.Doc.Create.NewDimension(view, Line.CreateBound(start, end), references);
            });
            return Result.Succeeded;
        }

        private static XYZ PointOf(Element element)
        {
            var grid = element as Grid;
            if (grid != null)
            {
                Curve curve = grid.Curve;
                return curve.GetEndPoint(0).Add(curve.GetEndPoint(1)).Multiply(0.5);
            }
            return Anchors.Of(element);
        }

        private static XYZ Flat(XYZ v)
        {
            var flat = new XYZ(v.X, v.Y, 0.0);
            return flat.GetLength() < 1e-9 ? null : flat.Normalize();
        }

        private static List<KeyValuePair<Reference, XYZ>> Options(Element element)
        {
            var list = new List<KeyValuePair<Reference, XYZ>>();

            var grid = element as Grid;
            if (grid != null)
            {
                var line = grid.Curve as Line;
                if (line != null)
                {
                    XYZ n = Flat(new XYZ(-line.Direction.Y, line.Direction.X, 0.0));
                    if (n != null) list.Add(new KeyValuePair<Reference, XYZ>(new Reference(grid), n));
                }
                return list;
            }

            var plane = element as ReferencePlane;
            if (plane != null)
            {
                XYZ n = Flat(plane.Normal);
                if (n != null) list.Add(new KeyValuePair<Reference, XYZ>(new Reference(plane), n));
                return list;
            }

            var instance = element as FamilyInstance;
            if (instance != null)
            {
                Transform t = instance.GetTransform();
                Add(list, instance.GetReferences(FamilyInstanceReferenceType.CenterLeftRight), Flat(t.BasisX));
                Add(list, instance.GetReferences(FamilyInstanceReferenceType.CenterFrontBack), Flat(t.BasisY));
            }
            return list;
        }

        private static void Add(List<KeyValuePair<Reference, XYZ>> list, IList<Reference> references, XYZ normal)
        {
            if (normal != null && references != null && references.Count > 0)
                list.Add(new KeyValuePair<Reference, XYZ>(references[0], normal));
        }
    }
}
