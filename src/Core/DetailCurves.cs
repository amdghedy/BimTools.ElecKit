using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class DetailCurves
    {
        /// <summary>
        /// Flattens a curve onto the view plane so it can become a detail curve.
        /// Lines are projected; arcs must already be parallel to the view; full circles become two half arcs.
        /// Returns an empty list when the curve cannot be used (splines, vertical arcs, too short).
        /// </summary>
        public static List<Curve> Flatten(Curve curve, View view)
        {
            var result = new List<Curve>();
            XYZ normal = view.ViewDirection;
            XYZ origin = view.Origin;

            var line = curve as Line;
            if (line != null)
            {
                XYZ a = Project(line.GetEndPoint(0), origin, normal);
                XYZ b = Project(line.GetEndPoint(1), origin, normal);
                if (a.DistanceTo(b) >= FilletMath.ShortCurveFeet) result.Add(Line.CreateBound(a, b));
                return result;
            }

            var arc = curve as Arc;
            if (arc != null)
            {
                if (Math.Abs(arc.Normal.DotProduct(normal)) < 0.999) return result;

                double offset = arc.Center.Subtract(origin).DotProduct(normal);
                XYZ center = arc.Center.Subtract(normal.Multiply(offset));

                if (arc.IsBound)
                {
                    result.Add(arc.CreateTransformed(Transform.CreateTranslation(normal.Multiply(-offset))));
                }
                else
                {
                    XYZ x = arc.XDirection;
                    XYZ y = arc.YDirection;
                    result.Add(Arc.Create(center, arc.Radius, 0.0, Math.PI, x, y));
                    result.Add(Arc.Create(center, arc.Radius, Math.PI, 2.0 * Math.PI, x, y));
                }
            }
            return result;
        }

        public static DetailCurve Create(Document doc, View view, Curve flat, GraphicsStyle style)
        {
            DetailCurve created = doc.Create.NewDetailCurve(view, flat);
            if (style != null) created.LineStyle = style;
            return created;
        }

        private static XYZ Project(XYZ point, XYZ origin, XYZ normal)
        {
            double distance = point.Subtract(origin).DotProduct(normal);
            return point.Subtract(normal.Multiply(distance));
        }
    }
}
