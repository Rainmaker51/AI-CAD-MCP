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
    public object ValidateQaView(JObject request)
    {
        using var lease=Lease();Recover();
        if(request.Value<string>("appHash")!=AppHash())throw new CadException("RESOURCE_CHANGED","Danh mục hoặc thông tin quản lý đã thay đổi. Quét và xem lại trước khi áp dụng.");
        return new{status="READY"};
    }
    public object CheckQaMetadata(JObject request)
    {
        using var lease=Lease();Recover();var state=ReadState();
        if(request.Value<int?>("catalogRevision") is int expected&&expected!=state.Revision)throw new CadException("RESOURCE_CHANGED","Danh mục đã thay đổi trước khi sửa hồ sơ.");
        foreach(var p in (request["metadata"] as JArray??new()).OfType<JObject>())
        {
            var label=Catalog.Label(p.Value<string>("kind")!,p.Value<string>("metadataId")!);
            bool applied=label.Name==p.Value<string>("name")&&label.Purpose==p.Value<string>("purpose")&&label.Notes==p.Value<string>("notes")&&label.GroupId==p.Value<string>("groupId")&&label.Reviewed==p.Value<bool>("reviewed");
            if(label.Revision!=p.Value<int>("revision")&&!applied)throw new CadException("RESOURCE_CHANGED","Hồ sơ đã được phiên khác sửa; giữ giao dịch để đối chiếu, không ghi đè.");
        }
        return new{status="READY"};
    }
    public object CheckQaRemoval(JObject request)
    {
        using var lease=Lease();Recover();var state=ReadState();
        if(request.Value<int?>("revision")!=state.Revision)throw new CadException("RESOURCE_CHANGED","Kho đã thay đổi. Làm mới trước khi xóa/gộp.");
        var ids=new HashSet<string>((request["ids"] as JArray??new()).Values<string>()!);
        var removed=state.Items.Where(i=>ids.Contains(i.Id)).ToList();
        string target=request.Value<string>("target")??"";
        if(target!=""&&!state.Items.Any(i=>i.Id==target&&i.Kind=="hatches")||target!=""&&removed.Any(i=>i.Kind!="hatches"||i.Id==target))throw new CadException("INVALID_TARGET","Hatch đích không hợp lệ.");
        DemandRemoval(removed,state.Items.Where(i=>!ids.Contains(i.Id)).ToList(),target==""?null:ids);
        return new{status="READY",revision=state.Revision};
    }
    public object RemapQaConsumers(JObject request)
    {
        using var lease=Lease();Recover();var state=ReadState();string target=request.Value<string>("target")??"";
        if(!state.Items.Any(i=>i.Id==target&&i.Kind=="hatches"))throw new CadException("INVALID_TARGET","Hatch đích không còn trong kho.");
        var ids=new HashSet<string>((request["ids"] as JArray??new()).Values<string>()!);
        if(state.Items.Any(i=>ids.Contains(i.Id)&&i.Kind!="hatches")||ids.Contains(target))throw new CadException("INVALID_TARGET","Chỉ chuyển tham chiếu của hatch nguồn sang hatch đích.");
        string operation=Json.Id(request.Value<string>("operation")??""),backup=Path.Combine(Warehouse,"Versions","qa-mutations",operation,"consumers",Json.Name(target));
        string plan=Path.Combine(backup,"remap-plan.json");var mapping=new JObject{["target"]=target,["ids"]=JArray.FromObject(ids.OrderBy(i=>i))};
        if(File.Exists(plan)){if(Json.Hash(Json.Read<JObject>(File.ReadAllText(plan)))!=Json.Hash(mapping))throw new CadException("RESOURCE_CHANGED","Kế hoạch chuyển tham chiếu đã thay đổi.");}
        else{if(ids.Any(id=>!state.Items.Any(i=>i.Id==id&&i.Kind=="hatches")))throw new CadException("INVALID_TARGET","Hatch nguồn không còn trong kho.");Directory.CreateDirectory(backup);Json.Atomic(plan,mapping);}
        string root=Path.Combine(Warehouse,"Catalog","addin-settings");int count=0;
        if(Directory.Exists(root))foreach(string file in Directory.GetFiles(root,"*.json"))
        {
            var settings=Json.Read<JObject>(File.ReadAllText(file));var values=settings.Descendants().OfType<JValue>().Where(v=>v.Type==JTokenType.String&&ids.Contains(v.Value<string>()!)).ToList();if(values.Count==0)continue;
            Directory.CreateDirectory(backup);string copy=Path.Combine(backup,Path.GetFileName(file));if(!File.Exists(copy))File.Copy(file,copy);
            else if(Json.Hash(Json.Read<JObject>(File.ReadAllText(copy)))!=Json.Hash(settings))throw new CadException("RESOURCE_CHANGED","Cấu hình add-in đã đổi sau lúc chuyển tham chiếu: "+Path.GetFileName(file));
            foreach(var value in values)value.Value=target;Json.Atomic(file,settings);count++;
        }
        return new{status="REMAPPED",count};
    }
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
                    if(request.Value<bool?>("mirrorMetadata")!=true){item.Purpose = change.App.Purpose; item.Notes = change.App.Notes;}
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
