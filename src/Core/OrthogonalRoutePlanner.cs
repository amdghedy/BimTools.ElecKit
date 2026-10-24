using System;
using System.Collections.Generic;
using System.Linq;

namespace ElecKit.Core
{
    public sealed class RoutePoint2D
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public RoutePoint2D(double x, double y) { X = x; Y = y; }
    }

    public sealed class RouteObstacle2D
    {
        public double MinX { get; private set; }
        public double MinY { get; private set; }
        public double MaxX { get; private set; }
        public double MaxY { get; private set; }
        public RouteObstacle2D(double minX, double minY, double maxX, double maxY)
        {
            if (!Finite(minX) || !Finite(minY) || !Finite(maxX) || !Finite(maxY))
                throw new ArgumentOutOfRangeException("Obstacle coordinates must be finite.");
            if (minX > maxX || minY > maxY) throw new ArgumentException("Obstacle bounds are reversed.");
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        }
        private static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
    }

    /// <summary>
    /// Revit-independent orthogonal visibility-grid router. Obstacles are expanded by the requested clearance;
    /// returns null when either endpoint is obstructed or no path can be found.
    /// </summary>
    public static class OrthogonalRoutePlanner
    {
        private const double Epsilon = 1e-7;
        private sealed class OpenNode
        {
            public int Id;
            public double G;
            public double F;
            public long Sequence;
        }
        private sealed class OpenNodeComparer : IComparer<OpenNode>
        {
            public int Compare(OpenNode a, OpenNode b)
            {
                int c = a.F.CompareTo(b.F);
                if (c != 0) return c;
                c = a.G.CompareTo(b.G);
                if (c != 0) return c;
                c = a.Id.CompareTo(b.Id);
                return c != 0 ? c : a.Sequence.CompareTo(b.Sequence);
            }
        }

        public static IList<RoutePoint2D> Plan(double startX, double startY, double endX, double endY,
            IEnumerable<RouteObstacle2D> obstacles, double clearance)
        {
            if (!Finite(startX) || !Finite(startY) || !Finite(endX) || !Finite(endY))
                throw new ArgumentOutOfRangeException("Route endpoints must be finite.");
            if (!Finite(clearance) || clearance < 0) throw new ArgumentOutOfRangeException(nameof(clearance));
            if (obstacles == null) throw new ArgumentNullException(nameof(obstacles));

            double margin = Math.Max(Epsilon * 10, clearance * 1e-6);
            var expanded = obstacles.Select(o =>
            {
                if (o == null) throw new ArgumentException("Obstacle collection cannot contain null.", nameof(obstacles));
                return new RouteObstacle2D(o.MinX - clearance - margin, o.MinY - clearance - margin,
                    o.MaxX + clearance + margin, o.MaxY + clearance + margin);
            }).ToList();

            if (Inside(startX, startY, expanded) || Inside(endX, endY, expanded)) return null;
            var xs = new List<double> { startX, endX };
            var ys = new List<double> { startY, endY };
            foreach (RouteObstacle2D o in expanded)
            {
                xs.Add(o.MinX); xs.Add(o.MaxX);
                ys.Add(o.MinY); ys.Add(o.MaxY);
            }
            xs = UniqueSorted(xs);
            ys = UniqueSorted(ys);
            int sx = IndexOf(xs, startX), sy = IndexOf(ys, startY);
            int ex = IndexOf(xs, endX), ey = IndexOf(ys, endY);
            if (sx < 0 || sy < 0 || ex < 0 || ey < 0) return null;

            int nx = xs.Count, ny = ys.Count, total = nx * ny;
            int start = sx * ny + sy, goal = ex * ny + ey;
            var g = Enumerable.Repeat(double.PositiveInfinity, total).ToArray();
            var previous = Enumerable.Repeat(-1, total).ToArray();
            var closed = new bool[total];
            var open = new SortedSet<OpenNode>(new OpenNodeComparer());
            long sequence = 0;
            g[start] = 0;
            open.Add(new OpenNode { Id = start, G = 0, F = Heuristic(xs[sx], ys[sy], endX, endY), Sequence = sequence++ });

            while (open.Count > 0)
            {
                OpenNode current = open.Min;
                open.Remove(current);
                int id = current.Id;
                if (closed[id]) continue;
                closed[id] = true;
                if (id == goal) break;
                int ix = id / ny, iy = id % ny;
                TryNeighbor(ix - 1, iy, id, xs, ys, ny, expanded, endX, endY, g, previous, closed, open, ref sequence);
                TryNeighbor(ix + 1, iy, id, xs, ys, ny, expanded, endX, endY, g, previous, closed, open, ref sequence);
                TryNeighbor(ix, iy - 1, id, xs, ys, ny, expanded, endX, endY, g, previous, closed, open, ref sequence);
                TryNeighbor(ix, iy + 1, id, xs, ys, ny, expanded, endX, endY, g, previous, closed, open, ref sequence);
            }
            if (!closed[goal]) return null;
            var path = new List<RoutePoint2D>();
            for (int at = goal; at >= 0; at = previous[at])
            {
                int ix = at / ny, iy = at % ny;
                path.Add(new RoutePoint2D(xs[ix], ys[iy]));
                if (at == start) break;
            }
            path.Reverse();
            return Compress(path);
        }

        private static void TryNeighbor(int nx, int nyIndex, int from, IList<double> xs, IList<double> ys, int ny,
            IList<RouteObstacle2D> obstacles, double endX, double endY, double[] g, int[] previous, bool[] closed,
            SortedSet<OpenNode> open, ref long sequence)
        {
            if (nx < 0 || nx >= xs.Count || nyIndex < 0 || nyIndex >= ys.Count) return;
            int to = nx * ny + nyIndex;
            if (closed[to] || Inside(xs[nx], ys[nyIndex], obstacles)) return;
            int fx = from / ny, fy = from % ny;
            if (Blocked(xs[fx], ys[fy], xs[nx], ys[nyIndex], obstacles)) return;
            double step = Math.Abs(xs[fx] - xs[nx]) + Math.Abs(ys[fy] - ys[nyIndex]);
            double candidate = g[from] + step;
            if (candidate + Epsilon >= g[to]) return;
            g[to] = candidate;
            previous[to] = from;
            open.Add(new OpenNode { Id = to, G = candidate, F = candidate + Heuristic(xs[nx], ys[nyIndex], endX, endY), Sequence = sequence++ });
        }

        private static bool Blocked(double x1, double y1, double x2, double y2, IList<RouteObstacle2D> obstacles)
        {
            foreach (RouteObstacle2D o in obstacles)
            {
                if (Math.Abs(y1 - y2) <= Epsilon)
                {
                    if (y1 > o.MinY + Epsilon && y1 < o.MaxY - Epsilon &&
                        Math.Max(Math.Min(x1, x2), o.MinX + Epsilon) < Math.Min(Math.Max(x1, x2), o.MaxX - Epsilon))
                        return true;
                }
                else if (Math.Abs(x1 - x2) <= Epsilon)
                {
                    if (x1 > o.MinX + Epsilon && x1 < o.MaxX - Epsilon &&
                        Math.Max(Math.Min(y1, y2), o.MinY + Epsilon) < Math.Min(Math.Max(y1, y2), o.MaxY - Epsilon))
                        return true;
                }
            }
            return false;
        }

        private static bool Inside(double x, double y, IList<RouteObstacle2D> obstacles)
        {
            foreach (RouteObstacle2D o in obstacles)
                if (x > o.MinX + Epsilon && x < o.MaxX - Epsilon &&
                    y > o.MinY + Epsilon && y < o.MaxY - Epsilon) return true;
            return false;
        }
        private static IList<RoutePoint2D> Compress(IList<RoutePoint2D> path)
        {
            if (path.Count < 3) return path;
            var result = new List<RoutePoint2D> { path[0] };
            for (int i = 1; i < path.Count - 1; i++)
            {
                RoutePoint2D a = result[result.Count - 1], b = path[i], c = path[i + 1];
                bool straightX = Math.Abs(a.Y - b.Y) <= Epsilon && Math.Abs(b.Y - c.Y) <= Epsilon;
                bool straightY = Math.Abs(a.X - b.X) <= Epsilon && Math.Abs(b.X - c.X) <= Epsilon;
                if (!straightX && !straightY) result.Add(b);
            }
            result.Add(path[path.Count - 1]);
            return result;
        }
        private static List<double> UniqueSorted(List<double> values)
        {
            values.Sort();
            var result = new List<double>();
            foreach (double value in values)
                if (result.Count == 0 || Math.Abs(value - result[result.Count - 1]) > Epsilon) result.Add(value);
            return result;
        }
        private static int IndexOf(IList<double> values, double target)
        {
            for (int i = 0; i < values.Count; i++) if (Math.Abs(values[i] - target) <= Epsilon) return i;
            return -1;
        }
        private static double Heuristic(double x, double y, double tx, double ty) { return Math.Abs(tx - x) + Math.Abs(ty - y); }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}