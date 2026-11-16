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
}
public sealed class CadRibbonPin {
 public string Command { get; set; } = "";
 public string Label { get; set; } = "";
 public string Group { get; set; } = "";
 public string Icon { get; set; } = "";
 public string Description { get; set; } = "";
 public int Order { get; set; }
}
public sealed class CadToolIndex {public int Revision{get;set;}public List<CadToolEntry> Items{get;set;}=new();}
public sealed class CadToolCatalog {
 public const string AxisToolId="rainmaker-axis";
 public const string AxisCommand="RAINMAKERAXIS";
 readonly string root;public string Root=>root;string Index=>Path.Combine(root,"catalog.json");
 public CadToolCatalog(string appRoot){root=Path.Combine(WarehousePaths.Resolve(appRoot),"Tools");}
 public CadToolIndex Read()=>File.Exists(Index)?Json.Read<CadToolIndex>(File.ReadAllText(Index)):new();
 static bool IsLegacyAxis(CadToolEntry tool)=>tool.Id==AxisToolId&&tool.Kind=="builtin";
 static CadToolEntry DefaultAxis()=>new(){
  Id=AxisToolId,Kind="builtin",Name="Tạo trục",Group="Trục & cao độ",
  Notes="Vẽ đường trục và chèn block ATT Quang Anh ở hai đầu.",
  Commands=new(){AxisCommand},RibbonPins=new(){new(){Command=AxisCommand,Label="Tạo trục",Icon="Trục"}}
 };
 static CadToolEntry Editable(CadToolIndex index,string id)=>index.Items.SingleOrDefault(t=>t.Id==id)
  ??throw new CadException("TOOL_MISSING","Công cụ không còn trong danh mục.");
 public string PathFor(CadToolEntry item){string full=Path.GetFullPath(Path.Combine(root,item.RelativePath));if(!full.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.IsPathRooted(item.RelativePath))throw new CadException("TOOL_PATH","Đường dẫn công cụ không nằm trong kho.");return full;}
 public string Demand(CadToolEntry item){RequireAddIn(item);if(!item.Enabled||item.DeletedUtc.HasValue)throw new CadException("TOOL_DISABLED","Công cụ đang tắt hoặc nằm trong thùng rác.");string file=PathFor(item);if(!File.Exists(file)||Json.FileHash(file)!=item.FileHash)throw new CadException("TOOL_CHANGED","File công cụ đã thay đổi. Nhập cập nhật rồi nạp lại.");string folder=Path.GetDirectoryName(file)!;if(PackageHash(Directory.GetFiles(folder))!=item.PackageHash)throw new CadException("TOOL_CHANGED","Gói công cụ hoặc dependency đã thay đổi. Nhập cập nhật rồi nạp lại.");return file;}
 static void RequireAddIn(CadToolEntry item){if(item.Kind!="dotnet")throw new CadException("TOOL_TYPE","CAD Tool Manager quản lý add-in .NET Framework 4.8.");}
 static string PackageHash(IEnumerable<string> files)=>Json.Hash(files.OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Select(p=>new{name=Path.GetFileName(p),hash=Json.FileHash(p)}).ToList());
 public CadToolEntry Import(string source,int revision,string? replaceId=null){
  source=Path.GetFullPath(source);
  if(!string.Equals(Path.GetExtension(source),".dll",StringComparison.OrdinalIgnoreCase))throw new CadException("TOOL_TYPE","Chọn DLL .NET Framework 4.8.");
  if(!File.Exists(source))throw new CadException("TOOL_MISSING","Không tìm thấy file công cụ.");
  string stem=Path.GetFileNameWithoutExtension(source);if(Reserved(stem))throw new CadException("TOOL_RESERVED","Không nhập DLL của AutoCAD hoặc của AI CAD làm công cụ.");
  var files=Directory.GetFiles(Path.GetDirectoryName(source)!).Where(p=>new[]{".dll",".pdb"}.Contains(Path.GetExtension(p).ToLowerInvariant())&&!Reserved(Path.GetFileNameWithoutExtension(p))).ToArray();
  if(files.Length>200||files.Sum(p=>new FileInfo(p).Length)>100L*1024*1024)throw new CadException("TOOL_SIZE","Gói công cụ tối đa 100 MB / 200 file. Đặt DLL và dependency trong thư mục riêng.");
  CadToolEntry? imported=null;Change(revision,index=>{
   bool axisPackage=string.Equals(stem,"Rainmaker.Axis",StringComparison.OrdinalIgnoreCase);
   var old=replaceId==null?(axisPackage?index.Items.SingleOrDefault(IsLegacyAxis):null):
    index.Items.SingleOrDefault(t=>t.Id==replaceId)??(replaceId==AxisToolId&&axisPackage?DefaultAxis():
     throw new CadException("TOOL_MISSING","Công cụ không còn trong danh mục."));
   if(old!=null&&!(axisPackage&&IsLegacyAxis(old)))RequireAddIn(old);
   string id=old?.Id??Guid.NewGuid().ToString("N"),package=PackageHash(files);string relative=Path.Combine("dotnet",id,package,Path.GetFileName(source)),destination=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
   foreach(string file in files){string target=Path.Combine(Path.GetDirectoryName(destination)!,Path.GetFileName(file));if(!File.Exists(target))File.Copy(file,target,false);if(Json.FileHash(file)!=Json.FileHash(target))throw new CadException("TOOL_CHANGED","File thay đổi trong lúc nhập.");}
   imported=new CadToolEntry{Id=id,Name=old?.Name??stem,Kind="dotnet",RelativePath=relative,FileHash=Json.FileHash(destination),PackageHash=package,Group=old?.Group??"",Notes=old?.Notes??"",Enabled=old?.Enabled??true,Commands=old?.Commands??new(),RibbonPins=old?.RibbonPins??new(),UpdatedUtc=DateTime.UtcNow};
   if(old!=null)index.Items.Remove(old);index.Items.Add(imported);
  });return imported!;
 }
 static bool Reserved(string name)=>new[]{"acmgd","acdbmgd","accoremgd","CAD.Bridge.AutoCAD","CAD.Contracts","CAD.Geometry"}.Contains(name,StringComparer.OrdinalIgnoreCase)||name.StartsWith("System.",StringComparison.OrdinalIgnoreCase);
 public void Update(string id,int revision,string name,string group,string notes,bool enabled,IEnumerable<string> commands,string action="save")=>Change(revision,index=>{
  var item=Editable(index,id);
  RequireAddIn(item);
  if(action=="delete")item.DeletedUtc=DateTime.UtcNow;else if(action=="restore")item.DeletedUtc=null;else{
   if(string.IsNullOrWhiteSpace(name)||name.Length>120||group.Length>120||notes.Length>4000)throw new CadException("TOOL_METADATA","Tên tối đa 120 ký tự; ghi chú tối đa 4.000 ký tự.");
   var values=commands.Where(c=>!string.IsNullOrWhiteSpace(c)).Select(c=>CommandName(c.Trim())).Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(values.Count>200)throw new CadException("TOOL_COMMAND","Tối đa 200 lệnh mỗi công cụ.");item.Name=name.Trim();item.Group=group.Trim();item.Notes=notes;item.Enabled=enabled;item.Commands=values;
  }item.UpdatedUtc=DateTime.UtcNow;
 });
 public void SetEnabled(string id,int revision,bool enabled)=>Change(revision,index=>{
  var tool=Editable(index,id);
  RequireAddIn(tool);
  if(tool.DeletedUtc.HasValue)throw new CadException("TOOL_DISABLED","Khôi phục công cụ trước khi bật hoặc tắt.");
  tool.Enabled=enabled;
  tool.UpdatedUtc=DateTime.UtcNow;
 });
 public void SetRibbonPin(string id,int revision,CadRibbonPin pin,bool pinned)=>Change(revision,index=>{
  var tool=Editable(index,id);
  if(tool.DeletedUtc.HasValue)throw new CadException("TOOL_MISSING","Công cụ không còn trong danh mục.");
  RequireAddIn(tool);
  pin.Command=CommandName(pin.Command.Trim());
  if(pinned&&!tool.Commands.Contains(pin.Command,StringComparer.OrdinalIgnoreCase))
   throw new CadException("TOOL_COMMAND","Nạp DLL để xác nhận lệnh trước khi ghim.");
  pin.Label=pin.Label.Trim();pin.Group=pin.Group.Trim();pin.Description=pin.Description.Trim();
  if(pin.Description.Length>500||pin.Order<0||pin.Order>9999)throw new CadException("TOOL_RIBBON","Công dụng tối đa 500 ký tự, thứ tự từ 0 đến 9999.");
  if(pin.Label.Length>60||pin.Group.Length>60||pin.Label.Any(char.IsControl)||pin.Group.Any(char.IsControl))
   throw new CadException("TOOL_RIBBON","Tên nút và nhóm tối đa 60 ký tự, trên một dòng.");
  tool.RibbonPins.RemoveAll(item=>string.Equals(item.Command,pin.Command,StringComparison.OrdinalIgnoreCase));
  if(pinned)tool.RibbonPins.Add(pin);
 });
 void Change(int revision,Action<CadToolIndex> action){Directory.CreateDirectory(root);using var mutex=new Mutex(false,"Local\\AICadTools_"+Json.Hash(root));bool held=false;try{try{held=mutex.WaitOne(2000);}catch(AbandonedMutexException){held=true;}if(!held)throw new CadException("TOOL_BUSY","Danh mục công cụ đang được cập nhật.");var index=Read();if(index.Revision!=revision)throw new CadException("STALE_EDIT","Danh mục đã đổi. Làm mới rồi thử lại.");action(index);index.Revision++;Json.Atomic(Index,index);}finally{if(held)mutex.ReleaseMutex();}}
 public static string CommandName(string value){if(!Regex.IsMatch(value,@"^[\p{L}\p{N}_$.-]{1,100}$"))throw new CadException("TOOL_COMMAND","Tên lệnh chỉ gồm chữ, số, _, $, ., -; không nhập biểu thức hoặc macro.");return value;}
}
