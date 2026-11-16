using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;
[assembly:CommandClass(typeof(AICad.Bridge.CadToolNativeTests))]
namespace AICad.Bridge;
public sealed class CadToolNativeTests {
 [CommandMethod("AICADTOOLSTEST")]
 public void Run(){string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-tools-test")))return;
  try{
   var config=new InstallConfig{ContentRoot=root,DataRoot=Path.Combine(root,"data")};var catalog=new CadToolCatalog(root);using var runtime=new CadToolRuntime(config);
   var one=catalog.Import(Path.Combine(root,"v1","ToolManager.Fixture.dll"),0);var first=runtime.Load(one);if(first.Commands.Count!=1||first.Commands[0].Name!="QATOOLFIXTURE")throw new System.Exception("Command discovery mismatch");runtime.Run(one,"QATOOLFIXTURE");if(File.ReadAllText(Path.Combine(root,"dotnet-ok.txt"))!="VERSION_1")throw new System.Exception("V1 did not run");
   var two=catalog.Import(Path.Combine(root,"v2","ToolManager.Fixture.dll"),1,one.Id);runtime.Load(two,true);runtime.Run(two,"QATOOLFIXTURE");if(File.ReadAllText(Path.Combine(root,"dotnet-ok.txt"))!="VERSION_2")throw new System.Exception("V2 reload did not run");
   using(File.Open(Path.Combine(root,"v2","ToolManager.Fixture.dll"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
   var lisp=catalog.Import(Path.Combine(root,"hello.lsp"),2);if(!lisp.Commands.Contains("QATOOLSHELLO"))throw new System.Exception("Lisp discovery mismatch");File.WriteAllText(Path.Combine(root,"lisp-run.scr"),CadToolRuntime.LispExpression(catalog.Demand(lisp),"QATOOLSHELLO")+"_QUIT\n_Y\n");
   JsonUtil.Atomic(Path.Combine(root,"native-results.json"),new{passed=true,dotnetCommands=first.Commands.Count,reloadedVersion="VERSION_2",sourceDllUnlocked=true,lispCommands=lisp.Commands});
  }catch(System.Exception e){JsonUtil.Atomic(Path.Combine(root,"native-results.json"),new{passed=false,error=e.Message,stack=e.StackTrace});}
 }
}
