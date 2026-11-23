using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

// The native QA browser and the web reader share the same published resources.
public sealed class QaLibraryService(InstallConfig config, LibraryRepository sync, LibraryIdentifierService identifiers)
{
    LibraryCatalog Catalog => new(config.ContentRoot);
    string AuthorityFile => Path.Combine(WarehousePaths.Resolve(config.ContentRoot), "Catalog", "library-authority.json");
    public JObject ReadLive(string kind,JObject capture)=>AICad.Library.LibraryLiveProjection.Build(JObject.FromObject(Read(kind)),capture);
    public object Authority() => new { editor = File.Exists(AuthorityFile) ? "QA" : "APP", protocol = 2 };
    public object Activate()
    {
        // Called only by the replacement UI after a successful state load. Merely installing
        // a new Host must not disable an older QA installation on the other machine.
        _ = Read("blocks");
        Json.Atomic(AuthorityFile, new { editor = "QA", protocol = 2, activatedUtc = DateTime.UtcNow });
        return Authority();
    }
    public object Read(string kind)
    {
        if (!LibrarySyncRules.Kinds.Contains(kind)) throw new CadException("INVALID_KIND", kind);
        var state = JObject.FromObject(sync.State());
        var saved = state["items"]!.ToObject<List<LibrarySyncItem>>()!;
        var codes = JObject.FromObject(identifiers.Ensure())["items"]!.ToObject<List<LibraryIdentifierItem>>()!;
        var template = new ContentStore(config.ContentRoot).Template("ISO_QUANGANH");
        var groups = Catalog.Groups(kind);
        var rows = new JArray();
        string version=template.Version;
        void Add(string resourceId, string name, JObject profile, string handle = "")
        {
            var code = codes.SingleOrDefault(c => c.Kind == kind && c.ResourceId == resourceId);
            var item = saved.SingleOrDefault(i => i.Kind == kind && code != null && i.Id == code.Id);
            string metadataId = kind is "blocks" or "layers" ? Json.Hash(new[] { template.Id, resourceId }) : resourceId;
            var label = Catalog.Label(kind, metadataId);
            rows.Add(new JObject {
                ["resourceId"] = resourceId, ["metadataId"] = metadataId,
                ["id"] = code?.Id ?? "", ["code"] = code?.Code ?? "", ["name"] = name,
                ["displayName"] = label.Name == "" ? name : label.Name,
                ["management"] = JObject.FromObject(label), ["profile"] = profile,
                ["item"] = item == null ? null : JObject.FromObject(item), ["handle"] = handle,
                ["image"] = kind == "blocks" ? "/api/block-previews/ISO_QUANGANH/" + Uri.EscapeDataString(resourceId) + "/image" :
                    kind == "hatches" ? "/api/hatch-library/" + Uri.EscapeDataString(resourceId) + "/image" : ""
            });
        }
        if (kind == "blocks") foreach (var b in template.Blocks) Add(b.Id, b.NativeSource?.BlockName ?? b.Id, JObject.FromObject(b));
        else if (kind == "layers") foreach (var l in template.Layers) Add(l.Name, l.Name, JObject.FromObject(l));
        else
        {
            var raw = kind == "hatches" ? sync.CatalogView("hatches") : sync.CatalogView(kind);
            version=raw.Value<string>("version")??"";
            foreach (var e in raw["items"]!.OfType<JObject>()) Add(e.Value<string>("id")!, e.Value<string>("name") ?? e.Value<string>("pattern") ?? "", (JObject)e.DeepClone(), e.Value<string>("handle") ?? "");
        }
        return new { protocol = 1, kind, revision = state.Value<int>("revision"), source = Path.Combine(WarehousePaths.Resolve(config.ContentRoot),"CAD","Originals",CadSourceFile.BaseLibraryName), version, groups, items = rows, authority = Authority() };
    }
    public object Manage(JObject request)
    {
        string kind = request.Value<string>("kind") ?? "";
        var rows = JObject.FromObject(Read(kind))["items"]!.OfType<JObject>().ToList();
        string id = request.Value<string>("metadataId") ?? "";
        if (!rows.Any(r => r.Value<string>("metadataId") == id)) throw new CadException("NOT_FOUND", "Tài nguyên không còn trong phiên bản hiện tại.");
        // The management record has its own revision; a geometry publication also invalidates
        // the caller's view, including block/layer renames.
        return sync.UpdateQaLabel(request, kind, id, rows.Single(r=>r.Value<string>("metadataId")==id).Value<string>("id")??"");
    }
    public object Group(JObject request)
    {
        string kind = request.Value<string>("kind") ?? "";
        if (!LibrarySyncRules.Kinds.Contains(kind)) throw new CadException("INVALID_KIND", kind);
        if (request.Value<string>("action") == "remove") { Catalog.RemoveGroup(kind, request.Value<string>("id") ?? ""); return new { status = "REMOVED" }; }
        return Catalog.Group(kind, request.Value<string>("id") ?? "", request.Value<string>("name") ?? "", request.Value<string>("parentId"));
    }
}
