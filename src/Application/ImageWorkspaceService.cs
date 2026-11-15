using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
namespace AICad.Application;
public sealed class ImageWorkspaceService(InstallConfig config,WorkspaceService workspace){
 public async Task<JObject> Create(string sourceId,string sessionId,string documentId,string revision,CancellationToken ct){
  var model=Json.Read<JObject>(File.ReadAllText(workspace.Intelligence.AnalysisPath(Json.Id(sourceId))));
  string templateId=model["reviewMetadata"]?.Value<string>("templateId")??"ARCHITECTURE",skillId=model["reviewMetadata"]?.Value<string>("skillId")??"SOURCE_FLOORPLAN";
  DrawingIntelligence.DemandDraft(skillId,model);
  if(workspace.Store.Skill(skillId).TemplateId!=templateId)throw new CadException("SKILL_MISMATCH","Thư viện và Skill nguồn không khớp.");
  var session=workspace.Cad.Sessions().SingleOrDefault(s=>s.SessionId==sessionId)??throw new CadException("SESSION_OFFLINE","Chọn phiên AutoCAD đang kết nối.");
  if(!Version.TryParse(session.PluginVersion,out var version)||version<new Version(0,5,0))throw new CadException("PLUGIN_UPDATE_REQUIRED","Lưu DWG và mở lại qua Mo AI CAD.cmd để dùng điểm đặt kết quả.");
  var info=(await workspace.Cad.Call(sessionId,"get_document_info",documentId,revision,cancellation:ct)).ToObject<DocumentInfo>()!;
  if(info.Space!="Model"||info.Units!="Millimeters")throw new CadException("WRONG_CONTEXT","Chọn DWG đang ở Model và đơn vị mm.");
  if(!info.IsActive){await workspace.Cad.Call(sessionId,"select_document",documentId,info.Revision,cancellation:ct);info=(await workspace.Cad.Call(sessionId,"get_document_info",documentId,cancellation:ct)).ToObject<DocumentInfo>()!;}
  var b=model["bounds"]??throw new CadException("NEEDS_ANALYSIS","Phân tích và hiệu chuẩn nguồn trước.");double width=Math.Ceiling((b.Value<double>("maxX")-b.Value<double>("minX")+(skillId==ResidentialSourcePlan.SkillId?4200:2500))/1000)*1000,height=Math.Ceiling((b.Value<double>("maxY")-b.Value<double>("minY")+(skillId==ResidentialSourcePlan.SkillId?4200:2500))/1000)*1000;
  string taskId="IMG_"+sourceId.Substring(0,8);DateTime started=DateTime.UtcNow;
  await workspace.Cad.Call(sessionId,"prepare_source_task",documentId,info.Revision,new(){["sourceId"]=sourceId,["skillId"]=skillId,["templateId"]=templateId,["width"]=width,["height"]=height},ct);
  for(int i=0;i<30;i++){await Task.Delay(250,ct);info=(await workspace.Cad.Call(sessionId,"get_document_info",documentId,cancellation:ct)).ToObject<DocumentInfo>()!;var tasks=await workspace.Scan(sessionId,documentId,info.Revision,ct);var task=tasks.SingleOrDefault(t=>t.TaskId==taskId);if(task?.OutputPoint!=null){if(task.Warnings.Count>0)throw new CadException("INVALID_TASK",string.Join("; ",task.Warnings));var result=JObject.FromObject(new{sessionId,documentId,revision=info.Revision,taskId,outputPoint=task.OutputPoint,width,height,name=info.Name,status="READY_IN_CONNECTED_DWG"});Json.Atomic(Path.Combine(workspace.Intelligence.Folder,sourceId+".workspace.json"),result);return result;}
   string error=Path.Combine(config.DataRoot,"last-source-task-error.json");if(File.Exists(error)){var e=Json.Read<JObject>(File.ReadAllText(error));if(e.Value<string>("sourceId")==sourceId&&e.Value<DateTime>("at")>=started)throw new CadException(e.Value<string>("code")??"CAD_ERROR",e.Value<string>("message")??"Chưa tạo được khung nguồn.");}
  }throw new CadException("CAD_BUSY","AutoCAD chưa hoàn thành tạo khung. Kiểm tra dòng lệnh rồi quét khung; không tự tạo phiên CAD khác.");
 }
}
