using System;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    public static class FilletOps
    {
        /// <summary>
        /// Trims two straight detail lines back to their tangent points and adds an arc between them.
        /// pick1/pick2 choose which side of each line to keep; pass null to keep the end farthest from the corner.
        /// Call inside an open transaction. Changes are rolled back if anything fails.
        /// </summary>
        public static bool Apply(Document doc, View view,
            DetailCurve c1, XYZ pick1, DetailCurve c2, XYZ pick2, double radius,
            out Line newLine1, out Line newLine2, out string error)
        {
            newLine1 = null;
            newLine2 = null;
            error = null;

            Line l1 = c1.GeometryCurve as Line;
            Line l2 = c2.GeometryCurve as Line;
            if (l1 == null || l2 == null)
            {
                error = "Both elements must be straight detail lines.";
                return false;
            }

            XYZ corner;
            if (!FilletMath.TryIntersect(l1, l2, out corner))
            {
                error = "The lines are parallel or not in the same plane.";
                return false;
            }

            int keep1 = pick1 != null ? FilletMath.KeptEndByPick(l1, corner, pick1) : FilletMath.KeptEndByDistance(l1, corner);
            int keep2 = pick2 != null ? FilletMath.KeptEndByPick(l2, corner, pick2) : FilletMath.KeptEndByDistance(l2, corner);

            XYZ t1, t2, mid;
            if (!FilletMath.TryCompute(corner, l1.GetEndPoint(keep1), l2.GetEndPoint(keep2), radius,
                    out t1, out t2, out mid, out error))
                return false;

            Line trimmed1 = Rebuild(l1, keep1, t1);
            Line trimmed2 = Rebuild(l2, keep2, t2);

            using (var sub = new SubTransaction(doc))
            {
                sub.Start();
                try
                {
                    c1.SetGeometryCurve(trimmed1, true);
                    c2.SetGeometryCurve(trimmed2, true);
                    DetailCurve arc = doc.Create.NewDetailCurve(view, Arc.Create(t1, t2, mid));
                    arc.LineStyle = c1.LineStyle;
                    sub.Commit();
                }
                catch (Exception ex)
                {
                    sub.RollBack();
                    Log.Error(ex);
                    error = ex.Message;
                    return false;
                }
            }

            newLine1 = trimmed1;
            newLine2 = trimmed2;
            return true;
        }

        private static Line Rebuild(Line original, int keptIndex, XYZ tangent)
        {
            XYZ a = original.GetEndPoint(0);
            XYZ b = original.GetEndPoint(1);
            return keptIndex == 0 ? Line.CreateBound(a, tangent) : Line.CreateBound(tangent, b);
        }
    }
}
