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

public sealed partial class LibrarySyncService(InstallConfig config):LibraryRepository(config)
{
    public object Save(JObject request)
    {
        using var lease = Lease(); Recover();
        var state = ReadState();
        if (request.Value<int?>("revision") != state.Revision) throw new CadException("RESOURCE_CHANGED", "Danh mục đã đổi trên máy khác, tải lại trước khi lưu.");
        var item = request["item"]?.ToObject<LibrarySyncItem>() ?? throw new CadException("SYNC_ITEM", "Chưa có tài nguyên.");
        if (item.Id == "") item.Id = Guid.NewGuid().ToString("N");
        LibrarySyncRules.Validate(item);
        var old = state.Items.SingleOrDefault(i => i.Id == item.Id);
        if (old != null && old.Kind != item.Kind) throw new CadException("SYNC_KIND", "Loại tài nguyên đã đổi.");
        if (state.Items.Any(i => i.Id != item.Id && i.Kind == item.Kind && string.Equals(i.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
            throw new CadException("SYNC_DUPLICATE", "Tên đã có trong thư viện.");
        if (item.Kind == "blocks" && old == null && item.Values.Value<string>("donorHash") == null)
            throw new CadException("SYNC_BLOCK", "Nhập DWG chứa block trước khi thêm.");
        // Native identity, handles and geometry summaries cannot be rewritten by the client.
        if (old != null)
        {
            item.Handle = old.Handle;
            item.OriginalName = old.OriginalName;
            foreach (string key in new[] { "geometryHash", "properties", "attributes", "minX", "minY", "maxX", "maxY", "originX", "originY", "dynamic", "units" })
                if (old.Values[key] != null) item.Values[key] = old.Values[key]!.DeepClone();
            state.Items.Remove(old);
        }
        item.Values.Remove("donorPath");
        item.Draft = true;
        state.Items.Add(item); state.Revision++;
        Json.Atomic(StateFile, state);
        return new { revision = state.Revision, item, message = "Đã lưu bản nháp. Nhấn Đồng bộ để ghi vào DWG và danh mục sử dụng." };
    }

    async Task<List<LibrarySyncItem>> Native(string job, List<LibrarySyncItem> managed, List<LibrarySyncItem>? writes, CancellationToken ct, string layoutKind = "", bool previews = false)
    {
        string scanner = Path.Combine(Path.GetDirectoryName(config.HostExecutable)!, "LibraryScanner", "CAD.LibraryScanner.dll");
        if (!File.Exists(scanner)) throw new CadException("SCANNER_MISSING", "Build bộ đọc AutoCAD trước khi đồng bộ.");
        string requestFile = Path.Combine(job, "sync-request.json");
        var prepared = writes?.Select(i => i.Copy()).ToList();
        foreach (var item in prepared ?? new())
        {
            item.Values.Remove("donorPath");
            if (item.Values.Value<string>("donorHash") is string donorHash)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(donorHash, "^[a-f0-9]{64}$")) throw new CadException("SYNC_BLOCK", "Mã nguồn block không hợp lệ.");
                string path = Path.Combine(WarehousePaths.Folder(config.ContentRoot, "content-assets"), donorHash + ".dwg");
                if (!File.Exists(path) || Json.FileHash(path) != donorHash) throw new CadException("RESOURCE_CHANGED", "DWG nguồn block đã thay đổi.");
                item.Values["donorPath"] = path;
            }
        }
        var groupNodes=LibrarySyncRules.Kinds.SelectMany(kind=>Catalog.Groups(kind).Select(group=>new{Id=group.Id,Kind=kind,Path=Group(kind,group.Id),Parent=Group(kind,group.ParentId)})).ToArray();
        Json.Atomic(requestFile, new { apply = writes != null, managed, writes = prepared ?? new(), layoutKind, groupNodes, previews, layoutX = 100000, layoutY = 0 });
        string result = Path.Combine(job, "sync-result.json"), error = Path.Combine(job, "sync-error.json");
        if (File.Exists(result)) File.Delete(result);
        if (File.Exists(error)) File.Delete(error);
        File.WriteAllText(Path.Combine(job, ".aicad-library-job"), "AI CAD library sync");
        string script = Path.Combine(job, "sync.scr");
        File.WriteAllText(script, "_NETLOAD\n\"" + scanner.Replace('\\', '/') + "\"\nAICADLIBRARYSYNC\n_QUIT\n_Y\n", Encoding.ASCII);
        var start = new ProcessStartInfo(@"C:\Program Files\Autodesk\AutoCAD 2021\accoreconsole.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "/i", Path.Combine(config.ContentRoot, "content", "launch", "blank-mm.dwg"), "/s", script, "/isolate", "AICadLibrarySync", Path.Combine(job, "profile") }) start.ArgumentList.Add(argument);
        start.Environment["AICAD_LIBRARY_JOB"] = job;
        using var process = Process.Start(start) ?? throw new CadException("SCAN_FAILED", "Không khởi chạy được AutoCAD.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); throw new CadException("SYNC_TIMEOUT", "Đã dừng đồng bộ do hết thời gian hoặc hủy thao tác."); }
        File.WriteAllText(Path.Combine(job, "native.log"), await stdout + await stderr);
        if (File.Exists(error)) throw new CadException("SYNC_NATIVE", Json.Read<JObject>(File.ReadAllText(error)).Value<string>("message")!);
        if (!File.Exists(result)) throw new CadException("SYNC_NATIVE", "AutoCAD chưa trả kết quả. Kiểm tra nguồn và bản quyền CAD.");
        return Json.Read<JObject>(File.ReadAllText(result))["items"]!.ToObject<List<LibrarySyncItem>>()!;
    }

    public async Task<object> UploadBlock(string name, Stream input, CancellationToken ct)
    {
        if (!string.Equals(Path.GetExtension(name), ".dwg", StringComparison.OrdinalIgnoreCase)) throw new CadException("INVALID_UPLOAD", "Chọn DWG chứa block.");
        string job = Job(Guid.NewGuid().ToString("N")); Directory.CreateDirectory(job);
        string source = Path.Combine(job, "source.dwg");
        using (var output = new FileStream(source, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            byte[] buffer = new byte[65536]; int count; long total = 0;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                total += count; if (total > 20 * 1024 * 1024) throw new CadException("INVALID_UPLOAD", "DWG tối đa 20 MB.");
                await output.WriteAsync(buffer.AsMemory(0, count), ct);
            }
        }
        var items = await Native(job, new(), null, ct);
        string hash = Json.FileHash(source), target = Path.Combine(WarehousePaths.Folder(config.ContentRoot, "content-assets"), hash + ".dwg");
        if (!File.Exists(target)) File.Copy(source, target, false);
        if (Json.FileHash(target) != hash) throw new CadException("RESOURCE_CHANGED", "Nguồn block không khớp.");
        return new { hash, blocks = items.Where(i => i.Kind == "blocks").ToArray() };
    }

    public async Task<object> Scan(CancellationToken ct, bool previews = false)
    {
        if (!await gate.WaitAsync(0, ct)) throw new CadException("SYNC_BUSY", "Đang đọc hoặc đồng bộ thư viện.");
        try
        {
            using var lease = Lease(); Recover();
            string id = Guid.NewGuid().ToString("N"), job = Job(id); Directory.CreateDirectory(job);
            string sourceHash = CadSourceFile.Hash(Source); CadSourceFile.Copy(Source, Path.Combine(job, "source.dwg"));
            if (Json.FileHash(Path.Combine(job, "source.dwg")) != sourceHash) throw new CadException("SOURCE_CHANGED", "DWG đang được lưu, thử lại sau.");
            var state = ReadState(); var hints = state.Items.Count == 0 ? Hints() : state.Items;
            var native = await Native(job, hints, null, ct, previews: previews);
            if (state.Items.Count == 0)
            {
                state.Items = native.Where(i => hints.Any(h => h.Id == i.Id)).Select(i => i.Copy()).ToList();
                // Keep app layer settings as a separate side of the first comparison.
                var template = Store.Template("ISO_QUANGANH");
                foreach (var layer in template.Layers)
                {
                    var item = state.Items.First(i => i.Kind == "layers" && i.Name == layer.Name);
                    item.Values["color"] = layer.Color;
                    if (layer.Appearance != null)
                    {
                        item.Values["rgb"] = layer.Appearance.Rgb.HasValue ? new JValue(layer.Appearance.Rgb.Value) : JValue.CreateNull();
                        item.Values["lineWeight"] = layer.Appearance.LineWeight; item.Values["lineType"] = layer.Appearance.LineType; item.Values["plottable"] = layer.Appearance.Plottable;
                    }
                }
                state.Revision++;
                Json.Atomic(StateFile, state);
            }
            // Deleted hatch configurations remain in the existing trash workflow.
            var deletedHandles = (Raw("Hatches")["items"] as JArray ?? new()).OfType<JObject>()
                .Where(h => Catalog.Label("hatches", h.Value<string>("id")!).DeletedUtc.HasValue).Select(h => h.Value<string>("handle")).ToHashSet();
            native.RemoveAll(i => i.Kind == "hatches" && !state.Items.Any(a => a.Id == i.Id) && deletedHandles.Contains(i.Handle));
            if (CadSourceFile.Hash(Source) != sourceHash) throw new CadException("SOURCE_CHANGED", "DWG đã thay đổi trong lúc đọc. Lưu và đọc lại.");
            string appHash = AppHash();
            Json.Atomic(Path.Combine(job, "review.json"), new { sourceHash, appHash, revision = state.Revision, native });
            return new { id, source = Source, revision = state.Revision, items = state.Items, differences = LibrarySyncRules.Compare(state, native) };
        }
        finally { gate.Release(); }
    }

    public async Task<object> Apply(JObject request, CancellationToken ct)
    {
        if (!await gate.WaitAsync(0, ct)) throw new CadException("SYNC_BUSY", "Đang đọc hoặc đồng bộ thư viện.");
        try
        {
            using var lease = Lease(); Recover();
            string job = Job(request.Value<string>("id") ?? "");
            var review = Json.Read<JObject>(File.ReadAllText(Path.Combine(job, "review.json")));
            void DemandCurrent()
            {
                if (review.Value<string>("sourceHash") != CadSourceFile.Hash(Source) || review.Value<string>("appHash") != AppHash())
                    throw new CadException("RESOURCE_CHANGED", "DWG hoặc app đã đổi từ lúc so sánh. Nhấn Đọc và so sánh lại.");
            }
            DemandCurrent();
            var state = ReadState(); var native = review["native"]!.ToObject<List<LibrarySyncItem>>()!;
            var differences = LibrarySyncRules.Compare(state, native);
            var choices = request["choices"]?.ToObject<Dictionary<string, string>>() ?? new();
            string layoutKind=request.Value<string>("layoutKind")??"";
            if(layoutKind!=""&&((layoutKind!="all"&&!LibrarySyncRules.Kinds.Contains(layoutKind))||choices.Values.Any(v=>v!="skip")))
                throw new CadException("SYNC_LAYOUT","Chọn một loại thư viện để sắp xếp; cập nhật dữ liệu bằng thao tác đồng bộ riêng.");
            if (choices.Keys.Any(id => differences.All(d => d.Id != id)) || choices.Values.Any(v => !new[] { "app", "cad", "skip", "remove" }.Contains(v)))
                throw new CadException("SYNC_CHOICE", "Lựa chọn đồng bộ không hợp lệ.");
            var desired = state.Items.Select(i => i.Copy()).ToList(); var writes = new List<LibrarySyncItem>(); var accepted = new HashSet<string>();
            var removed = new List<LibrarySyncItem>();
            foreach (var change in differences)
            {
                string choice = choices.TryGetValue(change.Id, out string? value) ? value : "skip";
                if (change.Status == "MATCH") accepted.Add(change.Id);
                if (choice == "remove")
                {
                    LibrarySyncRules.ValidateRemoval(change);
                    removed.Add(change.App!.Copy()); desired.RemoveAll(i => i.Id == change.Id);
                }
                if (choice == "cad")
                {
                    if (change.Cad == null) throw new CadException("SYNC_MISSING", "Tài nguyên thiếu trong CAD. Chọn giữ hoặc khôi phục từ app.");
                    var imported = change.Cad.Copy();
                    if (change.App != null && !change.CadGroupChanged) imported.Group = change.App.Group;
                    desired.RemoveAll(i => i.Id == change.Id); desired.Add(imported); accepted.Add(change.Id);
                }
                if (choice == "app")
                {
                    if (change.App == null) throw new CadException("SYNC_MISSING", "Tài nguyên chưa có trong app.");
                    LibrarySyncRules.Validate(change.App); writes.Add(change.App.Copy()); accepted.Add(change.Id);
                }
            }
            if (removed.Count > 0) DemandRemoval(removed, desired);
            // Removing entries already absent from CAD only changes app catalogs, even when the DWG is open.
            if (removed.Count > 0 && !choices.Values.Any(v => v == "app" || v == "cad"))
            {
                DemandCurrent(); ct.ThrowIfCancellationRequested();
                return Commit(job, state, desired, native, new HashSet<string>(), Source, removed, false);
            }
            var output = await Native(job, desired, writes, ct, layoutKind);
            int missingSamples=layoutKind==""?0:Json.Read<JObject>(File.ReadAllText(Path.Combine(job,"sync-result.json")))["layout"]?["MissingSampleIds"]?.Count()??0;
            string synced = Path.Combine(job, "synced.dwg");
            if (!File.Exists(synced)) throw new CadException("SYNC_NATIVE", "Chưa có DWG kết quả.");
            foreach (var item in desired)
            {
                var actual = output.SingleOrDefault(i => i.Id == item.Id);
                if (actual != null && accepted.Contains(item.Id))
                {
                    item.Handle = actual.Handle;
                    item.Values = (JObject)actual.Values.DeepClone();
                    item.Draft = false;
                }
            }
            if (output.Any(i => i.Kind == "layers" && desired.Any(a => a.Id == i.Id) && i.Name == "")) throw new CadException("SYNC_VERIFY", "Layer không có tên.");
            DemandCurrent(); ct.ThrowIfCancellationRequested();
            return Commit(job, state, desired, output, accepted, synced, removed,missingSamples:missingSamples);
        }
        finally { gate.Release(); }
    }

}
