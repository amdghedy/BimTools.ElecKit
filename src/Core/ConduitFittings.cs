using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;

namespace ElecKit.Core
{
    public enum ElbowResult { Made, Skipped, Failed }

    public static class ConduitFittings
    {
        /// <summary>Joins two conduits that meet at a corner with an elbow. Straight-through joints are skipped.</summary>
        public static ElbowResult TryElbow(Document doc, Conduit first, Conduit second, XYZ corner)
        {
            var lineA = ((LocationCurve)first.Location).Curve as Line;
            var lineB = ((LocationCurve)second.Location).Curve as Line;
            if (lineA != null && lineB != null && Math.Abs(lineA.Direction.DotProduct(lineB.Direction)) > 0.9999)
                return ElbowResult.Skipped;

            Connector a = Nearest(first, corner);
            Connector b = Nearest(second, corner);
            if (a == null || b == null) return ElbowResult.Failed;

            try
            {
                doc.Create.NewElbowFitting(a, b);
                return ElbowResult.Made;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return ElbowResult.Failed;
            }
        }

        public static Connector Nearest(Conduit conduit, XYZ point)
        {
            Connector best = null;
            double bestDistance = double.MaxValue;
            foreach (Connector connector in conduit.ConnectorManager.Connectors)
            {
                double distance = connector.Origin.DistanceTo(point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = connector;
                }
            }
            return best;
        }
    }
}
