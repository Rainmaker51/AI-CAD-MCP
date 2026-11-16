using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using AICad.Geometry;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

public static class SourceOpeningEvidence
{
    private const double WidthToleranceMm = 1;
    private const double WallAlignmentMm = 5;
    private const double JambToleranceMm = 35;

    public static void Validate(JArray evidence, List<Operation> operations, TemplateProfile template)
        => Validate(evidence, operations, template, true);

    // Repair a reference only when the full jamb/width/wall checks identify exactly
    // one block. Never alter geometry, tolerate ambiguity or weaken validation.
    public static JArray NormalizeReferences(JArray evidence, List<Operation> operations, TemplateProfile template)
    {
        var normalized = (JArray)evidence.DeepClone();
        foreach (var item in normalized.OfType<JObject>())
        {
            bool Matches(int index)
            {
                var probe = (JObject)item.DeepClone();
                probe["operationIndex"] = index;
                try { Validate(new JArray(probe), operations, template, false); return true; }
                catch (CadException) { return false; }
            }
            int original = item.Value<int?>("operationIndex") ?? -1;
            if (Matches(original)) continue;
            var matches = Enumerable.Range(0, operations.Count).Where(Matches).Take(2).ToArray();
            if (matches.Length == 1) item["operationIndex"] = matches[0];
        }
        Validate(normalized, operations, template);
        return normalized;
    }

    private static void Validate(JArray evidence, List<Operation> operations, TemplateProfile template, bool requireAll)
    {
        var blocks = template.Blocks.Where(b => b.Usage?.Role is "door" or "window")
            .ToDictionary(b => b.Id, b => b.Usage!);
        var walls = operations.Where(op => op.Kind == "draw_wall").ToArray();
        var found = new HashSet<int>();
        var pixelPairs = new List<(Point2 A, Point2 B)>();
        if (evidence.Any(item => item is not JObject))
            throw new CadException("OPENING_EVIDENCE", "Dữ liệu đối chiếu cửa không hợp lệ.");
        foreach (var item in evidence.OfType<JObject>())
        {
            int index = item.Value<int?>("operationIndex") ?? -1;
            if (index < 0 || index >= operations.Count || !found.Add(index))
                Fail(index, "Mỗi cửa cần một cặp mép riêng, gắn đúng block.");
            var op = operations[index];
            if (op.Kind != "insert_block" || !blocks.TryGetValue(op.AssetId, out var usage))
                throw Error(index, "Cặp mép phải gắn với block cửa của Template.");
            string expectedMark = usage.Role == "window" ? "frame_between_jambs" : "leaf_or_swing";
            if (item.Value<string>("mark") != expectedMark)
                Fail(index, "Dấu hiệu trên ảnh không khớp loại cửa.");

            var pixelA = ReadPoint(item, "pixelA");
            var pixelB = ReadPoint(item, "pixelB");
            if (Validation.Distance(pixelA, pixelB) < 2)
                Fail(index, "Hai mép trên ảnh chưa phân biệt được.");
            if (pixelPairs.Any(pair =>
                (Validation.Distance(pair.A, pixelA) < 2 && Validation.Distance(pair.B, pixelB) < 2) ||
                (Validation.Distance(pair.A, pixelB) < 2 && Validation.Distance(pair.B, pixelA) < 2)))
                Fail(index, "Cặp mép trên ảnh đã được dùng cho cửa khác.");
            pixelPairs.Add((pixelA, pixelB));

            var a = ReadPoint(item, "jambA");
            var b = ReadPoint(item, "jambB");
            double width = Validation.Distance(a, b);
            if (width < 1 || op.OpeningWidth is not double opening || Math.Abs(width - opening) > WidthToleranceMm)
                Fail(index, "Bề rộng ô chờ phải bằng khoảng cách giữa hai mép.");
            double ux = (b.X - a.X) / width, uy = (b.Y - a.Y) / width;
            double Along(Point2 p) => (p.X - a.X) * ux + (p.Y - a.Y) * uy;
            double Across(Point2 p) => (p.Y - a.Y) * ux - (p.X - a.X) * uy;
            var aligned = walls.Where(w => w.Points.All(p => Math.Abs(Across(p)) <= WallAlignmentMm)).ToArray();
            bool touchesJamb = false;
            foreach (var wall in aligned)
            {
                double u = Along(wall.Points[0]), v = Along(wall.Points[1]);
                double left = Math.Min(u, v), right = Math.Max(u, v);
                if (Math.Min(right, width) - Math.Max(left, 0) > WallAlignmentMm)
                    Fail(index, "Có nét tim tường đi xuyên qua ô cửa.");
                touchesJamb |= Math.Abs(right) <= JambToleranceMm || Math.Abs(left - width) <= JambToleranceMm;
            }
            if (!touchesJamb)
                Fail(index, "Không thấy đoạn tường nối vào mép cửa trên cùng đường tim.");

            double direction = Math.Cos(op.Rotation) * ux + Math.Sin(op.Rotation) * uy;
            double faceAllowance = Math.Max(110, op.WallThickness.GetValueOrDefault() / 2);
            if (Math.Abs(direction) < Math.Cos(Math.PI / 36) ||
                Math.Abs(Along(op.Points[0]) - (direction > 0 ? 0 : width)) > JambToleranceMm ||
                Math.Abs(Across(op.Points[0])) > faceAllowance + JambToleranceMm)
                Fail(index, "Block phải đặt ở mép đầu ô chờ và cùng hướng tường.");
        }
        for (int index = 0; requireAll && index < operations.Count; index++)
        {
            var op = operations[index];
            if (op.Kind == "insert_block" && blocks.TryGetValue(op.AssetId, out var asset) &&
                (op.OpeningWidth.HasValue || asset.ConversionConfirmed) && !found.Contains(index))
                Fail(index, "Thiếu hai mép cửa đối chiếu được trên ảnh.");
        }
    }

    private static Point2 ReadPoint(JObject item, string name)
    {
        var point = item[name]?.ToObject<Point2>() ?? throw new CadException("OPENING_EVIDENCE", "Thiếu tọa độ mép cửa.");
        Validation.Finite(point.X);
        Validation.Finite(point.Y);
        return point;
    }

    private static void Fail(int index, string message) => throw Error(index, message);
    private static CadException Error(int index, string message) =>
        new("OPENING_EVIDENCE", $"Vị trí cửa ở thao tác {index + 1}. {message}");
}
