using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

public partial class LibraryRepository
{
    public static string SnapshotSignature(List<LibrarySyncItem> items,JObject presence)=>AICad.Library.LibraryLiveSignature.Compute(items,presence);
    public JObject LiveCapture(string id)
    {
        string job=Job(id);
        if(!File.Exists(Path.Combine(job,".qa-native-snapshot")))throw new CadException("QA_SNAPSHOT","Không có bản đọc CAD của QA.");
        var capture=Json.Read<JObject>(File.ReadAllText(Path.Combine(job,"snapshot.json")));
        if(!string.Equals(Path.GetFullPath(capture.Value<string>("source")!),Path.GetFullPath(Source),StringComparison.OrdinalIgnoreCase)||capture.Value<string>("id")!=id||Json.FileHash(Path.Combine(job,"source.dwg"))!=capture.Value<string>("snapshotHash"))throw new CadException("QA_SNAPSHOT","Nguồn hoặc bản đọc CAD đã thay đổi.");
        var items=capture["items"]!.ToObject<List<LibrarySyncItem>>()!;
        if(items.Select(i=>i.Id).Distinct().Count()!=items.Count||items.Any(i=>!LibrarySyncRules.Kinds.Contains(i.Kind)))throw new CadException("QA_SNAPSHOT","ID hoặc loại tài nguyên CAD không hợp lệ.");
        foreach(var item in items)Json.Id(item.Id);
        var presence=(JObject)capture["presence"]!;
        string signature=capture.Value<int?>("signatureFormat")==2?AICad.Library.LibraryLiveSignature.Compute(items,presence):Json.Hash(new{items=items.OrderBy(i=>i.Id).Select(i=>new{i.Id,i.Kind,i.Name,i.Group,i.Purpose,i.Notes,i.Values}),presence=new JObject(presence.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new JProperty(p.Name,p.Value.DeepClone())))});
        bool valid=signature==capture.Value<string>("signature");
        if(!valid&&capture["signatureFormat"]==null)valid=AICad.Library.LibraryLiveSignature.Framework150(items,presence)==capture.Value<string>("signature");
        if(!valid)throw new CadException("QA_SNAPSHOT","Thông số bản đọc CAD đã thay đổi.");
        return capture;
    }
    public JObject RegisterLive(string id)
    {
        using var lease=Lease();Recover();var capture=LiveCapture(id);var state=ReadState();
        var native=capture["items"]!.ToObject<List<LibrarySyncItem>>()!;
        var differences=LibrarySyncRules.Compare(state,native);
        foreach(var change in differences.Where(d=>d.Cad?.Kind=="blocks"&&d.App!=null))
            if(capture["presence"]?[change.Id]?.Value<bool?>("samplePresent")==false){change.Status="CAD_SAMPLE_MISSING";change.Suggested="remove";}
        foreach(var change in differences.Where(d=>d.Cad?.Kind=="blocks"&&d.App==null))
            if(capture["presence"]?[change.Id]?.Value<bool?>("samplePresent")==false){change.Status="CAD_DEFINITION_ONLY";change.Suggested="keep";}
        Json.Atomic(Path.Combine(Job(id),"live-review.json"),new{appHash=AppHash(),signature=capture.Value<string>("signature"),differences});
        return new JObject{["id"]=id,["source"]=Source,["signature"]=capture["signature"]!.DeepClone(),["differences"]=JArray.FromObject(differences)};
    }
    public object PrepareLiveApproval(JObject request)
    {
        using var lease=Lease();Recover();string reviewed=request.Value<string>("reviewId")??"",saved=request.Value<string>("savedId")??"";
        var before=LiveCapture(reviewed);var current=LiveCapture(saved);
        var review=Json.Read<JObject>(File.ReadAllText(Path.Combine(Job(reviewed),"live-review.json")));
        string Content(JObject c)=>AICad.Library.LibraryLiveSignature.Compute(c["items"]!.ToObject<List<LibrarySyncItem>>()!,(JObject)c["presence"]!);
        if(review.Value<string>("appHash")!=AppHash()||Content(before)!=Content(current))throw new CadException("RESOURCE_CHANGED","CAD hoặc hồ sơ đã đổi sau lúc xem. Làm mới và xem lại thay đổi trước khi lưu danh mục.");
        if(current.Value<string>("readMode")!="SAVED_FILE"||CadSourceFile.Hash(Source)!=current.Value<string>("snapshotHash"))throw new CadException("SOURCE_CHANGED","Lưu đúng DWG nguồn rồi đọc kiểm tra lại.");
        Json.Atomic(Path.Combine(Job(saved),"review.json"),new{sourceHash=current.Value<string>("snapshotHash"),appHash=AppHash(),native=current["items"],presence=current["presence"]});
        return new{id=saved};
    }
}
