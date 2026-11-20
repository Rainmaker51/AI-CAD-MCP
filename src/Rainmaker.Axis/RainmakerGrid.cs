using System;
using System.Linq;
using AICad.Bridge;
using AICad.Contracts;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace Rainmaker.Axis;

public sealed partial class RainmakerAxis
{
    const string DimLayer = "QA_KT_Dim";

    static void RunGrid(Document doc, TemplateProfile template, AxisGridParameters? parameters = null)
    {
        var editor = doc.Editor;
        editor.WriteMessage("\nLưới theo WCS XY. Nhịp nhập bằng mm, cách nhau bằng dấu cách, ví dụ 4000 5000 hoặc 3*4000.");
        var xBays = parameters?.XBays.Length>0 ? parameters.XBays : ReadBays(editor, "Khoảng cách trục theo X", "4000 4000 4000");
        if (xBays == null) return;
        var yBays = parameters?.YBays.Length>0 ? parameters.YBays : ReadBays(editor, "Khoảng cách trục theo Y", "4000 4000");
        if (yBays == null) return;
        var xLabels = parameters?.XLabels.Length>0 ? parameters.XLabels : ReadLabels(editor, "Ký hiệu đầu của các trục đứng", "1", xBays.Length + 1);
        if (xLabels == null) return;
        var yLabels = parameters?.YLabels.Length>0 ? parameters.YLabels : ReadLabels(editor, "Ký hiệu đầu của các trục ngang", "A", yBays.Length + 1);
        if (yLabels == null) return;
        int? denominator = parameters?.Scale>0 ? parameters.Scale : ReadScale(doc);
        if (!denominator.HasValue) return;
        var origin = editor.GetPoint("\nChọn giao điểm trục đầu tiên (góc dưới trái của lưới): ");
        if (origin.Status != PromptStatus.OK) return;
        // Editor point input is in the current UCS. Geometry is stored in WCS.
        var worldOrigin = origin.Value.TransformBy(editor.CurrentUserCoordinateSystem);
        if(parameters?.Library==null)throw new CadException("AXIS_SELECTION","Mở bảng Tạo trục để chọn thư viện trước khi vẽ.");
        parameters.XBays=xBays;parameters.YBays=yBays;parameters.XLabels=xLabels;parameters.YLabels=yLabels;parameters.Scale=denominator.Value;
        WriteConfigured(doc.Database,CurrentLibrary(parameters.Library,false),parameters,worldOrigin);
        editor.Regen();
        editor.WriteMessage($"\nRainmaker: Đã tạo {xLabels.Length} trục đứng, {yLabels.Length} trục ngang; dùng block/layer/Dim theo lựa chọn trong kho.");
    }

    static double[]? ReadBays(Editor editor, string prompt, string defaults)
    {
        while (true)
        {
            var result = editor.GetString(new PromptStringOptions($"\n{prompt} <{defaults}>: ") { AllowSpaces = true });
            if (result.Status != PromptStatus.OK) return null;
            try { return AxisGridLayout.ParseBays(string.IsNullOrWhiteSpace(result.StringResult) ? defaults : result.StringResult); }
            catch (ArgumentException error) { editor.WriteMessage("\n" + error.Message); }
        }
    }

    static string[]? ReadLabels(Editor editor, string prompt, string defaults, int count)
    {
        while (true)
        {
            var result = editor.GetString(new PromptStringOptions($"\n{prompt} <{defaults}>: ") { AllowSpaces = false });
            if (result.Status != PromptStatus.OK) return null;
            try { return AxisGridLayout.Labels(string.IsNullOrWhiteSpace(result.StringResult) ? defaults : result.StringResult, count); }
            catch (ArgumentException error) { editor.WriteMessage("\n" + error.Message); }
        }
    }

    internal static void WriteGrid(Database db, TemplateProfile template, Point3d origin, double[] xBays,
        double[] yBays, string[] xLabels, string[] yLabels, int scale)
    {
        NativeWriteGuard.CheckDatabase(db);
        var plan = GridPlan(template, origin, xBays, yBays, xLabels, yLabels, scale);
        NativeLibraryAssets.Demand(plan);
        using var tx = db.TransactionManager.StartTransaction();
        EntityFactory.Resources(plan, db, tx);
        var styles = (DimStyleTable)tx.GetObject(db.DimStyleTableId, OpenMode.ForRead);
        var style = (DimStyleTableRecord)tx.GetObject(styles[EntityFactory.StyleName(plan.Template)], OpenMode.ForWrite);
        style.Dimexo = 0.5 * scale;
        style.Dimexe = 0.8 * scale;
        style.Dimgap = 0.6 * scale;
        EntityFactory.WritePlan(plan, db, tx);
        tx.Commit();
    }

    internal static DrawingPlan GridPlan(TemplateProfile template, Point3d origin, double[] xBays,
        double[] yBays, string[] xLabels, string[] yLabels, int scale)
    {
        var xs = AxisGridLayout.Positions(xBays);
        var ys = AxisGridLayout.Positions(yBays);
        if (xLabels.Length != xs.Length || yLabels.Length != ys.Length ||
            xLabels.Concat(yLabels).Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 8 || s.Any(char.IsControl)) ||
            xLabels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != xs.Length ||
            yLabels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ys.Length)
            throw new CadException("AXIS_LABEL", "Mỗi trục cần ký hiệu riêng, từ 1 đến 8 ký tự.");
        if (scale < 1 || scale > 1000 || !IsFinite(origin.X) || !IsFinite(origin.Y) || !IsFinite(origin.Z) || Math.Abs(origin.Z) > 1e-6)
            throw new CadException("AXIS_GRID", "Gốc lưới phải nằm trên WCS XY và tỷ lệ từ 1 đến 1000.");
        var head = template.Blocks.FirstOrDefault(b => b.Id == "QA_ATT_TRUC" && b.Usage?.Role == "axisHead" &&
            b.Usage.Reviewed && b.Usage.Layer == HeadLayer && !string.IsNullOrWhiteSpace(b.NativeSource?.LabelAttribute))
            ?? throw new CadException("AXIS_BLOCK_REQUIRED", "Template Quang Anh cần block ATT trục QA_ATT_TRUC đã duyệt.");
        var layers = template.Layers.Where(l => l.Name == AxisLayer || l.Name == HeadLayer || l.Name == DimLayer).ToList();
        if (layers.Count != 3)
            throw new CadException("AXIS_LAYERS_REQUIRED", "Template Quang Anh cần layer đường trục, ký hiệu trục và QA_KT_Dim.");
        double width = xs.Last(), height = ys.Last();
        double extension = 22.0 * scale;
        double pad = extension + 30.0 * scale;
        var plan = new DrawingPlan
        {
            Template = new TemplateProfile
            {
                Id = template.Id, Version = template.Version, Name = template.Name, Units = template.Units,
                Font = template.Font, TextHeight = 2.0 * scale, ArrowSize = 0.8 * scale,
                Layers = layers, Blocks = new() { head }
            },
            Scope = new()
            {
                new(origin.X - pad, origin.Y - pad), new(origin.X + width + pad, origin.Y - pad),
                new(origin.X + width + pad, origin.Y + height + pad), new(origin.X - pad, origin.Y + height + pad)
            }
        };
        Point2 Point(double x, double y) => new(origin.X + x, origin.Y + y);
        void Axis(Point2 first, Point2 second, string label)
        {
            plan.Operations.Add(new Operation { Kind = "draw_line", Layer = AxisLayer, Points = new() { first, second } });
            foreach (var point in new[] { first, second })
                plan.Operations.Add(new Operation { Kind = "insert_block", Layer = HeadLayer,
                    AssetId = head.Id, SymbolScale = scale, AttributeText = label, Points = new() { point } });
        }
        for (int i = 0; i < xs.Length; i++) Axis(Point(xs[i], -extension), Point(xs[i], height + extension), xLabels[i]);
        for (int i = 0; i < ys.Length; i++) Axis(Point(-extension, ys[i]), Point(width + extension, ys[i]), yLabels[i]);
        void Dim(Point2 first, Point2 second, Point2 line) => plan.Operations.Add(new Operation
        {
            Kind = "add_dimension", Layer = DimLayer, Points = new() { first, second, line }
        });
        // Dimension origins stay at the grid intersections. Heads extend beyond both dimension rows.
        foreach (int side in new[] { -1, 1 })
        {
            double edgeY = side < 0 ? 0 : height;
            double edgeX = side < 0 ? 0 : width;
            for (int i = 0; i < xBays.Length; i++)
                Dim(Point(xs[i], edgeY), Point(xs[i + 1], edgeY), Point(xs[i], edgeY + side * 8.0 * scale));
            for (int i = 0; i < yBays.Length; i++)
                Dim(Point(edgeX, ys[i]), Point(edgeX, ys[i + 1]), Point(edgeX + side * 8.0 * scale, ys[i]));
            Dim(Point(0, edgeY), Point(width, edgeY), Point(0, edgeY + side * 14.0 * scale));
            Dim(Point(edgeX, 0), Point(edgeX, height), Point(edgeX + side * 14.0 * scale, 0));
        }
        return plan;
    }

    static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
