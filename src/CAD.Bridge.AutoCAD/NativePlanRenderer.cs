using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using JsonUtil=AICad.Contracts.Json;
namespace AICad.Bridge;
internal static partial class EntityFactory {
 internal static string StyleName(TemplateProfile t)=>"AICAD_"+t.Id+"_"+JsonUtil.Hash(t).Substring(0,10);
 static string PlanStyle(DrawingPlan plan) => StyleName(plan.Template) + (plan.AnnotationScales == null ? "" : "_ANNO");
 internal static void Resources(DrawingPlan plan,Database db,Transaction tx){NativeLayerStandards.Resources(plan.Template,db,tx);
  string name=PlanStyle(plan);var text=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForRead);ObjectId textId;if(!text.Has(name)){text.UpgradeOpen();var style=new TextStyleTableRecord{Name=name,FileName=plan.AnnotationScales==null?plan.Template.Font:"tahoma.ttf"};textId=text.Add(style);tx.AddNewlyCreatedDBObject(style,true);}else textId=text[name];var dims=(DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForRead);if(!dims.Has(name)){dims.UpgradeOpen();var dim=new DimStyleTableRecord{Name=name,Dimtxt=plan.AnnotationScales==null?plan.Template.TextHeight:2,Dimasz=plan.AnnotationScales==null?plan.Template.ArrowSize:.8,Dimscale=plan.AnnotationScales==null?1:0,Dimlfac=1,Dimdec=0,Dimtad=1,Dimexo=plan.AnnotationScales==null?30:.5,Dimexe=plan.AnnotationScales==null?50:.8,Dimgap=plan.AnnotationScales==null?40:.6,Dimtxsty=textId,Annotative=plan.AnnotationScales==null?AnnotativeStates.False:AnnotativeStates.True};dims.Add(dim);tx.AddNewlyCreatedDBObject(dim,true);}
 }
 internal static Entity Make(Operation op,DrawingPlan plan,Database db,Transaction tx){if(op.Kind=="add_text"&&plan.AnnotationScales!=null)
   return new MText{Location=P(op.Points[0]),Contents=op.Text.Replace(@"\",@"\\").Replace("{",@"\{").Replace("}",@"\}"),TextHeight=2,Rotation=op.Rotation};
  if(op.Kind=="add_dimension"){
   var a=P(op.Points[0]);var b=P(op.Points[1]);var line=P(op.Points[2]);
   if(plan.AnnotationScales!=null){
    var delta=b-a;double x=delta.X*Math.Cos(op.Rotation)+delta.Y*Math.Sin(op.Rotation);
    double y=-delta.X*Math.Sin(op.Rotation)+delta.Y*Math.Cos(op.Rotation);
    if(Math.Abs(x)<1e-6||Math.Abs(y)<1e-6)
     return new RotatedDimension(op.Rotation+(Math.Abs(x)<1e-6?Math.PI/2:0),a,b,line,"",ObjectId.Null);
   }
   return new AlignedDimension(a,b,line,"",ObjectId.Null);
  }if(op.Kind=="create_hatch")return new Hatch();return Append(op,plan,db,tx);}
 internal static void Finish(Entity e,Operation op,DrawingPlan plan,Database db,Transaction tx){if(e is BlockReference block&&op.Kind=="insert_block"&&plan.Template.Blocks.Single(b=>b.Id==op.AssetId).NativeSource!=null){var asset=plan.Template.Blocks.Single(b=>b.Id==op.AssetId);NativeDynamic.Apply(block,op,asset);
  NativeLibraryAssets.Attributes(block,tx,op,asset.NativeSource!.LabelAttribute);
 }string name=PlanStyle(plan);if(e is DBText text){var styles=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForRead);text.TextStyleId=styles[name];}if(e is MText multi&&plan.AnnotationScales!=null){var styles=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForRead);multi.TextStyleId=styles[name];ApplyScales(multi,plan,db);}if(e is Dimension dim){var styles=(DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForRead);dim.DimensionStyle=styles[name];if(plan.AnnotationScales!=null){dim.Dimscale=0;ApplyScales(dim,plan,db);}dim.RecomputeDimensionBlock(true);}if(e is Hatch hatch){hatch.SetHatchPattern(HatchPatternType.PreDefined,op.Pattern);hatch.PatternScale=op.PatternScale;hatch.PatternAngle=op.Rotation;hatch.Associative=false;var loop=new HatchLoop(HatchLoopTypes.External|HatchLoopTypes.Polyline);foreach(var p in op.Points)loop.Polyline.Add(new BulgeVertex(new Point2d(p.X,p.Y),0));loop.Polyline.Add(new BulgeVertex(new Point2d(op.Points[0].X,op.Points[0].Y),0));hatch.AppendLoop(loop);hatch.EvaluateHatch(true);}}
 internal static AnnotationScale GetScale(Database db, int denominator)
 {
  var scales=db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");
  string scaleName="1:"+denominator;
  if(!scales.HasContext(scaleName))
   scales.AddContext(new AnnotationScale{Name=scaleName,PaperUnits=1,DrawingUnits=denominator});
  var scale=(AnnotationScale)scales.GetContext(scaleName);
  if(Math.Abs(scale.DrawingUnits/scale.PaperUnits-denominator)>1e-8)
   throw new CadException("ANNOTATION_SCALE_CONFLICT","Tỷ lệ "+scaleName+" trong DWG có hệ số khác tên.");
  return scale;
 }
 private static void ApplyScales(Entity entity, DrawingPlan plan, Database db)
 {
  entity.Annotative=AnnotativeStates.True;
  foreach(int denominator in plan.AnnotationScales!)
  {
   var scale=GetScale(db,denominator);
   if(!entity.HasContext(scale))
    entity.AddContext(scale);
  }
  var current=db.Cannoscale;
  if(!plan.AnnotationScales!.Any(d=>current.Name=="1:"+d)&&entity.HasContext(current))
   entity.RemoveContext(current);
 }
 internal static List<Entity> WritePlan(DrawingPlan plan,Database db,Transaction tx){Resources(plan,db,tx);var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var result=new List<Entity>();foreach(var op in plan.Operations){var e=Make(op,plan,db,tx);e.SetDatabaseDefaults(db);e.Layer=op.Layer;e.ColorIndex=256;ms.AppendEntity(e);tx.AddNewlyCreatedDBObject(e,true);Finish(e,op,plan,db,tx);DemandBounds(e,plan);result.Add(e);}return result;}
}
