using System;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
[assembly:CommandClass(typeof(AICad.LibraryScanner.ImageWorkspace))]
namespace AICad.LibraryScanner;
public sealed class ImageWorkspace {
 [CommandMethod("AICADIMAGEWORKSPACE",CommandFlags.Modal|CommandFlags.NoUndoMarker)]public void Run(){string root=Environment.GetEnvironmentVariable("AICAD_IMAGE_JOB")??"";if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-image-job")))throw new InvalidOperationException("Owned image workspace required.");try{
  var request=NativeJson.ReadObject(File.ReadAllText(Path.Combine(root,"request.json")));string task=JsonUtil.Name(request.Value<string>("taskId")!),skill=JsonUtil.Name(request.Value<string>("skillId")!),template=JsonUtil.Name(request.Value<string>("templateId")!);double width=request.Value<double>("width"),height=request.Value<double>("height");if(width<1000||width>100000||height<1000||height>100000)throw new InvalidOperationException("Invalid workspace bounds.");
  using var db=new Database(true,true);db.Insunits=UnitsValue.Millimeters;
  using(var tx=db.TransactionManager.StartTransaction()){
   var table=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);var def=new BlockTableRecord{Name="AI_TASK"};var defId=table.Add(def);tx.AddNewlyCreatedDBObject(def,true);
   var outline=new Polyline(4);foreach(var p in new[]{new Point2d(0,0),new Point2d(width,0),new Point2d(width,height),new Point2d(0,height)})outline.AddVertexAt(outline.NumberOfVertices,p,0,0,0);outline.Closed=true;outline.ColorIndex=8;def.AppendEntity(outline);tx.AddNewlyCreatedDBObject(outline,true);
   var values=new System.Collections.Generic.Dictionary<string,string>{{"TASK_ID",task},{"SKILL_ID",skill},{"TEMPLATE_ID",template},{"STATUS","PENDING"},{"DEPENDS_ON",""},{"PROMPT","Image floorplan trial; overall size confirmed; internal geometry approximate"},{"SOURCE_REF",request.Value<string>("sourceId")??""}};
   foreach(var pair in values){var attr=new AttributeDefinition{Tag=pair.Key,Prompt=pair.Key,TextString=pair.Value,Height=100,Invisible=true,Position=new Point3d(100,100,0)};def.AppendEntity(attr);tx.AddNewlyCreatedDBObject(attr,true);}
   var ms=(BlockTableRecord)tx.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var reference=new BlockReference(Point3d.Origin,defId);ms.AppendEntity(reference);tx.AddNewlyCreatedDBObject(reference,true);
   foreach(ObjectId id in def)if(tx.GetObject(id,OpenMode.ForRead) is AttributeDefinition ad){var a=new AttributeReference();a.SetAttributeFromBlock(ad,reference.BlockTransform);a.TextString=values[ad.Tag];reference.AttributeCollection.AppendAttribute(a);tx.AddNewlyCreatedDBObject(a,true);}
   tx.Commit();
  }
  string name=request.Value<string>("drawingName")??"MAT_BANG_ANH_16000x5000.dwg";if(Path.GetFileName(name)!=name||Path.GetExtension(name)!=".dwg")throw new InvalidOperationException("Invalid drawing name");db.SaveAs(Path.Combine(root,name),DwgVersion.AC1032);
 }catch(System.Exception e){JsonUtil.Atomic(Path.Combine(root,"workspace-error.json"),new{message=e.Message});}}
}
