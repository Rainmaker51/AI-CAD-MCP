using System;
using System.IO;
using System.Threading;
namespace AICad.Contracts;
public sealed class PlanFiles {
 readonly string root;public PlanFiles(string dataRoot){root=Path.Combine(dataRoot,"plans");Directory.CreateDirectory(root);}
 public string PathFor(string id)=>Path.Combine(root,Json.Id(id)+".json");
 public PlanRecord Read(string id)=>Json.Read<PlanRecord>(File.ReadAllText(PathFor(id)));
 public void Write(PlanRecord record){record.UpdatedUtc=DateTime.UtcNow;Json.Atomic(PathFor(record.Plan.PlanId),record);}
 public IDisposable Lock(string id){var mutex=new Mutex(false,"Local\\AICadPlan_"+Json.Hash(root).Substring(0,16)+Json.Id(id));bool taken=false;try{try{taken=mutex.WaitOne(2000);}catch(AbandonedMutexException){taken=true;}if(!taken)throw new CadException("BUSY","Plan is in use.");return new Lease(mutex);}catch{mutex.Dispose();throw;}}
 sealed class Lease:IDisposable{readonly Mutex mutex;public Lease(Mutex value){mutex=value;}public void Dispose(){mutex.ReleaseMutex();mutex.Dispose();}}
}
public sealed class SkillDefinition { [Newtonsoft.Json.JsonProperty(NullValueHandling=Newtonsoft.Json.NullValueHandling.Ignore)] public DraftingRecipe? Recipe {get;set;} public string Id {get;set;}="";public string Name {get;set;}="";public string Version {get;set;}="";public bool Enabled {get;set;}=true;public string TemplateId {get;set;}="STARTER";public string[] RequiredTools {get;set;}=Array.Empty<string>();public string[] RequiredInputs {get;set;}=Array.Empty<string>();public string Builder {get;set;}="workflow"; public System.Collections.Generic.Dictionary<string,double> Parameters {get;set;}=new(); public System.Collections.Generic.List<Operation> Workflow {get;set;}=new();}
