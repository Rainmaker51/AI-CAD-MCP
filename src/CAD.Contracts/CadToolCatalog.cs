using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
namespace AICad.Contracts;
public sealed class CadToolEntry {
 public string Id{get;set;}="";public string Name{get;set;}="";public string Kind{get;set;}="";
 public string RelativePath{get;set;}="";public string FileHash{get;set;}="";public string PackageHash{get;set;}="";
 public string Group{get;set;}="";public string Notes{get;set;}="";public bool Enabled{get;set;}=true;
 public DateTime? DeletedUtc{get;set;}public List<string> Commands{get;set;}=new();public DateTime UpdatedUtc{get;set;}
 public List<CadRibbonPin> RibbonPins { get; set; } = new();
 public string TabId { get; set; } = CadToolCatalog.DefaultTabId;
}
public sealed class CadRibbonPin {
 public string Command { get; set; } = "";
 public string Label { get; set; } = "";
 public string Group { get; set; } = "";
 public string Icon { get; set; } = "";
 public string Description { get; set; } = "";
 public int Order { get; set; }
 public string TabId { get; set; } = "";
}
public sealed class CadRibbonTab {
 public string Id{get;set;}="";public string Name{get;set;}="";public int Order{get;set;}public bool Enabled{get;set;}=true;
 public override string ToString()=>Name;
}
public sealed class CadToolIndex {public int Revision{get;set;}public int StorageVersion{get;set;}public List<CadToolEntry> Items{get;set;}=new();public List<CadRibbonTab> Tabs{get;set;}=new();}
public sealed partial class CadToolCatalog {
 public const string DefaultTabId="rainmaker";
 public const string AxisToolId="rainmaker-axis";
 public const string AxisCommand="RAINMAKERAXIS";
 public static string RibbonGroup(CadToolEntry tool,CadRibbonPin pin)=>
  !string.IsNullOrWhiteSpace(pin.Group)?pin.Group.Trim():!string.IsNullOrWhiteSpace(tool.Group)?tool.Group.Trim():"Công cụ";
 readonly string root;public string Root=>root;string Index=>Path.Combine(root,"catalog.json");
 public CadToolCatalog(string appRoot){root=Path.Combine(WarehousePaths.Resolve(appRoot),"Tools");}
 public CadToolIndex Read(){var index=File.Exists(Index)?Json.Read<CadToolIndex>(File.ReadAllText(Index)):new();if(index.StorageVersion>2)throw new CadException("TOOL_STORAGE_VERSION","Cập nhật CAD Manager trên máy này để đọc kho mới.");index.Tabs=(index.Tabs??new()).GroupBy(t=>t.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();if(!index.Tabs.Any(t=>t.Id==DefaultTabId))index.Tabs.Insert(0,new(){Id=DefaultTabId,Name="Rainmaker"});return index;}
 static bool IsLegacyAxis(CadToolEntry tool)=>tool.Id==AxisToolId&&tool.Kind=="builtin";
 static CadToolEntry DefaultAxis()=>new(){
  Id=AxisToolId,Kind="builtin",Name="Tạo trục",Group="Trục & cao độ",
  Notes="Vẽ đường trục và chèn block ATT Quang Anh ở hai đầu.",
  Commands=new(){AxisCommand},RibbonPins=new(){new(){Command=AxisCommand,Label="Tạo trục",Icon="Trục"}}
 };
 static CadToolEntry Editable(CadToolIndex index,string id)=>index.Items.SingleOrDefault(t=>t.Id==id)
  ??throw new CadException("TOOL_MISSING","Công cụ không còn trong danh mục.");
 public string PathFor(CadToolEntry item){string full=Path.GetFullPath(Path.Combine(root,item.RelativePath));if(!full.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.IsPathRooted(item.RelativePath))throw new CadException("TOOL_PATH","Đường dẫn công cụ không nằm trong kho.");return full;}
 public string Demand(CadToolEntry item){RequireAddIn(item);if(!item.Enabled||item.DeletedUtc.HasValue)throw new CadException("TOOL_DISABLED","Công cụ đang tắt hoặc nằm trong thùng rác.");var index=Read();var selected=index.Items.SingleOrDefault(t=>t.Id==item.Id);if(selected==null||selected.PackageHash!=item.PackageHash||selected.RelativePath!=item.RelativePath)throw new CadException("TOOL_CHANGED","Danh mục đã chọn bản khác. Làm mới Manager.");if(!selected.Enabled||selected.DeletedUtc.HasValue)throw new CadException("TOOL_DISABLED","Công cụ đang tắt hoặc nằm trong thùng rác.");foreach(var tool in index.Items.Where(t=>t.Kind=="dotnet"&&t.Enabled&&!t.DeletedUtc.HasValue))VerifyPackage(tool);return PathFor(item);}
 static void RequireAddIn(CadToolEntry item){if(item.Kind!="dotnet")throw new CadException("TOOL_TYPE","CAD Tool Manager quản lý add-in .NET Framework 4.8.");}
 static string PackageHash(IEnumerable<string> files)=>Json.Hash(files.OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Select(p=>new{name=Path.GetFileName(p),hash=Json.FileHash(p)}).ToList());
 public CadToolEntry Import(string source,int revision,string? replaceId=null)=>ImportInstalled(source,revision,replaceId);
 static bool Reserved(string name)=>new[]{"acmgd","acdbmgd","accoremgd","CAD.Bridge.AutoCAD","CAD.Contracts","CAD.Geometry","Rainmaker.Library.Reader"}.Contains(name,StringComparer.OrdinalIgnoreCase)||name.StartsWith("System.",StringComparison.OrdinalIgnoreCase);
 public void Update(string id,int revision,string name,string group,string notes,bool enabled,IEnumerable<string> commands,string action="save",string? tabId=null)=>Change(revision,index=>{
  var item=Editable(index,id);
  RequireAddIn(item);
  if(action=="delete")item.DeletedUtc=DateTime.UtcNow;else if(action=="restore")item.DeletedUtc=null;else{
   if(string.IsNullOrWhiteSpace(name)||name.Length>120||group.Length>120||notes.Length>4000)throw new CadException("TOOL_METADATA","Tên tối đa 120 ký tự; ghi chú tối đa 4.000 ký tự.");
   var values=commands.Where(c=>!string.IsNullOrWhiteSpace(c)).Select(c=>CommandName(c.Trim())).Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(values.Count>200)throw new CadException("TOOL_COMMAND","Tối đa 200 lệnh mỗi công cụ.");item.Name=name.Trim();item.Group=group.Trim();item.Notes=notes;item.Enabled=enabled;item.Commands=values;
   if(tabId!=null){DemandTab(index,tabId);item.TabId=tabId;}
  }item.UpdatedUtc=DateTime.UtcNow;
 });
 public void SetEnabled(string id,int revision,bool enabled)=>Change(revision,index=>{
  var tool=Editable(index,id);
  RequireAddIn(tool);
  if(tool.DeletedUtc.HasValue)throw new CadException("TOOL_DISABLED","Khôi phục công cụ trước khi bật hoặc tắt.");
  tool.Enabled=enabled;
  tool.UpdatedUtc=DateTime.UtcNow;
 });
 static void DemandTab(CadToolIndex index,string id){if(!index.Tabs.Any(t=>t.Id==id))throw new CadException("RIBBON_TAB_MISSING","Tab ribbon không còn tồn tại. Làm mới rồi chọn lại.");}
 public string SaveTab(int revision,string id,string name,int order,bool enabled=true){string saved=id==""?Guid.NewGuid().ToString("N"):id;Change(revision,index=>{
  name=name.Trim();if(name.Length==0||name.Length>60||name.Any(char.IsControl)||order<0||order>9999)throw new CadException("RIBBON_TAB","Tên tab 1–60 ký tự, thứ tự 0–9999.");
  if(index.Tabs.Any(t=>t.Id!=saved&&string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase)))throw new CadException("RIBBON_TAB_EXISTS","Tên tab đã tồn tại.");
  CadRibbonTab tab;if(id==""){if(index.Tabs.Count>=30)throw new CadException("RIBBON_TAB_LIMIT","Tối đa 30 tab do Manager quản lý.");tab=new(){Id=saved};index.Tabs.Add(tab);}else{DemandTab(index,id);tab=index.Tabs.Single(t=>t.Id==id);}
  tab.Name=name;tab.Order=order;tab.Enabled=saved==DefaultTabId||enabled;
 });return saved;}
 public void DeleteTab(int revision,string id)=>Change(revision,index=>{
  if(id==DefaultTabId)throw new CadException("RIBBON_DEFAULT_TAB","Giữ tab mặc định để luôn mở được CAD Tool Manager.");DemandTab(index,id);
  foreach(var item in index.Items){if(item.TabId==id)item.TabId=DefaultTabId;foreach(var pin in item.RibbonPins)if(pin.TabId==id)pin.TabId="";}
  index.Tabs.RemoveAll(t=>t.Id==id);
 });
 static void ValidatePin(CadToolIndex index,CadToolEntry tool,CadRibbonPin pin,bool pinned){
  if(tool.DeletedUtc.HasValue)throw new CadException("TOOL_MISSING","Công cụ không còn trong danh mục.");
  RequireAddIn(tool);
  pin.Command=CommandName(pin.Command.Trim());
  if(pinned&&!tool.Commands.Contains(pin.Command,StringComparer.OrdinalIgnoreCase))
   throw new CadException("TOOL_COMMAND","Nạp DLL để xác nhận lệnh trước khi ghim.");
  pin.Label=pin.Label.Trim();pin.Group=pin.Group.Trim();pin.Description=pin.Description.Trim();
  if(pin.Description.Length>500||pin.Order<0||pin.Order>9999)throw new CadException("TOOL_RIBBON","Công dụng tối đa 500 ký tự, thứ tự từ 0 đến 9999.");
  if(pin.Label.Length>60||pin.Group.Length>60||pin.Label.Any(char.IsControl)||pin.Group.Any(char.IsControl))
   throw new CadException("TOOL_RIBBON","Tên nút và nhóm tối đa 60 ký tự, trên một dòng.");
  if(pin.TabId!="")DemandTab(index,pin.TabId);
 }
 public void SetRibbonPin(string id,int revision,CadRibbonPin pin,bool pinned)=>Change(revision,index=>{
  var tool=Editable(index,id);ValidatePin(index,tool,pin,pinned);
  tool.RibbonPins.RemoveAll(item=>string.Equals(item.Command,pin.Command,StringComparison.OrdinalIgnoreCase));
  if(pinned)tool.RibbonPins.Add(pin);
 });
 public void PinCommands(string id,int revision,IEnumerable<CadRibbonPin> commands)=>PinCommands(id,revision,commands,null);
 public void PinCommands(string id,int revision,IEnumerable<CadRibbonPin> commands,string? tabId)=>Change(revision,index=>{
  var tool=Editable(index,id);if(tabId!=null){DemandTab(index,tabId);tool.TabId=tabId;}
  var pins=commands.ToList();if(pins.Count>200||pins.Select(p=>p.Command).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=pins.Count)throw new CadException("TOOL_RIBBON","Danh sách ghim tối đa 200 lệnh riêng biệt.");
  foreach(var pin in pins){ValidatePin(index,tool,pin,true);if(!tool.RibbonPins.Any(p=>string.Equals(p.Command,pin.Command,StringComparison.OrdinalIgnoreCase)))tool.RibbonPins.Add(pin);}
 });
 void Change(int revision,Action<CadToolIndex> action,Action<CadToolIndex>? committed=null){Directory.CreateDirectory(root);using var mutex=new Mutex(false,"Local\\AICadTools_"+Json.Hash(root));bool held=false;try{try{held=mutex.WaitOne(2000);}catch(AbandonedMutexException){held=true;}if(!held)throw new CadException("TOOL_BUSY","Danh mục công cụ đang được cập nhật.");using var sharedLock=AcquireCatalogLock();var index=Read();if(index.Revision!=revision)throw new CadException("STALE_EDIT","Danh mục đã đổi. Làm mới rồi thử lại.");action(index);index.Revision++;Json.Atomic(Index,index);committed?.Invoke(index);}finally{if(held)mutex.ReleaseMutex();}}
 public static string CommandName(string value){if(!Regex.IsMatch(value,@"^[\p{L}\p{N}_$.-]{1,100}$"))throw new CadException("TOOL_COMMAND","Tên lệnh chỉ gồm chữ, số, _, $, ., -; không nhập biểu thức hoặc macro.");return value;}
}
