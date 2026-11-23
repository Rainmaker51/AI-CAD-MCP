using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Axis;

internal sealed class AxisLibrarySettings
{
 public int SchemaVersion {get;set;}=1;
 public int Revision {get;set;}
 public string SourceId {get;set;}="ISO_QUANGANH";
 public string AxisLayerId {get;set;}="";
 public string HeadLayerId {get;set;}="";
 public string HeadBlockId {get;set;}="";
 public string BayDimId {get;set;}="";
 public string TotalDimId {get;set;}="";
 public string DimLayerId {get;set;}="";
 public string AttributeTag {get;set;}="";
 public bool Bottom {get;set;}=true;
 public bool Top {get;set;}=true;
 public bool Left {get;set;}=true;
 public bool Right {get;set;}=true;
 public string HeadEnds {get;set;}="Both";
 public double DimOffset {get;set;}=1000;
 public double DimRowGap {get;set;}=600;
 public string ColumnLayerId {get;set;}="";
 public string WallLayerId {get;set;}="";
 public string HatchLayerId {get;set;}="";
 public string WallHatchId {get;set;}="";
 public string ColumnHatchId {get;set;}="";
 public string ColumnHatchLayerId {get;set;}="";
}
internal sealed class AxisLibraryOption
{
 internal string Id="",Code="",Kind="",Name="";
 internal AxisSyncItem Sync=null!;
 public string Label=>Code+" · "+Name;
 public override string ToString()=>Label;
}
// Parse the on-disk schema locally: Manager versions may expose Values as
// JToken or JObject under the same CAD.Contracts assembly version.
// Calling that changing getter creates a binary MissingMethodException.
internal sealed class AxisSyncItem
{
 public string Id {get;set;}="";
 public string Kind {get;set;}="";
 public string Name {get;set;}="";
 public string Handle {get;set;}="";
 public bool Draft {get;set;}
 public JObject Values {get;set;}=new();
}
internal sealed class AxisResolvedLibrary
{
 internal AxisLibrarySettings Settings=null!;
 internal TemplateProfile Template=null!;
 internal LayerAsset AxisLayer=null!,HeadLayer=null!,DimLayer=null!;
 internal BlockAsset Head=null!;
 internal AxisLibraryOption BayDim=null!,TotalDim=null!;
 internal string DimFile="",DimHash="";
 internal JObject BayParameters=null!,TotalParameters=null!;
 internal string Snapshot="";
 internal LayerAsset? ColumnLayer,WallLayer,HatchLayer;
 internal JObject? WallHatch;
 internal LayerAsset? ColumnHatchLayer;
 internal JObject? ColumnHatch;
}
internal sealed class AxisLibrary
{
 internal static bool ValidDistance(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>0&&value<=10000000;
 readonly string appRoot,root;
 internal readonly List<AxisLibraryOption> Options=new();
 internal readonly TemplateProfile Template;
 readonly JObject styles;
 internal string SettingsPath=>Path.Combine(root,"Catalog","addin-settings","rainmaker-axis.json");
 internal AxisLibrary(string appRoot)
 {
  this.appRoot=appRoot;root=WarehousePaths.Resolve(appRoot);
  if(File.Exists(Path.Combine(root,"Catalog","sync","pending.json")))throw new CadException("AXIS_SYNC_BUSY","Kho đang có giao dịch đồng bộ chưa hoàn tất. Hoàn tất đồng bộ rồi mở lại Tạo trục.");
  Template=new ContentStore(appRoot).Template("ISO_QUANGANH");
  string indexFile=Path.Combine(root,"Catalog","resource-codes.json");
  if(!File.Exists(indexFile))throw new CadException("AXIS_CODES","Mở Thư viện QA trong AutoCAD và lưu danh mục để cấp mã tài nguyên.");
  var index=JObject.Parse(File.ReadAllText(indexFile));
  if(index.Value<int>("SchemaVersion")!=1)throw new CadException("AXIS_CODES","Phiên bản sổ mã không được hỗ trợ.");
  string stateFile=Path.Combine(root,"Catalog","sync","state.json");
  var state=JObject.Parse(File.ReadAllText(stateFile))["Items"]!.ToObject<List<AxisSyncItem>>()!;
  foreach(var entry in index["Items"]!.OfType<JObject>().Where(e=>e.Value<string>("SourceId")=="ISO_QUANGANH")){
   var matches=state.Where(s=>!s.Draft&&s.Kind==entry.Value<string>("Kind")&&s.Id==entry.Value<string>("Id")).ToList();
   if(matches.Count!=1)continue;
   var sync=matches[0];if(sync.Kind!="blocks"&&sync.Kind!="layers"&&sync.Kind!="dims"&&sync.Kind!="hatches")continue;
   Options.Add(new(){Id=sync.Id,Code=entry.Value<string>("Code")!,Kind=sync.Kind,Name=sync.Name,Sync=sync});
  }
  if(Options.GroupBy(o=>o.Kind+":"+o.Id).Any(g=>g.Count()!=1)||Options.GroupBy(o=>o.Code).Any(g=>g.Count()!=1))throw new CadException("AXIS_CODES","Sổ mã có định danh trùng.");
  string folder=Path.Combine(root,"Styles","ISO_QUANGANH");var active=JObject.Parse(File.ReadAllText(Path.Combine(folder,"active.json")));
  string raw=Path.Combine(folder,ContentStore.VersionName(active.Value<string>("version")!),"catalog.json");
  if(JsonUtil.FileHash(raw)!=active.Value<string>("checksum"))throw new CadException("RESOURCE_CHANGED","Danh mục Dim đã thay đổi ngoài ứng dụng.");
  styles=JObject.Parse(File.ReadAllText(raw));
 }
 internal AxisLibraryOption Demand(string kind,string id)=>Options.SingleOrDefault(o=>o.Kind==kind&&o.Id==id)??throw new CadException("AXIS_SELECTION","Chọn tài nguyên "+kind+" còn tồn tại và đã đồng bộ.");
 internal AxisLibrarySettings Load()
 {
  if(File.Exists(SettingsPath)){
   var settings=JObject.Parse(File.ReadAllText(SettingsPath)).ToObject<AxisLibrarySettings>()!;
   if(settings.SchemaVersion!=1||settings.SourceId!="ISO_QUANGANH")throw new CadException("AXIS_SETTINGS","Cấu hình Tạo trục không tương thích.");return settings;
  }
  // Preserve known prior layer/head choices; never guess which Dim style the user wants.
  string Id(string kind,string name)=>Options.FirstOrDefault(o=>o.Kind==kind&&o.Name==name)?.Id??"";
  var head=Template.Blocks.FirstOrDefault(b=>b.Id=="QA_ATT_TRUC");
  return new(){AxisLayerId=Id("layers","QA_KT_Truc"),HeadLayerId=Id("layers","QA_KH_Truc Dinh Vi"),DimLayerId=Id("layers","QA_KT_Dim"),HeadBlockId=Id("blocks",head?.NativeSource?.BlockName??""),AttributeTag=head?.NativeSource?.LabelAttribute??""};
 }
 internal void Save(AxisLibrarySettings settings,bool single=false)
 {
  Resolve(settings,single);Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
  using var lease=new FileStream(SettingsPath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  int revision=File.Exists(SettingsPath)?JObject.Parse(File.ReadAllText(SettingsPath)).Value<int>("Revision"):0;
  if(revision!=settings.Revision)throw new CadException("STALE_EDIT","Cấu hình đã đổi trên máy khác. Đóng và mở lại bảng trước khi lưu.");
  settings.Revision++;try{JsonUtil.Atomic(SettingsPath,settings);}catch{settings.Revision--;throw;}
 }
 internal AxisResolvedLibrary Resolve(AxisLibrarySettings settings,bool single)
 {
  if(!single&&(!ValidDistance(settings.DimOffset)||!ValidDistance(settings.DimRowGap)))throw new CadException("AXIS_DIM_OFFSET","Khoảng cách dim phải lớn hơn 0 và không vượt quá 10.000.000 mm.");
  if(settings.SourceId!="ISO_QUANGANH"||settings.SchemaVersion!=1)throw new CadException("AXIS_SETTINGS","Nguồn cấu hình không được hỗ trợ.");
  if(settings.HeadEnds!="Both"&&settings.HeadEnds!="Start"&&settings.HeadEnds!="End")throw new CadException("AXIS_SETTINGS","Chọn đầu trục hợp lệ.");
  var management=new LibraryCatalog(appRoot);
  LayerAsset Layer(string id){var option=Demand("layers",id);var layer=Template.Layers.SingleOrDefault(l=>l.Name==option.Name)??throw new CadException("AXIS_LAYER","Layer chưa khớp thư viện; đọc và đồng bộ trước.");management.Available("layers",JsonUtil.Hash(new[]{Template.Id,layer.Name}));return layer;}
  var selected=Demand("blocks",settings.HeadBlockId);
  var candidates=Template.Blocks.Where(b=>b.NativeSource?.BlockName==selected.Name).ToList();
  var head=candidates.OrderByDescending(b=>b.Id==selected.Name).ThenBy(b=>b.Id,StringComparer.Ordinal).FirstOrDefault()??throw new CadException("AXIS_HEAD","Block đã chọn chưa có trong thư viện.");
  management.Available("blocks",JsonUtil.Hash(new[]{Template.Id,head.Id}));
  string tag=settings.AttributeTag.Trim();
  if(tag==""||!(selected.Sync.Values["attributes"] as JArray??new()).OfType<JObject>().Any(a=>a.Value<string>("tag")==tag&&a.Value<bool>("constant")==false))throw new CadException("AXIS_ATTRIBUTE","Chọn đúng tag attribute không cố định của block đầu trục.");
  var copy=JObject.FromObject(head).ToObject<BlockAsset>()!;copy.NativeSource!.LabelAttribute=tag;
  // The add-in's selected block, layer and ATT are authoritative. Legacy Skill
  // role, review, layer and dynamic overrides must never control this command.
  copy.Usage=null;
  var result=new AxisResolvedLibrary{Settings=settings,Template=Template,AxisLayer=Layer(settings.AxisLayerId),HeadLayer=Layer(settings.HeadLayerId),DimLayer=Layer(settings.DimLayerId),Head=copy};
  if(!single){
   result.BayDim=Demand("dims",settings.BayDimId);result.TotalDim=Demand("dims",settings.TotalDimId);
   JObject Parameters(AxisLibraryOption option){var entries=styles["dims"]!.OfType<JObject>().Where(d=>d.Value<string>("name")==option.Name).ToList();if(entries.Count!=1)throw new CadException("AXIS_DIM","Dim đã chọn chưa khớp danh mục; đồng bộ lại.");management.Available("dims",entries[0].Value<string>("id")!);return entries[0];}
   result.BayParameters=Parameters(result.BayDim);result.TotalParameters=Parameters(result.TotalDim);
   result.DimHash=styles.Value<string>("sourceHash")??"";
   if(!System.Text.RegularExpressions.Regex.IsMatch(result.DimHash,"^[a-f0-9]{64}$"))throw new CadException("AXIS_DIM_SOURCE","Nguồn Dim không có hash hợp lệ.");
   result.DimFile=Path.Combine(WarehousePaths.Folder(appRoot,"content-assets"),result.DimHash+".dwg");
   if(!File.Exists(result.DimFile)||JsonUtil.FileHash(result.DimFile)!=result.DimHash)throw new CadException("RESOURCE_CHANGED","DWG nguồn Dim thiếu hoặc đã thay đổi.");
  }
  result.Snapshot=JsonUtil.Hash(new{Template,settings,styles});return result;
 }
 internal void ResolveConstruction(AxisResolvedLibrary result,AxisGridParameters p)
 {
  var management=new LibraryCatalog(appRoot);
  LayerAsset Layer(string id){var option=Demand("layers",id);var layer=Template.Layers.SingleOrDefault(l=>l.Name==option.Name)??throw new CadException("AXIS_LAYER","Layer chưa đồng bộ: "+option.Label);management.Available("layers",JsonUtil.Hash(new[]{Template.Id,layer.Name}));return layer;}
  JObject Hatch(string id){var option=Demand("hatches",id);var values=(JObject)option.Sync.Values.DeepClone();
   var folder=Path.Combine(root,"Hatches","ISO_QUANGANH");var active=JObject.Parse(File.ReadAllText(Path.Combine(folder,"active.json")));var raw=Path.Combine(folder,ContentStore.VersionName(active.Value<string>("version")!),"catalog.json");if(JsonUtil.FileHash(raw)!=active.Value<string>("checksum"))throw new CadException("RESOURCE_CHANGED","Danh mục hatch thay đổi.");
   var hatches=JObject.Parse(File.ReadAllText(raw));var entries=hatches["items"]?.OfType<JObject>().Where(h=>(h.Value<string>("id")??"").StartsWith(option.Id,StringComparison.Ordinal)&&new[]{"pattern","type","scale","angle","layer","color","spacing","doublePattern"}.All(k=>JToken.DeepEquals(h[k],option.Sync.Values[k]))).ToList();if(entries==null||entries.Count!=1)throw new CadException("AXIS_HATCH","Hatch chưa khớp kho; đọc và đồng bộ lại trước.");management.Available("hatches",entries[0].Value<string>("id")!);
   result.Snapshot=JsonUtil.Hash(new{result.Snapshot,hatches,values});return values;
  }
  if(p.Columns){result.ColumnLayer=Layer(result.Settings.ColumnLayerId);if(p.ColumnHatch){result.ColumnHatchLayer=Layer(result.Settings.ColumnHatchLayerId);result.ColumnHatch=Hatch(result.Settings.ColumnHatchId);}}
  if(p.Walls){result.WallLayer=Layer(result.Settings.WallLayerId);if(p.WallHatch){result.HatchLayer=Layer(result.Settings.HatchLayerId);result.WallHatch=Hatch(result.Settings.WallHatchId);}}
 }
}
