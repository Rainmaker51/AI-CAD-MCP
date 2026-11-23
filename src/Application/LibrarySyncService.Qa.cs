using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

public partial class LibraryRepository
{
    public object UpdateQaLabel(JObject request,string kind,string metadataId,string resourceId)
    {
        using var lease=Lease();Recover();var state=ReadState();
        if(request.Value<int?>("catalogRevision")!=state.Revision)throw new CadException("RESOURCE_CHANGED","Kho đã thay đổi. Tải lại trước khi lưu hồ sơ.");
        string action=request.Value<string>("action")??"update";
        if(action=="delete")
        {
            var item=state.Items.SingleOrDefault(i=>i.Kind==kind&&i.Id==resourceId);
            if(item!=null)DemandRemoval(new(){item},state.Items.Where(i=>i.Id!=item.Id).ToList());
        }
        return Catalog.UpdateHatch(metadataId,request.Value<int?>("revision")??-1,request.Value<string>("name")??"",request.Value<string>("purpose")??"",request.Value<string>("notes")??"",request.Value<string>("groupId")??"",request.Value<bool?>("reviewed")??false,action,kind);
    }
    public object ConfigureQaBlock(JObject request,Action<TemplateProfile>? publish=null)
    {
        using var lease=Lease();Recover();var template=Store.Template("ISO_QUANGANH");
        if(request.Value<string>("version")!=template.Version)throw new CadException("RESOURCE_CHANGED","Thư viện đã đổi. Tải lại trước khi cấu hình block.");
        string id=request.Value<string>("resourceId")??"";
        var block=template.Blocks.SingleOrDefault(b=>b.Id==id)??throw new CadException("NOT_FOUND","Block không còn trong thư viện.");
        Catalog.Available("blocks",Json.Hash(new[]{template.Id,id}));
        block.Usage=request["usage"]?.ToObject<BlockUsage>()??throw new CadException("INVALID_CATALOG","Chưa có cấu hình sử dụng block.");
        template.Version=Next(template.Version);while(Store.Versions("templates",template.Id).Contains(template.Version))template.Version=Next(template.Version);
        if(publish!=null)publish(template);else PublishConfiguredTemplate(template);return new{status="PUBLISHED",version=template.Version};
    }
    /// <summary>Approve selected saved CAD resources. Never replace the source DWG, which
    /// may still be open in AutoCAD. Unselected changes are not published.</summary>
    public async Task<object> ApproveCad(JObject request, CancellationToken ct)
    {
        if (!await gate.WaitAsync(0, ct)) throw new CadException("SYNC_BUSY", "Đang kiểm tra hoặc duyệt thư viện.");
        try
        {
            using var lease = Lease(); Recover();
            string job = Job(request.Value<string>("id") ?? "");
            var review = Json.Read<JObject>(File.ReadAllText(Path.Combine(job, "review.json")));
            if (review.Value<string>("sourceHash") != CadSourceFile.Hash(Source) || review.Value<string>("appHash") != AppHash())
                throw new CadException("RESOURCE_CHANGED", "DWG hoặc hồ sơ đã đổi. Lưu DWG và đọc kiểm tra lại.");
            if (File.Exists(Path.Combine(job, "committed.json"))) throw new CadException("REVIEW_USED", "Lần kiểm tra này đã được duyệt.");
            var state = ReadState();
            var native = review["native"]!.ToObject<List<LibrarySyncItem>>()!;
            var changes = LibrarySyncRules.Compare(state, native);
            var choices = request["choices"]?.ToObject<Dictionary<string, string>>() ?? new();
            if (choices.Count == 0 || choices.Keys.Any(id => changes.All(d => d.Id != id)) || choices.Values.Any(v => v != "cad" && v != "remove"))
                throw new CadException("QA_CHOICE", "Chọn các mục lấy từ CAD hoặc các mục đã mất trong CAD để loại khỏi danh mục.");
            var desired = state.Items.Select(i => i.Copy()).ToList();
            var accepted = new HashSet<string>(); var removed = new List<LibrarySyncItem>();
            foreach (var change in changes.Where(d => choices.ContainsKey(d.Id)))
            {
                if (choices[change.Id] == "remove")
                {
                    bool absentSample=change.App?.Kind=="blocks"&&review["presence"]?[change.Id]?.Value<bool?>("samplePresent")==false;
                    if(!absentSample)LibrarySyncRules.ValidateRemoval(change);
                    removed.Add(change.App!.Copy()); desired.RemoveAll(i => i.Id == change.Id);
                    continue;
                }
                if (change.Cad == null) throw new CadException("QA_MISSING", "Tài nguyên không còn trong CAD. Không tự khôi phục từ danh mục.");
                if(change.Cad.Kind=="blocks"&&review["presence"]?[change.Id]?.Value<bool?>("samplePresent")==false)throw new CadException("QA_MISSING_SAMPLE","Block chỉ còn định nghĩa. Chèn lại mẫu trong DWG nếu muốn đưa vào danh mục đang dùng.");
                var item = change.Cad.Copy();
                if (change.App != null)
                {
                    item.OriginalName = change.App.OriginalName == "" ? change.App.Name : change.App.OriginalName;
                    if (!change.CadGroupChanged) item.Group = change.App.Group;
                    // Notes edited in QA's management profile are not stale CAD annotations.
                    item.Purpose = change.App.Purpose; item.Notes = change.App.Notes;
                }
                item.Draft = false; desired.RemoveAll(i => i.Id == item.Id); desired.Add(item); accepted.Add(item.Id);
            }
            if (removed.Count > 0) DemandRemoval(removed, desired);
            ct.ThrowIfCancellationRequested();
            // Publish the exact inspected snapshot, not a file that could be saved again
            // between hashing it and copying it into the immutable asset store.
            string snapshot = Path.Combine(job, "source.dwg");
            if (Json.FileHash(snapshot) != review.Value<string>("sourceHash")) throw new CadException("SOURCE_CHANGED", "Bản kiểm tra DWG đã thay đổi.");
            return Commit(job, state, desired, native, accepted, snapshot, removed, updateSource: false);
        }
        finally { gate.Release(); }
    }
}
