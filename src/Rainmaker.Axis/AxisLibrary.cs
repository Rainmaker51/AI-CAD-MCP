using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
using Rainmaker.Library.Reader;
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
// Private Axis projection of the Reader V1 payload. No storage file schema or
// changing CAD.Contracts.LibrarySyncItem.Values getter crosses this boundary.
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
 internal string ReaderRevision="";
}
internal sealed class AxisLibrary
{
 internal static bool ValidDistance(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>0&&value<=10000000;
 readonly string appRoot,root;
 internal readonly List<AxisLibraryOption> Options=new();
 internal readonly TemplateProfile Template;
 readonly LibrarySnapshotV1 snapshot;
 internal string SettingsPath=>Path.Combine(root,"Catalog","addin-settings","rainmaker-axis.json");
 internal AxisLibrary(string appRoot)
 {
  this.appRoot=appRoot;root=WarehousePaths.Resolve(appRoot);
  try{snapshot=LibraryReader.Open(appRoot).Read();}catch(LibraryReadException error){throw new CadException(error.Code,error.Message);}
  Template=JObject.Parse(snapshot.TemplateJsonV1).ToObject<TemplateProfile>()!;
  foreach(var resource in snapshot.ListResources().Where(r=>r.Key.Kind is "blocks" or "layers" or "dims" or "hatches"))
   Options.Add(new(){Id=resource.Key.Id,Code=resource.Code,Kind=resource.Key.Kind,Name=resource.Name,Sync=new AxisSyncItem{Id=resource.Key.Id,Kind=resource.Key.Kind,Name=resource.Name,Values=JObject.Parse(resource.ParametersJsonV1)}});
 }
 ResourceV1 Resource(string kind,string id)
 {
  try{return snapshot.ResolveResources(new[]{new ResourceKeyV1("ISO_QUANGANH",kind,id)}).Single();}catch(LibraryReadException error){throw new CadException(error.Code,error.Message);}
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
  LayerAsset Layer(string id){Demand("layers",id);return JObject.Parse(Resource("layers",id).ProfileJsonV1).ToObject<LayerAsset>()!;}
  var selected=Demand("blocks",settings.HeadBlockId);
  var candidates=Template.Blocks.Where(b=>b.NativeSource?.BlockName==selected.Name).ToList();
  var head=candidates.OrderByDescending(b=>b.Id==selected.Name).ThenBy(b=>b.Id,StringComparer.Ordinal).FirstOrDefault()??throw new CadException("AXIS_HEAD","Block đã chọn chưa có trong thư viện.");
  Resource("blocks",settings.HeadBlockId);
  string tag=settings.AttributeTag.Trim();
  if(tag==""||!(selected.Sync.Values["attributes"] as JArray??new()).OfType<JObject>().Any(a=>a.Value<string>("tag")==tag&&a.Value<bool>("constant")==false))throw new CadException("AXIS_ATTRIBUTE","Chọn đúng tag attribute không cố định của block đầu trục.");
  var copy=JObject.FromObject(head).ToObject<BlockAsset>()!;copy.NativeSource!.LabelAttribute=tag;
  // The add-in's selected block, layer and ATT are authoritative. Legacy Skill
  // role, review, layer and dynamic overrides must never control this command.
  copy.Usage=null;
  var result=new AxisResolvedLibrary{Settings=settings,Template=Template,ReaderRevision=snapshot.Revision,AxisLayer=Layer(settings.AxisLayerId),HeadLayer=Layer(settings.HeadLayerId),DimLayer=Layer(settings.DimLayerId),Head=copy};
  if(!single){
   result.BayDim=Demand("dims",settings.BayDimId);result.TotalDim=Demand("dims",settings.TotalDimId);
   JObject Parameters(AxisLibraryOption option)=>JObject.Parse(Resource("dims",option.Id).ParametersJsonV1);
   result.BayParameters=Parameters(result.BayDim);result.TotalParameters=Parameters(result.TotalDim);
   var dimSource=Resource("dims",result.BayDim.Id);var totalSource=Resource("dims",result.TotalDim.Id);if(totalSource.SourceHash!=dimSource.SourceHash)throw new CadException("AXIS_DIM_SOURCE","Hai Dim phải thuộc cùng bản xuất nguồn.");result.DimHash=dimSource.SourceHash;result.DimFile=dimSource.SourceFile;
  }
  result.Snapshot=JsonUtil.Hash(new{snapshot.Revision,settings});return result;
 }
 internal void ResolveConstruction(AxisResolvedLibrary result,AxisGridParameters p)
 {
  if(result.ReaderRevision!=snapshot.Revision)throw new CadException("RESOURCE_CHANGED","Kho đã đổi giữa các bước phân giải. Nạp lại thư viện.");
  LayerAsset Layer(string id){Demand("layers",id);return JObject.Parse(Resource("layers",id).ProfileJsonV1).ToObject<LayerAsset>()!;}
  JObject Hatch(string id){Demand("hatches",id);var values=JObject.Parse(Resource("hatches",id).ParametersJsonV1);
   result.Snapshot=JsonUtil.Hash(new{result.Snapshot,values});return values;
  }
  if(p.Columns){result.ColumnLayer=Layer(result.Settings.ColumnLayerId);if(p.ColumnHatch){result.ColumnHatchLayer=Layer(result.Settings.ColumnHatchLayerId);result.ColumnHatch=Hatch(result.Settings.ColumnHatchId);}}
  if(p.Walls){result.WallLayer=Layer(result.Settings.WallLayerId);if(p.WallHatch){result.HatchLayer=Layer(result.Settings.HatchLayerId);result.WallHatch=Hatch(result.Settings.WallHatchId);}}
 }
}
