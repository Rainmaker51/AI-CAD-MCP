using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using AICad.Geometry;

namespace AICad.Application;

internal static class ResidentialWalls
{
    private const double Epsilon = 0.00001;
    private const double JoinToleranceMm = 2;
    private sealed record Edge(Point2 A, Point2 B)
    {
        public Point2 At(double t) => new(A.X + t * (B.X - A.X), A.Y + t * (B.Y - A.Y));
        public double Y(double x) => At((x - A.X) / (B.X - A.X)).Y;
    }
    private sealed record HatchStrip(double Left, double Right, Edge Bottom, Edge Top);

    internal static List<Operation> Render(IReadOnlyList<Operation> walls, string layer, string hatchLayer)
    {
        var bodies = new List<List<Point2>>();
        foreach (var wall in walls)
            bodies.Add(DraftingRules.Wall(wall));

        // Fill the outside wedge between the offset faces at a shared endpoint.
        // A bounded miter prevents long spikes at near-parallel joins.
        for (int i = 0; i < walls.Count; i++)
            for (int j = i + 1; j < walls.Count; j++)
                for (int a = 0; a < 2; a++)
                    for (int b = 0; b < 2; b++)
                        if (Validation.Distance(walls[i].Points[a], walls[j].Points[b]) <= JoinToleranceMm)
                            AddCorner(bodies, walls[i], a, walls[j], b);

        var edges = bodies.SelectMany(p => p.Select((point, i) => new Edge(point, p[(i + 1) % p.Count]))).ToArray();
        var events = new List<double>(edges.Select(e => e.A.X));
        var operations = new List<Operation>();
        var outline = new List<Edge>();
        var emitted = new HashSet<(double, double, double, double)>();

        foreach (var edge in edges)
        {
            var cuts = new List<double> { 0, 1 };
            foreach (var other in edges)
            {
                if (Intersect(edge, other, out var t, out var u))
                {
                    if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
                        cuts.Add(t);
                }
                else
                {
                    AddCollinearCut(edge, other.A, cuts);
                    AddCollinearCut(edge, other.B, cuts);
                }
            }
            var sorted = cuts.Distinct().OrderBy(t => t).ToArray();
            for (int i = 1; i < sorted.Length; i++)
            {
                var a = edge.At(sorted[i - 1]);
                var b = edge.At(sorted[i]);
                var length = Validation.Distance(a, b);
                events.Add(a.X);
                if (length < Epsilon)
                    continue;
                var middle = edge.At((sorted[i - 1] + sorted[i]) / 2);
                double offset = Math.Min(.0001, length / 100);
                double nx = (b.Y - a.Y) * offset / length, ny = (a.X - b.X) * offset / length;
                bool left = bodies.Any(p => Inside(p, middle.X + nx, middle.Y + ny));
                bool right = bodies.Any(p => Inside(p, middle.X - nx, middle.Y - ny));
                if (left == right)
                    continue;
                if (a.X > b.X || (a.X == b.X && a.Y > b.Y))
                    (a, b) = (b, a);
                if (emitted.Add((Math.Round(a.X, 5), Math.Round(a.Y, 5), Math.Round(b.X, 5), Math.Round(b.Y, 5))))
                    outline.Add(new Edge(a, b));
            }
        }
        operations.AddRange(OutlinePaths(outline, layer));

        // Sweep the same union into disjoint convex hatch regions.
        var xs = events.Select(x => Math.Round(x, 7)).Distinct().OrderBy(x => x).ToArray();
        var strips = new List<HatchStrip>();
        var lastStrip = new Dictionary<(double, double, double, double), int>();
        (double Slope, double Intercept) Support(Edge edge)
        {
            double slope = (edge.B.Y - edge.A.Y) / (edge.B.X - edge.A.X);
            return (Math.Round(slope, 10), Math.Round(edge.A.Y - slope * edge.A.X, 7));
        }
        for (int i = 1; i < xs.Length; i++)
        {
            double left = xs[i - 1], right = xs[i], x = (left + right) / 2;
            if (right - left < Epsilon)
                continue;
            var intervals = new List<(Edge Bottom, Edge Top)>();
            foreach (var body in bodies)
            {
                var sides = body.Select((a, n) => new Edge(a, body[(n + 1) % body.Count]))
                    .Where(e => x > Math.Min(e.A.X, e.B.X) && x < Math.Max(e.A.X, e.B.X))
                    .OrderBy(e => e.Y(x)).ToArray();
                if (sides.Length == 2)
                    intervals.Add((sides[0], sides[1]));
            }
            Edge? bottom = null, top = null;
            void AddStrip()
            {
                if (bottom == null || top == null)
                    return;
                var low = Support(bottom);
                var high = Support(top);
                var key = (low.Slope, low.Intercept, high.Slope, high.Intercept);
                if (lastStrip.TryGetValue(key, out int index) && Math.Abs(strips[index].Right - left) < Epsilon)
                    strips[index] = strips[index] with { Right = right };
                else
                {
                    lastStrip[key] = strips.Count;
                    strips.Add(new(left, right, bottom, top));
                }
            }
            foreach (var interval in intervals.OrderBy(s => s.Bottom.Y(x)))
            {
                if (top == null || interval.Bottom.Y(x) > top.Y(x) + Epsilon)
                {
                    AddStrip();
                    (bottom, top) = interval;
                }
                else if (interval.Top.Y(x) > top.Y(x))
                    top = interval.Top;
            }
            AddStrip();
        }
        foreach (var strip in strips)
        {
            var (left, right, bottom, top) = strip;
            var polygon = new[] { new Point2(left, bottom.Y(left)), new Point2(right, bottom.Y(right)),
                new Point2(right, top.Y(right)), new Point2(left, top.Y(left)) };
            var points = polygon.Where((p, n) => Validation.Distance(p, polygon[(n + 3) % 4]) >= Epsilon).ToList();
            double middle = (left + right) / 2;
            if (points.Count >= 3 && (right - left) * (top.Y(middle) - bottom.Y(middle)) > Epsilon)
                operations.Add(new() { Kind = "create_hatch", Layer = hatchLayer, Pattern = "ANSI31",
                    PatternScale = 300, Points = points });
        }
        return operations;
    }

    private static IEnumerable<Operation> OutlinePaths(List<Edge> outline, string layer)
    {
        static (double, double) Key(Point2 p) => (Math.Round(p.X, 5), Math.Round(p.Y, 5));
        var edges = outline.Select(e => (A: Key(e.A), B: Key(e.B))).ToArray();
        var adjacency = new Dictionary<(double, double), List<int>>();
        for (int i = 0; i < edges.Length; i++)
            foreach (var point in new[] { edges[i].A, edges[i].B }.Distinct())
            {
                if (!adjacency.TryGetValue(point, out var list))
                    adjacency[point] = list = new();
                list.Add(i);
            }
        var used = new bool[edges.Length];
        foreach (int i in Enumerable.Range(0, edges.Length)
            .OrderBy(index => adjacency[edges[index].A].Count == 2 && adjacency[edges[index].B].Count == 2))
        {
            if (used[i])
                continue;
            var start = adjacency[edges[i].A].Count == 2 ? edges[i].B : edges[i].A;
            var current = start;
            var points = new List<Point2> { new(start.Item1, start.Item2) };
            int edgeIndex = i;
            while (!used[edgeIndex] && points.Count < 2000)
            {
                used[edgeIndex] = true;
                var edge = edges[edgeIndex];
                current = edge.A == current ? edge.B : edge.A;
                if (current == start)
                    break;
                points.Add(new(current.Item1, current.Item2));
                var next = adjacency[current];
                if (next.Count != 2)
                    break;
                edgeIndex = next[0] == edgeIndex ? next[1] : next[0];
            }
            if (points.Count >= 2)
                yield return new() { Kind = "draw_polyline", Layer = layer, Points = points, Closed = current == start };
        }
    }

    private static void AddCorner(List<List<Point2>> bodies, Operation a, int ai, Operation b, int bi)
    {
        var p = a.Points[ai];
        var q = b.Points[bi];
        var da = new Edge(p, a.Points[1 - ai]);
        var db = new Edge(q, b.Points[1 - bi]);
        double cross = Cross(da.B.X - p.X, da.B.Y - p.Y, db.B.X - q.X, db.B.Y - q.Y);
        double la = Validation.Distance(p, da.B), lb = Validation.Distance(q, db.B);
        if (Math.Abs(cross) < .001 * la * lb)
            return;
        double side = Math.Sign(cross);
        double ax = side * (da.B.Y - p.Y) * a.Height / (2 * la);
        double ay = -side * (da.B.X - p.X) * a.Height / (2 * la);
        double bx = -side * (db.B.Y - q.Y) * b.Height / (2 * lb);
        double by = side * (db.B.X - q.X) * b.Height / (2 * lb);
        var af = new Edge(new(p.X + ax, p.Y + ay), new(da.B.X + ax, da.B.Y + ay));
        var bf = new Edge(new(q.X + bx, q.Y + by), new(db.B.X + bx, db.B.Y + by));
        Intersect(af, bf, out var t, out _);
        var tip = af.At(t);
        var center = new Point2((p.X + q.X) / 2, (p.Y + q.Y) / 2);
        var wedge = new List<Point2> { center, af.A, bf.A };
        if (Validation.Distance(tip, center) <= 2 * Math.Max(a.Height, b.Height))
            wedge.Insert(2, tip);
        bodies.Add(wedge);
        if (Validation.Distance(p, q) > Epsilon)
        {
            bodies.Add(new() { p, q, af.A });
            bodies.Add(new() { q, bf.A, af.A });
        }
    }

    private static bool Inside(List<Point2> polygon, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > y) != (b.Y > y) && x < a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y))
                inside = !inside;
        }
        return inside;
    }

    private static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;

    private static bool Intersect(Edge a, Edge b, out double t, out double u)
    {
        double ax = a.B.X - a.A.X, ay = a.B.Y - a.A.Y, bx = b.B.X - b.A.X, by = b.B.Y - b.A.Y;
        double determinant = Cross(ax, ay, bx, by);
        t = u = 0;
        if (Math.Abs(determinant) < Epsilon)
            return false;
        double x = b.A.X - a.A.X, y = b.A.Y - a.A.Y;
        t = Cross(x, y, bx, by) / determinant;
        u = Cross(x, y, ax, ay) / determinant;
        return true;
    }

    private static void AddCollinearCut(Edge edge, Point2 point, List<double> cuts)
    {
        double dx = edge.B.X - edge.A.X, dy = edge.B.Y - edge.A.Y;
        double x = point.X - edge.A.X, y = point.Y - edge.A.Y;
        if (Math.Abs(Cross(dx, dy, x, y)) > Epsilon)
            return;
        double t = (dx * x + dy * y) / (dx * dx + dy * dy);
        if (t > 0 && t < 1)
            cuts.Add(t);
    }
}
