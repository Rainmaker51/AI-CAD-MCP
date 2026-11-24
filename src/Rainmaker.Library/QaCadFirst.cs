using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AICad.Application;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;
internal sealed partial class QaBrowserWindow
{
    internal Action<string>? PublicationProgress;
    void PublishProgress(string step){status.Text=step switch{"VALIDATING_PLAN"=>"Đang kiểm tra kế hoạch đã duyệt…","SOURCE_SAVED"=>"Đã lưu phần trình bày CAD; đang cập nhật kho…","PUBLICATION_COMPLETE"=>"Đã đối chiếu các mục đã chọn.","REFRESHING"=>"Đang đọc lại thư viện…",_=>"Đang cập nhật thư viện…"};PublicationProgress?.Invoke(step);}
    QaUpdateJournal Journal=>new(client!.Config);
    async Task ScanCadChanges()
    {
        if(Journal.Read()!=null){await ShowRecovery();return;}
        QaCadEditor.Open(Source);QaCadEditor.DemandSaved(Source);
        var sync=await client!.Call("api/library-sync",null,lifetime.Token);hints=sync["items"]!.ToObject<List<LibrarySyncItem>>()!;
        var saved=QaCadSnapshot.CaptureSaved(client.Config,Source,hints);
        try
        {
            var review=await Api("live/register",new(){["id"]=saved["id"]!.DeepClone()});
            var groups=new Dictionary<string,string>();var labels=new JObject();
            foreach(string k in LibrarySyncRules.Kinds)
            {
                var view=await Api(k);var tree=view["groups"]!.ToObject<List<LibraryGroup>>()!;
                string PathFor(string id){var names=new List<string>();var seen=new HashSet<string>();while(id!=""&&seen.Add(id)){var g=tree.SingleOrDefault(x=>x.Id==id);if(g==null)break;names.Insert(0,g.Name);id=g.ParentId;}return string.Join(" / ",names);}
                foreach(var row in view["items"]!.OfType<JObject>())
                {
                    string id=row.Value<string>("id")??"";if(id==""||labels[id]!=null)continue;
                    labels[id]=row["management"]!.DeepClone();groups[id]=PathFor(row["management"]!.Value<string>("GroupId")??"");
                }
            }
            var dialog=new QaCadReview(review,groups);if(AcApp.ShowModalWindow(dialog)!=true){status.Text="Đã quét. Chưa cập nhật kho hoặc trình bày.";return;}
            var plan=dialog.Plan;plan["sourceHash"]=saved["snapshotHash"]!.DeepClone();plan["appHash"]=review["appHash"]!.DeepClone();plan["labels"]=labels;
            await ApplyCadPlan(plan);
        }
        finally{QaCadSnapshot.Discard(client.Config,saved);}
    }
    static string EffectiveId(JObject row)=>row.Value<string>("bindId") is string bind&&bind!=""?bind:row.Value<string>("id")!;
    async Task ApplyCadPlan(JObject plan)
    {
        PublishProgress("VALIDATING_PLAN");
        using var operation=Journal.Lock();PublishProgress("LOCK_ACQUIRED");QaCadEditor.Open(Source);PublishProgress("SOURCE_OPEN");QaCadEditor.DemandSaved(Source);
        PublishProgress("SOURCE_VALIDATED");
        if(QaUpdateJournal.SharedHash(Source)!=plan.Value<string>("sourceHash"))throw new CadException("SOURCE_CHANGED","DWG đã đổi sau khi xem. Quét lại trước khi áp dụng.");
        await Api("validate-view",new(){["appHash"]=plan["appHash"]!.DeepClone()});
        var rows=plan["rows"]!.OfType<JObject>().ToList();
        var removals=rows.Where(r=>r.Value<string>("action")=="remove"&&r["app"] is JObject).Select(r=>r["app"]!.ToObject<LibrarySyncItem>()!).ToList();await CheckRemoval(removals);
        foreach(var merge in rows.Where(r=>r.Value<string>("mergeId")!=""))
            await CheckRemoval(hints.Where(i=>i.Id==merge.Value<string>("id")).ToList(),merge.Value<string>("mergeId")!);
        var record=Journal.Begin(plan);
        PublishProgress("PREPARED");
        try
        {
            var doc=Commands.Document;using(var locked=doc.LockDocument())
            {
                using(var tx=doc.Database.TransactionManager.StartTransaction())
                {
                    var native=LibrarySyncNative.ReadDrawing(doc.Database,tx,hints);
                    foreach(var row in rows.Where(r=>r.Value<string>("bindId")!=""))
                    {var actual=native.Single(i=>i.Id==row.Value<string>("id"));LibrarySyncNative.BindCadIdentity(doc.Database,tx,actual,row.Value<string>("bindId")!);}
                    native=LibrarySyncNative.ReadDrawing(doc.Database,tx,hints);
                    foreach(var row in rows.Where(r=>r.Value<string>("mergeId")!=""))
                    {var from=native.Single(i=>i.Id==EffectiveId(row));var to=native.Single(i=>i.Id==row.Value<string>("mergeId"));LibrarySyncNative.ConsolidateCadHatchIdentity(doc.Database,tx,from,to);}
                    native=LibrarySyncNative.ReadDrawing(doc.Database,tx,hints);
                    var selected=new List<LibrarySyncItem>();var newIds=new HashSet<string>();
                    foreach(var row in rows.Where(r=>r.Value<string>("action")=="cad"&&r.Value<string>("mergeId")==""))
                    {
                        var item=native.Single(i=>i.Id==EffectiveId(row));item.Group=row.Value<string>("group")??"";
                        var label=plan["labels"]?[item.Id] as JObject;
                        if(label!=null){item.Purpose=label.Value<string>("Purpose")??"";item.Notes=label.Value<string>("Notes")??"";string alias=label.Value<string>("Name")??"";string before=row["app"]?.Value<string>("Name")??item.Name;item.Values["qaDisplayName"]=alias==""||alias==before?item.Name:alias;}
                        if(row.Value<bool?>("new")==true)newIds.Add(item.Id);selected.Add(item);
                    }
                    // Include destinations explicitly, so only identity/group/labels change
                    // after a user has already made native hatch configurations equal.
                    foreach(string target in rows.Select(r=>r.Value<string>("mergeId")??"").Where(s=>s!="").Distinct())if(selected.All(i=>i.Id!=target))selected.Add(native.Single(i=>i.Id==target));
                    LibrarySyncNative.PresentFromCad(doc.Database,tx,selected,newIds,hints);
                    LibrarySyncNative.RemoveOrphanPresentation(doc.Database,tx,removals.Select(i=>i.Id));tx.Commit();
                }
                PublishProgress("PRESENTATION_COMMITTED");doc.Database.SaveAs(Source,true,DwgVersion.Current,doc.Database.SecurityParameters);PublishProgress("SOURCE_SAVED");doc.Editor.Regen();
            }
            record["afterHash"]=QaUpdateJournal.SharedHash(Source);record["phase"]="CAD_SAVED";Journal.Save(record);
            await FinishCadPlan(record);
        }
        catch(Exception e){throw new InvalidOperationException("Cập nhật chưa hoàn tất. Nhấn Xử lý cập nhật để tiếp tục hoặc giữ CAD hiện tại và quét lại. "+e.Message,e);}
    }
    async Task FinishCadPlan(JObject record)
    {
        if(!Journal.CanContinue(record))throw new CadException("SOURCE_CHANGED","DWG hiện tại khác bản đã lưu của kế hoạch. Chọn giữ CAD hiện tại và quét lại.");
        var plan=(JObject)record["plan"]!;var rows=plan["rows"]!.OfType<JObject>().ToList();
        var saved=QaCadSnapshot.CaptureSaved(client!.Config,Source,hints);
        try
        {
            PublishProgress("SAVED_SNAPSHOT_READ");var review=await Api("live/register",new(){["id"]=saved["id"]!.DeepClone()});var differences=review["differences"]!.OfType<JObject>().ToList();var choices=new JObject();
            foreach(var row in rows)
            {
                string id=EffectiveId(row),merge=row.Value<string>("mergeId")??"";
                if(merge!="")
                {if(differences.Any(d=>d.Value<string>("Id")==id&&d["App"] is JObject&&d["Cad"] is not JObject))choices[id]="remove";choices[merge]="cad";}
                else if(row.Value<string>("action")=="remove")
                {if(differences.Any(d=>d.Value<string>("Id")==id&&d["App"] is JObject))choices[id]="remove";}
                else choices[id]="cad";
            }
            bool applied=choices.Properties().All(p=>p.Value.Value<string>()=="remove"?differences.All(d=>d.Value<string>("Id")!=p.Name||d["App"] is not JObject):differences.Any(d=>d.Value<string>("Id")==p.Name&&d.Value<string>("Status")=="MATCH"));
            if(!applied&&review.Value<string>("appHash")!=plan.Value<string>("appHash"))throw new CadException("RESOURCE_CHANGED","Kho đã thay đổi sau khi xem kế hoạch. Giữ CAD và quét lại để đối chiếu, không ghi đè phiên khác.");
            foreach(var group in rows.Where(r=>r.Value<string>("mergeId")!=""&&r["app"] is JObject).GroupBy(r=>r.Value<string>("mergeId")!))
                await Api("remap-consumers",new(){["target"]=group.Key,["ids"]=JArray.FromObject(group.Select(r=>r.Value<string>("id"))),["operation"]=Path.GetFileName(Journal.Folder(record))});
            record["phase"]="CONSUMERS_CHECKED";Journal.Save(record);
            if(choices.HasValues&&!applied)
            {
                var prepared=await Api("live/prepare",new(){["reviewId"]=saved["id"]!.DeepClone(),["savedId"]=saved["id"]!.DeepClone()});
                await Api("approve",new(){["id"]=prepared["id"]!.DeepClone(),["choices"]=choices,["mirrorMetadata"]=true});
            }
            record["phase"]="CATALOG_PUBLISHED";Journal.Save(record);await MirrorNativeMetadata(choices.Properties().Where(p=>p.Value.Value<string>()=="cad").Select(p=>p.Name).ToArray());record["phase"]="METADATA_UPDATED";Journal.Save(record);
            var verification=await Api("live/register",new(){["id"]=saved["id"]!.DeepClone()});var verified=verification["differences"]!.OfType<JObject>().ToList();
            foreach(var choice in choices.Properties())
            {
                var difference=verified.SingleOrDefault(d=>d.Value<string>("Id")==choice.Name);
                if(choice.Value.Value<string>()=="cad"?(difference?.Value<string>("Status")!="MATCH"):(difference?["App"] is JObject))
                    throw new CadException("QA_VERIFY","Mục đã chọn chưa khớp sau khi xuất: "+choice.Name+". Giữ hàng chờ để kiểm tra, chưa báo hoàn tất.");
            }
            // Verify the exact saved bytes again. No clone/signature is used as proof
            // that the user's source has been saved successfully.
            if(QaUpdateJournal.SharedHash(Source)!=saved.Value<string>("snapshotHash"))throw new CadException("SOURCE_CHANGED","Nguồn đổi trong khi xuất danh mục. Chưa xác nhận hoàn tất.");
            Journal.Complete(record);PublishProgress("PUBLICATION_COMPLETE");
        }
        finally{QaCadSnapshot.Discard(client.Config,saved);}
        PublishProgress("REFRESHING");await RefreshLive();PublishProgress("REFRESHED");status.Text="Đã nhận các mục đã chọn từ CAD, lưu kho và trình bày. Mục chưa chọn vẫn chờ đối chiếu: "+plan.Value<int>("deferred")+".";
    }
    async Task ShowRecovery()
    {
        if(client==null)return;var record=Journal.Read();if(record==null){await ScanCadChanges();return;}
        var window=new Window{Title="Xử lý cập nhật chưa hoàn tất",Width=780,Height=590,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Resources=Theme.Load()};window.SetResourceReference(BackgroundProperty,"ToolBackground");window.SetResourceReference(ForegroundProperty,"ToolText");
        var panel=new StackPanel{Margin=new Thickness(20)};window.Content=new ScrollViewer{Content=panel};
        panel.Children.Add(new TextBlock{Text="Thao tác: "+record.Value<string>("operation")+"\nThời điểm: "+record["createdUtc"]+"\nBước đã ghi: "+record.Value<string>("phase")+"\nNguồn: "+Source+"\nBản sao: "+Journal.Folder(record),TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock{Text="Giữ CAD hiện tại sẽ sao lưu bản đã lưu và chụp trạng thái kho hiện hành, kể cả khi đã áp dụng một phần. Không khôi phục DWG cũ hoặc hoàn tác các liên kết đã đổi. Sau đó quét lại để bạn duyệt kế hoạch mới.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,16)});
        string decision="";
        void Choice(string text,string value,bool enabled=true){var b=new Button{Content=text,IsEnabled=enabled,Margin=new Thickness(0,6,0,0),Padding=new Thickness(10)};b.Click+=(_,_)=>{decision=value;window.DialogResult=true;};panel.Children.Add(b);}
        Choice("Tiếp tục cập nhật — chỉ khi DWG còn đúng bản đã ghi","continue",Journal.CanContinue(record));
        Choice("Giữ CAD hiện tại, hủy kế hoạch cũ và quét lại","rescan");Choice("Xem chi tiết lỗi / journal / bản sao","details");Choice("Đóng","close");
        if(AcApp.ShowModalWindow(window)!=true||decision=="close")return;
        if(decision=="details"){MessageBox.Show(this,record.ToString()+"\n\nBản sao: "+Journal.Folder(record),"Thông tin phục hồi");return;}
        QaCadEditor.Open(Source);QaCadEditor.DemandSaved(Source);
        if(decision=="continue")
        {
            if(record.Value<string>("operation")=="cad-publish"){using var locked=Journal.Lock();await FinishCadPlan(record);}
            else await CompletePendingSync();
        }
        else
        {
            using(var locked=Journal.Lock())
            {
                var checkpoint=await client.Call("api/library-sync",null,lifetime.Token);
                string recovery=Journal.AbandonKeepingCad(checkpoint);status.Text="Đã giữ CAD và ghi hồ sơ hủy kế hoạch: "+recovery;
            }
            await RefreshLive();await ScanCadChanges();
        }
    }
    async Task RemoveMissingProfiles()
    {
        DemandLive();if(SelectedRows.Count==0)throw new InvalidOperationException("Chọn hồ sơ đã mất trong CAD.");
        if(SelectedRows.Any(r=>r.Value<string>("liveStatus")!="MISSING"))throw new InvalidOperationException("Chỉ loại hồ sơ đã mất trong CAD. Với mục chỉ còn định nghĩa, kiểm tra và PURGE trong CAD trước.");
        await ScanCadChanges();
    }
    Task ShowCadUsage()
    {
        DemandLive();var selected=NativeSelection(SelectedRows);QaCadEditor.Open(Source);var doc=Commands.Document;
        using var locked=doc.LockDocument();using var tx=doc.Database.TransactionManager.StartOpenCloseTransaction();
        var locations=LibrarySyncNative.DeletionLocations(doc.Database,tx,selected);
        MessageBox.Show(this,"Thêm/sửa/xóa/thay hatch thực hiện trực tiếp trên CAD.\n"+string.Join("\n",locations.Take(60))+"\n\nBlock chỉ còn định nghĩa: dùng PURGE sau khi kiểm tra tham chiếu lồng/layout. Ctrl+S rồi Quét thay đổi để cập nhật kho.","Vị trí sử dụng / hướng dẫn CAD");return Task.CompletedTask;
    }
}
