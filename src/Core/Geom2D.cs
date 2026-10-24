using System;

namespace ElecKit.Core
{
    public static class Geom2D
    {
        /// <summary>Shortest distance from point (px,py) to the segment (ax,ay)-(bx,by).</summary>
        public static double PointToSegment(double px, double py, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax;
            double dy = by - ay;
            double lengthSquared = dx * dx + dy * dy;
            double t = lengthSquared < 1e-12 ? 0.0 : ((px - ax) * dx + (py - ay) * dy) / lengthSquared;
            t = Math.Max(0.0, Math.Min(1.0, t));
            double cx = ax + t * dx;
            double cy = ay + t * dy;
            return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }
    }
}
