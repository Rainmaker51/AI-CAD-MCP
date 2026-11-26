using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using AICad.Bridge;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Axis;

public sealed partial class RainmakerAxis
{
 internal static DrawingPlan ConfiguredPlan(AxisResolvedLibrary library,AxisGridParameters p,Point3d origin,Point3d? singleEnd=null)
 {
  var head=JObject.FromObject(library.Head).ToObject<BlockAsset>()!;
  var template=library.Template;DrawingPlan plan;
  if(!p.Single&&p.FixedDimExtensions&&(!AxisLibrary.ValidDistance(p.DimExtensionLength)||p.DimExtensionLength>=library.Settings.DimOffset))throw new ArgumentException("Khoảng trục → chân dim phải lớn hơn 0 và nhỏ hơn khoảng trục → dim nhịp.");
  if(p.Single){
   var end=singleEnd??new Point3d(origin.X+4000,origin.Y,0);if(origin.DistanceTo(end)<1e-6||Math.Abs(origin.Z)>1e-6||Math.Abs(end.Z)>1e-6)throw new CadException("AXIS_POINTS","Hai điểm phải khác nhau và nằm trên WCS XY.");
   double pad=p.Scale*52.0;plan=new(){Scope=new(){new(Math.Min(origin.X,end.X)-pad,Math.Min(origin.Y,end.Y)-pad),new(Math.Max(origin.X,end.X)+pad,Math.Min(origin.Y,end.Y)-pad),new(Math.Max(origin.X,end.X)+pad,Math.Max(origin.Y,end.Y)+pad),new(Math.Min(origin.X,end.X)-pad,Math.Max(origin.Y,end.Y)+pad)}};
   plan.Operations.Add(new(){Kind="draw_line",Layer=AxisLayer,Points=new(){new(origin.X,origin.Y),new(end.X,end.Y)}});
   foreach(var point in new[]{origin,end})plan.Operations.Add(new(){Kind="insert_block",Layer=HeadLayer,AssetId=head.Id,SymbolScale=p.Scale,AttributeText=p.SingleLabel,Points=new(){new(point.X,point.Y)}});
  }else{
   // Reuse the tested grid geometry for both preview and actual drawing.
   var canonical=JObject.FromObject(head).ToObject<BlockAsset>()!;canonical.Id="QA_ATT_TRUC";canonical.Usage=new(){Role="axisHead",Layer=HeadLayer,Reviewed=true};
   var synthetic=new TemplateProfile{Id=template.Id,Version=template.Version,Name=template.Name,Font=template.Font,Units=template.Units,Blocks=new(){canonical},Layers=new(){new(){Name=AxisLayer},new(){Name=HeadLayer},new(){Name=DimLayer}}};
   plan=GridPlan(synthetic,origin,p.XBays,p.YBays,p.XLabels,p.YLabels,p.Scale);
   int index=0,perSide=p.XBays.Length+p.YBays.Length+2;
   if(!AxisLibrary.ValidDistance(library.Settings.DimOffset)||!AxisLibrary.ValidDistance(library.Settings.DimRowGap))throw new ArgumentException("Khoảng cách dim phải lớn hơn 0 và không vượt quá 10.000.000 mm.");
   foreach(var op in plan.Operations.Where(o=>o.Kind=="add_dimension")){
    op.Style=index%perSide>=perSide-2?"total":"bay";
    int sign=index/perSide==0?-1:1;double offset=library.Settings.DimOffset+(op.Style=="total"?library.Settings.DimRowGap:0);
    var a=op.Points[0];var b=op.Points[1];op.Points[2]=Math.Abs(a.Y-b.Y)<1e-6?new Point2(a.X,a.Y+sign*offset):new Point2(a.X+sign*offset,a.Y);index++;
   }
   double width=p.XBays.Sum(),height=p.YBays.Sum();
   double extension=Math.Max(22*p.Scale,library.Settings.DimOffset+library.Settings.DimRowGap+8*p.Scale);
   Point2 Extend(Point2 point)=>new(point.X<origin.X?origin.X-extension:point.X>origin.X+width?origin.X+width+extension:point.X,point.Y<origin.Y?origin.Y-extension:point.Y>origin.Y+height?origin.Y+height+extension:point.Y);
   foreach(var op in plan.Operations.Where(o=>o.Kind=="draw_line"||o.Kind=="insert_block"))for(int i=0;i<op.Points.Count;i++)op.Points[i]=Extend(op.Points[i]);
   double pad=extension+30*p.Scale;plan.Scope=new(){new(origin.X-pad,origin.Y-pad),new(origin.X+width+pad,origin.Y-pad),new(origin.X+width+pad,origin.Y+height+pad),new(origin.X-pad,origin.Y+height+pad)};
   plan.Operations.RemoveAll(op=>op.Kind=="add_dimension"&&(
    op.Points[2].Y<origin.Y&&!library.Settings.Bottom||op.Points[2].Y>origin.Y+height&&!library.Settings.Top||
    op.Points[2].X<origin.X&&!library.Settings.Left||op.Points[2].X>origin.X+width&&!library.Settings.Right));
  }
  int headIndex=0;plan.Operations.RemoveAll(op=>op.Kind=="insert_block"&&((headIndex++%2==0)?library.Settings.HeadEnds=="End":library.Settings.HeadEnds=="Start"));
  foreach(var op in plan.Operations){if(op.Kind=="draw_line")op.Layer=library.AxisLayer.Name;else if(op.Kind=="insert_block"){op.Layer=library.HeadLayer.Name;op.AssetId=head.Id;}else op.Layer=library.DimLayer.Name;}
  plan.Template=new(){Id=template.Id,Version=template.Version,Name=template.Name,Units=template.Units,Font=template.Font,TextHeight=template.TextHeight,ArrowSize=template.ArrowSize,Blocks=new(){head},Layers=new[]{library.AxisLayer,library.HeadLayer,library.DimLayer}.GroupBy(l=>l.Name).Select(g=>g.First()).ToList()};
  return plan;
 }

 internal static List<ObjectId> WriteConfigured(Database db,AxisResolvedLibrary library,AxisGridParameters p,Point3d origin,Point3d? singleEnd=null,bool replaceDims=false)
 {
  NativeWriteGuard.CheckDatabase(db);var plan=ConfiguredPlan(library,p,origin,singleEnd);NativeLibraryAssets.Demand(plan);
  using var tx=db.TransactionManager.StartTransaction();
  NativeLayerStandards.Resources(plan.Template,db,tx);
  var styles=p.Single?new Dictionary<string,ObjectId>():DimensionLibrary.Import(db,tx,library,new[]{library.BayDim,library.TotalDim},replaceDims);
  var ids=new List<ObjectId>();
  var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
  foreach(var op in plan.Operations){
   Entity entity;
   if(op.Kind=="add_dimension")entity=new AlignedDimension(EntityFactory.P(op.Points[0]),EntityFactory.P(op.Points[1]),EntityFactory.P(op.Points[2]),"",styles[op.Style=="bay"?library.BayDim.Id:library.TotalDim.Id]);
   else entity=EntityFactory.Make(op,plan,db,tx);
   entity.SetDatabaseDefaults(db);entity.Layer=op.Layer;entity.ColorIndex=256;model.AppendEntity(entity);tx.AddNewlyCreatedDBObject(entity,true);
   ids.Add(entity.ObjectId);
   if(entity is Dimension dim){
    var style=styles[op.Style=="bay"?library.BayDim.Id:library.TotalDim.Id];dim.DimensionStyle=style;var record=(DimStyleTableRecord)tx.GetObject(style,OpenMode.ForRead);
    // SetDatabaseDefaults copied the drawing's current DIM overrides. Assigning
    // DimensionStyle alone does not clear them: apply the chosen record explicitly.
    dim.SetDimstyleData(record);
    dim.Annotative=record.Annotative==AnnotativeStates.True?AnnotativeStates.True:AnnotativeStates.False;
    dim.Dimscale=record.Annotative==AnnotativeStates.True?0:p.Scale;
    if(p.FixedDimExtensions){if(!AxisLibrary.ValidDistance(p.DimExtensionLength))throw new ArgumentException("Khoảng cách từ trục đến chân dim phải lớn hơn 0.");dim.DimfxlenOn=false;dim.Dimexo=p.DimExtensionLength/p.Scale;}
    if(record.Annotative==AnnotativeStates.True){var scale=EntityFactory.GetScale(db,p.Scale);if(!dim.HasContext(scale))dim.AddContext(scale);var current=db.Cannoscale;if(current.Name!=scale.Name&&dim.HasContext(current))dim.RemoveContext(current);}
    dim.RecomputeDimensionBlock(true);
   }else{EntityFactory.Finish(entity,op,plan,db,tx);EntityFactory.DemandBounds(entity,plan);}
  }
  tx.Commit();
  return ids;
 }

}
