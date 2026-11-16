using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICad.Contracts;
using AICad.Geometry;
using Newtonsoft.Json.Linq;
namespace AICad.Application;
public sealed class LibraryService(InstallConfig config,WorkspaceService workspace){
 readonly SemaphoreSlim scanGate=new(1,1);
 string Folder=>Path.Combine(config.DataRoot,"library-imports");
 string Job(string id)=>Path.Combine(Folder,Json.Id(id));
 public object Content(string kind,string id,string? version=null)=>kind=="skills"?workspace.Store.Skill(id,version):kind=="templates"?(object)workspace.Store.Template(id,version):throw new CadException("INVALID_KIND",kind);
 public object Validate(string kind,JObject content){if(kind=="skills"){var s=Json.Read<SkillDefinition>(content.ToString());workspace.ValidateSkill(s);return new{valid=true,id=s.Id,version=s.Version,template=s.TemplateId};}if(kind=="templates"){var t=Json.Read<TemplateProfile>(content.ToString());Validation.Template(t);workspace.DemandTemplateImpact(t);return new{valid=true,id=t.Id,version=t.Version,blocks=t.Blocks.Count,layers=t.Layers.Count,consumers=workspace.TemplateConsumers(t.Id)};}throw new CadException("INVALID_KIND",kind);}
 public async Task<LibraryAnalysis> Analyze(string name,Stream input,CancellationToken ct){
  string extension=Path.GetExtension(name).ToLowerInvariant();if(extension!=".dwg"&&extension!=".dwt")throw new CadException("INVALID_UPLOAD","Chọn DWG hoặc DWT, tối đa 20 MB.");
  if(!await scanGate.WaitAsync(0,ct))throw new CadException("LIBRARY_BUSY","Đang đọc một tài liệu CAD khác. Chờ hoàn tất rồi thử lại.");
  string id=Guid.NewGuid().ToString("N"),root=Job(id);Directory.CreateDirectory(root);
  try{
   string source=Path.Combine(root,"source.dwg");await using(var output=new FileStream(source,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var buffer=new byte[65536];long size=0;int n;while((n=await input.ReadAsync(buffer,ct))>0){size+=n;if(size>20*1024*1024)throw new CadException("INVALID_UPLOAD","DWG/DWT tối đa 20 MB.");await output.WriteAsync(buffer.AsMemory(0,n),ct);}}
   using(var header=File.OpenRead(source)){byte[] b=new byte[6];if(header.Read(b,0,6)!=6||!Encoding.ASCII.GetString(b).StartsWith("AC10"))throw new CadException("INVALID_CAD_FILE","Tệp không có định dạng DWG/DWT hợp lệ.");}
   File.WriteAllText(Path.Combine(root,".aicad-library-job"),"AI CAD owned library scan");
   string scanner=Path.Combine(Path.GetDirectoryName(config.HostExecutable)!,"LibraryScanner","CAD.LibraryScanner.dll");
   string console=@"C:\Program Files\Autodesk\AutoCAD 2021\accoreconsole.exe";
   if(!File.Exists(scanner)||!File.Exists(console))throw new CadException("SCANNER_MISSING","Thiếu công cụ đọc thư viện AutoCAD 2021.");
   string script=Path.Combine(root,"scan.scr");File.WriteAllText(script,"_NETLOAD\n\""+scanner.Replace('\\','/')+"\"\nAICADLIBRARYSCAN\n_QUIT\n_Y\n",Encoding.ASCII);
   var start=new ProcessStartInfo(console){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
   foreach(string a in new[]{"/i",Path.Combine(config.ContentRoot,"content","launch","blank-mm.dwg"),"/s",script,"/isolate","AICadLibrary",Path.Combine(root,"profile")})start.ArgumentList.Add(a);
   start.Environment["AICAD_LIBRARY_JOB"]=root;
   using var child=Process.Start(start)??throw new CadException("SCANNER_FAILED","Không khởi động được công cụ đọc CAD.");
   var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(2));
   try{await child.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){child.Kill(true);throw new CadException("SCAN_TIMEOUT","Đã dừng đọc tài liệu CAD do quá thời gian hoặc bị hủy.");}
   string diagnostic=await stdout+await stderr;File.WriteAllText(Path.Combine(root,"console.log"),diagnostic.Length>12000?diagnostic.Substring(diagnostic.Length-12000):diagnostic);
   string error=Path.Combine(root,"scan-error.json");if(File.Exists(error))throw new CadException("CAD_SCAN_FAILED",Json.Read<JObject>(File.ReadAllText(error)).Value<string>("message")??"Không đọc được DWG.");
   string analysis=Path.Combine(root,"analysis.json");if(!File.Exists(analysis))throw new CadException("CAD_SCAN_FAILED","CAD chưa trả dữ liệu thư viện. Kiểm tra tệp hoặc bản quyền AutoCAD.");
   var result=Json.Read<LibraryAnalysis>(File.ReadAllText(analysis));if(result.SourceHash!=Json.FileHash(source))throw new CadException("SOURCE_CHANGED","Tệp CAD đã đổi trong lúc đọc.");
   result.Name=Path.GetFileName(name);result.Id=id;
   foreach(var group in result.Blocks.GroupBy(b=>b.SuggestedId,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))foreach(var b in group)b.SuggestedId+="_"+Json.Hash(b.Name).Substring(0,6);
   foreach(var group in result.Layers.GroupBy(b=>b.SuggestedId,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))foreach(var b in group)b.SuggestedId+="_"+Json.Hash(b.Name).Substring(0,6);
   Json.Atomic(analysis,result);File.WriteAllText(Path.Combine(root,"analysis.sha256"),Json.FileHash(analysis));return result;
  }finally{scanGate.Release();}
 }
 public LibraryAnalysis Analysis(string id){string dir=Job(id),path=Path.Combine(dir,"analysis.json");if(Json.FileHash(path)!=File.ReadAllText(Path.Combine(dir,"analysis.sha256")))throw new CadException("SOURCE_CHANGED","Kết quả phân tích thư viện đã bị thay đổi.");var a=Json.Read<LibraryAnalysis>(File.ReadAllText(path));if(a.SourceHash!=Json.FileHash(Path.Combine(dir,"source.dwg")))throw new CadException("SOURCE_CHANGED","DWG nguồn đã bị thay đổi.");return a;}
 public TemplateProfile Build(JObject request){
  var source=Analysis(request.Value<string>("importId")??"");
  if(source.Units!="Millimeters"&&(source.Units!="Undefined"||request.Value<bool?>("confirmedMillimeters")!=true))throw new CadException("UNITS_CONFIRMATION_REQUIRED","Nguồn phải ở mm. Với nguồn không khai báo đơn vị, cần xác nhận mm; nguồn m/inch cần đổi đơn vị trong CAD trước.");
  string id=request.Value<string>("id")??"";Json.Name(id);
  bool existing=workspace.Store.Ids("templates").Contains(id);
  var t=existing&&request.Value<bool?>("mergeExisting")!=false?Json.Read<TemplateProfile>(Json.Write(workspace.Template(id))):new TemplateProfile{Id=id,Layers=new(),Blocks=new()};
  t.Id=id;t.Name=request.Value<string>("name")??"";t.Version=request.Value<string>("version")??"";
  if(string.IsNullOrWhiteSpace(t.Name)||t.Name.Length>120)throw new CadException("INVALID_TEMPLATE","Nhập tên thư viện tối đa 120 ký tự.");
  ContentStore.VersionName(t.Version);if(workspace.Store.Versions("templates",id).Contains(t.Version))throw new CadException("VERSION_EXISTS","Phiên bản đã có; hãy tăng số phiên bản.");
  t.TextHeight=request.Value<double?>("textHeight")??t.TextHeight;t.ArrowSize=request.Value<double?>("arrowSize")??t.ArrowSize;
  string? style=request.Value<string>("textStyle");if(!string.IsNullOrEmpty(style)){var s=source.TextStyles.SingleOrDefault(s=>s.Name==style&&s.Supported)??throw new CadException("INVALID_STYLE","Style chữ nguồn chưa hỗ trợ.");t.Font=s.Font;}
  var selected=request["blocks"]?.ToObject<List<LibrarySelection>>()??new();var layers=request["layers"]?.ToObject<List<LibrarySelection>>()??new();
  if(selected.Count>1000||layers.Count>100||selected.Select(s=>s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=selected.Count||layers.Select(s=>s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=layers.Count)throw new CadException("INVALID_SELECTION","Tối đa 1.000 block và 100 layer; mã tài nguyên phải duy nhất.");
  foreach(var selectedBlock in selected){Json.Name(selectedBlock.Id);var b=source.Blocks.SingleOrDefault(b=>b.Name==selectedBlock.Name&&b.Supported)??throw new CadException("UNSUPPORTED_ASSET","Block chưa hỗ trợ: "+selectedBlock.Name);var previous=t.Blocks.SingleOrDefault(b=>b.Id==selectedBlock.Id);var usage=previous?.NativeSource?.BlockName==(b.ModelSpace?"*Model_Space":b.Name)?previous.Usage:null;t.Blocks.RemoveAll(b=>b.Id==selectedBlock.Id);t.Blocks.Add(new(){Id=selectedBlock.Id,Usage=usage,NativeSource=new(){FileHash=source.SourceHash,BlockName=b.ModelSpace?"*Model_Space":b.Name,ModelSpace=b.ModelSpace,Min=b.Min,Max=b.Max,DynamicProperties=b.DynamicProperties.Where(p=>!p.ReadOnly&&p.DefaultValue!=null&&(p.DefaultValue.Type==JTokenType.String||p.DefaultValue.Type==JTokenType.Integer||p.DefaultValue.Type==JTokenType.Float||p.DefaultValue.Type==JTokenType.Boolean)).ToList()}});}
  foreach(var selectedLayer in layers){Json.LayerName(selectedLayer.Id);var l=source.Layers.SingleOrDefault(l=>l.Name==selectedLayer.Name&&l.Supported)??throw new CadException("UNSUPPORTED_LAYER",selectedLayer.Name);t.Layers.RemoveAll(l=>l.Name==selectedLayer.Id);t.Layers.Add(new(){Name=selectedLayer.Id,Color=l.Color,Appearance=new(){LineType=l.LineType,Dashes=l.Dashes,PatternLength=l.PatternLength,LineWeight=l.LineWeight,Rgb=l.Rgb,Plottable=l.Plottable,SourceHash=source.SourceHash}});}
  if(t.Layers.Count==0)t.Layers.Add(new(){Name="A-AI",Color=7});Validation.Template(t);workspace.DemandTemplateImpact(t);return t;
 }
 public object Review(JObject request){var t=Build(request);return new{template=t,consumers=workspace.TemplateConsumers(t.Id),summary=new{blocks=t.Blocks.Count,layers=t.Layers.Count,font=t.Font,t.TextHeight,t.ArrowSize},warnings=new[]{"Block được giữ nguyên từ DWG, gồm màu/layer/style/thuộc tính; tên block nội bộ có thể đổi; layer gốc được giữ tên và kiểm tra xung đột, không tự đổi màu.","Style cho chữ/kích thước mới lấy font, chiều cao chữ, mũi tên; các thiết lập khác theo chuẩn app."}};}
 public object Publish(JObject request){if(request.Value<bool?>("confirmed")!=true)throw new CadException("REVIEW_REQUIRED","Xem lại tài nguyên và xác nhận trước khi xuất bản.");var t=Build(request);var source=Analysis(request.Value<string>("importId")!);string assets=WarehousePaths.Folder(config.ContentRoot,"content-assets");Directory.CreateDirectory(assets);string target=Path.Combine(assets,source.SourceHash+".dwg");if(!File.Exists(target))try{File.Copy(Path.Combine(Job(source.Id),"source.dwg"),target,false);}catch(IOException)when(File.Exists(target)){}if(Json.FileHash(target)!=source.SourceHash)throw new CadException("SOURCE_CHANGED","Bản sao thư viện CAD không khớp mã kiểm tra.");workspace.ImportTemplate(t);Json.Atomic(Path.Combine(WarehousePaths.Folder(config.ContentRoot,"packages"),"templates",t.Id,t.Version,"provenance.json"),new{source.Name,source.SourceHash,selectedBlocks=request["blocks"],selectedLayers=request["layers"],at=DateTime.UtcNow});return new{status="PUBLISHED",t.Id,t.Version,blocks=t.Blocks.Count,layers=t.Layers.Count};}
 public sealed class LibrarySelection {public string Name{get;set;}="";public string Id{get;set;}="";}
}
