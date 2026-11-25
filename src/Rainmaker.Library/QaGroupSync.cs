using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AICad.Contracts;
using AICad.LibraryScanner;
using Newtonsoft.Json.Linq;

namespace Rainmaker.Library;
internal sealed partial class QaBrowserWindow
{
    internal static string TreePath(List<LibraryGroup> groups,string id)
    {
        var names=new List<string>();var seen=new HashSet<string>();
        while(id!=""){if(!seen.Add(id)||seen.Count>15)throw new InvalidOperationException("Nhóm cha tạo vòng lặp hoặc quá sâu.");var g=groups.Single(g=>g.Id==id);names.Insert(0,g.Name);id=g.ParentId;}
        return string.Join(" / ",names);
    }
    internal static JObject PrepareGroupPlan(JObject view,JObject snapshot,JObject request)
    {
        string kind=view.Value<string>("kind")!;var before=view["groups"]!.ToObject<List<LibraryGroup>>()!;var after=view["groups"]!.ToObject<List<LibraryGroup>>()!;
        string id=request.Value<string>("id")??"",parent=request.Value<string>("parentId")??"",name=request.Value<string>("name")??"",target=request.Value<string>("targetId")??"";
        bool remove=request.Value<string>("action")=="remove",create=request.Value<string>("action")=="create"||id=="";
        if(remove)
        {
            var old=after.Single(g=>g.Id==id);
            if(target!=""&&(target==id||LibraryFrames.Under(TreePath(before,target),TreePath(before,id))))throw new InvalidOperationException("Không chuyển vào nhóm đang xóa hoặc nhóm con của nó.");
            foreach(var child in after.Where(g=>g.ParentId==id))child.ParentId=request.Value<string>("childMode")=="branch"?target:old.ParentId;
            after.Remove(old);
        }
        else
        {
            if(id=="")id=Guid.NewGuid().ToString("N");
            var g=create?new LibraryGroup{Id=id}:after.Single(g=>g.Id==id);g.Name=name.Trim();g.ParentId=parent;if(create)after.Add(g);
        }
        foreach(var g in after){if(string.IsNullOrWhiteSpace(g.Name)||g.Name.Contains('/')||g.Name.Length>120||g.Name.Any(char.IsControl))throw new InvalidOperationException("Tên nhóm từ 1–120 ký tự, không chứa dấu / hoặc xuống dòng.");TreePath(after,g.Id);}
        if(after.GroupBy(g=>g.ParentId+"|"+g.Name,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new InvalidOperationException("Tên nhóm trùng trong cùng cấp cha.");
        var map=new Dictionary<string,string>();foreach(var g in before)map[TreePath(before,g.Id)]=remove&&g.Id==id?TreePath(after,target):TreePath(after,g.Id);
        var physical=(snapshot["presence"]?["groups"]?["frames"] as JArray??new()).ToObject<List<LibraryFrameNode>>()!.Where(n=>n.Kind==kind).ToList();
        var registry=(snapshot["presence"]?["groups"]?["registered"] as JArray??new()).ToObject<List<LibraryFrameNode>>()!;
        if(!physical.Any(n=>n.Path==""))throw new InvalidOperationException("Thiếu khung gốc của loại thư viện.");
        var nodes=physical.ToList();
        foreach(var old in before)
        {
            string oldPath=TreePath(before,old.Id);
            var node=physical.SingleOrDefault(n=>n.CatalogGroupId==old.Id)??physical.SingleOrDefault(n=>n.Path==oldPath)??physical.SingleOrDefault(n=>registry.Any(r=>r.Id==n.Id&&r.Kind==kind&&r.Path==oldPath));
            if(node==null)continue;
            if(remove&&old.Id==id){nodes.Remove(node);continue;}
            node.Path=TreePath(after,old.Id);node.Parent=TreePath(after,after.Single(g=>g.Id==old.Id).ParentId);node.CatalogGroupId=old.Id;
        }
        if(create)
        {
            string path=TreePath(after,id),parentPath=TreePath(after,parent);
            if(!nodes.Any(n=>n.Path==parentPath))throw new InvalidOperationException("Khung nhóm cha đã bị xóa. Xử lý nhóm cha trước khi thêm nhóm con.");
            var node=nodes.SingleOrDefault(n=>n.Path==path);
            if(node==null){node=new LibraryFrameNode{Id=request.Value<string>("frameId")??Guid.NewGuid().ToString("N"),Kind=kind,Path=path,Parent=parentPath};nodes.Add(node);}node.CatalogGroupId=id;
        }
        foreach(var node in nodes.Where(n=>n.Path!=""&&n.CatalogGroupId==""))
        {var change=map.Where(p=>p.Key!=p.Value&&LibraryFrames.Under(node.Path,p.Key)).OrderByDescending(p=>p.Key.Length).FirstOrDefault();if(change.Key!=null){string suffix=node.Path.Substring(change.Key.Length);node.Path=(change.Value+suffix).Trim(' ','/');node.Parent=node.Path.Contains(" / ")?node.Path.Substring(0,node.Path.LastIndexOf(" / ",StringComparison.Ordinal)):"";}}
        if(nodes.GroupBy(n=>n.Path,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new InvalidOperationException("Khung CAD trùng đường dẫn nhóm; cần xử lý trước khi đổi nhóm.");
        var labels=new JArray();foreach(var row in (view["items"] as JArray??new()).OfType<JObject>())
        {var m=row["management"]!;string old=m.Value<string>("GroupId")??"",next=remove&&old==id?target:old;if(old==next)continue;labels.Add(new JObject{["metadataId"]=row["metadataId"]!.DeepClone(),["beforeGroupId"]=old,["groupId"]=next,["revision"]=m.Value<int>("Revision")});}
        return new JObject{["kind"]=kind,["beforeGroups"]=JArray.FromObject(before),["afterGroups"]=JArray.FromObject(after),["nodes"]=JArray.FromObject(nodes),["pathMap"]=JObject.FromObject(map),["labelGroups"]=labels,["request"]=request.DeepClone()};
    }
    async Task SyncGroup(JObject request)
    {
        var plan=PrepareGroupPlan(state,capture!,request);
        await MutateAndPublish("group",(db,tx)=>LibrarySyncNative.ApplyGroupLayout(db,tx,plan,hints),groupAction:plan);
        if((request.Value<string>("action")=="create"||request.Value<string>("id")=="")&&System.Diagnostics.Process.GetCurrentProcess().ProcessName.Equals("acad",StringComparison.OrdinalIgnoreCase))ZoomCreatedGroup(plan);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    void ZoomCreatedGroup(JObject plan)
        {
            var doc=Commands.Document;using var tx=doc.Database.TransactionManager.StartOpenCloseTransaction();
            string path=TreePath(plan["afterGroups"]!.ToObject<List<LibraryGroup>>()!,plan["afterGroups"]!.OfType<JObject>().Last().Value<string>("Id")!);
            var frame=LibraryFrames.Model(doc.Database,tx).OfType<Autodesk.AutoCAD.DatabaseServices.BlockReference>().SingleOrDefault(b=>LibraryFrames.Info(b,tx)?.Kind==kind&&LibraryFrames.Info(b,tx)?.Path==path);
            if(frame!=null){using var view=doc.Editor.GetCurrentView();var e=frame.GeometricExtents;view.CenterPoint=new Autodesk.AutoCAD.Geometry.Point2d((e.MinPoint.X+e.MaxPoint.X)/2,(e.MinPoint.Y+e.MaxPoint.Y)/2);view.Width=(e.MaxPoint.X-e.MinPoint.X)*1.2;view.Height=(e.MaxPoint.Y-e.MinPoint.Y)*1.2;doc.Editor.SetCurrentView(view);}
        }
}
