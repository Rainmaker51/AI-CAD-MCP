using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
namespace AICad.Geometry;
public static class TaskPlacement {
 public static List<Point2> OutputScope(TaskInfo task){if(task.OutputPoint==null)return task.Scope;Validation.Finite(task.OutputPoint.X);Validation.Finite(task.OutputPoint.Y);var origin=task.Scope[0];return task.Scope.Select(p=>new Point2(p.X-origin.X+task.OutputPoint.X,p.Y-origin.Y+task.OutputPoint.Y)).ToList();}
}
