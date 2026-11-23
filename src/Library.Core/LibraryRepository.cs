using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

public partial class LibraryRepository
{
    protected readonly InstallConfig config;
    public LibraryRepository(InstallConfig config){this.config=config;}
    protected readonly SemaphoreSlim gate = new(1, 1);
    protected string Warehouse => WarehousePaths.Resolve(config.ContentRoot);
    protected string Root => Path.Combine(Warehouse, "Catalog", "sync");
    protected string StateFile => Path.Combine(Root, "state.json");
    protected string Jobs => Path.Combine(config.DataRoot, "library-sync");
    protected string Job(string id) => Path.Combine(Jobs, Json.Id(id));
    protected string Source => CadSourceFile.BaseLibraryPath(config.ContentRoot);
    protected ContentStore Store => new(config.ContentRoot);
    protected LibraryCatalog Catalog => new(config.ContentRoot);

    protected static string Next(string version)
    {
        var v = Version.Parse(version);
        return $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build) + 1}";
    }

    protected FileStream Lease()
    {
        Directory.CreateDirectory(Root);
        try { return new FileStream(Path.Combine(Root, "write.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new CadException("SYNC_BUSY", "Kho đang được đồng bộ từ một phiên khác. Chờ hoàn tất rồi thử lại."); }
    }

    protected JObject Raw(string kind)
    {
        string root = Path.Combine(Warehouse, kind, "ISO_QUANGANH"), pointer = Path.Combine(root, "active.json");
        if (!File.Exists(pointer)) return new JObject { [kind == "Hatches" ? "items" : "texts"] = new JArray(), ["dims"] = new JArray() };
        var active = Json.Read<JObject>(File.ReadAllText(pointer));
        string file = Path.Combine(root, ContentStore.VersionName(active.Value<string>("version")!), "catalog.json");
        if (Json.FileHash(file) != active.Value<string>("checksum")) throw new CadException("RESOURCE_CHANGED", "Danh mục thư viện đã thay đổi bên ngoài app.");
        return Json.Read<JObject>(File.ReadAllText(file));
    }

    protected List<LibrarySyncItem> Hints()
    {
        var template = Store.Template("ISO_QUANGANH");
        var result = new List<LibrarySyncItem>();
        foreach (var layer in template.Layers)
        {
            var meta = Catalog.Label("layers", Json.Hash(new[] { template.Id, layer.Name }));
            result.Add(new LibrarySyncItem { Id = Json.Hash(new[] { "layers", layer.Name }).Substring(0, 32), Kind = "layers", Name = layer.Name,
                Group = Group("layers", meta.GroupId), Purpose = meta.Purpose, Notes = meta.Notes });
        }
        foreach (var group in template.Blocks.Where(b => b.NativeSource != null).GroupBy(b => b.NativeSource!.BlockName))
        {
            var b = group.First(); var meta = Catalog.Label("blocks", Json.Hash(new[] { template.Id, b.Id }));
            result.Add(new LibrarySyncItem { Id = Json.Hash(new[] { "blocks", group.Key }).Substring(0, 32), Kind = "blocks", Name = group.Key,
                Group = Group("blocks", meta.GroupId), Purpose = meta.Purpose, Notes = meta.Notes });
        }
        foreach (var hatch in (Raw("Hatches")["items"] as JArray ?? new()).OfType<JObject>())
        {
            var meta = Catalog.Label("hatches", hatch.Value<string>("id")!);
            if (meta.DeletedUtc.HasValue) continue;
            var settings = new JObject();
            foreach (string key in new[] { "pattern", "type", "scale", "angle", "layer", "color", "spacing", "doublePattern" }) if (hatch[key] != null) settings[key] = hatch[key]!.DeepClone();
            result.Add(new LibrarySyncItem { Id = hatch.Value<string>("id")!.Substring(0, 32), Kind = "hatches", Name = string.IsNullOrEmpty(meta.Name) ? hatch.Value<string>("pattern")! + " · " + hatch["scale"] + " · " + Math.Round(hatch.Value<double>("angle") * 180 / Math.PI, 2) + "°" : meta.Name,
                Handle = hatch.Value<string>("handle") ?? "", Values = settings, Group = Group("hatches", meta.GroupId), Purpose = meta.Purpose, Notes = meta.Notes });
        }
        var styles = Raw("Styles");
        foreach (string kind in new[] { "texts", "dims" }) foreach (var style in (styles[kind] as JArray ?? new()).OfType<JObject>())
        {
            var meta = Catalog.Label(kind, style.Value<string>("id")!);
            if (meta.DeletedUtc.HasValue) continue;
            result.Add(new LibrarySyncItem { Id = Json.Hash(new[] { kind, style.Value<string>("name")! }).Substring(0, 32), Kind = kind,
                Name = style.Value<string>("name")!, Group = Group(kind, meta.GroupId), Purpose = meta.Purpose, Notes = meta.Notes });
        }
        return result;
    }

    protected string Group(string kind, string id)
    {
        var groups=Catalog.Groups(kind).ToDictionary(g=>g.Id);var path=new List<string>();var visited=new HashSet<string>();
        while(id!=""&&groups.TryGetValue(id,out var group)&&visited.Add(id)){path.Insert(0,group.Name);id=group.ParentId;}
        return string.Join(" / ",path);
    }

    protected LibrarySyncState ReadState()
    {
        var state=File.Exists(StateFile)?Json.Read<LibrarySyncState>(File.ReadAllText(StateFile)):new();
        if(state.Items.Count==0)return state;
        // Upgrade legacy baselines only when the saved snapshot proves its original signature.
        foreach(var item in state.Items)
        {
            if(!state.Baselines.TryGetValue(item.Id,out var baseline))continue;
            string signature=item.Signature();
            if(baseline.AppContent==""&&signature==baseline.App)baseline.AppContent=item.ContentSignature();
            if(baseline.CadContent==""&&signature==baseline.Cad)baseline.CadContent=item.ContentSignature();
        }
        var template=Store.Template("ISO_QUANGANH");var styles=Raw("Styles");var hatches=Raw("Hatches")["items"] as JArray??new();
        foreach(var item in state.Items.Where(i=>!i.Draft))
        {
            string? metadataId=null;
            if(item.Kind=="layers")
            {
                var layer=template.Layers.FirstOrDefault(l=>l.Name==item.Name);
                if(layer!=null&&layer.Appearance!=null)
                {
                    item.Values["color"]=layer.Color;item.Values["lineWeight"]=layer.Appearance.LineWeight;item.Values["lineType"]=layer.Appearance.LineType;item.Values["plottable"]=layer.Appearance.Plottable;
                    item.Values["rgb"]=layer.Appearance.Rgb.HasValue?new JValue(layer.Appearance.Rgb.Value):JValue.CreateNull();
                }
                metadataId=Json.Hash(new[]{template.Id,item.Name});
            }
            else if(item.Kind=="blocks")
            {
                var block=template.Blocks.FirstOrDefault(b=>b.NativeSource?.BlockName==item.Name);
                if(block!=null)metadataId=Json.Hash(new[]{template.Id,block.Id});
            }
            else
            {
                var entries=item.Kind=="hatches"?hatches:styles[item.Kind] as JArray??new();
                var entry=entries.OfType<JObject>().FirstOrDefault(e=>item.Kind=="hatches"?e.Value<string>("handle")==item.Handle||e.Value<string>("id")?.StartsWith(item.Id)==true:e.Value<string>("name")==item.Name);
                if(entry!=null)
                {
                    foreach(string key in item.Values.Properties().Select(p=>p.Name).ToArray())if(entry[key]!=null)item.Values[key]=entry[key]!.DeepClone();
                    metadataId=entry.Value<string>("id");
                }
            }
            if(metadataId!=null)
            {
                var metadata=Catalog.Label(item.Kind,metadataId);item.Group=Group(item.Kind,metadata.GroupId);item.Purpose=metadata.Purpose;item.Notes=metadata.Notes;
                if(item.Kind=="hatches"&&metadata.Name!="")item.Name=metadata.Name;
            }
        }
        return state;
    }

    protected string AppHash()
    {
        string management = Path.Combine(Warehouse, "Catalog", "management");
        return Json.Hash(new
        {
            template = Store.Template("ISO_QUANGANH"), hatches = Raw("Hatches"), styles = Raw("Styles"), state = ReadState(),
            metadata = Directory.Exists(management) ? Directory.GetFiles(management, "*.json", SearchOption.AllDirectories).OrderBy(p => p).Select(p => new { name = RelativePath(management, p), hash = Json.FileHash(p) }).ToArray() : Array.Empty<object>()
        });
    }

    public object State()
    {
        using var lease = Lease();
        Recover();
        var state = ReadState();
        return new { revision = state.Revision, source = Source, items = state.Items, initialized = state.Baselines.Count > 0 };
    }

    protected object Commit(string job, LibrarySyncState state, List<LibrarySyncItem> desired, List<LibrarySyncItem> native, HashSet<string> accepted, string synced, List<LibrarySyncItem> removed, bool updateSource = true,int missingSamples=0)
    {
        string hash = Json.FileHash(synced), backup = Path.Combine(Warehouse, "CAD", "Backups", "sync-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var pointers = new[] { StateFile, Path.Combine(Warehouse,"Versions","templates","ISO_QUANGANH","active.json"), Path.Combine(Warehouse,"Hatches","ISO_QUANGANH","active.json"), Path.Combine(Warehouse,"Styles","ISO_QUANGANH","active.json") };
        var saved = new List<object>();
        var tracked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string journal = Path.Combine(Root, "pending.json");
        void Track(string path)
        {
            if (!tracked.Add(path)) return;
            string relative = RelativePath(Warehouse, path), copy = Path.Combine(backup, relative);
            bool exists = File.Exists(path); if (exists) { Directory.CreateDirectory(Path.GetDirectoryName(copy)!); File.Copy(path, copy, false); }
            saved.Add(new { path = relative, backup = RelativePath(Warehouse, copy), exists });
            Json.Atomic(journal, new { files = saved, status = "PREPARED" });
        }
        foreach (string path in (updateSource ? pointers.Append(Source) : pointers)) Track(path);
        try
        {
            string asset = Path.Combine(WarehousePaths.Folder(config.ContentRoot, "content-assets"), hash + ".dwg");
            if (!File.Exists(asset)) File.Copy(synced, asset, false);
            if (Json.FileHash(asset) != hash) throw new CadException("RESOURCE_CHANGED", "DWG kết quả không khớp mã kiểm tra.");
            PublishTemplate(state, desired, native, accepted, hash, removed);
            var published = desired.Where(i => accepted.Contains(i.Id)).ToList();
            PublishLayerDependencies(published, Track);
            PublishStyles(state, published, hash, removed);
            PublishHatches(state, published, hash, job, removed);
            PublishMetadata(published, Track);
            foreach (var item in published)
            {
                var cad = native.SingleOrDefault(i => i.Id == item.Id);
                if (cad != null) state.Baselines[item.Id] = new LibrarySyncBaseline { App = item.Signature(), Cad = cad.Signature(), AppContent = item.ContentSignature(), CadContent = cad.ContentSignature(), CadGroup = cad.Group, AppGroup = item.Group };
                item.OriginalName = item.Name;
            }
            state.Items = desired; state.Revision++;
            foreach (var item in removed) state.Baselines.Remove(item.Id);
            if (updateSource)
            {
                string temporary = Source + ".sync-" + Guid.NewGuid().ToString("N"); File.Copy(synced, temporary, false);
                try { File.Replace(temporary, Source, null); }
                catch (IOException) { File.Delete(temporary); throw new CadException("SOURCE_BUSY", "Lưu và đóng DWG thư viện trước khi áp dụng đồng bộ."); }
            }
            Json.Atomic(StateFile, state);
            Json.Atomic(journal, new { files = saved, status = "COMMITTED" });
            File.Delete(journal);
            var result = new { status = "SYNCED", source = Source, sourceHash = hash, revision = state.Revision, items = desired.Count, samples = updateSource ? desired.Count : 0, removed = removed.Count, missingSamples, sourceUpdated = updateSource, backup };
            Json.Atomic(Path.Combine(job, "committed.json"), result);
            return result;
        }
        catch { Recover(); throw; }
    }

    protected void Recover()
    {
        string journal = Path.Combine(Root, "pending.json");
        if (!File.Exists(journal)) return;
        var pending = Json.Read<JObject>(File.ReadAllText(journal));
        if (pending.Value<string>("status") == "COMMITTED") { File.Delete(journal); return; }
        foreach (var entry in pending["files"]!.OfType<JObject>())
        {
            string Checked(string relative)
            {
                string path = Path.GetFullPath(Path.Combine(Warehouse, relative));
                if (!path.StartsWith(Path.GetFullPath(Warehouse) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new CadException("SYNC_RECOVERY", "Đường dẫn phục hồi không hợp lệ.");
                return path;
            }
            string path = Checked(entry.Value<string>("path")!);
            if (entry.Value<bool>("exists"))
            {
                string backup = Checked(entry.Value<string>("backup")!);
                if (File.Exists(path) && Json.FileHash(path) == Json.FileHash(backup)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string temp = path + ".recover-" + Guid.NewGuid().ToString("N"); File.Copy(backup, temp, false);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            else if (File.Exists(path)) File.Delete(path);
        }
        File.Delete(journal);
    }

    protected static bool RemovedName(LibrarySyncItem item, string name) => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) || item.OriginalName != "" && string.Equals(item.OriginalName, name, StringComparison.OrdinalIgnoreCase);

    protected static void RemoveTemplateItems(TemplateProfile template, List<LibrarySyncItem> removed)
    {
        template.Blocks.RemoveAll(b => b.NativeSource != null && removed.Any(i => i.Kind == "blocks" && RemovedName(i, b.NativeSource.BlockName)));
        template.Layers.RemoveAll(l => removed.Any(i => i.Kind == "layers" && RemovedName(i, l.Name)));
    }

    protected void DemandRemoval(List<LibrarySyncItem> removed, List<LibrarySyncItem> remaining)
    {
        string settingsRoot=Path.Combine(Warehouse,"Catalog","addin-settings");
        if(Directory.Exists(settingsRoot))
        {
            var tools=new CadToolCatalog(config.ContentRoot).Read().Items.Where(t=>t.Enabled&&!t.DeletedUtc.HasValue).ToDictionary(t=>t.Id);
            var identities=new HashSet<string>(removed.Select(i=>i.Id),StringComparer.OrdinalIgnoreCase);
            foreach(string file in Directory.GetFiles(settingsRoot,"*.json"))
            {
                string toolId=Path.GetFileNameWithoutExtension(file);if(!tools.TryGetValue(toolId,out var tool))continue;
                var settings=Json.Read<JObject>(File.ReadAllText(file));
                if(settings.Descendants().OfType<JValue>().Any(v=>v.Type==JTokenType.String&&identities.Contains(v.Value<string>()!)))
                    throw new CadException("DEPENDENCY_IMPACT","Add-in "+tool.Name+" đang chọn tài nguyên này. Đổi lựa chọn thư viện trong add-in trước khi loại khỏi danh mục.");
            }
        }
        var template = Store.Template("ISO_QUANGANH");
        var removedBlockIds = new HashSet<string>(template.Blocks.Where(b => b.NativeSource != null && removed.Any(i => i.Kind == "blocks" && RemovedName(i, b.NativeSource.BlockName))).Select(b => b.Id));
        RemoveTemplateItems(template, removed);
        if (Store.Ids("standards").Contains("QUANGANH_LAYERS"))
        {
            var standard = Store.Read<LayerStandard>("standards", "QUANGANH_LAYERS");
            if (standard.Roles.Values.Any(name => removed.Any(i => i.Kind == "layers" && RemovedName(i, name))))
                throw new CadException("DEPENDENCY_IMPACT", "Layer còn được vai trò trong bộ layer chuẩn sử dụng. Đổi liên kết trước khi xóa.");
        }
        foreach (var block in template.Blocks)
            if (removed.Any(i => i.Kind == "layers" && (RemovedName(i, block.Usage?.Layer ?? "") || block.Geometry.Any(o => RemovedName(i, o.Layer)))))
                throw new CadException("DEPENDENCY_IMPACT", "Layer còn được block sử dụng: " + block.Id);
        foreach (var item in remaining)
            if (removed.Any(r => r.Kind == "layers" && item.Kind == "hatches" && RemovedName(r, item.Values.Value<string>("layer") ?? "") || r.Kind == "texts" && item.Kind == "dims" && RemovedName(r, item.Values.Value<string>("textStyle") ?? "")))
                throw new CadException("DEPENDENCY_IMPACT", "Tài nguyên còn được thư viện sử dụng: " + item.Name);
        foreach (string id in Store.Ids("skills").Where(id => !WarehousePaths.IsRetired(config.ContentRoot, "skills", id) && !Catalog.Deleted("skills", id)))
        {
            var skill = Store.Skill(id); if (skill.TemplateId != template.Id) continue;
            try
            {
                if (skill.Recipe != null && (skill.Recipe.AllowedBlocks.Any(removedBlockIds.Contains) || skill.Recipe.Layers.Values.Any(name => removed.Any(i => i.Kind == "layers" && RemovedName(i, name)))))
                    throw new CadException("DEPENDENCY_IMPACT", "Recipe đang tham chiếu tài nguyên được chọn xóa.");
                foreach (var operation in skill.Workflow)
                {
                    if (operation.Kind == "insert_block" && removedBlockIds.Contains(operation.AssetId) || removed.Any(i => i.Kind == "layers" && RemovedName(i, operation.Layer)))
                        throw new CadException("DEPENDENCY_IMPACT", operation.AssetId != "" ? operation.AssetId : operation.Layer);
                    if (removed.Any(i => (i.Kind == "texts" && operation.Kind == "add_text" || i.Kind == "dims" && operation.Kind == "add_dimension") && RemovedName(i, operation.Style)))
                        throw new CadException("DEPENDENCY_IMPACT", operation.Style);
                    if (operation.Kind == "create_hatch" && removed.Any(i => i.Kind == "hatches" && i.Values.Value<string>("pattern") == operation.Pattern) && !remaining.Any(i => i.Kind == "hatches" && i.Values.Value<string>("pattern") == operation.Pattern))
                        throw new CadException("DEPENDENCY_IMPACT", operation.Pattern);
                }
            }
            catch (CadException error) { throw new CadException("DEPENDENCY_IMPACT", "Skill " + id + " cần tài nguyên này: " + error.Message); }
        }
    }

    protected void PublishTemplate(LibrarySyncState previous, List<LibrarySyncItem> desired, List<LibrarySyncItem> native, HashSet<string> accepted, string hash, List<LibrarySyncItem> removed)
    {
        if(!desired.Any(i=>accepted.Contains(i.Id)&&(i.Kind=="layers"||i.Kind=="blocks"))&&!removed.Any(i=>i.Kind=="layers"||i.Kind=="blocks"))return;
        var template = Store.Template("ISO_QUANGANH");
        RemoveTemplateItems(template, removed);
        foreach (var item in desired.Where(i => i.Kind == "layers" && accepted.Contains(i.Id)))
        {
            string oldName = string.IsNullOrEmpty(item.OriginalName) ? item.Name : item.OriginalName;
            // A rename staged in the app still matches the stable native handle.
            var layer = template.Layers.FirstOrDefault(l => l.Name == item.Name || l.Name == oldName);
            if (layer == null)
            {
                var sourceOld = native.SingleOrDefault(i => i.Id == item.Id);
                layer = template.Layers.FirstOrDefault(l => sourceOld != null && l.Name == sourceOld.Name);
            }
            if (layer == null) { layer = new LayerAsset(); template.Layers.Add(layer); }
            layer.Name = item.Name; layer.Color = (short)item.Values.Value<int>("color");
            if (oldName != item.Name)
                foreach (var block in template.Blocks)
                {
                    if (block.Usage?.Layer == oldName) block.Usage.Layer = item.Name;
                    foreach (var operation in block.Geometry.Where(o => o.Layer == oldName)) operation.Layer = item.Name;
                }
            layer.Appearance = new LayerAppearance { Rgb = item.Values.Value<int?>("rgb"), LineType = item.Values.Value<string>("lineType")!, LineWeight = item.Values.Value<int>("lineWeight"), Plottable = item.Values.Value<bool>("plottable"),
                Dashes = item.Values["dashes"]?.ToObject<List<double>>(), PatternLength = item.Values.Value<double?>("patternLength"), SourceHash = hash };
        }
        foreach (var item in desired.Where(i => i.Kind == "blocks" && accepted.Contains(i.Id)))
        {
            var matches = template.Blocks.Where(b => b.NativeSource?.BlockName == item.Name || b.NativeSource?.BlockName == item.OriginalName).ToArray();
            if (matches.Length == 0)
            {
                string id = System.Text.RegularExpressions.Regex.Replace(item.Name, "[^\\p{L}\\p{N}_-]", "_");
                if (id.Length > 70) id = id.Substring(0, 70);
                if (template.Blocks.Any(b => b.Id == id)) id += "_" + item.Id.Substring(0, 6);
                var added = new BlockAsset { Id = id, NativeSource = new NativeBlockSource() }; template.Blocks.Add(added); matches = new[] { added };
            }
            foreach (var block in matches)
            {
                var source = block.NativeSource!; source.FileHash = hash; source.BlockName = item.Name;
                source.Min = new Point2(item.Values.Value<double>("minX"), item.Values.Value<double>("minY")); source.Max = new Point2(item.Values.Value<double>("maxX"), item.Values.Value<double>("maxY"));
                if (item.Values["properties"] is JArray properties) source.DynamicProperties = properties.OfType<JObject>().Select(p => new DynamicPropertySpec { Name = p.Value<string>("name")!, ReadOnly = p.Value<bool>("readOnly"), Visible = p.Value<bool>("visible"), ValueType = p.Value<string>("valueType") ?? "", Units = p.Value<string>("units")!, DefaultValue = p["value"]?.DeepClone(), AllowedValues = p["allowed"]?.ToObject<List<JToken>>() ?? new() }).ToList();
            }
        }
        if (template.Layers.Count > 100 || template.Blocks.Count > 1000) throw new CadException("SYNC_LIMIT", "Thư viện tối đa 100 layer và 1.000 block.");
        template.Version = Next(template.Version);
        while (Store.Versions("templates", template.Id).Contains(template.Version)) template.Version = Next(template.Version);
        Store.Publish("templates", template.Id, template.Version, template);
    }

    protected void PublishStyles(LibrarySyncState previous, List<LibrarySyncItem> desired, string hash, List<LibrarySyncItem> removed)
    {
        if(!desired.Any(i=>i.Kind=="texts"||i.Kind=="dims")&&!removed.Any(i=>i.Kind=="texts"||i.Kind=="dims"))return;
        var catalog = Raw("Styles");
        foreach (string kind in new[] { "texts", "dims" })
        {
            if (catalog[kind] is not JArray) catalog[kind] = new JArray();
            var array = (JArray)catalog[kind]!;
            foreach (var entry in array.OfType<JObject>().Where(e => removed.Any(i => i.Kind == kind && RemovedName(i, e.Value<string>("name") ?? ""))).ToArray()) entry.Remove();
            foreach (var item in desired.Where(i => i.Kind == kind))
            {
                string oldName = string.IsNullOrEmpty(item.OriginalName) ? item.Name : item.OriginalName;
                var old = array.OfType<JObject>().FirstOrDefault(o => o.Value<string>("name") == item.Name || o.Value<string>("name") == oldName);
                var value = (JObject)item.Values.DeepClone(); value["id"] = old?["id"]?.DeepClone() ?? Json.Hash(item.Name); value["name"] = item.Name; value["handle"] = item.Handle;
                if (old != null) old.Replace(value); else array.Add(value);
            }
        }
        var textRenames=desired.Where(i=>i.Kind=="texts"&&i.OriginalName!=""&&i.OriginalName!=i.Name).ToDictionary(i=>i.OriginalName,i=>i.Name);
        foreach(var dim in (catalog["dims"] as JArray??new()).OfType<JObject>())if(textRenames.TryGetValue(dim.Value<string>("textStyle")??"",out string? renamed))dim["textStyle"]=renamed;
        SaveCatalog("Styles", catalog, hash);
    }

    protected void PublishHatches(LibrarySyncState previous, List<LibrarySyncItem> desired, string hash, string job, List<LibrarySyncItem> removed)
    {
        var layerRenames=desired.Where(i=>i.Kind=="layers"&&i.OriginalName!=""&&i.OriginalName!=i.Name).ToDictionary(i=>i.OriginalName,i=>i.Name);
        if(!desired.Any(i=>i.Kind=="hatches")&&!removed.Any(i=>i.Kind=="hatches")&&layerRenames.Count==0)return;
        var catalog = Raw("Hatches");
        if (catalog["items"] is not JArray) catalog["items"] = new JArray();
        var array = (JArray)catalog["items"]!;
        foreach (var entry in array.OfType<JObject>().Where(e => removed.Any(i => i.Kind == "hatches" && (i.Handle != "" && i.Handle == e.Value<string>("handle") || e.Value<string>("id")?.StartsWith(i.Id) == true || Json.Hash(i.Id) == e.Value<string>("id")))).ToArray()) entry.Remove();
        foreach (var item in desired.Where(i => i.Kind == "hatches"))
        {
            var oldItem = previous.Items.SingleOrDefault(i => i.Id == item.Id);
            var old = array.OfType<JObject>().FirstOrDefault(h => h.Value<string>("handle") == item.Handle || oldItem != null && h.Value<string>("handle") == oldItem.Handle);
            var value = (JObject)item.Values.DeepClone();
            value["id"] = old?["id"]?.DeepClone() ?? Json.Hash(item.Id); value["handle"] = item.Handle; value["count"] = old?["count"]?.DeepClone() ?? new JValue(1); value["locations"] = old?["locations"]?.DeepClone() ?? new JArray("*Model_Space");
            bool sameShape = old != null && new[] { "pattern", "scale", "angle", "color" }.All(k => JToken.DeepEquals(old[k], value[k]));
            if (sameShape) foreach (string key in new[] { "imageFile", "imageHash", "width", "height", "previewTruncated" }) if (old![key] != null) value[key] = old[key]!.DeepClone();
            value["engineSupported"] = new[] { "ANSI31", "SOLID" }.Contains(value.Value<string>("pattern"));
            if (old != null) old.Replace(value); else array.Add(value);
        }
        foreach(var entry in array.OfType<JObject>())if(layerRenames.TryGetValue(entry.Value<string>("layer")??"",out string? renamed))entry["layer"]=renamed;
        SaveCatalog("Hatches", catalog, hash, job, desired);
    }

    protected void SaveCatalog(string kind, JObject catalog, string hash, string? job = null, List<LibrarySyncItem>? desired = null)
    {
        string root = Path.Combine(Warehouse, kind, "ISO_QUANGANH"); Directory.CreateDirectory(root);
        string pointer = Path.Combine(root, "active.json");
        string oldVersion = catalog.Value<string>("version") ?? "1.0.0", version = Next(oldVersion);
        while (Directory.Exists(Path.Combine(root, version))) version = Next(version);
        string target = Path.Combine(root, version); Directory.CreateDirectory(target);
        if (File.Exists(pointer))
        {
            string old = Path.Combine(root, Json.Read<JObject>(File.ReadAllText(pointer)).Value<string>("version")!);
            foreach (string image in Directory.GetFiles(old, "*.png")) File.Copy(image, Path.Combine(target, Path.GetFileName(image)), false);
        }
        catalog["version"] = version; catalog["sourceHash"] = hash; catalog["sourceName"] = Path.GetFileName(Source); catalog["importedUtc"] = DateTime.UtcNow;
        if (kind == "Hatches" && job != null && desired != null && File.Exists(Path.Combine(job,"sync-previews.json")))
        {
            var previews=Json.Read<JArray>(File.ReadAllText(Path.Combine(job,"sync-previews.json")));
            foreach(var item in desired.Where(i=>i.Kind=="hatches"))
            {
                var preview=previews.OfType<JObject>().SingleOrDefault(p=>p.Value<string>("id")==item.Id&&p["file"]!=null);
                var entry=(catalog["items"] as JArray)?.OfType<JObject>().SingleOrDefault(h=>h.Value<string>("handle")==item.Handle);
                if(preview==null||entry==null)continue;
                string input=Path.Combine(job,item.Id+".png");
                if(Json.FileHash(input)!=preview.Value<string>("hash"))throw new CadException("RESOURCE_CHANGED","Ảnh hatch thay đổi trong lúc đồng bộ.");
                string id=entry.Value<string>("id")!,image=Json.Hash(id)+".png";
                File.Copy(input,Path.Combine(target,image),true);entry["imageFile"]=id+".png";entry["imageHash"]=Json.FileHash(Path.Combine(target,image));
            }
        }
        string file = Path.Combine(target, "catalog.json"); Json.Atomic(file, catalog);
        Json.Atomic(pointer, new { version, checksum = Json.FileHash(file) });
    }

    protected void PublishMetadata(List<LibrarySyncItem> items, Action<string> track)
    {
        string management = Path.Combine(Warehouse, "Catalog", "management");
        var template = Store.Template("ISO_QUANGANH");
        var hatches = (Raw("Hatches")["items"] as JArray ?? new()).OfType<JObject>().ToArray();
        var styles = Raw("Styles");
        foreach (var item in items)
        {
            var groups = Catalog.Groups(item.Kind);
            string groupId = "";
            if (!string.IsNullOrWhiteSpace(item.Group))
            {
                var group = groups.FirstOrDefault(g => Group(item.Kind,g.Id) == item.Group);
                if (group == null)
                {
                    track(Path.Combine(management, item.Kind + "-groups.json"));
                    string parent="";
                    foreach(string segment in item.Group.Split('/').Select(x=>x.Trim()).Where(x=>x!=""))
                    {
                        group=Catalog.Groups(item.Kind).FirstOrDefault(g=>g.ParentId==parent&&g.Name==segment)??Catalog.Group(item.Kind,"",segment,parent);
                        parent=group.Id;
                    }
                }
                groupId = group?.Id??"";
            }
            IEnumerable<string> ids = item.Kind switch
            {
                "layers" => new[] { Json.Hash(new[] { template.Id, item.Name }) },
                "blocks" => template.Blocks.Where(b => b.NativeSource?.BlockName == item.Name).Select(b => Json.Hash(new[] { template.Id, b.Id })),
                "hatches" => hatches.Where(h => h.Value<string>("handle") == item.Handle).Select(h => h.Value<string>("id")!),
                _ => (styles[item.Kind] as JArray ?? new()).OfType<JObject>().Where(s => s.Value<string>("name") == item.Name).Select(s => s.Value<string>("id")!)
            };
            foreach (string id in ids)
            {
                var label = Catalog.Label(item.Kind, id);
                if (item.Kind == "layers" && item.OriginalName != "" && item.OriginalName != item.Name)
                    label = Catalog.Label("layers", Json.Hash(new[] { template.Id, item.OriginalName }));
                label.GroupId = groupId; label.Purpose = item.Purpose; label.Notes = item.Notes;
                if (item.Kind == "hatches") label.Name = item.Name;
                label.Revision++;
                string path = Path.Combine(management, item.Kind, id + ".json"); track(path); Json.Atomic(path, label);
            }
        }
    }

    protected void PublishLayerDependencies(List<LibrarySyncItem> items, Action<string> track)
    {
        var renames=items.Where(i=>i.Kind=="layers"&&i.OriginalName!=""&&i.OriginalName!=i.Name).ToDictionary(i=>i.OriginalName,i=>i.Name);
        string id="QUANGANH_LAYERS";
        if(items.Any(i=>i.Kind=="layers")&&Store.Ids("standards").Contains(id))
        {
            var standard=Store.Read<LayerStandard>("standards",id);
            standard.Layers=Store.Template("ISO_QUANGANH").Layers;
            foreach(string key in standard.Roles.Keys.ToArray())if(renames.TryGetValue(standard.Roles[key],out string? name))standard.Roles[key]=name;
            standard.Version=Next(standard.Version);while(Store.Versions("standards",id).Contains(standard.Version))standard.Version=Next(standard.Version);
            track(Path.Combine(Warehouse,"Versions","standards",id,"active.json"));
            Store.Publish("standards",id,standard.Version,standard);
        }
        var textRenames=items.Where(i=>i.Kind=="texts"&&i.OriginalName!=""&&i.OriginalName!=i.Name).ToDictionary(i=>i.OriginalName,i=>i.Name);
        var dimRenames=items.Where(i=>i.Kind=="dims"&&i.OriginalName!=""&&i.OriginalName!=i.Name).ToDictionary(i=>i.OriginalName,i=>i.Name);
        if(renames.Count==0&&textRenames.Count==0&&dimRenames.Count==0)return;
        foreach(string skillId in Store.Ids("skills"))
        {
            var skill=Store.Skill(skillId);
            if(skill.TemplateId!="ISO_QUANGANH")continue;
            bool changed=false;
            if(skill.Recipe!=null)foreach(string key in skill.Recipe.Layers.Keys.ToArray())if(renames.TryGetValue(skill.Recipe.Layers[key],out string? name)){skill.Recipe.Layers[key]=name;changed=true;}
            foreach(var operation in skill.Workflow){
                if(renames.TryGetValue(operation.Layer,out string? layer)){operation.Layer=layer;changed=true;}
                var styles=operation.Kind=="add_text"?textRenames:operation.Kind=="add_dimension"?dimRenames:null;
                if(styles!=null&&styles.TryGetValue(operation.Style,out string? style)){operation.Style=style;changed=true;}
            }
            if(!changed)continue;
            skill.Version=Next(skill.Version);while(Store.Versions("skills",skillId).Contains(skill.Version))skill.Version=Next(skill.Version);
            track(Path.Combine(Warehouse,"Versions","skills",skillId,"active.json"));
            Store.Publish("skills",skillId,skill.Version,skill);
        }
    }
}
