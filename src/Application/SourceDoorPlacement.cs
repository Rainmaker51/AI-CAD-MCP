using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using AICad.Geometry;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

public static class SourceDoorPlacement
{
    private sealed record Gap(Point2 A, Point2 B, double Thickness);

    public static void Fit(DrawingPlan plan, IReadOnlyList<Operation> walls)
    {
        if (plan.Template.Presets["doorFramePlacement"] is not JObject config)
            return;
        var gaps = FindGaps(walls);
        int moved = 0, unresolved = 0;
        foreach (var op in plan.Operations.Where(o => o.Kind == "insert_block" && config[o.AssetId] is JObject))
        {
            var settings = (JObject)config[op.AssetId]!;
            var asset = plan.Template.Blocks.Single(b => b.Id == op.AssetId);
            var values = DraftingRules.Values(op, asset);
            string visibility = values.Value<string>("CUA") ?? "";
            if (settings["visibilities"] is not JArray visibilities || !visibilities.Values<string>().Contains(visibility) ||
                Math.Abs(values.Value<double?>("Xoay") ?? 0) > 1e-6 ||
                op.Points.Count != 1 || op.OpeningWidth is not double width || width <= 0)
            {
                unresolved++;
                continue;
            }
            double maxSnap = settings.Value<double>("maxCorrectionMm");
            double openingTolerance = settings.Value<double>("openingToleranceMm");
            double frameSide = settings.Value<double>("zeroFlipFrameSide");
            if (!double.IsFinite(maxSnap) || maxSnap <= 0 || maxSnap > 500 ||
                !double.IsFinite(openingTolerance) || openingTolerance <= 0 || openingTolerance > 20 ||
                frameSide is not (-1 or 1))
                throw new CadException("DOOR_FRAME_CONFIG", "Kiểm tra cấu hình mốc khung của " + asset.Id);

            var origin = op.Points[0];
            double vx = Math.Cos(op.Rotation), vy = Math.Sin(op.Rotation);
            double alongSign = values.Value<int?>("Flip state1") == 1 ? -1 : 1;
            var options = new List<(Point2 Point, double Score, double Thickness)>();
            foreach (var gap in gaps)
            {
                double length = Validation.Distance(gap.A, gap.B);
                if (Math.Abs(length - width) > openingTolerance ||
                    Math.Abs((gap.B.X - gap.A.X) * vy - (gap.B.Y - gap.A.Y) * vx) > length * .001)
                    continue;
                double dot = (gap.B.X - gap.A.X) * vx + (gap.B.Y - gap.A.Y) * vy;
                var jamb = dot * alongSign > 0 ? gap.A : gap.B;
                double howFarAlong = Math.Abs((origin.X - jamb.X) * vx + (origin.Y - jamb.Y) * vy);
                if (howFarAlong > 35)
                    continue;
                double sign = values.Value<int?>("Flip state") == 1 ? -frameSide : frameSide;
                var point = new Point2(jamb.X + sign * vy * gap.Thickness / 2, jamb.Y - sign * vx * gap.Thickness / 2);
                double score = Validation.Distance(origin, point);
                if (score <= maxSnap)
                    options.Add((point, score, gap.Thickness));
            }
            var best = options.OrderBy(o => o.Score).Take(2).ToArray();
            if (best.Length == 0 || (best.Length == 2 && best[1].Score - best[0].Score < 10))
            {
                unresolved++;
                continue;
            }
            if (best[0].Score > .01 || op.WallThickness != best[0].Thickness)
            {
                op.Points[0] = best[0].Point;
                op.WallThickness = best[0].Thickness;
                moved++;
            }
        }
        if (moved > 0)
            plan.Assumptions.Add($"Đã căn {moved} khung cửa theo jamb và bề dày tường, giữ chiều mở.");
        if (unresolved > 0)
            plan.Assumptions.Add($"{unresolved} cửa cần kiểm tra vị trí: chưa có đúng một ô hở khớp cấu hình hoặc visibility chưa đo.");
    }

    private static List<Gap> FindGaps(IReadOnlyList<Operation> walls)
    {
        var gaps = new List<Gap>();
        for (int i = 0; i < walls.Count; i++)
        {
            var a = walls[i];
            double length = Validation.Distance(a.Points[0], a.Points[1]);
            if (length < 1)
                continue;
            var origin = a.Points[0];
            double ux = (a.Points[1].X - origin.X) / length, uy = (a.Points[1].Y - origin.Y) / length;
            double Along(Point2 p) => (p.X - origin.X) * ux + (p.Y - origin.Y) * uy;
            double Across(Point2 p) => (p.Y - origin.Y) * ux - (p.X - origin.X) * uy;
            for (int j = i + 1; j < walls.Count; j++)
            {
                var b = walls[j];
                if (a.Height != b.Height || b.Points.Any(p => Math.Abs(Across(p)) > 2))
                    continue;
                var pair = b.Points.OrderBy(Along).ToArray();
                Point2 from, to;
                if (Along(pair[0]) > length)
                    (from, to) = (a.Points[1], pair[0]);
                else if (Along(pair[1]) < 0)
                    (from, to) = (pair[1], a.Points[0]);
                else
                    continue;
                double lo = Along(from), hi = Along(to);
                if (hi - lo < 450 || hi - lo > 5000 || walls.Any(w =>
                    w.Points.All(p => Math.Abs(Across(p)) <= 2) &&
                    Math.Min(hi, w.Points.Max(Along)) - Math.Max(lo, w.Points.Min(Along)) > 2))
                    continue;
                gaps.Add(new(from, to, a.Height));
            }
        }
        return gaps;
    }
}
