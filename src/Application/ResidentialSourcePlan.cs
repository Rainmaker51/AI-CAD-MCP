using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using AICad.Geometry;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

public static class ResidentialSourcePlan
{
    internal const string SkillId = "SOURCE_ISO_QUANGANH";

    public static void FitCatalogBlocks(DrawingPlan plan)
    {
        if (plan.Template.Id != "ISO_QUANGANH")
            return;
        var asset = plan.Template.Blocks.SingleOrDefault(b => b.Id == "QA_KE_TIVI");
        if (asset?.NativeSource is not { } native || asset.Usage is not { Reviewed: true } usage ||
            usage.Values.Value<string>("NOI THAT") != "Ke_Tivi")
            return;

        var box = Validation.Box(native.Min.X, native.Min.Y, native.Max.X, native.Max.Y).ToArray();
        int rotated = 0;
        foreach (var op in plan.Operations.Where(o => o.Kind == "insert_block" && o.AssetId == asset.Id))
        {
            // The library envelope applies to the default dimensions. A custom
            // size or visibility still needs measurement by the native preview.
            if (op.Points.Count != 1 || op.OpeningWidth != null || op.WallThickness != null ||
                op.SymbolScale != null || (op.DynamicJson is string json && Json.Read<JObject>(json)
                    .Properties().Any(p => !JToken.DeepEquals(usage.Values[p.Name], p.Value))))
                continue;

            bool Fits(double angle) => box.All(p => Validation.Contains(plan.Scope, Validation.Rotate(p, op.Points[0], angle)));
            if (Fits(op.Rotation))
                continue;
            double opposite = Math.IEEERemainder(op.Rotation + Math.PI, 2 * Math.PI);
            if (!Fits(opposite))
                throw new CadException("OUTSIDE_SCOPE",
                    "Phạm vi kệ TV QA_KE_TIVI không vừa khung ở cả hai hướng. Kiểm tra vị trí kệ hoặc kích thước khung rồi lập lại kế hoạch.");
            op.Rotation = opposite;
            rotated++;
        }
        if (rotated > 0)
            plan.Assumptions.Add($"Đã xoay 180° {rotated} block kệ TV để vừa khung, giữ nguyên điểm chèn. Kiểm tra hướng kệ trên preview.");
    }

    internal static void Complete(DrawingPlan plan, SkillDefinition skill, DraftingRecipe recipe, JObject input, TaskInfo task)
    {
        if (input["annotationScales"] is not JArray scales ||
            scales.Count < 1 || scales.Count > 6 ||
            scales.Any(s => s.Type != JTokenType.Integer || (decimal)s < 10 || (decimal)s > 200))
            throw new CadException("ANNOTATION_SCALE_REQUIRED", "Chọn tỷ lệ chính và tối đa 5 tỷ lệ bổ sung trong khoảng 1:10–1:200.");
        plan.AnnotationScales = scales.Values<int>().Distinct().ToList();
        foreach (var text in plan.Operations.Where(o => o.Kind == "add_text"))
            text.Height = 2 * plan.AnnotationScales[0];

        var walls = plan.Operations.Where(o => o.Kind == "draw_wall" && o.Layer == recipe.Layers["wall"]).ToArray();
        if (walls.Length == 0)
            throw new CadException("NO_WALLS", "Bản nháp cần tim tường để tạo hatch và đề xuất trục.");

        SourceDoorPlacement.Fit(plan, walls);
        var columns = plan.Operations.Where(o => o.Kind == "draw_polyline" && o.Closed &&
            o.Layer == recipe.Layers.GetValueOrDefault("column", "QA_KT_Cot") && o.Points.Count >= 4)
            .Select(o => new Point2((o.Points.Min(p => p.X) + o.Points.Max(p => p.X)) / 2,
                (o.Points.Min(p => p.Y) + o.Points.Max(p => p.Y)) / 2)).ToArray();
        plan.Operations.RemoveAll(walls.Contains);
        plan.Operations.AddRange(ResidentialWalls.Render(walls, recipe.Layers["wall"], "QA_KT_Hatch"));

        if (!recipe.IncludeAxes)
        {
            var points = walls.SelectMany(w => w.Points).ToArray();
            double extension = Math.Max(500, 110 + 5.2 * plan.AnnotationScales[0]);
            double left = points.Min(p => p.X), right = points.Max(p => p.X);
            double bottom = points.Min(p => p.Y), top = points.Max(p => p.Y);
            var axes = new DraftingRecipe { IncludeAxes = true, Layers = recipe.Layers };
            double tolerance = skill.Parameters.GetValueOrDefault("axisMergeToleranceMm");
            var horizontal = AxisPositions(walls, columns, horizontal: true, tolerance);
            var vertical = AxisPositions(walls, columns, horizontal: false, tolerance);
            for (int i = 0; i < horizontal.Count; i++)
                axes.Axes.Add(new() { Name = (i + 1).ToString(), Start = new(left - extension, horizontal[i]), End = new(right + extension, horizontal[i]) });
            for (int i = 0; i < vertical.Count; i++)
                axes.Axes.Add(new() { Name = ((char)('A' + i)).ToString(), Start = new(vertical[i], bottom - extension), End = new(vertical[i], top + extension) });
            string? symbol = recipe.AllowedBlocks.FirstOrDefault(id =>
                plan.Template.Blocks.Any(b => b.Id == id && b.Usage?.Role == "axisHead" && b.NativeSource?.LabelAttribute != null));
            if (symbol == null)
                throw new CadException("AXIS_BLOCK_REQUIRED", "Liên kết block ATT trục đã duyệt vào Skill.");
            int scale = plan.AnnotationScales[0];
            foreach (var axis in axes.Axes)
            {
                plan.Operations.Add(new() { Kind = "draw_line", Layer = recipe.Layers["axis"], Points = new() { axis.Start, axis.End } });
                foreach (var point in new[] { axis.Start, axis.End })
                    plan.Operations.Add(new() { Kind = "insert_block", Layer = recipe.Layers["axisHead"],
                        AssetId = symbol, SymbolScale = scale, AttributeText = axis.Name, Points = new() { point } });
            }
            AddAxisDimensions(plan, task, recipe.Layers["dimension"], axes.Axes, scale);
            plan.Assumptions.Add("Trục đề xuất từ tim các đoạn tường dài; kiểm tra vị trí và ký hiệu trên preview.");
        }
    }

    private static void AddAxisDimensions(DrawingPlan plan, TaskInfo task, string layer, List<AxisSpec> axes, int scale)
    {
        double width = Validation.Distance(task.Scope[0], task.Scope[1]);
        double height = Validation.Distance(task.Scope[0], task.Scope[3]);
        var sourceDims = plan.Operations.Where(o => o.Kind == "add_dimension").ToArray();
        foreach (bool horizontal in new[] { true, false })
        {
            // A horizontal string measures the spacing of the vertical axes, and vice versa.
            double Along(Point2 p) => horizontal ? p.X : p.Y;
            double Across(Point2 p) => horizontal ? p.Y : p.X;
            Point2 Point(double along, double across) => horizontal ? new(along, across) : new(across, along);
            var group = axes.Where(a => Math.Abs(Along(a.Start) - Along(a.End)) < .001)
                .OrderBy(a => Along(a.Start)).ToArray();
            if (group.Length < 2)
                continue;

            double acrossLimit = horizontal ? height : width;
            var locations = new List<(double Start, double Chain, double Total, double Room, int Conflicts)>();
            foreach (int side in new[] { -1, 1 })
            {
                double head = Across(side < 0 ? group[0].Start : group[0].End);
                double start = head + side * 5.2 * scale;
                double chain = start + side * 2 * scale;
                double total = chain + side * 3.2 * scale;
                double margin = plan.Template.TextHeight * 4;
                double room = side < 0 ? total - margin : acrossLimit - total - margin;
                int conflicts = sourceDims.Count(o =>
                    Math.Abs(Across(o.Points[0]) - Across(o.Points[1])) < .001 &&
                    (side < 0 ? Across(o.Points[2]) < head + 500 : Across(o.Points[2]) > head - 500));
                if (room >= 0)
                    locations.Add((start, chain, total, room, conflicts));
            }
            if (locations.Count == 0)
                throw new CadException("AXIS_DIMENSION_SCOPE",
                    "Khung chưa đủ chỗ cho hai hàng dim trục. Mở rộng khung hoặc chọn tỷ lệ chú thích nhỏ hơn rồi lập lại kế hoạch.");
            var location = locations.OrderBy(x => x.Conflicts).ThenByDescending(x => x.Room).First();
            void Add(double a, double b, double offset) => plan.Operations.Add(new()
            {
                Kind = "add_dimension", Layer = layer,
                Points = new() { Point(a, location.Start), Point(b, location.Start), Point((a + b) / 2, offset) }
            });
            for (int i = 1; i < group.Length; i++)
                Add(Along(group[i - 1].Start), Along(group[i].Start), location.Chain);
            if (group.Length > 2)
                Add(Along(group[0].Start), Along(group[^1].Start), location.Total);
        }
    }

    public static List<double> AxisPositions(IEnumerable<Operation> walls, IReadOnlyList<Point2> columns,
        bool horizontal, double tolerance)
    {
        double Cross(Point2 p) => horizontal ? p.Y : p.X;
        double Along(Point2 p) => horizontal ? p.X : p.Y;
        var positions = walls.Where(w => Cross(w.Points[0]) == Cross(w.Points[1]))
            .GroupBy(w => Cross(w.Points[0]))
            .Select(group => (At: group.Key, Length: group.Sum(w => Math.Abs(Along(w.Points[0]) - Along(w.Points[1])))))
            .Where(p => p.Length >= 2000).OrderBy(p => p.At).ToArray();
        var result = new List<double>();
        for (int first = 0; first < positions.Length;)
        {
            int end = first + 1;
            while (end < positions.Length && positions[end].At - positions[first].At <= tolerance + 1e-6)
                end++;
            // Match a column line only if two columns support the same coordinate.
            var hits = columns.Select(Cross)
                .Where(c => c >= positions[first].At - tolerance && c <= positions[end - 1].At + tolerance)
                .GroupBy(c => Math.Round(c, 3)).Where(g => g.Count() >= 2)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToArray();
            if (end - first > 1 && hits.Length > 0)
                result.AddRange(hits.Select(h => h.Key));
            else
                result.Add(positions.Skip(first).Take(end - first)
                    .OrderByDescending(p => p.Length).ThenBy(p => p.At).First().At);
            first = end;
        }
        var distinct = result.Distinct().OrderBy(v => v).ToList();
        if (distinct.Count > 26)
            throw new CadException("AXIS_LIMIT", "Có hơn 26 vị trí trục đề xuất. Khai báo trục trong Skill để giới hạn phạm vi.");
        return distinct;
    }
}
