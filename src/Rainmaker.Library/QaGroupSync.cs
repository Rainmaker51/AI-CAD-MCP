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
    async Task SyncGroup(JObject request)
    {
        var before=state["groups"]!.ToObject<List<LibraryGroup>>()!;var planned=state["groups"]!.ToObject<List<LibraryGroup>>()!;
        string id=request.Value<string>("id")??"",parent=request.Value<string>("parentId")??"",name=request.Value<string>("name")??"";
        bool remove=request.Value<string>("action")=="remove";
        string Path(List<LibraryGroup> groups,string key)
        {
            var names=new List<string>();var seen=new HashSet<string>();
            while(key!=""){if(!seen.Add(key))throw new InvalidOperationException("Nhóm cha tạo vòng lặp.");var g=groups.Single(g=>g.Id==key);names.Insert(0,g.Name);key=g.ParentId;}
            return string.Join(" / ",names);
        }
        if(remove)
        {
            var old=planned.Single(g=>g.Id==id);foreach(var child in planned.Where(g=>g.ParentId==id))child.ParentId=old.ParentId;planned.Remove(old);
        }
        else
        {
            if(string.IsNullOrWhiteSpace(name)||name.Contains("/"))throw new InvalidOperationException("Tên nhóm không rỗng và không chứa dấu /.");
            if(planned.Any(g=>g.Id!=id&&g.ParentId==parent&&g.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Nhóm đã tồn tại trong nhóm cha.");
            var target=id==""?new LibraryGroup{Id=Guid.NewGuid().ToString("N")}:planned.Single(g=>g.Id==id);target.Name=name;target.ParentId=parent;if(id=="")planned.Add(target);
        }
        foreach(var g in planned)Path(planned,g.Id);
        var items=NativeSelection(Rows.Where(r=>r["item"] is JObject&&r.Value<string>("liveStatus") is not ("MISSING" or "DEFINITION_ONLY")));
        foreach(var item in items)
        {
            var row=Rows.First(r=>r.Value<string>("id")==item.Id||r.Value<string>("name")==item.Name);string key=row["management"]!.Value<string>("GroupId")??"";
            item.Group=remove&&key==id?"":Path(planned,key);
        }
        await MutateAndPublish("group",(db,tx)=>
        {
            foreach(var item in items)LibrarySyncNative.SetResourceMetadata(db,tx,item);
            var nodes=LibraryFrames.Nodes(db,tx);
            foreach(var old in before)
            {
                var node=nodes.SingleOrDefault(n=>n.Kind==kind&&n.Path==Path(before,old.Id));if(node==null)continue;
                if(remove&&old.Id==id)nodes.Remove(node);else node.Path=Path(planned,old.Id);
            }
            foreach(var g in planned)LibraryFrames.Ensure(nodes,kind,Path(planned,g.Id));LibraryFrames.Save(db,tx,nodes);
            LibrarySyncNative.ArrangeExistingDrawing(db,tx,items,kind);
        },groupAction:request);
    }
}
