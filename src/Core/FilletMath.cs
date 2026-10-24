using System;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class FilletMath
    {
        /// <summary>Revit rejects curves shorter than about 1/32 inch (feet).</summary>
        public const double ShortCurveFeet = 0.0026;

        private const double ParallelTolerance = 1e-6;
        private const double PlanarToleranceFeet = 1e-3;

        /// <summary>Intersection of the two infinite lines. False if parallel or not coplanar.</summary>
        public static bool TryIntersect(Line a, Line b, out XYZ point)
        {
            point = null;
            XYZ d1 = a.Direction;
            XYZ d2 = b.Direction;
            XYZ cross = d1.CrossProduct(d2);
            double den = cross.DotProduct(cross);
            if (den < ParallelTolerance) return false;

            XYZ w = b.GetEndPoint(0).Subtract(a.GetEndPoint(0));
            if (Math.Abs(w.DotProduct(cross)) / Math.Sqrt(den) > PlanarToleranceFeet) return false;

            double t = w.CrossProduct(d2).DotProduct(cross) / den;
            point = a.GetEndPoint(0).Add(d1.Multiply(t));
            return true;
        }

        /// <summary>Position along the line (feet from its start) of a point that lies on the line.</summary>
        public static double Param(Line line, XYZ point)
        {
            return point.Subtract(line.GetEndPoint(0)).DotProduct(line.Direction);
        }

        /// <summary>Index (0 or 1) of the end that lies on the picked side of the corner.</summary>
        public static int KeptEndByPick(Line line, XYZ corner, XYZ pick)
        {
            XYZ toPick = pick.Subtract(corner);
            double s0 = line.GetEndPoint(0).Subtract(corner).DotProduct(toPick);
            double s1 = line.GetEndPoint(1).Subtract(corner).DotProduct(toPick);
            if (Math.Abs(s0 - s1) < 1e-9) return KeptEndByDistance(line, corner);
            return s0 > s1 ? 0 : 1;
        }

        /// <summary>Index (0 or 1) of the end farthest from the corner.</summary>
        public static int KeptEndByDistance(Line line, XYZ corner)
        {
            return line.GetEndPoint(0).DistanceTo(corner) >= line.GetEndPoint(1).DistanceTo(corner) ? 0 : 1;
        }

        /// <summary>Index (0 or 1) of the end nearest the corner.</summary>
        public static int NearEnd(Line line, XYZ corner)
        {
            return line.GetEndPoint(0).DistanceTo(corner) <= line.GetEndPoint(1).DistanceTo(corner) ? 0 : 1;
        }

        /// <summary>
        /// Fillet between two lines that meet at <paramref name="corner"/>.
        /// keep1/keep2 are the ends of each line that stay. Returns the two tangent points and an arc mid-point.
        /// </summary>
        public static bool TryCompute(XYZ corner, XYZ keep1, XYZ keep2, double radius,
            out XYZ tangent1, out XYZ tangent2, out XYZ arcMid, out string error)
        {
            tangent1 = null;
            tangent2 = null;
            arcMid = null;
            error = null;

            XYZ v1 = keep1.Subtract(corner);
            XYZ v2 = keep2.Subtract(corner);
            double len1 = v1.GetLength();
            double len2 = v2.GetLength();
            if (len1 < ShortCurveFeet || len2 < ShortCurveFeet)
            {
                error = "A line is too short at the corner.";
                return false;
            }

            XYZ u1 = v1.Normalize();
            XYZ u2 = v2.Normalize();
            double cos = u1.DotProduct(u2);
            if (cos > 0.9999 || cos < -0.9999)
            {
                error = "The lines are parallel or collinear.";
                return false;
            }

            double theta = Math.Acos(cos);
            double half = theta / 2.0;
            double tangentLength = radius / Math.Tan(half);
            if (len1 - tangentLength < ShortCurveFeet || len2 - tangentLength < ShortCurveFeet)
            {
                error = "The radius is too large for these lines.";
                return false;
            }
            if (radius * (Math.PI - theta) < ShortCurveFeet)
            {
                error = "The arc would be too short.";
                return false;
            }

            tangent1 = corner.Add(u1.Multiply(tangentLength));
            tangent2 = corner.Add(u2.Multiply(tangentLength));
            XYZ bisector = u1.Add(u2).Normalize();
            arcMid = corner.Add(bisector.Multiply(radius / Math.Sin(half) - radius));
            return true;
        }
    }
}
