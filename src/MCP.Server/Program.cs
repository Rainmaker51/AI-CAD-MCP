using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;
var builder=Host.CreateApplicationBuilder(args);builder.Logging.ClearProviders();builder.Services.AddSingleton<HostClient>();builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<CadTools>();await builder.Build().RunAsync();
public sealed class HostClient {
 readonly string dataRoot;readonly HttpClient client=new(){Timeout=TimeSpan.FromSeconds(20)};
 public HostClient(){var config=JsonUtil.Read<InstallConfig>(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","install.json"))));dataRoot=config.DataRoot;}
 public async Task<JToken> Call(string path,JObject? body,CancellationToken ct){var info=JsonUtil.Read<JObject>(File.ReadAllText(Path.Combine(dataRoot,"host.json")));var uri=new Uri(info.Value<string>("url")!);if(uri.Scheme!="http"||uri.Host!="127.0.0.1")throw new CadException("UNTRUSTED_HOST","Host must listen only on loopback.");using var request=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,new Uri(uri,path));request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",info.Value<string>("token"));if(body!=null)request.Content=new StringContent(body.ToString(),Encoding.UTF8,"application/json");using var response=await client.SendAsync(request,ct);string json=await response.Content.ReadAsStringAsync(ct);if(!response.IsSuccessStatusCode){try{var error=JsonUtil.Read<JObject>(json);throw new CadException(error.Value<string>("code")??"HOST_ERROR",error.Value<string>("message")??"Host unavailable.");}catch(CadException){throw;}}return JsonUtil.Read<JToken>(json);}
}
[McpServerToolType]
public sealed class CadTools(HostClient host){
 async Task<CallToolResult> Call(string path,JObject? body,CancellationToken ct){try{var data=await host.Call(path,body,ct);return new(){Content=[new TextContentBlock{Text=data.ToString()}]};}catch(CadException e){return Error(e.Code,e.Message);}catch(Exception){return Error("HOST_OFFLINE","Start AI CAD app locally and load its plugin in AutoCAD.");}}
 static CallToolResult Error(string code,string message)=>new(){IsError=true,Content=[new TextContentBlock{Text=new JObject{["code"]=code,["message"]=message}.ToString()}]};
 async Task<CallToolResult> State(string key,CancellationToken ct){try{var s=await host.Call("/api/state",null,ct);return new(){Content=[new TextContentBlock{Text=s[key]!.ToString()}]};}catch(Exception){return Error("HOST_OFFLINE","Start AI CAD app locally.");}}
 [McpServerTool(Name="list_cad_utilities",ReadOnly=true),Description("List registered local LISP/.NET utilities, commands, purpose and source paths. Read-only discovery; loading/running code is done locally in AutoCAD CAD Tool Manager.")]public Task<CallToolResult> Utilities(CancellationToken ct=default)=>Call("/api/cad-tools",null,ct);
 [McpServerTool(Name="list_sessions",ReadOnly=true),Description("List live connected AutoCAD plugin sessions. No write authority.")]public Task<CallToolResult> Sessions(CancellationToken ct=default)=>State("sessions",ct);
 [McpServerTool(Name="list_documents",ReadOnly=true),Description("List open DWGs with explicit session/document/revision identity.")]public Task<CallToolResult> Documents(CancellationToken ct=default)=>State("documents",ct);
 [McpServerTool(Name="list_skills",ReadOnly=true),Description("List declarative Skills with enabled status, required inputs and missing tools.")]public Task<CallToolResult> Skills(CancellationToken ct=default)=>State("skills",ct);
 [McpServerTool(Name="list_templates",ReadOnly=true),Description("Read central template IDs, pinned versions and assets.")]public Task<CallToolResult> Templates(CancellationToken ct=default)=>State("templates",ct);
 [McpServerTool(Name="get_execution_log",ReadOnly=true),Description("Read persisted plan/execution history. UNKNOWN or DISPATCHED must be reconciled; never retry writing blindly.")]public Task<CallToolResult> History(CancellationToken ct=default)=>State("plans",ct);
 [McpServerTool(Name="scan_task_blocks",ReadOnly=true),Description("Read AI_TASK attributes and transformed scope. Detect duplicate IDs, unsupported scope and overlap.")]public Task<CallToolResult> Scan(string sessionId,string documentId,string revision,CancellationToken ct=default)=>Call("/api/scan",new(){["sessionId"]=sessionId,["documentId"]=documentId,["revision"]=revision},ct);
 [McpServerTool(Name="get_entities",ReadOnly=true),Description("Read paginated entity metadata in the explicit target DWG. Maximum 200 per page. Requires exact current revision.")]public Task<CallToolResult> Entities(string sessionId,string documentId,string revision,int offset=0,int limit=100,CancellationToken ct=default)=>Query("get_entities",sessionId,documentId,revision,new(){["offset"]=offset,["limit"]=limit},ct);
 [McpServerTool(Name="get_layers",ReadOnly=true)]public Task<CallToolResult> Layers(string sessionId,string documentId,string revision,CancellationToken ct=default)=>Query("get_layers",sessionId,documentId,revision,new(),ct);
 [McpServerTool(Name="get_blocks",ReadOnly=true)]public Task<CallToolResult> Blocks(string sessionId,string documentId,string revision,CancellationToken ct=default)=>Query("get_blocks",sessionId,documentId,revision,new(),ct);
 [McpServerTool(Name="get_document_info",ReadOnly=true)]public Task<CallToolResult> Info(string sessionId,string documentId,string revision,CancellationToken ct=default)=>Query("get_document_info",sessionId,documentId,revision,new(),ct);
 Task<CallToolResult> Query(string method,string session,string document,string revision,JObject p,CancellationToken ct)=>Call("/api/query",new(){["method"]=method,["sessionId"]=session,["documentId"]=document,["revision"]=revision,["parameters"]=p},ct);
 [McpServerTool(Name="run_skill_plan",ReadOnly=false),Description("Validate Skill dependencies and confirmed inputs; create a pinned plan for an AI_TASK. Does not modify CAD. inputsJson is a JSON object of confirmed numeric parameters.")]public Task<CallToolResult> Plan(string sessionId,string documentId,string revision,string taskId,string inputsJson="{}",CancellationToken ct=default)=>Call("/api/plan",new(){["sessionId"]=sessionId,["documentId"]=documentId,["revision"]=revision,["taskId"]=taskId,["inputs"]=JsonUtil.Read<JObject>(inputsJson)},ct);
 [McpServerTool(Name="create_preview",ReadOnly=false),Description("Queue a preview of an existing validated plan in the explicitly bound AutoCAD DWG. User must activate target DWG and approve inside AutoCAD. This tool cannot approve or Apply.")]public Task<CallToolResult> Preview(string planId,CancellationToken ct=default)=>Call("/api/preview",new(){["planId"]=planId},ct);
 [McpServerTool(Name="cancel_preview",ReadOnly=false),Description("Cancel only an undispatched plan. Cannot undo or cancel an uncertain execution.")]public Task<CallToolResult> Cancel(string planId,CancellationToken ct=default)=>Call("/api/cancel",new(){["planId"]=planId},ct);
}
