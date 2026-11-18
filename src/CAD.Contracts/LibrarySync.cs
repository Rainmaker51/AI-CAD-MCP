using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AICad.Contracts;

public sealed class LibrarySyncItem
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Group { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Handle { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public bool Draft { get; set; }
    public bool GroupEditedInCad { get; set; }
    public JObject Values { get; set; } = new();
    public string Signature() => Json.Hash(new { Kind, Name, Group, Purpose, Notes, Values });
    public string ContentSignature() => Json.Hash(new { Kind, Name, Purpose, Notes, Values });
    public LibrarySyncItem Copy() => Json.Read<LibrarySyncItem>(Json.Write(this));
}

public sealed class LibrarySyncBaseline
{
    public string App { get; set; } = "";
    public string Cad { get; set; } = "";
    public string AppContent { get; set; } = "";
    public string CadContent { get; set; } = "";
    public string? CadGroup { get; set; }
    public string? AppGroup { get; set; }
}

public sealed class LibrarySyncState
{
    public int Revision { get; set; }
    public List<LibrarySyncItem> Items { get; set; } = new();
    public Dictionary<string, LibrarySyncBaseline> Baselines { get; set; } = new();
}

public sealed class LibrarySyncDifference
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string Suggested { get; set; } = "skip";
    public bool GroupDifferent { get; set; }
    public bool CadGroupChanged { get; set; }
    public LibrarySyncItem? App { get; set; }
    public LibrarySyncItem? Cad { get; set; }
}

public static class LibrarySyncRules
{
    public static readonly string[] Kinds = { "blocks", "layers", "hatches", "texts", "dims" };
    public static void ValidateRemoval(LibrarySyncDifference change)
    {
        if (change.App == null || change.Cad != null)
            throw new CadException("SYNC_REMOVE", "Chỉ xóa khỏi app khi tài nguyên đã thiếu trong DWG. Đọc và so sánh lại trước khi xóa.");
    }
    public static List<LibrarySyncDifference> Compare(LibrarySyncState state, IEnumerable<LibrarySyncItem> native)
    {
        var app = state.Items.ToDictionary(i => i.Id);
        var cad = native.ToDictionary(i => i.Id);
        return app.Keys.Union(cad.Keys).Select(id =>
        {
            app.TryGetValue(id, out var a);
            cad.TryGetValue(id, out var c);
            state.Baselines.TryGetValue(id, out var baseline);
            bool cadGroupChanged = a != null && c != null && a.Group != c.Group && c.GroupEditedInCad && (baseline?.CadGroup == null || c.Group != baseline.CadGroup);
            string status;
            if (a == null) status = baseline == null ? "CAD_NEW" : "APP_MISSING";
            else if (c == null) status = baseline == null ? "APP_NEW" : "CAD_MISSING";
            else if (a.ContentSignature() == c.ContentSignature()) status = cadGroupChanged ? baseline?.AppGroup != null && a.Group != baseline.AppGroup ? "CONFLICT" : "CAD_GROUP_CHANGED" : "MATCH";
            else if (baseline == null) status = "CONFLICT";
            else
            {
                bool appChanged = baseline.AppContent != "" ? a.ContentSignature() != baseline.AppContent : a.Signature() != baseline.App;
                bool cadChanged = baseline.CadContent != "" ? c.ContentSignature() != baseline.CadContent : c.Signature() != baseline.Cad;
                status = appChanged && !cadChanged ? "APP_CHANGED" : cadChanged && !appChanged ? "CAD_CHANGED" : "CONFLICT";
            }
            return new LibrarySyncDifference
            {
                Id = id, App = a, Cad = c, Status = status, GroupDifferent = a != null && c != null && a.Group != c.Group, CadGroupChanged = cadGroupChanged,
                Suggested = status == "APP_NEW" || status == "APP_CHANGED" ? "app" : status == "CAD_NEW" || status == "CAD_CHANGED" || status == "CAD_GROUP_CHANGED" ? "cad" : "skip"
            };
        }).OrderBy(d => (d.App ?? d.Cad)!.Kind).ThenBy(d => (d.App ?? d.Cad)!.Name).ToList();
    }

    public static void Validate(LibrarySyncItem item)
    {
        Json.Id(item.Id);
        if (!Kinds.Contains(item.Kind)) throw new CadException("SYNC_KIND", "Loại tài nguyên không hợp lệ.");
        Json.LayerName(item.Name);
        if (item.Group.Length > 120 || item.Purpose.Length > 500 || item.Notes.Length > 2000)
            throw new CadException("SYNC_TEXT", "Nhóm tối đa 120, công dụng 500, ghi chú 2.000 ký tự.");
        foreach (var property in item.Values.Properties())
            if (property.Value.Type == JTokenType.Float && (double.IsNaN(property.Value.Value<double>()) || double.IsInfinity(property.Value.Value<double>())))
                throw new CadException("SYNC_VALUE", "Thông số phải là số hữu hạn.");
        if (item.Kind == "layers")
        {
            int color = item.Values.Value<int?>("color") ?? -1;
            int weight = item.Values.Value<int?>("lineWeight") ?? -99;
            if (color < 1 || color > 255 || !new[] { -3, 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 }.Contains(weight))
                throw new CadException("SYNC_LAYER", "Màu ACI 1–255 và độ dày nét chuẩn AutoCAD là bắt buộc.");
            Json.LayerName(item.Values.Value<string>("lineType") ?? "");
        }
        if (item.Kind == "hatches" && ((item.Values.Value<double?>("scale") ?? 0) <= 0 || (item.Values.Value<double?>("scale") ?? 0) > 1000000))
            throw new CadException("SYNC_HATCH", "Scale hatch phải lớn hơn 0 và tối đa 1.000.000.");
        if (item.Kind == "texts")
        {
            string font = item.Values.Value<string>("font") ?? "";
            if (font != System.IO.Path.GetFileName(font) || !new[] { ".ttf", ".shx", ".otf" }.Contains(System.IO.Path.GetExtension(font).ToLowerInvariant()))
                throw new CadException("SYNC_FONT", "Nhập tên file font TTF, SHX hoặc OTF.");
            if ((item.Values.Value<double?>("height") ?? 0) < 0 || (item.Values.Value<double?>("widthFactor") ?? 0) <= 0)
                throw new CadException("SYNC_TEXT_STYLE", "Chiều cao từ 0, hệ số rộng phải lớn hơn 0.");
        }
        if (item.Kind == "dims" && ((item.Values.Value<double?>("textHeight") ?? 0) <= 0 || (item.Values.Value<double?>("scale") ?? 0) < 0))
            throw new CadException("SYNC_DIM", "Chiều cao chữ dim phải lớn hơn 0, tỷ lệ từ 0.");
    }
}
