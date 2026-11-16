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
   JsonUtil.Atomic(Path.Combine(root,"native-results.json"),new{passed=true,dotnetCommands=first.Commands.Count,reloadedVersion="VERSION_2",sourceDllUnlocked=true});
  }catch(System.Exception e){JsonUtil.Atomic(Path.Combine(root,"native-results.json"),new{passed=false,error=e.Message,stack=e.StackTrace});}
 }

 [CommandMethod("AICADRAINMAKERAXISTEST")]
 public void DrawExternalAxis()
 {
  string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";
  if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-tools-test")))return;
  var isolated=Path.Combine(root,"axis");
  var catalog=new CadToolCatalog(isolated);
  var package=catalog.Import(Path.Combine(root,"package","Rainmaker.Axis.dll"),catalog.Read().Revision,CadToolCatalog.AxisToolId);
  using var runtime=new CadToolRuntime(new InstallConfig{ContentRoot=isolated,DataRoot=isolated});
  runtime.Run(package,CadToolCatalog.AxisCommand);
 }

 [CommandMethod("AICADRAINMAKERTEST")]
 public void VerifyRainmaker()
 {
  string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";
  if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-tools-test")))return;
  try
  {
   var plugin=Plugin.Current!;
   var catalog=new CadToolCatalog(root);
   var config=new InstallConfig{ContentRoot=root,DataRoot=Path.Combine(root,"data")};
   using var runtime=new CadToolRuntime(config);
   var first=catalog.Import(Path.Combine(root,"v1","ToolManager.Fixture.dll"),0);
   var v1Names=runtime.Load(first).Commands.Select(c=>c.Name);
   catalog.Update(first.Id,1,first.Name,"Kiểm thử","",true,v1Names);
   catalog.SetRibbonPin(first.Id,2,new CadRibbonPin{Command="QATOOLFIXTURE",Label="Công cụ mẫu"},true);
   var updated=catalog.Import(Path.Combine(root,"v2","ToolManager.Fixture.dll"),3,first.Id);
   if(updated.RibbonPins.Single().Label!="Công cụ mẫu")throw new System.Exception("Pin lost during DLL update.");
   var v2Names=runtime.Load(updated).Commands.Select(c=>c.Name);
   catalog.Update(updated.Id,4,updated.Name,updated.Group,updated.Notes,true,v2Names);
   catalog.SetRibbonPin(updated.Id,5,new CadRibbonPin{Command="QATOOLNEW",Label="Lệnh mới",Order=10},true);
   runtime.Run(updated,"QATOOLNEW");
   if(catalog.Read().Items.Single().RibbonPins.Count!=2||File.ReadAllText(Path.Combine(root,"dotnet-ok.txt"))!="VERSION_2")
    throw new System.Exception("New command was not pinned and run.");
   var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
   var db=doc.Database;
   using var tx=db.TransactionManager.StartOpenCloseTransaction();
   var table=(Autodesk.AutoCAD.DatabaseServices.BlockTable)tx.GetObject(db.BlockTableId,Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
   var model=(Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tx.GetObject(table[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
   var entities=model.Cast<Autodesk.AutoCAD.DatabaseServices.ObjectId>().Select(id=>tx.GetObject(id,Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)).ToList();
   var line=entities.OfType<Autodesk.AutoCAD.DatabaseServices.Line>().Single(e=>e.Layer=="QA_KT_Truc");
   var heads=entities.OfType<Autodesk.AutoCAD.DatabaseServices.BlockReference>().Where(e=>e.Layer=="QA_KH_Truc Dinh Vi").ToList();
   if(heads.Count!=2||!heads.Any(h=>h.Position==line.StartPoint)||!heads.Any(h=>h.Position==line.EndPoint))
    throw new System.Exception("Axis endpoints or layers differ.");
   foreach(var head in heads)
   {
    var label=head.AttributeCollection.Cast<Autodesk.AutoCAD.DatabaseServices.ObjectId>()
     .Select(id=>(Autodesk.AutoCAD.DatabaseServices.AttributeReference)tx.GetObject(id,Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead))
     .Single(a=>a.Tag=="KI-HIEU-TRUC");
    if(label.TextString!="A"||head.ScaleFactors.X!=60)throw new System.Exception("Axis label or scale differs.");
   }
   if(plugin.Ribbon!=null) VerifyRibbon(plugin.Ribbon);
   JsonUtil.Atomic(Path.Combine(root,"rainmaker-results.json"),new{passed=true,axisHeads=heads.Count,verifiedRibbon=plugin.Ribbon!=null,updatedCommands=2});
  }
  catch(System.Exception e){JsonUtil.Atomic(Path.Combine(root,"rainmaker-results.json"),new{passed=false,error=e.ToString()});}
 }

 static void VerifyRibbon(RainmakerRibbon ribbon)
 {
  ribbon.Invalidate();
  ribbon.Update();
  var tab=Autodesk.Windows.ComponentManager.Ribbon.Tabs.Single(t=>t.Id==RainmakerRibbon.TabId);
  if(tab.Title!="Rainmaker"||!tab.Panels.SelectMany(p=>p.Source.Items).OfType<Autodesk.Windows.RibbonButton>().Any(b=>b.Text=="Tạo trục"))
   throw new System.Exception("Rainmaker tab or axis button missing.");
 }
}
