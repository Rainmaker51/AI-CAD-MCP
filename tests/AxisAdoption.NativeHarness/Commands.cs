using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using AICad.Contracts;
using Autodesk.AutoCAD.Runtime;

[assembly:CommandClass(typeof(AxisAdoptionHarness.Commands))]
namespace AxisAdoptionHarness;
public sealed class Commands
{
 [CommandMethod("AXISADOPTIONVERIFY")]
 public void Run(){
  string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";
  if(!File.Exists(Path.Combine(root,".aicad-tools-test")))return;
  try{
   var axis=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Rainmaker.Axis");
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
   var type=axis.GetType("Rainmaker.Axis.AxisLibrary")!;
   var library=Activator.CreateInstance(type,flags,null,new object[]{root},null)!;
   var settings=type.GetMethod("Load",flags)!.Invoke(library,null)!;
   var options=((IEnumerable)type.GetField("Options",flags)!.GetValue(library)!).Cast<object>().ToArray();
   string Field(object o,string key)=>(string)o.GetType().GetField(key,flags)!.GetValue(o)!;
   var dims=options.Where(o=>Field(o,"Kind")=="dims"&&Field(o,"Name")!="Standard").Take(2).ToArray();
   if(dims.Length<2)throw new System.Exception("Two fixture Dim styles required");
   settings.GetType().GetProperty("BayDimId")!.SetValue(settings,Field(dims[0],"Id"));
   settings.GetType().GetProperty("TotalDimId")!.SetValue(settings,Field(dims[1],"Id"));
   var resolved=type.GetMethod("Resolve",flags)!.Invoke(library,new[]{settings,(object)false})!;
   axis.GetType("Rainmaker.Axis.AxisAdoptionNativeTests")!.GetMethod("Run",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new[]{(object)root,resolved});
  }catch(System.Exception error){AICad.Contracts.Json.Atomic(Path.Combine(root,"axis-adoption-results.json"),new{passed=false,error=error.ToString()});}
 }
}
