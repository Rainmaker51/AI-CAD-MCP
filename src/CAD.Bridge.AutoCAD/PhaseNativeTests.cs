using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
[assembly:CommandClass(typeof(AICad.Bridge.PhaseNativeTests))]
namespace AICad.Bridge;
public sealed class PhaseNativeTests {
 [CommandMethod("AICADPHASETEST",CommandFlags.Modal|CommandFlags.NoUndoMarker)]public void Run(){string root=Environment.GetEnvironmentVariable("AICAD_TEST_ROOT")??"";if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-test-root")))throw new InvalidOperationException("Isolated test root required.");var cases=new List<object>();int failed=0;void Test(string name,Action action){try{action();cases.Add(new{name,passed=true});}catch(System.Exception e){failed++;cases.Add(new{name,passed=false,error=e.ToString()});}}var config=JsonUtil.Read<InstallConfig>(File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"..","install.json"))));var content=new ContentStore(config.ContentRoot);var template=content.Template("ARCHITECTURE");
 Test("P0 source DWG and DWT with nine task frames",()=>{using var db=new Database(true,true);db.DisableUndoRecording(false);using(var tx=db.TransactionManager.StartTransaction()){PhaseDemo.Populate(db,tx);tx.Commit();}db.SaveAs(Path.Combine(root,"phase-source.dwg"),DwgVersion.AC1032);db.SaveAs(Path.Combine(root,"architecture.dwt"),DwgVersion.AC1032);});
 foreach(string kind in new[]{"STAIR_SECTION","WC_SECTION","BUILDING_SECTION"})Test("P6 "+kind+" native entities, dimension and hatch",()=>{var input=kind=="STAIR_SECTION"?JObject.Parse("{floorHeight:3600,risers:20,riser:180,tread:270,flightWidth:1000,landing:1200,slabThickness:120,confirmed:true}"):kind=="WC_SECTION"?JObject.Parse("{ceilingHeight:2600,slabThickness:120,tileHeight:2200,waterproofUpstand:300,confirmed:true}"):JObject.Parse("{floorHeight:3600,floors:2,slabThickness:120,confirmed:true}");double width=kind=="STAIR_SECTION"?6060:kind=="WC_SECTION"?3400:6000;double depth=kind=="STAIR_SECTION"?1000:2200;var built=ArchitectureBuilder.Build(kind,input,width,depth);var plan=Plan(template,kind,built.Operations);Validation.Plan(plan);Write(plan,Path.Combine(root,kind+".dwg"));});
 string fixturePlans=Path.Combine(config.ContentRoot,"tests","fixtures","phase-plans");if(Directory.Exists(fixturePlans))foreach(string path in Directory.EnumerateFiles(fixturePlans,"*.json"))Test("P5 native reconstruction "+Path.GetFileNameWithoutExtension(path),()=>{var plan=JsonUtil.Read<DrawingPlan>(File.ReadAllText(path));Validation.Plan(plan);Write(plan,Path.Combine(root,Path.GetFileNameWithoutExtension(path)+".dwg"));});
 Test("Library DWG/DWT fixture has nested block, arc, attribute and 3D rejection case",()=>CreateLibrary(Path.Combine(root,"library-source.dwg")));
 string originalAssetRoot=NativeLibraryAssets.ContentRoot;NativeLibraryAssets.ContentRoot=root;
 Test("Native library cloning preserves nested geometry and attributes without overwriting layer",()=>{
  string source=Path.Combine(root,"library-source.dwg"),hash=JsonUtil.FileHash(source),assets=Path.Combine(root,"content-assets");Directory.CreateDirectory(assets);string stored=Path.Combine(assets,hash+".dwg");if(!File.Exists(stored))File.Copy(source,stored);
  var t=new TemplateProfile{Id="LIB_NATIVE_TEST",Name="Native library fixture",Version="1.0.0",Layers=new(){new(){Name="A-AI",Color=7}},Blocks=new(){new(){Id="NATIVE_DOOR",NativeSource=new(){FileHash=hash,BlockName="NATIVE_DOOR",Min=new(-500,-500),Max=new(3000,3000)}}}};
  var p=Plan(t,"LIB_NATIVE_TEST",new(){new(){Kind="insert_block",AssetId="NATIVE_DOOR",Layer="A-AI",Points=new(){new(4000,4000)}}});Validation.Plan(p);
  using var db=new Database(true,true);db.DisableUndoRecording(false);db.Insunits=UnitsValue.Millimeters;
  using(var tx=db.TransactionManager.StartTransaction()){
   var layers=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForWrite);var original=new LayerTableRecord{Name="A-LIB",Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,3)};layers.Add(original);tx.AddNewlyCreatedDBObject(original,true);
   var reference=(BlockReference)EntityFactory.WritePlan(p,db,tx).Single();
   if(reference.AttributeCollection.Count!=1)throw new System.Exception("Default attribute missing.");
   var def=(BlockTableRecord)tx.GetObject(reference.BlockTableRecord,OpenMode.ForRead);var children=def.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).ToList();if(!children.OfType<BlockReference>().Any()||!children.OfType<Arc>().Any())throw new System.Exception("Nested geometry was flattened/lost.");
   if(((LayerTableRecord)tx.GetObject(layers["A-LIB"],OpenMode.ForRead)).Color.ColorIndex!=3)throw new System.Exception("Existing layer was overwritten.");
   tx.Commit();
  }
  db.SaveAs(Path.Combine(root,"library-inserted.dwg"),DwgVersion.AC1032);
  using var check=new Database(false,true);check.ReadDwgFile(Path.Combine(root,"library-inserted.dwg"),FileOpenMode.OpenForReadAndAllShare,true,"");using var read=check.TransactionManager.StartOpenCloseTransaction();var bt=(BlockTable)read.GetObject(check.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)read.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);var block=(BlockReference)read.GetObject(model.Cast<ObjectId>().Single(),OpenMode.ForRead);if(block.AttributeCollection.Count!=1)throw new System.Exception("Reopened attribute missing.");
 });
 Test("Model space can be imported as one native detail block",()=>{
  string source=Path.Combine(root,"library-source.dwg"),hash=JsonUtil.FileHash(source);var t=new TemplateProfile{Id="LIB_MODEL_TEST",Name="Model detail",Version="1.0.0",Layers=new(){new(){Name="A-AI",Color=7}},Blocks=new(){new(){Id="MODEL_DETAIL",NativeSource=new(){FileHash=hash,BlockName=BlockTableRecord.ModelSpace,ModelSpace=true,Min=new(0,0),Max=new(4000,4000)}}}};
  var p=Plan(t,"MODEL_DETAIL",new(){new(){Kind="insert_block",AssetId="MODEL_DETAIL",Layer="A-AI",Points=new(){new(4000,4000)}}});Validation.Plan(p);Write(p,Path.Combine(root,"library-model-inserted.dwg"));
 });
 NativeLibraryAssets.ContentRoot=originalAssetRoot;
 if(content.Ids("templates").Contains("LIB_CAD_MAU"))Test("Published DWT template can create an editable native library block",()=>{
  var t=content.Template("LIB_CAD_MAU","1.0.0");var p=Plan(t,"PUBLISHED_LIBRARY",new(){new(){Kind="insert_block",AssetId="CUA_MAU",Layer="A-LIB",Points=new(){new(4000,4000)}}});Validation.Plan(p);Write(p,Path.Combine(root,"published-library-block.dwg"));
 });
 Test("P2 complete-plan staging does not mutate a target DWG",()=>{var built=ArchitectureBuilder.Build("STAIR_SECTION",JObject.Parse("{floorHeight:3600,risers:20,riser:180,tread:270,flightWidth:1000,landing:1200,slabThickness:120,confirmed:true}"),6060,1000);var p=Plan(template,"STAGING",built.Operations);using var scene=new NativePreviewScene(p);if(scene.Entities.Count!=p.Operations.Count)throw new System.Exception("Preview/apply operation count mismatch.");if(!scene.Entities.Any(e=>e is Dimension)||!scene.Entities.Any(e=>e is Hatch))throw new System.Exception("Full preview lost dimensions or hatch.");});
 JsonUtil.Atomic(Path.Combine(root,"phase-native-results.json"),new{total=cases.Count,passed=cases.Count-failed,failed,cases});}
 static void CreateLibrary(string path){using var db=new Database(true,true);db.Insunits=UnitsValue.Millimeters;using(var tx=db.TransactionManager.StartTransaction()){
  var lt=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForWrite);var layer=new LayerTableRecord{Name="A-LIB",Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,3)};lt.Add(layer);tx.AddNewlyCreatedDBObject(layer,true);
  var table=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);var inner=new BlockTableRecord{Name="NESTED_PART"};var innerId=table.Add(inner);tx.AddNewlyCreatedDBObject(inner,true);var c=new Circle(new Autodesk.AutoCAD.Geometry.Point3d(450,450,0),Autodesk.AutoCAD.Geometry.Vector3d.ZAxis,120){LayerId=layer.ObjectId};inner.AppendEntity(c);tx.AddNewlyCreatedDBObject(c,true);
  var outer=new BlockTableRecord{Name="NATIVE_DOOR"};var outerId=table.Add(outer);tx.AddNewlyCreatedDBObject(outer,true);
  var nested=new BlockReference(Autodesk.AutoCAD.Geometry.Point3d.Origin,innerId);outer.AppendEntity(nested);tx.AddNewlyCreatedDBObject(nested,true);
  var arc=new Arc(Autodesk.AutoCAD.Geometry.Point3d.Origin,900,0,Math.PI){LayerId=layer.ObjectId};outer.AppendEntity(arc);tx.AddNewlyCreatedDBObject(arc,true);
  var attr=new AttributeDefinition{Tag="MARK",Prompt="Mark",TextString="D01",Height=100,Position=new Autodesk.AutoCAD.Geometry.Point3d(100,1000,0)};outer.AppendEntity(attr);tx.AddNewlyCreatedDBObject(attr,true);
  var unsupported=new BlockTableRecord{Name="UNSUPPORTED_3D"};table.Add(unsupported);tx.AddNewlyCreatedDBObject(unsupported,true);var line=new Line(new Autodesk.AutoCAD.Geometry.Point3d(0,0,0),new Autodesk.AutoCAD.Geometry.Point3d(100,100,100));unsupported.AppendEntity(line);tx.AddNewlyCreatedDBObject(line,true);
  var ms=(BlockTableRecord)tx.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var sample=new BlockReference(new Autodesk.AutoCAD.Geometry.Point3d(1000,1000,0),outerId);ms.AppendEntity(sample);tx.AddNewlyCreatedDBObject(sample,true);NativeLibraryAssets.Attributes(sample,tx);
  tx.Commit();}db.SaveAs(path,DwgVersion.AC1032);db.SaveAs(Path.ChangeExtension(path,".dwt"),DwgVersion.AC1032);}
 static DrawingPlan Plan(TemplateProfile template,string id,List<Operation> ops)=>new(){SessionId=Guid.NewGuid().ToString("N"),DocumentId=Guid.NewGuid().ToString("N"),DocumentRevision="fixture:0",TaskId=id,Template=template,TemplateHash=JsonUtil.Hash(template),Scope=new(){new(0,0),new(15000,0),new(15000,15000),new(0,15000)},Operations=ops};
 static void Write(DrawingPlan p,string output){using var db=new Database(true,true);db.DisableUndoRecording(false);db.Insunits=UnitsValue.Millimeters;using(var tx=db.TransactionManager.StartTransaction()){var entities=EntityFactory.WritePlan(p,db,tx);foreach(var d in entities.OfType<Dimension>())if(d.Measurement<=0)throw new System.Exception("Dimension is not measuring actual geometry.");foreach(var h in entities.OfType<Hatch>())if(h.NumberOfLoops!=1)throw new System.Exception("Hatch has no closed loop.");tx.Commit();}db.SaveAs(output,DwgVersion.AC1032);using var check=new Database(false,true);check.ReadDwgFile(output,FileOpenMode.OpenForReadAndAllShare,true,"");using var t=check.TransactionManager.StartOpenCloseTransaction();var bt=(BlockTable)t.GetObject(check.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)t.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);foreach(var h in ms.Cast<ObjectId>().Select(id=>t.GetObject(id,OpenMode.ForRead)).OfType<Hatch>())if(h.Area<=0)throw new System.Exception("Reopened hatch area is not positive.");if(ms.Cast<ObjectId>().Count(id=>!id.IsErased)!=p.Operations.Count)throw new System.Exception("DWG saved entity count differs.");}
}
