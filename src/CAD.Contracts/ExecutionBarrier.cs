using System;
using System.IO;
using Newtonsoft.Json.Linq;
namespace AICad.Contracts;
// Caller holds the physical drawing mutex. Unknown/dispatched intent never expires.
public sealed class ExecutionBarrier {
 readonly string path;public ExecutionBarrier(string root,string physicalDrawingKey){path=Path.Combine(root,"barriers",Json.Hash(physicalDrawingKey)+".json");}
 public void DemandAvailable(){if(!File.Exists(path))return;var state=Json.Read<JObject>(File.ReadAllText(path));string status=state.Value<string>("status")??"UNKNOWN";if(status!="DONE"&&status!="FAILED")throw new CadException("EXECUTION_UNKNOWN","This DWG has an unresolved execution. Reconcile its transactional receipt before any further write.");}
 public void Dispatch(string planId,string executionId){DemandAvailable();Json.Atomic(path,new{status="DISPATCHED",planId=Json.Id(planId),executionId=Json.Id(executionId),at=DateTime.UtcNow});}
 public void Finish(string planId,string executionId,string status){if(status!="DONE"&&status!="FAILED")throw new CadException("INVALID_STATE","Only known terminal executions may release a drawing fence.");var state=Json.Read<JObject>(File.ReadAllText(path));if(state.Value<string>("planId")!=planId||state.Value<string>("executionId")!=executionId)throw new CadException("EXECUTION_UNKNOWN","Drawing fence belongs to another execution.");Json.Atomic(path,new{status,planId,executionId,at=DateTime.UtcNow});}
}
