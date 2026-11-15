using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICad.Contracts;
using AICad.Geometry;
using Newtonsoft.Json.Linq;
namespace AICad.Application;
public sealed class TaskOrchestrator(InstallConfig config,WorkspaceService workspace){
 readonly SemaphoreSlim gate=new(1,1);string Folder=>Path.Combine(config.DataRoot,"batches");
 public List<BatchRecord> List(){Directory.CreateDirectory(Folder);return Directory.EnumerateFiles(Folder,"*.json").Select(f=>Json.Read<BatchRecord>(File.ReadAllText(f))).OrderByDescending(b=>b.UpdatedUtc).Take(50).ToList();}
 void Save(BatchRecord b){b.UpdatedUtc=DateTime.UtcNow;Json.Atomic(Path.Combine(Folder,Json.Id(b.Id)+".json"),b);}
 public async Task<BatchRecord> Create(string session,string document,string revision,JObject inputs,CancellationToken ct){var tasks=await workspace.Scan(session,document,revision,ct);TaskGraph.Order(tasks);if(tasks.Any(t=>t.Warnings.Count>0))throw new CadException("INVALID_TASK",string.Join("; ",tasks.SelectMany(t=>t.Warnings)));var store=new ContentStore(config.ContentRoot);var batch=new BatchRecord{SessionId=session,DocumentId=document};foreach(var t in tasks)batch.Nodes.Add(new(){TaskId=t.TaskId,Dependencies=t.Dependencies,SkillVersion=store.Active("skills",t.SkillId),TemplateVersion=store.Active("templates",t.TemplateId),Inputs=inputs[t.TaskId] as JObject??new()});Save(batch);await Tick(ct);return List().Single(b=>b.Id==batch.Id);}
 public async Task Tick(CancellationToken ct){if(!await gate.WaitAsync(0,ct))return;try{foreach(var batch in List().Where(b=>b.Status=="ACTIVE")){foreach(var node in batch.Nodes.Where(n=>n.PlanId!="")){var p=workspace.Plans.Read(node.PlanId);node.Status=p.Status;node.Error=p.Status=="FAILED"||p.Status=="STALE"||p.Status=="UNKNOWN"?p.Message:"";}foreach(var node in batch.Nodes.Where(n=>TaskGraph.Ready(n,batch.Nodes))){try{var docs=await workspace.Cad.Documents(ct);var d=docs.SingleOrDefault(d=>d.SessionId==batch.SessionId&&d.DocumentId==batch.DocumentId)??throw new CadException("SESSION_OFFLINE","Document unavailable; batch waits.");var input=(JObject)node.Inputs.DeepClone();input["_skillVersion"]=node.SkillVersion;input["_templateVersion"]=node.TemplateVersion;var p=await workspace.CreatePlan(batch.SessionId,batch.DocumentId,d.Revision,node.TaskId,input,ct);p.BatchId=batch.Id;using(workspace.Plans.Lock(p.Plan.PlanId))workspace.Plans.Write(p);node.PlanId=p.Plan.PlanId;node.Status=p.Status;}catch(CadException e){if(e.Code=="SESSION_OFFLINE"||e.Code=="CAD_TIMEOUT"){node.Error=e.Message;continue;}node.Status=e.Code=="NEEDS_INPUT"||e.Code=="NEEDS_SOURCE"?"NEEDS_INPUT":"FAILED";node.Error=e.Message;}}foreach(var node in batch.Nodes.Where(n=>n.Status=="PENDING"&&n.Dependencies.Any(dep=>batch.Nodes.Single(x=>x.TaskId==dep).Status is "FAILED" or "CANCELLED")))node.Status="BLOCKED";if(batch.Nodes.All(n=>n.Status is "DONE" or "FAILED" or "CANCELLED" or "BLOCKED"))batch.Status=batch.Nodes.All(n=>n.Status=="DONE")?"DONE":"COMPLETED_WITH_ERRORS";Save(batch);}}finally{gate.Release();}}
 public void Cancel(string id){var b=List().SingleOrDefault(b=>b.Id==Json.Id(id))??throw new CadException("BATCH_NOT_FOUND",id);foreach(var n in b.Nodes){if(n.PlanId!=""){var p=workspace.Plans.Read(n.PlanId);if(p.Status=="DISPATCHED"||p.Status=="UNKNOWN")continue;if(p.Status=="READY"||p.Status=="PREVIEW")workspace.Cancel(n.PlanId);}if(n.Status!="DONE")n.Status="CANCELLED";}b.Status="CANCELLED";Save(b);}
}
