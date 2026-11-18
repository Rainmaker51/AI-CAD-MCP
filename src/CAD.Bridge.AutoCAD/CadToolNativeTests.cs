using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;
[assembly:CommandClass(typeof(AICad.Bridge.CadToolNativeTests))]
namespace AICad.Bridge;
public sealed class CadToolNativeTests {
 [CommandMethod("AICADRIBBONMANAGERTEST")]
 public void VerifyManagedTabs(){
  string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-tools-test")))return;
  try{
   string app=Path.Combine(root,"r-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(app);var catalog=new CadToolCatalog(app);var config=new InstallConfig{ContentRoot=app,DataRoot=Path.Combine(root,"d"),HostExecutable=Plugin.Current!.Config.HostExecutable};string dispatched="";
   using var runtime=new CadToolRuntime(config,(id,command)=>dispatched=id+":"+command);
   var tool=catalog.Import(Path.Combine(root,"library","Rainmaker.Library.dll"),0);var loaded=runtime.Load(tool);
   string dataTest=Path.Combine(root,"library-data");Directory.CreateDirectory(dataTest);File.WriteAllText(Path.Combine(dataTest,".library-manager-test"),"Isolated library data test");
   string? previousRoot=Environment.GetEnvironmentVariable("QALIB_TEST_ROOT");
   try{
    Environment.SetEnvironmentVariable("QALIB_TEST_ROOT",dataTest);
    var tests=loaded.Assembly.GetType("Rainmaker.Library.NativeTests")!;
    tests.GetMethod("Run")!.Invoke(Activator.CreateInstance(tests),null);
    var result=JsonUtil.Read<Newtonsoft.Json.Linq.JObject>(File.ReadAllText(Path.Combine(dataTest,"result.json")));
    if(result.Value<bool>("passed")!=true)throw new System.Exception("Library data round-trip through Manager failed: "+result["error"]);
   }finally{Environment.SetEnvironmentVariable("QALIB_TEST_ROOT",previousRoot);}
   if(loaded.Commands.Count!=17||loaded.Commands.Any(c=>c.Name.EndsWith("TEST")||c.Name=="QALIBUIVERIFY"))throw new System.Exception("Managed command manifest not respected");
   catalog.Update(tool.Id,1,"Thư viện","Thư viện","",true,loaded.Commands.Select(c=>c.Name));string tab=catalog.SaveTab(2,"","Thư viện",10);
   catalog.Update(tool.Id,3,"Thư viện","Thư viện","",true,loaded.Commands.Select(c=>c.Name),tabId:tab);catalog.PinCommands(tool.Id,4,loaded.Commands.Select(c=>c.SuggestedPin!));
   loaded.Assembly.GetType("Rainmaker.Library.LibraryPlugin")!.GetMethod("Send",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,new object[]{"QALIBPACK"});if(dispatched!=tool.Id+":QALIBPACK")throw new System.Exception("Palette bypassed Manager dispatcher");
   var control=new Autodesk.Windows.RibbonControl();var external=new Autodesk.Windows.RibbonTab{Id="EXTERNAL_NATIVE_HOME",Title="Home"};control.Tabs.Add(external);var fake=new Plugin{Config=config};var renderer=new RainmakerRibbon(fake);renderer.RenderForTest(control,catalog.Read());
   var created=control.Tabs.Single(t=>t.Id==RainmakerRibbon.NativeTabId(tab));var labels=created.Panels.SelectMany(p=>p.Source.Items).OfType<Autodesk.Windows.RibbonButton>().Select(b=>b.Text).ToArray();if(!labels.Contains("Nhận vào nhóm")||!labels.Contains("Chuyển nhóm")||!labels.Contains("Xếp gọn nhóm"))throw new System.Exception("Library command labels missing");
   catalog.SaveTab(5,tab,"Thư viện mới",20);renderer.RenderForTest(control,catalog.Read());if(control.Tabs.Single(t=>t.Id==RainmakerRibbon.NativeTabId(tab)).Title!="Thư viện mới")throw new System.Exception("Tab rename failed");
   catalog.DeleteTab(6,tab);renderer.RenderForTest(control,catalog.Read());if(control.Tabs.Any(t=>t.Id==RainmakerRibbon.NativeTabId(tab))||!control.Tabs.Contains(external)||!control.Tabs.Single(t=>t.Id==RainmakerRibbon.TabId).Panels.SelectMany(p=>p.Source.Items).OfType<Autodesk.Windows.RibbonButton>().Any(b=>b.Text=="Nhận vào nhóm"))throw new System.Exception("Delete tab removed external UI or lost command routing");
   File.WriteAllText(Path.Combine(root,"native-step.txt"),"before-disable");
   runtime.Disable(tool.Id);File.WriteAllText(Path.Combine(root,"native-step.txt"),"after-disable");if(loaded.Assembly.GetType("Rainmaker.Library.LibraryPlugin")!.GetField("Current",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!=null)throw new System.Exception("Disable did not stop addin lifecycle");
   renderer.Remove();File.WriteAllText(Path.Combine(root,"native-step.txt"),"after-remove");if(control.Tabs.Count!=1||control.Tabs[0]!=external)throw new System.Exception("Manager removed foreign tabs");
   if(System.Diagnostics.Process.GetCurrentProcess().ProcessName.Equals("acad",StringComparison.OrdinalIgnoreCase))using(var palette=new CadToolPalette(fake)){
    var tabs=(System.Windows.Controls.TabControl)typeof(CadToolPalette).GetField("tabs",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(palette);tabs.SelectedIndex=2;
    var view=(System.Windows.FrameworkElement)System.Windows.Media.VisualTreeHelper.GetParent(tabs);view.Measure(new System.Windows.Size(620,860));view.Arrange(new System.Windows.Rect(0,0,620,860));view.UpdateLayout();
    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(620,860,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(root,"ribbon-manager-palette.png"));encoder.Save(output);
   }
   JsonUtil.Atomic(Path.Combine(root,"ribbon-manager-results.json"),new{passed=true,managedCommands=17,customTabs=true,paletteDispatch=true,rename=true,deleteMovesCommands=true,externalTabsPreserved=true,disableTerminates=true});
  }catch(System.Exception error){JsonUtil.Atomic(Path.Combine(root,"ribbon-manager-results.json"),new{passed=false,error=error.ToString()});}
 }
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
