using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
using Exception=System.Exception;
[assembly:CommandClass(typeof(AICad.Bridge.CadSharedComponentNativeTests))]
namespace AICad.Bridge;
public sealed class CadSharedComponentNativeTests {
 [CommandMethod("AICADDETACHEDREADERTEST")]
 public void Run(){
  string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";if(!File.Exists(Path.Combine(root,".aicad-tools-test")))return;
  try{
   void Check(bool value,string message){if(!value)throw new Exception(message);}
   var plugin=Plugin.Current!;var store=new CadSharedComponentStore(plugin.Config.DataRoot);
   Check(!File.Exists(Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"Rainmaker.Library.Reader.dll")),"Manager package still contains Reader");
   using(var palette=new CadToolPalette(plugin,true)){Check(palette.RootForTest!=null&&store.Read()==null,"Manager must open without Reader");}
   var catalog=new CadToolCatalog(Path.Combine(root,"isolated-manager"));var tool=catalog.Import(Path.Combine(root,"package","Rainmaker.Axis.dll"),0);
   using var runtime=new CadToolRuntime(new InstallConfig{ContentRoot=Path.Combine(root,"isolated-manager"),DataRoot=Path.Combine(root,"runtime-data")});
   try{runtime.Load(tool);throw new Exception("Axis loaded without Reader");}catch(CadException e)when(e.Code=="LIBRARY_READER_MISSING"){}
   try{CadReaderDependency.Load();throw new Exception("Missing Reader silently loaded");}catch(CadException e)when(e.Code=="LIBRARY_READER_MISSING"){}
   var installed=store.Install(Path.Combine(root,"reader-package","Rainmaker.Library.Reader.dll"),0);var reader=CadReaderDependency.Load();
   Check(reader.Location==store.Demand(installed)&&AppDomain.CurrentDomain.GetAssemblies().Count(a=>a.GetName().Name==CadSharedComponentStore.ReaderName)==1,"Reader not loaded from separate installation");
   using(var palette=new CadToolPalette(plugin,true)){
    var tabs=(TabControl)typeof(CadToolPalette).GetField("tabs",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(palette)!;tabs.SelectedIndex=3;
    var view=palette.RootForTest;view.Measure(new Size(620,900));view.Arrange(new Rect(0,0,620,900));view.UpdateLayout();var bmp=new RenderTargetBitmap(620,900,96,96,PixelFormats.Pbgra32);bmp.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var image=File.Create(Path.Combine(root,"shared-components.png"));png.Save(image);
   }
   var axis=runtime.Load(tool).Assembly;
   const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
   var libraryType=axis.GetType("Rainmaker.Axis.AxisLibrary")!;var library=Activator.CreateInstance(libraryType,flags,null,new object[]{plugin.Config.ContentRoot},null)!;
   object Invoke(string name,params object[] args)=>libraryType.GetMethod(name,flags)!.Invoke(library,args)!;
   var settings=Invoke("Load");var options=((IEnumerable)libraryType.GetField("Options",flags)!.GetValue(library)!).Cast<object>().ToList();
   string Field(object o,string key)=>(string)o.GetType().GetField(key,flags)!.GetValue(o)!;
   var dims=options.Where(o=>Field(o,"Kind")=="dims"&&Field(o,"Name")!="Standard").Take(2).ToArray();Check(dims.Length==2,"Missing published dimensions");
   settings.GetType().GetProperty("BayDimId")!.SetValue(settings,Field(dims[0],"Id"));settings.GetType().GetProperty("TotalDimId")!.SetValue(settings,Field(dims[1],"Id"));
   var resolved=Invoke("Resolve",settings,false);var parameters=new JObject{["Scale"]=60,["XBays"]=new JArray(4000,5000),["YBays"]=new JArray(3000),["XLabels"]=new JArray("1","2","3"),["YLabels"]=new JArray("A","B")}.ToObject(axis.GetType("Rainmaker.Axis.AxisGridParameters")!)!;
   parameters.GetType().GetField("Library",flags)!.SetValue(parameters,resolved);
   var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
   axis.GetType("Rainmaker.Axis.RainmakerAxis")!.GetMethod("WriteConfigured",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{doc.Database,resolved,parameters,Point3d.Origin,null,false});
   using(var tx=doc.Database.TransactionManager.StartOpenCloseTransaction()){
    var bt=(BlockTable)tx.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);var entities=model.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).ToList();Check(entities.OfType<Dimension>().Count()==10&&entities.OfType<Line>().Count()==5&&entities.OfType<BlockReference>().Count()==10,"Existing Axis binary failed with detached Reader");
   }
   doc.Database.SaveAs(Path.Combine(root,"detached-axis.dwg"),DwgVersion.AC1032);
   // A changed selected dependency cannot silently reuse the DLL already in memory.
   var changed=JsonUtil.Read<CadSharedComponent>(JsonUtil.Write(installed));changed.Files.Single(f=>f.Name.EndsWith(".dll")).Hash=new string('a',64);changed.PackageHash=JsonUtil.Hash(changed.Files);JsonUtil.Atomic(Path.Combine(store.Root,"current.json"),changed);
   try{CadReaderDependency.Load();throw new Exception("Changed dependency accepted");}catch(CadException e)when(e.Code=="COMPONENT_CHANGED"){}finally{JsonUtil.Atomic(Path.Combine(store.Root,"current.json"),installed);}
   JsonUtil.Atomic(Path.Combine(root,"axis-upgrade-results.json"),new{passed=true,managerVersion=typeof(Plugin).Assembly.GetName().Version!.ToString(),managerWithoutReader=true,missingReaderMessage=true,separateInstall=true,singleReader=true,existingAxisBinary=axis.GetName().Version!.ToString(),axisLines=5,axisHeads=10,dimensions=10,changedReaderRejected=true,uiRendered=true});
  }catch(Exception error){JsonUtil.Atomic(Path.Combine(root,"axis-upgrade-results.json"),new{passed=false,error=error.ToString()});}
 }
}
