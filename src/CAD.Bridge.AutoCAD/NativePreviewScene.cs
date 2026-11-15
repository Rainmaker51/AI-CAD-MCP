using System;
using System.Collections.Generic;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
namespace AICad.Bridge;
// Full plan is rendered once into a staging DWG; preview never changes target DB.
internal sealed class NativePreviewScene:IDisposable {
 readonly Database staging=new(true,true);internal Database Database=>staging;internal List<Entity> Entities {get;}=new();
 internal NativePreviewScene(DrawingPlan plan)
 {
  var previous=HostApplicationServices.WorkingDatabase;
  try{
   HostApplicationServices.WorkingDatabase=staging;
   staging.DisableUndoRecording(false);
   staging.Insunits=UnitsValue.Millimeters;
   if(plan.AnnotationScales!=null)staging.Cannoscale=EntityFactory.GetScale(staging,plan.AnnotationScales[0]);
   using var tx=staging.TransactionManager.StartTransaction();
   var rendered=EntityFactory.WritePlan(plan,staging,tx);
   for(int i=0;i<rendered.Count;i++){
    if(rendered[i] is BlockReference block&&plan.Operations[i].AttributeText!=null){
     AddAxisPreview(block,plan.Operations[i],tx);
     continue;
    }
    var clone=(Entity)rendered[i].Clone();
    clone.ColorIndex=3;
    Entities.Add(clone);
   }
   tx.Commit();
  }catch{Dispose();throw;}finally{HostApplicationServices.WorkingDatabase=previous;}
 }
 void AddAxisPreview(BlockReference block,Operation op,Transaction tx)
 {
  // Wipeouts and database-owned attribute references do not render reliably as
  // transients. Draw the visible vector and label parts from the staged block.
  using var parts=new DBObjectCollection();
  block.Explode(parts);
  foreach(DBObject part in parts){
   if(part is Entity vector&&vector.Visible&&part is not Wipeout&&part is not AttributeDefinition&&part is not AttributeReference){
    vector.ColorIndex=3;
    Entities.Add(vector);
   }else part.Dispose();
  }
  foreach(ObjectId id in block.AttributeCollection)
  {
   if(tx.GetObject(id,OpenMode.ForRead) is not AttributeReference a||a.Invisible||a.TextString!=op.AttributeText)
    continue;
   var text=new DBText{Position=a.Position,Height=a.Height,Rotation=a.Rotation,TextString=a.TextString,TextStyleId=a.TextStyleId,ColorIndex=3};
   text.HorizontalMode=a.HorizontalMode;
   text.VerticalMode=a.VerticalMode;
   if(!a.IsDefaultAlignment)text.AlignmentPoint=a.AlignmentPoint;
   Entities.Add(text);
  }
 }
 public void Dispose(){foreach(var e in Entities)e.Dispose();Entities.Clear();staging.Dispose();}
}
