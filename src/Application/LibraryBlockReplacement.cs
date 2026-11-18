using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

public sealed partial class LibrarySyncService
{
    (LibrarySyncState State, JObject Review, LibrarySyncItem Old, List<LibrarySyncItem> Native) ReplacementContext(JObject request)
    {
        var review = Json.Read<JObject>(File.ReadAllText(Path.Combine(Job(request.Value<string>("id") ?? ""), "review.json")));
        if (review.Value<string>("sourceHash") != CadSourceFile.Hash(Source) || review.Value<string>("appHash") != AppHash())
            throw new CadException("RESOURCE_CHANGED", "Kho hoặc DWG đã đổi. Đọc và so sánh lại trước khi thay thế.");
        var state = ReadState();
        var old = state.Items.SingleOrDefault(i => i.Id == request.Value<string>("sourceId") && i.Kind == "blocks")
            ?? throw new CadException("REPLACEMENT_SOURCE", "Không tìm thấy block cũ trong danh mục.");
        var native = review["native"]!.ToObject<List<LibrarySyncItem>>()!;
        if (native.Any(i => i.Id == old.Id)) throw new CadException("REPLACEMENT_SOURCE", "Chỉ thay thế và loại khỏi app khi block cũ đã thiếu trong DWG.");
        return (state, review, old, native);
    }

    List<SkillDefinition> ReplacementSkills() => Store.Ids("skills")
        .Where(id => !WarehousePaths.IsRetired(config.ContentRoot, "skills", id) && !Catalog.Deleted("skills", id))
        .Select(id => Store.Skill(id)).ToList();

    public object ReplacementOptions(JObject request)
    {
        using var lease = Lease(); Recover();
        var context = ReplacementContext(request); var template = Store.Template("ISO_QUANGANH");
        var differences = LibrarySyncRules.Compare(context.State, context.Native);
        var targets = template.Blocks.Where(b => b.NativeSource != null && b.NativeSource.BlockName != context.Old.Name)
            .Where(b => differences.Any(d => d.Status == "MATCH" && d.App?.Kind == "blocks" && d.App.Name == b.NativeSource!.BlockName))
            .Select(b => new { id = b.Id, name = b.NativeSource!.BlockName, role = b.Usage?.Role ?? "", layer = b.Usage?.Layer ?? "", reviewed = b.Usage?.Reviewed == true }).ToList();
        return new { source = context.Old, targets };
    }

    (BlockReplacementChange Change, string Fingerprint, LibrarySyncState State, LibrarySyncItem Old, List<string> OldIds) BuildReplacement(JObject request)
    {
        var context = ReplacementContext(request); var template = Store.Template("ISO_QUANGANH");
        string targetId = request.Value<string>("targetId") ?? "";
        var target = template.Blocks.SingleOrDefault(b => b.Id == targetId)
            ?? throw new CadException("REPLACEMENT_TARGET", "Chọn block thay thế.");
        if (!LibrarySyncRules.Compare(context.State, context.Native).Any(d => d.Status == "MATCH" && d.App?.Kind == "blocks" && d.App.Name == target.NativeSource?.BlockName))
            throw new CadException("REPLACEMENT_TARGET", "Block thay thế phải đã đồng bộ với DWG. Đồng bộ block mới trước.");
        string asset = Path.Combine(WarehousePaths.Folder(config.ContentRoot, "content-assets"), target.NativeSource!.FileHash + ".dwg");
        if (!File.Exists(asset) || Json.FileHash(asset) != target.NativeSource.FileHash)
            throw new CadException("REPLACEMENT_TARGET", "File DWG nguồn block thay thế thiếu hoặc đã thay đổi.");
        var skills = ReplacementSkills();
        var oldIds = template.Blocks.Where(b => b.NativeSource?.BlockName == context.Old.Name).Select(b => b.Id).ToList();
        var change = BlockReplacement.Build(template, skills, context.Old.Name, targetId,
            request["parameterMap"]?.ToObject<Dictionary<string,string>>() ?? new(),
            request.Value<double?>("offsetX") ?? 0, request.Value<double?>("offsetY") ?? 0, request.Value<double?>("rotation") ?? 0);
        string VersionFor(string kind, string id, string current)
        {
            string next = Next(current); while (Store.Versions(kind, id).Contains(next)) next = Next(next); return next;
        }
        change.Template.Version = VersionFor("templates", template.Id, template.Version);
        foreach (var skill in change.Skills) skill.Version = VersionFor("skills", skill.Id, skill.Version);
        string fingerprint = Json.Hash(new { app = AppHash(), skills, source = context.Review.Value<string>("sourceHash"), request, change.Template, change.Skills });
        return (change, fingerprint, context.State, context.Old, oldIds);
    }

    public object PreviewReplacement(JObject request)
    {
        using var lease = Lease(); Recover();
        var built = BuildReplacement(request);
        var oldBlocks = Store.Template("ISO_QUANGANH").Blocks.Where(b => built.OldIds.Contains(b.Id));
        return new { fingerprint = built.Fingerprint, errors = built.Change.Errors, references = built.Change.References,
            oldBlocks, target = built.Change.Template.Blocks.Single(b => b.Id == request.Value<string>("targetId")),
            skills = built.Change.Skills.Select(s => new { s.Id, s.Name, s.Version }), templateVersion = built.Change.Template.Version,
            workflow = built.Change.Skills.Select(s => new { s.Id, operations = s.Workflow.Where(o => o.AssetId == request.Value<string>("targetId")) }) };
    }

    public object ApplyReplacement(JObject envelope)
    {
        if (!gate.Wait(0)) throw new CadException("SYNC_BUSY", "Đang đồng bộ thư viện.");
        try
        {
            using var lease = Lease(); Recover();
            if (envelope.Value<bool?>("confirmed") != true) throw new CadException("REPLACEMENT_APPROVAL", "Kiểm tra bảng thay đổi rồi xác nhận thay thế.");
            var request = envelope["request"] as JObject ?? throw new CadException("REPLACEMENT_INPUT", "Thiếu bản đặc tả thay thế.");
            var built = BuildReplacement(request);
            if (built.Fingerprint != envelope.Value<string>("fingerprint")) throw new CadException("RESOURCE_CHANGED", "Cấu hình đã đổi. Xem lại bảng thay đổi trước khi áp dụng.");
            if (built.Change.Errors.Count > 0) throw new CadException("REPLACEMENT_CONFIGURATION", string.Join("\n", built.Change.Errors));
            if (new CadClient(config).Sessions().Any(s => !Version.TryParse(s.PluginVersion, out var v) || v < new Version(0,7,19)))
                throw new CadException("PLUGIN_UPDATE_REQUIRED", "Lưu và mở lại các phiên AutoCAD bằng bản plugin 0.7.19 trước khi thay thế để chặn duyệt kế hoạch dùng block cũ.");
            string backup = Path.Combine(Warehouse, "CAD", "Backups", "replacement-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            string policy = Path.Combine(Warehouse, "Catalog", "block-replacements.json");
            var saved = new List<object>();
            var paths = new List<string> { StateFile, policy, Path.Combine(WarehousePaths.Folder(config.ContentRoot, "packages"), "templates", "ISO_QUANGANH", "active.json") };
            paths.AddRange(built.Change.Skills.Select(s => Path.Combine(WarehousePaths.Folder(config.ContentRoot, "packages"), "skills", s.Id, "active.json")));
            foreach (string path in paths)
            {
                string relative = Path.GetRelativePath(Warehouse, path), copy = Path.Combine(backup, relative);
                if (File.Exists(path)) { Directory.CreateDirectory(Path.GetDirectoryName(copy)!); File.Copy(path, copy); }
                saved.Add(new { path = relative, backup = Path.GetRelativePath(Warehouse, copy), exists = File.Exists(path) });
            }
            string journal = Path.Combine(Root, "pending.json");
            Json.Atomic(journal, new { files = saved, status = "PREPARED" });
            try
            {
                Json.Atomic(Path.Combine(backup, "replacement.json"), new { request, built.Fingerprint, built.Change.References, versions = built.Change.Skills.Select(s => new { s.Id, s.Version }) });
                var policies = File.Exists(policy) ? Json.Read<JArray>(File.ReadAllText(policy)) : new JArray();
                policies.Add(JObject.FromObject(new { templateId = "ISO_QUANGANH", throughVersion = Store.Active("templates", "ISO_QUANGANH"), oldIds = built.OldIds,
                    targetId = request.Value<string>("targetId"), fingerprint = built.Fingerprint, utc = DateTime.UtcNow }));
                Json.Atomic(policy, policies);
                Store.Publish("templates", "ISO_QUANGANH", built.Change.Template.Version, built.Change.Template);
                foreach (var skill in built.Change.Skills) Store.Publish("skills", skill.Id, skill.Version, skill);
                built.State.Items.RemoveAll(i => i.Id == built.Old.Id); built.State.Baselines.Remove(built.Old.Id); built.State.Revision++;
                Json.Atomic(StateFile, built.State);
                Json.Atomic(journal, new { files = saved, status = "COMMITTED" }); File.Delete(journal);
                return new { status = "REPLACED", skills = built.Change.Skills.Count, backup, sourceUpdated = false };
            }
            catch { Recover(); throw; }
        }
        finally { gate.Release(); }
    }
}
