using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
namespace AICad.Application;
public sealed class StyleLibraryService(InstallConfig config){
 readonly SemaphoreSlim gate=new(1,1);
 string Root=>Path.Combine(WarehousePaths.Resolve(config.ContentRoot),"Styles","ISO_QUANGANH");
 static string Kind(string kind)=>kind is "texts" or "dims"?kind:throw new CadException("INVALID_KIND",kind);
 JObject Raw(){string active=Path.Combine(Root,"active.json");if(!File.Exists(active))return new JObject{["texts"]=new JArray(),["dims"]=new JArray()};var p=Json.Read<JObject>(File.ReadAllText(active));string file=Path.Combine(Root,ContentStore.VersionName(p.Value<string>("version")!),"catalog.json");if(Json.FileHash(file)!=p.Value<string>("checksum"))throw new CadException("RESOURCE_CHANGED","Danh mục Text/Dim đã thay đổi.");return Json.Read<JObject>(File.ReadAllText(file));}
 public JObject Read(string kind){Kind(kind);var raw=Raw();var catalog=new LibraryCatalog(config.ContentRoot);var items=(JArray)raw[kind]!.DeepClone();foreach(var i in items.OfType<JObject>())i["management"]=JObject.FromObject(catalog.Label(kind,i.Value<string>("id")!));return new JObject{["version"]=raw["version"]?.DeepClone(),["sourceHash"]=raw["sourceHash"]?.DeepClone(),["sourceName"]=raw["sourceName"]?.DeepClone(),["items"]=items,["groups"]=JArray.FromObject(catalog.Groups(kind))};}
 public LibraryLabel Update(string kind,JObject p){Kind(kind);string id=p.Value<string>("id")??"";if(!Raw()[kind]!.OfType<JObject>().Any(i=>i.Value<string>("id")==id))throw new CadException("NOT_FOUND","Style không còn trong thư viện.");return new LibraryCatalog(config.ContentRoot).UpdateHatch(id,p.Value<int?>("revision")??-1,p.Value<string>("name")??"",p.Value<string>("purpose")??"",p.Value<string>("notes")??"",p.Value<string>("groupId")??"",p.Value<bool?>("reviewed")??false,p.Value<string>("action")??"update",kind);}
 public JArray SkillContext(string kind)=>new(Read(kind)["items"]!.OfType<JObject>().Where(i=>i["management"]!.Value<bool>("Reviewed")&&i["management"]!["DeletedUtc"]?.Type==JTokenType.Null).Select(i=>i.DeepClone()));
 public async Task<JObject> Import(CancellationToken ct){
  if(!await gate.WaitAsync(0,ct))throw new CadException("LIBRARY_BUSY","Đang đọc Text/Dim.");
  try{
   string source=CadSourceFile.BaseLibraryPath(config.ContentRoot);string hash=CadSourceFile.Hash(source);
   string job=Path.Combine(config.DataRoot,"style-jobs",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);CadSourceFile.Copy(source,Path.Combine(job,"source.dwg"));if(Json.FileHash(Path.Combine(job,"source.dwg"))!=hash)throw new CadException("SOURCE_CHANGED","DWG đang thay đổi.");File.WriteAllText(Path.Combine(job,".aicad-library-job"),"AI CAD styles");
   string scanner=Path.Combine(Path.GetDirectoryName(config.HostExecutable)!,"LibraryScanner","CAD.LibraryScanner.dll"),script=Path.Combine(job,"scan.scr");File.WriteAllText(script,"_NETLOAD\n\""+scanner.Replace('\\','/')+"\"\nAICADSTYLECATALOG\n_QUIT\n_Y\n",Encoding.ASCII);
   var start=new ProcessStartInfo(@"C:\Program Files\Autodesk\AutoCAD 2021\accoreconsole.exe"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string a in new[]{"/i",Path.Combine(config.ContentRoot,"content","launch","blank-mm.dwg"),"/s",script,"/isolate","AICadStyles",Path.Combine(job,"profile")})start.ArgumentList.Add(a);start.Environment["AICAD_LIBRARY_JOB"]=job;
   using var process=Process.Start(start)??throw new CadException("SCANNER_MISSING","Không mở được bộ đọc style.");var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();using var limit=CancellationTokenSource.CreateLinkedTokenSource(ct);limit.CancelAfter(TimeSpan.FromMinutes(3));try{await process.WaitForExitAsync(limit.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);ct.ThrowIfCancellationRequested();throw new CadException("SCAN_TIMEOUT","Đọc Text/Dim quá thời gian.");}await Task.WhenAll(stdout,stderr);
   string output=Path.Combine(job,"styles.json");if(!File.Exists(output))throw new CadException("SCAN_FAILED","Chưa có kết quả Text/Dim.");var data=Json.Read<JObject>(File.ReadAllText(output));if(data.Value<string>("error") is string error)throw new CadException("SCAN_FAILED",error);if(data.Value<string>("sourceHash")!=hash||CadSourceFile.Hash(source)!=hash)throw new CadException("SOURCE_CHANGED","DWG nguồn đổi trong lúc quét.");
   foreach(var note in data["annotations"]!.OfType<JObject>())note["decodedText"]=SourceAnnotationText.Decode(note.Value<string>("text")??"",note.Value<string>("style")??"");
   long sequence=DateTimeOffset.UtcNow.ToUnixTimeSeconds();while(Directory.Exists(Path.Combine(Root,"1.0."+sequence)))sequence++;string version="1.0."+sequence;data["version"]=version;data["sourceName"]=Path.GetFileName(source);string folder=Path.Combine(Root,version);Directory.CreateDirectory(folder);Json.Atomic(Path.Combine(folder,"catalog.json"),data);Json.Atomic(Path.Combine(Root,"active.json"),new{version,checksum=Json.FileHash(Path.Combine(folder,"catalog.json"))});
   JObject legend=new(){["updated"]=0,["warning"]="Cập nhật Hatch từ cùng DWG nguồn để lấy chú giải vật liệu."};
   if(new HatchLibraryService(config).Read().Value<string>("sourceHash")==hash)legend=ApplyHatchLegend(data);
   return new JObject{["texts"]=Read("texts"),["dims"]=Read("dims"),["legendResult"]=legend};
  }finally{gate.Release();}
 }
 public JObject ApplyHatchLegend(JObject data){
  var hatchService=new HatchLibraryService(config);var hatches=hatchService.Read();if(hatches.Value<string>("sourceHash")!=data.Value<string>("sourceHash"))throw new CadException("SOURCE_CHANGED","Nguồn hatch khác DWG chú giải. Cập nhật Hatch từ cùng DWG trước.");
  var annotations=data["annotations"]!.OfType<JObject>().ToArray();var results=new JArray();int updated=0;
  foreach(var hatch in data["hatchPositions"]!.OfType<JObject>().Where(h=>h.Value<string>("block")=="*Model_Space")){
   double width=hatch.Value<double>("maxX")-hatch.Value<double>("minX"),minY=hatch.Value<double>("minY"),maxY=hatch.Value<double>("maxY");
   var labels=annotations.Where(a=>a.Value<string>("block")==hatch.Value<string>("block")&&a.Value<double>("maxX")<hatch.Value<double>("minX")&&hatch.Value<double>("minX")-a.Value<double>("maxX")<width*2&&((a.Value<double>("minY")+a.Value<double>("maxY"))/2)>=minY&&((a.Value<double>("minY")+a.Value<double>("maxY"))/2)<=maxY).ToArray();
   if(labels.Length!=1)continue;string label=labels[0].Value<string>("decodedText")??"";if(string.IsNullOrWhiteSpace(label)||label.Length>120)continue;
   string id=hatch.Value<string>("id")!;var item=hatches["items"]!.OfType<JObject>().SingleOrDefault(i=>i.Value<string>("id")==id);if(item==null)continue;
   var catalog=new LibraryCatalog(config.ContentRoot);var m=catalog.Label("hatches",id);results.Add(new JObject{["id"]=id,["pattern"]=hatch["pattern"]!.DeepClone(),["label"]=label,["textHandle"]=labels[0]["handle"]!.DeepClone(),["hatchHandle"]=hatch["handle"]!.DeepClone()});
  }
  foreach(var matchGroup in results.OfType<JObject>().GroupBy(x=>x.Value<string>("id")!)){
   string id=matchGroup.Key;var m=new LibraryCatalog(config.ContentRoot).Label("hatches",id);string label=string.Join("; ",matchGroup.Select(x=>x.Value<string>("label")).Distinct());
   if(m.Name!=""||m.Purpose!=""||m.DeletedUtc.HasValue)continue;
   hatchService.Update(JObject.FromObject(new{id,revision=m.Revision,name=label.Length<=120?label:label.Substring(0,120),purpose=label,notes="Chú giải DWG gốc; text handle "+string.Join(", ",matchGroup.Select(x=>x.Value<string>("textHandle")))+", hatch handle "+string.Join(", ",matchGroup.Select(x=>x.Value<string>("hatchHandle")))+". Cần rà soát trước khi duyệt cho Skill.",reviewed=false,action="update"}));updated++;
  }
  Json.Atomic(Path.Combine(Root,"hatch-legend-evidence.json"),new{sourceHash=data.Value<string>("sourceHash"),updated,matches=results});return new JObject{["updated"]=updated,["matches"]=results};
 }
}
