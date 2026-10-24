using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class GeometryCurves
    {
        /// <summary>
        /// Collects every curve in a geometry tree (CAD imports, wires...). Nested instances are expanded
        /// with their transform applied, and polylines are split into straight segments.
        /// </summary>
        public static void Collect(GeometryElement geometry, Func<GeometryObject, bool> accept, List<Curve> output)
        {
            foreach (GeometryObject obj in geometry)
            {
                var instance = obj as GeometryInstance;
                if (instance != null)
                {
                    GeometryElement inner = instance.GetInstanceGeometry();
                    if (inner != null) Collect(inner, accept, output);
                    continue;
                }

                if (!accept(obj)) continue;

                var curve = obj as Curve;
                if (curve != null)
                {
                    output.Add(curve);
                    continue;
                }

                var poly = obj as PolyLine;
                if (poly != null)
                {
                    IList<XYZ> points = poly.GetCoordinates();
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        if (points[i].DistanceTo(points[i + 1]) > FilletMath.ShortCurveFeet)
                            output.Add(Line.CreateBound(points[i], points[i + 1]));
                    }
                }
            }
        }
    }
}
