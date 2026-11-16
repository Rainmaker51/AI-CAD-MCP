using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
[assembly:CommandClass(typeof(AICad.LibraryScanner.LibrarySourceVerification))]
namespace AICad.LibraryScanner;
public sealed class LibrarySourceVerification {
 [CommandMethod("AICADVERIFYLIBRARYSOURCE",CommandFlags.Modal)]
 public void Run(){
  string job=Environment.GetEnvironmentVariable("AICAD_LIBRARY_JOB")??"";
  if(!Path.IsPathRooted(job)||!File.Exists(Path.Combine(job,".aicad-library-job")))return;
  var previous=HostApplicationServices.WorkingDatabase;
  try{
   var template=JsonUtil.Read<TemplateProfile>(File.ReadAllText(Path.Combine(job,"template.json")));
   using var source=new Database(false,true);source.ReadDwgFile(Path.Combine(job,"source.dwg"),FileOpenMode.OpenForReadAndAllShare,true,"");source.CloseInput(true);
   using var target=new Database(true,true);HostApplicationServices.WorkingDatabase=target;
   using var read=source.TransactionManager.StartTransaction();var sourceTable=(BlockTable)read.GetObject(source.BlockTableId,OpenMode.ForRead);
   var results=new JArray();
   foreach(var asset in template.Blocks){
    var native=asset.NativeSource??throw new InvalidOperationException("Not native: "+asset.Id);
    if(!sourceTable.Has(native.BlockName))throw new InvalidOperationException("Missing definition: "+native.BlockName);
    var mapping=new IdMapping();source.WblockCloneObjects(new ObjectIdCollection(new[]{sourceTable[native.BlockName]}),target.BlockTableId,mapping,DuplicateRecordCloning.MangleName,false);
    using var tx=target.TransactionManager.StartTransaction();var table=(BlockTable)tx.GetObject(target.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
    var block=new BlockReference(Point3d.Origin,mapping[sourceTable[native.BlockName]].Value);model.AppendEntity(block);tx.AddNewlyCreatedDBObject(block,true);
    if(block.IsDynamicBlock&&asset.Usage!=null)foreach(var entry in asset.Usage.Values){var prop=block.DynamicBlockReferencePropertyCollection.Cast<DynamicBlockReferenceProperty>().SingleOrDefault(p=>p.PropertyName==entry.Key);if(prop==null||prop.ReadOnly)throw new InvalidOperationException("Missing parameter: "+asset.Id+" / "+entry.Key);prop.Value=entry.Value.ToObject(prop.Value.GetType())!;}
    var definition=(BlockTableRecord)tx.GetObject(block.BlockTableRecord,OpenMode.ForRead);int attributes=0;
    foreach(ObjectId id in definition)if(tx.GetObject(id,OpenMode.ForRead) is AttributeDefinition attribute&&!attribute.Constant){var reference=new AttributeReference();reference.SetAttributeFromBlock(attribute,block.BlockTransform);block.AttributeCollection.AppendAttribute(reference);tx.AddNewlyCreatedDBObject(reference,true);attributes++;}
    if(!string.IsNullOrEmpty(native.LabelAttribute)&&!definition.Cast<ObjectId>().Any(id=>tx.GetObject(id,OpenMode.ForRead) is AttributeDefinition a&&a.Tag==native.LabelAttribute))throw new InvalidOperationException("Missing label attribute: "+asset.Id);
    var bounds=block.GeometricExtents;if(double.IsNaN(bounds.MinPoint.X)||bounds.MaxPoint.X<bounds.MinPoint.X)throw new InvalidOperationException("Invalid bounds: "+asset.Id);
    results.Add(JObject.FromObject(new{id=asset.Id,block=native.BlockName,dynamic=block.IsDynamicBlock,attributes,width=bounds.MaxPoint.X-bounds.MinPoint.X,height=bounds.MaxPoint.Y-bounds.MinPoint.Y}));tx.Commit();
   }
   JsonUtil.Atomic(Path.Combine(job,"native-verification.json"),new{verified=true,sourceHash=JsonUtil.FileHash(Path.Combine(job,"source.dwg")),blocks=results});
  }catch(System.Exception e){JsonUtil.Atomic(Path.Combine(job,"native-verification.json"),new{verified=false,error=e.Message});}
  finally{HostApplicationServices.WorkingDatabase=previous;}
 }
}
