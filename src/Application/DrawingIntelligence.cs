using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
namespace AICad.Application;
public sealed class DrawingIntelligence(InstallConfig config){
 public string Folder=>Path.Combine(config.DataRoot,"sources");
 public string AnalysisPath(string id)=>Path.Combine(Folder,Json.Id(id)+".analysis.json");
 public string? SourceForTask(TaskInfo task)
 {
  if(!System.Text.RegularExpressions.Regex.IsMatch(task.TaskId,@"\AIMG_[a-f0-9]{8}\z")||!Directory.Exists(Folder))
   return null;
  var matches=Directory.EnumerateFiles(Folder,task.TaskId.Substring(4)+"*.analysis.json")
   .Select(path=>Path.GetFileName(path).Split('.')[0])
   .Where(id=>Guid.TryParseExact(id,"N",out _))
   .Where(id=>{
    var model=Json.Read<JObject>(File.ReadAllText(AnalysisPath(id)));
    var profile=model["reviewMetadata"];
    return profile?.Value<string>("skillId")==task.SkillId&&profile.Value<string>("templateId")==task.TemplateId
     &&(task.SkillId!=ResidentialSourcePlan.SkillId||model["reviewOperations"] is JArray {Count:>0});
   }).Take(2).ToArray();
  return matches.Length==1?matches[0]:null;
 }
 internal static void DemandDraft(string skillId,JObject model){
  if(skillId==ResidentialSourcePlan.SkillId&&model["reviewOperations"] is not JArray { Count: > 0 })
   throw new CadException("SOURCE_DRAFT_REQUIRED","Ảnh mới qua OCR. Nhập đủ hai kích thước bao và bấm Phân tích & dựng bằng AI trong app trước khi chuyển sang CAD.");
 }
 public async Task<JObject> Analyze(string id,JObject calibration,int page,CancellationToken ct){string metadata=Path.Combine(Folder,Json.Id(id)+".json");var meta=Json.Read<JObject>(File.ReadAllText(metadata));string filename=meta.Value<string>("name")??"";string ext=Path.GetExtension(filename).ToLowerInvariant();string source=Path.Combine(Folder,id+ext);if(!File.Exists(source)||Json.FileHash(source)!=meta.Value<string>("hash"))throw new CadException("SOURCE_CHANGED","Uploaded source no longer matches checksum.");string cached=AnalysisPath(id);if(File.Exists(cached)){var old=Json.Read<JObject>(File.ReadAllText(cached));if(old.Value<string>("sourceHash")==meta.Value<string>("hash")&&old.Value<int>("page")==page&&Json.Hash(old["calibration"]!)==Json.Hash(calibration))return old;}var (python,renderer)=SourceRuntime.Resolve(config);var request=new JObject{["sourceId"]=id,["path"]=source,["hash"]=meta["hash"],["calibration"]=calibration,["page"]=page,["pdftoppm"]=renderer};string input=Path.Combine(Folder,id+".request.json");Json.Atomic(input,request);string output=AnalysisPath(id);var start=new ProcessStartInfo(python){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=config.ContentRoot};start.ArgumentList.Add(Path.Combine(config.ContentRoot,"src","Drawing.Intelligence","worker.py"));foreach(string arg in new[]{"--request",input,"--output",output})start.ArgumentList.Add(arg);start.Environment["PYTHONIOENCODING"]="utf-8";using var process=Process.Start(start)??throw new CadException("INTELLIGENCE_FAILED","Cannot start source worker.");var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(150));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw new CadException("INTELLIGENCE_TIMEOUT","Source analysis timed out; no CAD write was attempted.");}string error=await stderr;_ = await stdout;if(process.ExitCode!=0)throw new CadException("INTELLIGENCE_FAILED",error.Length>1500?error.Substring(0,1500):error);meta["status"]="REVIEW_REQUIRED";meta["analysisHash"]=Json.FileHash(output);Json.Atomic(metadata,meta);return Json.Read<JObject>(File.ReadAllText(output));}
 public List<Operation> Map(string id,JObject input,out SourceBinding binding,out string modelHash){string path=AnalysisPath(id);var model=Json.Read<JObject>(File.ReadAllText(path));if(input.Value<bool?>("confirmGeometry")!=true)throw new CadException("NEEDS_INPUT","Review source geometry/confidence and explicitly confirm before creating CAD entities.");double minX=model["bounds"]!.Value<double>("minX"),minY=model["bounds"]!.Value<double>("minY");Point2 Position(JToken p)=>new(p.Value<double>("X")-minX+1000,p.Value<double>("Y")-minY+1000);var ops=new List<Operation>();if(model["reviewOperations"] is JArray reviewed){ops=reviewed.ToObject<List<Operation>>()!;foreach(var op in ops)op.Points=op.Points.Select(p=>new Point2(p.X-minX+1000,p.Y-minY+1000)).ToList();binding=new(){SourceId=id,Hash=Json.FileHash(path)};modelHash=Json.Hash(model);return ops;}var elements=(JArray)model["elements"]!;var rejected=input["rejectElementIds"]?.Values<string>().Select(x=>x!).ToHashSet()??new HashSet<string>();foreach(var e in elements){if(rejected.Contains(e.Value<string>("id")??""))continue;string kind=e.Value<string>("kind")!;if(kind=="door_candidate")continue;var points=((JArray)e["points"]!).Select(Position).ToList();string layer=kind=="wall"?"A-WALL":kind=="column"?"A-COLUMN":kind=="axis"?"A-AXIS":"A-AI";ops.Add(new(){Kind=points.Count>2?"draw_polyline":"draw_line",Layer=layer,Points=points,Closed=points.Count>2});}if(input["doors"] is JArray doors)foreach(var door in doors){if(door.Value<bool?>("confirmed")!=true)throw new CadException("NEEDS_INPUT","Confirm door hinge, size and rotation.");double width=door.Value<double>("widthMm");if(Math.Abs(width-900)>.01)throw new CadException("MISSING_ASSET","Starter door asset is 900 mm. Publish the correct-size asset first.");ops.Add(new(){Kind="insert_block",Layer="A-DOOR",AssetId="DOOR_900",Rotation=door.Value<double>("rotation"),Points=new(){new(door.Value<double>("x")-minX+1000,door.Value<double>("y")-minY+1000)}});}if(ops.Count<1)throw new CadException("NO_GEOMETRY","No reviewed geometry to map.");binding=new(){SourceId=id,Hash=Json.FileHash(path)};modelHash=Json.Hash(model);return ops;}
}
