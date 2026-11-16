using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AICad.Contracts;
public sealed class LibraryLabel {public string Name{get;set;}="";public string GroupId{get;set;}="";public DateTime? DeletedUtc{get;set;}public string Purpose{get;set;}="";public string Notes{get;set;}="";public bool Reviewed{get;set;}public int Revision{get;set;}}
public sealed class LibraryGroup {public string Id{get;set;}="";public string Name{get;set;}="";public string ParentId{get;set;}="";}
// Presentation metadata is separate from immutable plan resources.
public sealed class LibraryCatalog(string appRoot){
 string Root=>Path.Combine(WarehousePaths.Resolve(appRoot),"Catalog","management");
 public static string Kind(string kind)=>kind=="skills"||kind=="templates"||kind=="layers"||kind=="blocks"||kind=="hatches"||kind=="texts"||kind=="dims"?kind:throw new CadException("INVALID_KIND",kind);
 string Item(string kind,string id)=>Path.Combine(Root,Kind(kind),Json.Name(id)+".json");
 public LibraryLabel Label(string kind,string id){string p=Item(kind,id);return File.Exists(p)?Json.Read<LibraryLabel>(File.ReadAllText(p)):new();}
 public string Name(string kind,string id,string fallback){string name=Label(kind,id).Name;return name==""?fallback:name;}
 public bool Deleted(string kind,string id)=>Label(kind,id).DeletedUtc.HasValue;
 public void Available(string kind,string id){if(Deleted(kind,id))throw new CadException("RESOURCE_DELETED","Mục đã ở thùng rác; khôi phục trước khi sử dụng: "+id);}
 public List<LibraryGroup> Groups(string kind){string p=Path.Combine(Root,Kind(kind)+"-groups.json");return File.Exists(p)?Json.Read<List<LibraryGroup>>(File.ReadAllText(p)):new();}
 public T Edit<T>(Func<T> change){using var mutex=new Mutex(false,"Local\\AICadLibraryCatalog_"+Json.Hash(Root));bool held=false;try{try{held=mutex.WaitOne(2000);}catch(AbandonedMutexException){held=true;}if(!held)throw new CadException("BUSY","Đang cập nhật danh mục.");return change();}finally{if(held)mutex.ReleaseMutex();}}
 static string Text(string text){text=text.Trim();if(text.Length<1||text.Length>120||text.Any(char.IsControl))throw new CadException("INVALID_NAME","Nhập tên từ 1 đến 120 ký tự.");return text;}
 public LibraryLabel Update(string kind,string id,string name,string groupId)=>Edit(()=>{var item=Label(kind,id);Available(kind,id);if(groupId!=""&&!Groups(kind).Any(g=>g.Id==groupId))throw new CadException("GROUP_NOT_FOUND","Nhóm không còn tồn tại.");item.Name=Text(name);item.GroupId=groupId;Json.Atomic(Item(kind,id),item);return item;});
 public LibraryLabel Trash(string kind,string id,bool deleted)=>Edit(()=>{var item=Label(kind,id);item.DeletedUtc=deleted?DateTime.UtcNow:null;Json.Atomic(Item(kind,id),item);return item;});
 public LibraryLabel UpdateHatch(string id,int revision,string name,string purpose,string notes,string groupId,bool reviewed,string action,string kind="hatches")=>Edit(()=>{
  var item=Label(kind,id);if(item.Revision!=revision)throw new CadException("RESOURCE_CHANGED","Hồ sơ hatch đã thay đổi; tải lại trước khi lưu.");
  if(action=="delete")item.DeletedUtc=DateTime.UtcNow;
  else if(action=="restore")item.DeletedUtc=null;
  else if(action=="update"){
   if(item.DeletedUtc.HasValue)throw new CadException("RESOURCE_DELETED","Khôi phục hatch trước khi sửa.");
   if(purpose.Length>500||notes.Length>2000||purpose.Any(c=>char.IsControl(c)&&c!='\n'&&c!='\r')||notes.Any(c=>char.IsControl(c)&&c!='\n'&&c!='\r'))throw new CadException("INVALID_NOTES","Công dụng tối đa 500 ký tự, ghi chú tối đa 2.000 ký tự.");
   if(reviewed&&string.IsNullOrWhiteSpace(purpose))throw new CadException("PURPOSE_REQUIRED","Ghi rõ công dụng hatch trước khi duyệt cho Skill.");
   if(groupId!=""&&!Groups(kind).Any(g=>g.Id==groupId))throw new CadException("GROUP_NOT_FOUND","Nhóm hatch không tồn tại.");
   item.Name=Text(name);item.Purpose=purpose.Trim();item.Notes=notes.Trim();item.GroupId=groupId;item.Reviewed=reviewed;
  }else throw new CadException("INVALID_ACTION",action);
  item.Revision++;Json.Atomic(Item(kind,id),item);return item;
 });
 public void SetGroups(string kind,List<string> ids,string groupId)=>Edit(()=>{
  if(groupId!=""&&!Groups(kind).Any(g=>g.Id==groupId))throw new CadException("GROUP_NOT_FOUND","Nhóm không còn tồn tại.");
  foreach(string id in ids){var item=Label(kind,id);item.GroupId=groupId;Json.Atomic(Item(kind,id),item);}return true;
 });
 public void EnsureLayerGroups()=>Edit(()=>{
  string marker=Path.Combine(Root,"layer-tree-v1.json");if(File.Exists(marker))return true;
  var groups=Groups("layers");foreach(string name in new[]{"Thư viện chung","Kiến trúc","Kết cấu","Điện nước"})if(!groups.Any(g=>g.Name==name&&g.ParentId==""))groups.Add(new(){Id=Guid.NewGuid().ToString("N"),Name=name});
  Json.Atomic(Path.Combine(Root,"layers-groups.json"),groups);Json.Atomic(marker,new{schemaVersion=1});return true;
 });
 public void EnsureBlockGroups()=>Edit(()=>{
  string marker=Path.Combine(Root,"block-tree-v1.json");if(File.Exists(marker))return true;
  var groups=Groups("blocks");foreach(string name in new[]{"Thư viện chung","Kiến trúc","Kết cấu","Điện nước"})if(!groups.Any(g=>g.Name==name&&g.ParentId==""))groups.Add(new(){Id=Guid.NewGuid().ToString("N"),Name=name});
  Json.Atomic(Path.Combine(Root,"blocks-groups.json"),groups);Json.Atomic(marker,new{schemaVersion=1});return true;
 });
 public void EnsureTemplateGroups()=>Edit(()=>{
  string marker=Path.Combine(Root,"template-tree-v1.json");if(File.Exists(marker))return true;
  var groups=Groups("templates");foreach(string name in new[]{"Thư viện chung","Kiến trúc","Kết cấu","Điện nước"})if(!groups.Any(g=>g.Name==name&&g.ParentId==""))groups.Add(new(){Id=Guid.NewGuid().ToString("N"),Name=name});
  Json.Atomic(Path.Combine(Root,"templates-groups.json"),groups);Json.Atomic(marker,new{schemaVersion=1});return true;
 });
 public LibraryGroup Group(string kind,string id,string name,string? parentId=null)=>Edit(()=>{
  var groups=Groups(kind);name=Text(name);LibraryGroup group=id==""?new(){Id=Guid.NewGuid().ToString("N")}:groups.SingleOrDefault(g=>g.Id==Json.Id(id))??throw new CadException("GROUP_NOT_FOUND",id);
  string parent=parentId??group.ParentId;
  if(parent!=""&&!groups.Any(g=>g.Id==parent))throw new CadException("GROUP_NOT_FOUND","Nhóm cha không tồn tại.");
  var visited=new HashSet<string>{group.Id};string cursor=parent;
  while(cursor!=""){if(!visited.Add(cursor)||visited.Count>16)throw new CadException("INVALID_GROUP_TREE","Không thể chuyển nhóm vào chính nó, nhóm con hoặc cây quá 15 cấp.");cursor=groups.Single(g=>g.Id==cursor).ParentId;}
  if(groups.Any(g=>g.Id!=id&&g.ParentId==parent&&string.Equals(g.Name,name,StringComparison.OrdinalIgnoreCase)))throw new CadException("GROUP_EXISTS","Tên nhóm đã tồn tại trong cùng nhóm cha.");
  group.Name=name;group.ParentId=parent;if(id=="")groups.Add(group);Json.Atomic(Path.Combine(Root,Kind(kind)+"-groups.json"),groups);return group;
 });
 public void RemoveGroup(string kind,string id){Edit(()=>{var groups=Groups(kind);if(!groups.Any(g=>g.Id==id))throw new CadException("GROUP_NOT_FOUND",id);string folder=Path.Combine(Root,Kind(kind));if(Directory.Exists(folder))foreach(string file in Directory.EnumerateFiles(folder,"*.json")){var item=Json.Read<LibraryLabel>(File.ReadAllText(file));if(item.GroupId==id){item.GroupId="";item.Revision++;Json.Atomic(file,item);}}var parent=groups.Single(g=>g.Id==id).ParentId;foreach(var child in groups.Where(g=>g.ParentId==id))child.ParentId=parent;groups.RemoveAll(g=>g.Id==id);Json.Atomic(Path.Combine(Root,Kind(kind)+"-groups.json"),groups);return true;});}
}
