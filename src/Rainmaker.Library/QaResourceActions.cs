using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Newtonsoft.Json.Linq;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Library;

internal sealed partial class QaBrowserWindow
{
    string PendingFile=>Path.Combine(WarehousePaths.Resolve(client!.Config.ContentRoot),"Catalog","qa-transactions","ISO_QUANGANH.pending.json");
    List<LibrarySyncItem> NativeSelection(IEnumerable<JObject> rows)
    {
        var native=capture!["items"]!.ToObject<List<LibrarySyncItem>>()!;
        return rows.Select(r=>native.SingleOrDefault(i=>i.Id==r.Value<string>("id"))??
            native.FirstOrDefault(i=>i.Kind==kind&&string.Equals(i.Name,r.Value<string>("name"),StringComparison.OrdinalIgnoreCase))??r["item"]?.ToObject<LibrarySyncItem>())
            .Where(i=>i!=null).Cast<LibrarySyncItem>().GroupBy(i=>i.Id).Select(g=>g.First()).ToList();
    }
    async Task CheckRemoval(List<LibrarySyncItem> items,string target="")
    {
        var sync=await client!.Call("api/library-sync",null,lifetime.Token);
        // Every legacy usage profile sharing a native definition is part of deletion.
        var removed=hints.Where(i=>items.Any(s=>s.Id==i.Id||s.Kind==i.Kind&&s.Kind=="blocks"&&string.Equals(s.Name,i.Name,StringComparison.OrdinalIgnoreCase))).ToList();
        await Api("mutation-check",new(){["revision"]=(sync["Revision"]??sync["revision"])!.DeepClone(),["ids"]=JArray.FromObject(removed.Select(i=>i.Id)),["target"]=target});
    }
    async Task DeleteSelectedResources()
    {
        DemandLive();var rows=SelectedRows.ToList();if(rows.Count==0)throw new InvalidOperationException("Chọn mục cần xóa trong danh sách QA.");
        var items=NativeSelection(rows);if(items.Count==0)throw new InvalidOperationException("Không tìm thấy hồ sơ đã chọn.");
        await CheckRemoval(items);
        QaCadEditor.Open(Source);QaCadEditor.DemandSaved(Source);
        var doc=Commands.Document;List<string> locations;
        using(var locked=doc.LockDocument())using(var tx=doc.Database.TransactionManager.StartOpenCloseTransaction())locations=LibrarySyncNative.DeletionLocations(doc.Database,tx,items);
        string message="Xóa khỏi DWG nguồn và danh mục kho:\n"+string.Join("\n",items.Select(i=>i.Name))+"\n\n"+locations.Count+" đối tượng trong Model / layout / block lồng.\n"+string.Join("\n",locations.Take(15))+"\nĐịnh nghĩa còn phụ thuộc sẽ chặn toàn bộ lần xóa. Có lưu bản sao trước thao tác.";
        if(MessageBox.Show(this,message,"Xóa khỏi CAD và kho",MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)return;
        await MutateAndPublish("delete",(db,tx)=>LibrarySyncNative.DeleteResources(db,tx,items));
    }
    async Task MergeSelectedHatches(bool fromCad)
    {
        DemandLive();if(kind!="hatches")throw new InvalidOperationException("Chọn tab Hatch để gộp.");
        var selected=NativeSelection(SelectedRows);
        QaCadEditor.Open(Source);QaCadEditor.DemandSaved(Source);var doc=Commands.Document;
        if(fromCad)
        {
            Hide();try
            {
                var result=doc.Editor.GetSelection(new SelectionFilter(new[]{new TypedValue(0,"HATCH")}));
                if(result.Status!=PromptStatus.OK)return;
                using var locked=doc.LockDocument();using var tx=doc.Database.TransactionManager.StartOpenCloseTransaction();
                var native=capture!["items"]!.ToObject<List<LibrarySyncItem>>()!.Where(i=>i.Kind=="hatches").ToList();
                selected=result.Value.GetObjectIds().Select(id=>(Hatch)tx.GetObject(id,OpenMode.ForRead)).SelectMany(h=>native.Where(i=>LibrarySyncNative.MatchesHatch(h,i,tx))).GroupBy(i=>i.Id).Select(g=>g.First()).ToList();
            }finally{Show();Activate();}
        }
        if(selected.Count==0)throw new InvalidOperationException("Chọn các cấu hình hatch nguồn trên danh sách hoặc trên CAD.");
        var targets=Rows.Where(r=>r.Value<string>("liveStatus") is not ("MISSING" or "DEFINITION_ONLY")&&r.Value<bool?>("published")!=false).ToList();
        var choose=new QaChoice("Giữ mã hatch đích",targets.Select(r=>(r.Value<string>("id")!,r.Value<string>("code")+" · "+r.Value<string>("displayName"))).ToArray(),Selected?.Value<string>("id")??"");
        if(AcApp.ShowModalWindow(choose)!=true)return;
        var target=NativeSelection(targets.Where(r=>r.Value<string>("id")==choose.Choice)).Single();
        selected.RemoveAll(i=>i.Id==target.Id);if(selected.Count==0)throw new InvalidOperationException("Chọn thêm hatch nguồn khác hatch đích.");
        await CheckRemoval(selected,target.Id);
        List<string> locations;using(var locked=doc.LockDocument())using(var tx=doc.Database.TransactionManager.StartOpenCloseTransaction())locations=LibrarySyncNative.HatchLocations(doc.Database,tx,selected);
        var targetRow=targets.Single(r=>r.Value<string>("id")==target.Id);
        string message="Gộp "+selected.Count+" cấu hình / "+locations.Count+" hatch về "+targetRow.Value<string>("code")+".\n"+target.Values.ToString()+"\nToàn bộ hatch tương ứng trong DWG nguồn sẽ thay đổi, giữ vùng biên.\n"+string.Join("\n",locations.Take(15));
        if(MessageBox.Show(this,message,"Gộp hatch trong CAD và kho",MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)return;
        var publishedIds=selected.Where(i=>hints.Any(h=>h.Kind=="hatches"&&h.Id==i.Id)).Select(i=>i.Id).ToList();
        // New CAD-only hatch configurations have no published consumer IDs to remap.
        JObject? remap=publishedIds.Count==0?null:new(){["target"]=target.Id,["ids"]=JArray.FromObject(publishedIds)};
        await MutateAndPublish("merge-hatches",(db,tx)=>LibrarySyncNative.MergeHatches(db,tx,selected,target),consumerRemap:remap);
    }
    async Task PublishSavedDrawing()
    {
        QaCadEditor.DemandSaved(Source);
        var saved=QaCadSnapshot.CaptureSaved(client!.Config,Source,hints);
        try
        {
            var review=await Api("live/register",new(){["id"]=saved["id"]!.DeepClone()});
            var differences=(review["differences"] as JArray??new()).OfType<JObject>().Where(d=>d.Value<string>("Status")!="MATCH"&&d.Value<string>("Status")!="CAD_DEFINITION_ONLY").ToList();
            if(differences.Count==0)return;
            var choices=new JObject();foreach(var d in differences)choices[d.Value<string>("Id")!]=d["Cad"]?.Type==JTokenType.Null||d["Cad"]==null||d.Value<string>("Status")=="CAD_SAMPLE_MISSING"?"remove":"cad";
            var prepared=await Api("live/prepare",new(){["reviewId"]=saved["id"]!.DeepClone(),["savedId"]=saved["id"]!.DeepClone()});
            await Api("approve",new(){["id"]=prepared["id"]!.DeepClone(),["choices"]=choices,["mirrorMetadata"]=true});
        }
        finally{QaCadSnapshot.Discard(client.Config,saved);}
    }
    async Task ApplyMetadataJournal(JObject journal)
    {
        foreach(var original in (journal["metadata"] as JArray??new()).OfType<JObject>())
        {
            var view=await Api(original.Value<string>("kind")!);
            var row=view["items"]!.OfType<JObject>().Single(r=>r.Value<string>("metadataId")==original.Value<string>("metadataId"));
            var request=(JObject)original.DeepClone();request["catalogRevision"]=view["revision"]!.DeepClone();request["revision"]=row["management"]!["Revision"]!.DeepClone();
            await Api("manage",request);
        }
    }
    async Task MirrorNativeMetadata(IEnumerable<string>? scope=null)
    {
        var saved=QaCadSnapshot.CaptureSaved(client!.Config,Source,hints);
        try
        {
        var actual=saved["items"]!.ToObject<List<LibrarySyncItem>>()!;
        foreach(string resourceKind in LibrarySyncRules.Kinds)
        {
            var view=await Api(resourceKind);var groups=view["groups"]!.OfType<JObject>().ToList();
            foreach(var row in view["items"]!.OfType<JObject>().Where(r=>r["item"] is JObject&&(scope==null||scope.Contains(r.Value<string>("id")??""))))
            {
                var published=row["item"]!.ToObject<LibrarySyncItem>()!;
                var item=actual.SingleOrDefault(i=>i.Id==published.Id)??actual.SingleOrDefault(i=>i.Kind==resourceKind&&i.Name==row.Value<string>("name"));
                if(item==null)continue;
                string parent="";
                foreach(string name in item.Group.Split(new[]{" / "},StringSplitOptions.RemoveEmptyEntries))
                {
                    var g=groups.SingleOrDefault(g=>g.Value<string>("Name")==name&&g.Value<string>("ParentId")==parent);
                    if(g==null){g=await Api("group",new(){["kind"]=resourceKind,["name"]=name,["parentId"]=parent});groups.Add(g);}parent=g.Value<string>("Id")!;
                }
                var m=(JObject)row["management"]!;string alias=item.Values.Value<string>("qaDisplayName")??row.Value<string>("displayName")??item.Name;
                if(m.Value<string>("Purpose")==item.Purpose&&m.Value<string>("Notes")==item.Notes&&m.Value<string>("GroupId")==parent&&row.Value<string>("displayName")==alias)continue;
                await Api("manage",new(){["kind"]=resourceKind,["metadataId"]=row["metadataId"]!.DeepClone(),["catalogRevision"]=view["revision"]!.DeepClone(),["revision"]=m["Revision"]!.DeepClone(),["name"]=alias,["purpose"]=item.Purpose,["notes"]=item.Notes,["groupId"]=parent,["reviewed"]=m["Reviewed"]!.DeepClone(),["action"]="update"});
            }
        }
        }
        finally{QaCadSnapshot.Discard(client.Config,saved);}
    }
    async Task ApplyGroupJournal(JObject journal)
    {
        if(journal["consumerRemap"] is JObject remap){remap["operation"]=Path.GetFileName(Path.GetDirectoryName(journal.Value<string>("backup")!));await Api("remap-consumers",remap);}
        if(journal["groupAction"] is not JObject p)return;
        var view=await Api(p.Value<string>("kind")!);var groups=view["groups"]!.OfType<JObject>().ToList();
        string id=p.Value<string>("id")??"";
        if(p.Value<string>("action")=="remove"&&groups.All(g=>g.Value<string>("Id")!=id))return;
        if(id==""&&groups.Any(g=>g.Value<string>("Name")==p.Value<string>("name")&&g.Value<string>("ParentId")==p.Value<string>("parentId")))return;
        await Api("group",p);
    }
    async Task SyncMetadata(List<JObject> requests)
    {
        await Api("metadata-check",new(){["catalogRevision"]=state["revision"]!.DeepClone(),["metadata"]=JArray.FromObject(requests)});
        DemandLive();var native=NativeSelection(Rows.Where(r=>requests.Any(p=>p.Value<string>("metadataId")==r.Value<string>("metadataId"))));
        foreach(var item in native)
        {
            var row=Rows.First(r=>r.Value<string>("id")==item.Id||r.Value<string>("name")==item.Name);
            var request=requests.Single(p=>p.Value<string>("metadataId")==row.Value<string>("metadataId"));
            item.Purpose=request.Value<string>("purpose")??"";item.Notes=request.Value<string>("notes")??"";item.Group=GroupPath(request.Value<string>("groupId")??"");
            item.Values["qaDisplayName"]=request.Value<string>("name")??item.Name;
        }
        await MutateAndPublish("metadata",(db,tx)=>{foreach(var item in native)LibrarySyncNative.SetResourceMetadata(db,tx,item);},JArray.FromObject(requests));
    }
    async Task MutateAndPublish(string operation,Action<Database,Transaction> mutation,JArray? metadata=null,JObject? groupAction=null,JObject? consumerRemap=null)
    {
        if(operation is not ("metadata" or "group" or "arrange"))throw new InvalidOperationException("Thêm/sửa/xóa tài nguyên thực hiện trên CAD. Lưu nguồn rồi Quét thay đổi.");
        DemandLive();if(File.Exists(PendingFile))throw new InvalidOperationException("Có thao tác chưa hoàn tất. Nhấn Hoàn tất đồng bộ trước khi sửa tiếp.");
        Directory.CreateDirectory(Path.GetDirectoryName(PendingFile)!);
        using var operationLock=new FileStream(PendingFile+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        QaCadEditor.Open(Source);QaCadEditor.DemandSaved(Source);
        // Refuse a stale selection before touching the user's drawing.
        var fresh=QaCadSnapshot.Capture(client!.Config,Source,hints);
        try{if(fresh.Value<string>("signature")!=capture!.Value<string>("signature"))throw new InvalidOperationException("CAD đã thay đổi. Làm mới rồi chọn lại trước khi thực hiện.");}
        finally{QaCadSnapshot.Discard(client.Config,fresh);}
        var existingReview=await Api("live/register",new(){["id"]=capture!["id"]!.DeepClone()});
        var existingChanges=existingReview["differences"]!.OfType<JObject>().Where(d=>d.Value<string>("Status") is not ("MATCH" or "CAD_DEFINITION_ONLY")).ToList();
        if(existingChanges.Count>0)throw new InvalidOperationException("Có thay đổi CAD chưa được duyệt. Quét và xác nhận trước khi sửa thông tin quản lý hoặc xếp gọn.");
        var missing=existingChanges.Where(d=>d["App"] is JObject&&(d["Cad"]==null||d["Cad"]!.Type==JTokenType.Null||d.Value<string>("Status")=="CAD_SAMPLE_MISSING")).Select(d=>d["App"]!.ToObject<LibrarySyncItem>()!).ToList();await CheckRemoval(missing);
        if(existingChanges.Count>0&&MessageBox.Show(this,"Lần cập nhật này cũng sẽ đưa "+existingChanges.Count+" thay đổi đang chờ của DWG vào kho:\n"+string.Join("\n",existingChanges.Take(20).Select(d=>d.Value<string>("Status")+" · "+(d["Cad"] is JObject cad?cad:d["App"])?["Name"])),"Các thay đổi cùng cập nhật",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
        string folder=Path.Combine(WarehousePaths.Resolve(client.Config.ContentRoot),"Versions","qa-mutations",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string backup=Path.Combine(folder,"before.dwg");QaCadSnapshot.CopyShared(Source,backup);
        var journal=new JObject{["source"]=Source,["sourceId"]="ISO_QUANGANH",["operation"]=operation,["backup"]=backup,["backupRelative"]="Versions/qa-mutations/"+Path.GetFileName(folder)+"/before.dwg",["metadata"]=metadata??new JArray(),["groupAction"]=groupAction,["consumerRemap"]=consumerRemap,["phase"]="PREPARED",["createdUtc"]=DateTime.UtcNow};JsonUtil.Atomic(PendingFile,journal);
        bool committed=false;
        try
        {
            var doc=Commands.Document;using(var locked=doc.LockDocument())
            {
                using(var tx=doc.Database.TransactionManager.StartTransaction()){mutation(doc.Database,tx);tx.Commit();committed=true;}
                var intended=QaCadSnapshot.Capture(client.Config,Source,hints);
                try{journal["expectedSignature"]=intended["signature"]!.DeepClone();JsonUtil.Atomic(PendingFile,journal);}finally{QaCadSnapshot.Discard(client.Config,intended);}
                doc.Database.SaveAs(Source,true,DwgVersion.Current,doc.Database.SecurityParameters);
                doc.Editor.Regen();
            }
            journal["phase"]="CAD_SAVED";journal["sourceHash"]=JsonUtil.FileHash(Source);JsonUtil.Atomic(PendingFile,journal);
            await Api("metadata-check",new(){["metadata"]=journal["metadata"]!.DeepClone()});await ApplyGroupJournal(journal);await PublishSavedDrawing();await MirrorNativeMetadata();await ApplyMetadataJournal(journal);journal["phase"]="COMPLETE";JsonUtil.Atomic(Path.Combine(folder,"result.json"),journal);File.Delete(PendingFile);
            await RefreshLive();status.Text="Đã cập nhật DWG nguồn và danh mục kho. Bản sao trước thao tác: "+backup;
        }
        catch(Exception error)
        {
            if(!committed)File.Delete(PendingFile);
            throw new InvalidOperationException(committed?"CHƯA ĐỒNG BỘ: CAD đã thay đổi; kho chưa được xác nhận. Lưu nguồn rồi nhấn Hoàn tất đồng bộ. Bản sao: "+backup+". "+error.Message:error.Message,error);
        }
    }
    async Task CompletePendingSync()
    {
        if(File.Exists(PendingFile))
        {
            using var operationLock=new FileStream(PendingFile+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            var pending=JObject.Parse(File.ReadAllText(PendingFile));
            if(pending.Value<string>("sourceId")!="ISO_QUANGANH")throw new InvalidOperationException("Thao tác chờ thuộc DWG nguồn khác.");
            string relative=pending.Value<string>("backupRelative")??"";
            if(!System.Text.RegularExpressions.Regex.IsMatch(relative,"^Versions/qa-mutations/[a-f0-9]{32}/before\\.dwg$"))throw new InvalidOperationException("Đường dẫn bản sao của giao dịch không hợp lệ.");
            pending["backup"]=Path.Combine(WarehousePaths.Resolve(client!.Config.ContentRoot),relative.Replace('/',Path.DirectorySeparatorChar));
            QaCadEditor.Open(Source);QaCadEditor.DemandSaved(Source);
            if(pending.Value<string>("sourceHash") is string hash&&hash!=JsonUtil.FileHash(Source))throw new InvalidOperationException("DWG đã thay đổi sau thao tác chờ. Không tự tiếp tục trên bản khác; giữ bản sao để đối chiếu.");
            if(pending.Value<string>("sourceHash")==null)
            {
                if(pending.Value<string>("expectedSignature") is not string expected)throw new InvalidOperationException("Thao tác dừng trước khi ghi nhận kết quả CAD. Giữ bản sao để đối chiếu; chưa xác nhận đồng bộ.");
                var actual=QaCadSnapshot.CaptureSaved(client.Config,Source,hints);
                try{if(actual.Value<string>("signature")!=expected)throw new InvalidOperationException("DWG đã lưu chưa đúng nội dung thao tác chờ. Chưa thể hoàn tất đồng bộ.");}finally{QaCadSnapshot.Discard(client.Config,actual);}
            }
            await Api("metadata-check",new(){["metadata"]=pending["metadata"]!.DeepClone()});await ApplyGroupJournal(pending);await PublishSavedDrawing();await MirrorNativeMetadata();await ApplyMetadataJournal(pending);pending["phase"]="COMPLETE";JsonUtil.Atomic(Path.Combine(Path.GetDirectoryName(pending.Value<string>("backup")!)!,"result.json"),pending);File.Delete(PendingFile);
        }
        else
        {
            await ScanCadChanges();return;
        }
        await RefreshLive();status.Text="Đã đối chiếu và cập nhật danh mục theo DWG đã lưu.";
    }
}
