using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
namespace AICad.Geometry;
public static class TaskGraph {
 public static List<string> Order(IReadOnlyList<TaskInfo> tasks){var byId=new Dictionary<string,TaskInfo>();foreach(var t in tasks){Json.Name(t.TaskId);if(byId.ContainsKey(t.TaskId))throw new CadException("DUPLICATE_TASK","Duplicate TASK_ID.");byId[t.TaskId]=t;}var visited=new HashSet<string>();var path=new HashSet<string>();var order=new List<string>();void Visit(string id){if(!byId.ContainsKey(id))throw new CadException("MISSING_DEPENDENCY",id);if(path.Contains(id))throw new CadException("DEPENDENCY_CYCLE","Cycle involving "+id);if(visited.Contains(id))return;path.Add(id);foreach(string dep in byId[id].Dependencies)Visit(dep);path.Remove(id);visited.Add(id);order.Add(id);}foreach(var t in tasks)Visit(t.TaskId);return order;}
 public static bool Ready(BatchNode node,IReadOnlyList<BatchNode> nodes)=>node.Status=="PENDING"&&node.Dependencies.All(d=>nodes.Single(n=>n.TaskId==d).Status=="DONE");
}
