using System;
using System.Linq;
using AICad.Contracts;
using AICad.Bridge;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Rainmaker.Axis.RainmakerAxis))]

namespace Rainmaker.Axis;

public sealed class RainmakerAxis
{
    const string AxisLayer = "QA_KT_Truc";
    const string HeadLayer = "QA_KH_Truc Dinh Vi";

    [CommandMethod(CadToolCatalog.AxisCommand, CommandFlags.Modal)]
    public static void Run()
    {
        var plugin = Plugin.Current ??
            throw new CadException("PLUGIN_OFFLINE", "Mở AutoCAD với plugin AI CAD trước khi chạy Tạo trục.");
        var catalog = new CadToolCatalog(plugin.Config.ContentRoot);
        var package = catalog.Read().Items.SingleOrDefault(tool => tool.Id == CadToolCatalog.AxisToolId);
        if (package != null && (!package.Enabled || package.DeletedUtc.HasValue))
            throw new CadException("TOOL_DISABLED", "Công cụ Tạo trục đang tắt hoặc nằm trong thùng rác.");
        var doc = AcApp.DocumentManager.MdiActiveDocument ??
            throw new CadException("DOCUMENT_REQUIRED", "Mở DWG trước khi tạo trục.");
        if (!doc.Database.TileMode || doc.Database.Insunits != UnitsValue.Millimeters)
            throw new CadException("WRONG_CONTEXT", "Mở Model và đặt đơn vị bản vẽ là mm.");
        NativeWriteGuard.Check(doc);
        var editor = doc.Editor;
        var first = editor.GetPoint("\nChọn đầu thứ nhất của trục: ");
        if (first.Status != PromptStatus.OK) return;
        var second = editor.GetPoint(new PromptPointOptions("\nChọn đầu thứ hai của trục: ")
        {
            UseBasePoint = true,
            BasePoint = first.Value
        });
        if (second.Status != PromptStatus.OK) return;
        var name = editor.GetString(new PromptStringOptions("\nKý hiệu trục (ví dụ A hoặc 1): ") { AllowSpaces = false });
        if (name.Status != PromptStatus.OK) return;
        double current = doc.Database.Cannoscale.DrawingUnits / doc.Database.Cannoscale.PaperUnits;
        int defaultScale = current >= 2 && current <= 1000 ? (int)Math.Round(current) : 60;
        var scale = editor.GetInteger(new PromptIntegerOptions($"\nMẫu số tỷ lệ <{defaultScale}>: ")
        {
            AllowNone = true,
            UseDefaultValue = true,
            DefaultValue = defaultScale,
            LowerLimit = 1,
            UpperLimit = 1000
        });
        if (scale.Status != PromptStatus.OK && scale.Status != PromptStatus.None) return;
        int denominator = scale.Status == PromptStatus.None ? defaultScale : scale.Value;
        Write(doc.Database, new ContentStore(plugin.Config.ContentRoot).Template("ISO_QUANGANH"),
            first.Value, second.Value, name.StringResult.Trim(), denominator);
        editor.WriteMessage("\nRainmaker: Đã tạo trục " + name.StringResult.Trim() + ".");
    }

    internal static void Write(Database db, TemplateProfile template, Point3d first, Point3d second, string label, int scale)
    {
        if (label.Length < 1 || label.Length > 8 || label.Any(char.IsControl))
            throw new CadException("AXIS_LABEL", "Nhập ký hiệu trục từ 1 đến 8 ký tự.");
        if (Math.Abs(first.Z) > 1e-6 || Math.Abs(second.Z) > 1e-6 || first.DistanceTo(second) < 1e-6)
            throw new CadException("AXIS_POINTS", "Hai điểm phải khác nhau và nằm trên mặt phẳng WCS XY.");
        var head = template.Blocks.FirstOrDefault(b => b.Id == "QA_ATT_TRUC" && b.Usage?.Role == "axisHead" &&
            b.Usage.Reviewed && b.Usage.Layer == HeadLayer && !string.IsNullOrWhiteSpace(b.NativeSource?.LabelAttribute))
            ?? throw new CadException("AXIS_BLOCK_REQUIRED", "Template Quang Anh cần block ATT trục QA_ATT_TRUC đã duyệt.");
        var layers = template.Layers.Where(l => l.Name == AxisLayer || l.Name == HeadLayer).ToList();
        if (layers.Count != 2)
            throw new CadException("AXIS_LAYERS_REQUIRED", "Template Quang Anh thiếu layer đường trục hoặc ký hiệu trục.");

        var a = new Point2(first.X, first.Y);
        var b = new Point2(second.X, second.Y);
        double pad = scale * 30.0;
        double left = Math.Min(a.X, b.X) - pad, right = Math.Max(a.X, b.X) + pad;
        double bottom = Math.Min(a.Y, b.Y) - pad, top = Math.Max(a.Y, b.Y) + pad;
        var plan = new DrawingPlan
        {
            Template = new TemplateProfile
            {
                Id = template.Id, Version = template.Version, Name = template.Name, Units = template.Units,
                Font = template.Font, TextHeight = template.TextHeight, ArrowSize = template.ArrowSize,
                Layers = layers, Blocks = new() { head }
            },
            Scope = new() { new(left, bottom), new(right, bottom), new(right, top), new(left, top) },
            Operations = new() { new Operation { Kind = "draw_line", Layer = AxisLayer, Points = new() { a, b } } }
        };
        foreach (var point in new[] { a, b })
            plan.Operations.Add(new Operation { Kind = "insert_block", Layer = HeadLayer,
                AssetId = head.Id, SymbolScale = scale, AttributeText = label, Points = new() { point } });

        NativeLibraryAssets.Demand(plan);
        using var tx = db.TransactionManager.StartTransaction();
        EntityFactory.WritePlan(plan, db, tx);
        tx.Commit();
    }
}
