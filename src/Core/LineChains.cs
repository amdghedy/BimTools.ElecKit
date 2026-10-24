using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace ElecKit.Core
{
    /// <summary>Joins straight lines that share end points into ordered point lists (polylines).</summary>
    public static class LineChains
    {
        private sealed class Edge
        {
            public int Line;
            public int End;
            public Edge(int line, int end) { Line = line; End = end; }
        }

        public static List<List<XYZ>> Build(IList<Line> lines, double tolerance)
        {
            var nodes = new List<XYZ>();
            var adjacency = new List<List<Edge>>();
            var nodeOf = new int[lines.Count, 2];

            for (int i = 0; i < lines.Count; i++)
            {
                for (int end = 0; end < 2; end++)
                {
                    XYZ p = lines[i].GetEndPoint(end);
                    int n = FindNode(nodes, p, tolerance);
                    if (n < 0)
                    {
                        nodes.Add(p);
                        adjacency.Add(new List<Edge>());
                        n = nodes.Count - 1;
                    }
                    nodeOf[i, end] = n;
                    adjacency[n].Add(new Edge(i, end));
                }
            }

            var used = new bool[lines.Count];
            var chains = new List<List<XYZ>>();

            // open chains first: start at nodes that are not simple pass-through points
            for (int n = 0; n < nodes.Count; n++)
            {
                if (adjacency[n].Count == 2) continue;
                foreach (Edge e in adjacency[n].ToList())
                {
                    if (!used[e.Line]) chains.Add(Walk(lines, e.Line, e.End, nodeOf, adjacency, used));
                }
            }

            // whatever is left forms closed loops
            for (int i = 0; i < lines.Count; i++)
            {
                if (!used[i]) chains.Add(Walk(lines, i, 0, nodeOf, adjacency, used));
            }
            return chains;
        }

        /// <summary>Drops vertices that sit on a straight stretch.</summary>
        public static List<XYZ> RemoveStraightVertices(List<XYZ> points)
        {
            if (points.Count < 3) return points;

            var result = new List<XYZ> { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                XYZ before = points[i].Subtract(result[result.Count - 1]);
                XYZ after = points[i + 1].Subtract(points[i]);
                if (before.GetLength() < 1e-9 || after.GetLength() < 1e-9) continue;
                if (before.Normalize().DotProduct(after.Normalize()) > 0.99999) continue;
                result.Add(points[i]);
            }
            result.Add(points[points.Count - 1]);
            return result;
        }

        private static int FindNode(List<XYZ> nodes, XYZ p, double tolerance)
        {
            for (int k = 0; k < nodes.Count; k++)
            {
                XYZ q = nodes[k];
                if (Math.Abs(q.X - p.X) <= tolerance && Math.Abs(q.Y - p.Y) <= tolerance && Math.Abs(q.Z - p.Z) <= tolerance)
                    return k;
            }
            return -1;
        }

        private static List<XYZ> Walk(IList<Line> lines, int startLine, int startEnd,
            int[,] nodeOf, List<List<Edge>> adjacency, bool[] used)
        {
            var points = new List<XYZ> { lines[startLine].GetEndPoint(startEnd) };
            int current = startLine;
            int fromEnd = startEnd;

            while (true)
            {
                used[current] = true;
                int toEnd = 1 - fromEnd;
                points.Add(lines[current].GetEndPoint(toEnd));

                int node = nodeOf[current, toEnd];
                if (adjacency[node].Count != 2) break;

                Edge next = null;
                foreach (Edge e in adjacency[node])
                {
                    if (e.Line != current && !used[e.Line]) { next = e; break; }
                }
                if (next == null) break;

                current = next.Line;
                fromEnd = next.End;
            }
            return points;
        }
    }
}
