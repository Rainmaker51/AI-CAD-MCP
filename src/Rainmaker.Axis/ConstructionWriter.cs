using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using AICad.Bridge;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Newtonsoft.Json.Linq;

namespace Rainmaker.Axis;

internal static class ConstructionWriter
{
 internal static void ConfigureHatch(Hatch hatch,JObject values){var type=(HatchPatternType)Enum.Parse(typeof(HatchPatternType),values.Value<string>("type")??"PreDefined");hatch.SetHatchPattern(type,values.Value<string>("pattern"));hatch.PatternScale=values.Value<double>("scale");hatch.PatternAngle=values.Value<double>("angle");if(type==HatchPatternType.UserDefined){hatch.PatternSpace=values.Value<double>("spacing");hatch.PatternDouble=values.Value<bool>("doublePattern");}var rgb=values["rgb"] as JArray;hatch.Color=rgb!=null&&rgb.Count==3?Color.FromRgb((byte)rgb[0],(byte)rgb[1],(byte)rgb[2]):Color.FromColorIndex(ColorMethod.ByAci,(short)values.Value<int>("color"));}
 internal static Polyline Polygon(IEnumerable<Point2> points){var poly=new Polyline();foreach(var p in points)poly.AddVertexAt(poly.NumberOfVertices,new Point2d(p.X,p.Y),0,0,0);poly.Closed=true;return poly;}
 internal static Curve Outline(ColumnFootprint c)=>c.Circle?new Circle(new Point3d(c.Center.X,c.Center.Y,0),Vector3d.ZAxis,c.B/2):Polygon(c.Corners());
 internal static Region RegionOf(Curve curve){using(curve){var regions=Region.CreateFromCurves(new DBObjectCollection{curve});if(regions.Count!=1){foreach(DBObject obj in regions)obj.Dispose();throw new CadException("WALL_BOUNDARY","Không tạo được vùng kín.");}return (Region)regions[0];}}
 internal static List<Polyline> WallLoops(ConstructionGeometry geometry)
 {
  var regions=new List<Region>();
  try{
   foreach(var wall in geometry.Walls){var next=RegionOf(Polygon(wall.Corners));bool merged=true;
    while(merged){merged=false;for(int i=0;i<regions.Count;i++){using var overlap=(Region)next.Clone();using var other=(Region)regions[i].Clone();overlap.BooleanOperation(BooleanOperationType.BoolIntersect,other);if(overlap.IsNull||overlap.Area<1e-6)continue;next.BooleanOperation(BooleanOperationType.BoolUnite,regions[i]);regions[i].Dispose();regions.RemoveAt(i);merged=true;break;}}
    regions.Add(next);
   }
   foreach(var column in geometry.Columns)foreach(var region in regions){using var cut=RegionOf(Outline(column));region.BooleanOperation(BooleanOperationType.BoolSubtract,cut);}
   var result=new List<Polyline>();foreach(var region in regions){if(region.IsNull)continue;var curves=new DBObjectCollection();var pieces=new DBObjectCollection();region.Explode(pieces);void Flatten(DBObject obj){if(obj is Region nested){var inner=new DBObjectCollection();nested.Explode(inner);foreach(DBObject child in inner)Flatten(child);nested.Dispose();}else curves.Add(obj);}foreach(DBObject piece in pieces)Flatten(piece);try{
    var remaining=curves.Cast<DBObject>().ToList();
    while(remaining.Count>0){if(remaining[0] is Circle circle){var p=new Polyline();p.AddVertexAt(0,new Point2d(circle.Center.X+circle.Radius,circle.Center.Y),1,0,0);p.AddVertexAt(1,new Point2d(circle.Center.X-circle.Radius,circle.Center.Y),1,0,0);p.Closed=true;result.Add(p);remaining.RemoveAt(0);continue;}
     var first=remaining[0] as Curve??throw new CadException("WALL_BOUNDARY","Biên tường không được hỗ trợ.");var start=first.StartPoint;var end=start;var loop=new Polyline();int guard=remaining.Count+1;
     do{int match=remaining.FindIndex(obj=>obj is Curve c&&(c.StartPoint.DistanceTo(end)<1e-5||c.EndPoint.DistanceTo(end)<1e-5));if(match<0||guard--==0){loop.Dispose();throw new CadException("WALL_OPEN","Biên tường hở sau khi trừ cột.");}var edge=(Curve)remaining[match];remaining.RemoveAt(match);bool forward=edge.StartPoint.DistanceTo(end)<1e-5;double bulge=0;if(edge is Arc arc){double sweep=arc.EndAngle-arc.StartAngle;if(sweep<0)sweep+=2*Math.PI;bulge=Math.Tan(sweep/4)*(forward?1:-1);}else if(!(edge is Line)){loop.Dispose();throw new CadException("WALL_BOUNDARY","Biên không phải đoạn thẳng/cung tròn.");}loop.AddVertexAt(loop.NumberOfVertices,new Point2d(end.X,end.Y),bulge,0,0);end=forward?edge.EndPoint:edge.StartPoint;}while(end.DistanceTo(start)>1e-5);
     loop.Closed=true;if(loop.Area<1e-6){loop.Dispose();throw new CadException("WALL_AREA","Vùng tường suy biến.");}result.Add(loop);
    }
   }finally{foreach(DBObject obj in curves)obj.Dispose();}}
   return result;
  }finally{foreach(var region in regions)region.Dispose();}
 }
 internal static List<ObjectId> Write(Database db,Transaction tx,AxisResolvedLibrary lib,AxisGridParameters p,Point3d origin)
 {
  var geometry=ConstructionGeometry.Make(p,new(origin.X,origin.Y));var ids=new List<ObjectId>();var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
  foreach(var layer in new[]{lib.ColumnLayer,lib.WallLayer,lib.HatchLayer,lib.ColumnHatchLayer}.Where(l=>l!=null))NativeLayerStandards.Resources(new(){Layers=new(){layer!}},db,tx);
  ObjectId Append(Entity entity,string layer){entity.SetDatabaseDefaults(db);entity.Layer=layer;entity.ColorIndex=256;model.AppendEntity(entity);tx.AddNewlyCreatedDBObject(entity,true);ids.Add(entity.ObjectId);return entity.ObjectId;}
  foreach(var c in geometry.Columns){string signature=AICad.Contracts.Json.Hash(new{c.Circle,c.B,c.H,hatch=p.ColumnHatch?lib.ColumnHatch:null,layer=p.ColumnHatch?lib.ColumnHatchLayer?.Name:null});string name="QA_AXIS_COL_"+signature.Substring(0,16);ObjectId definition;
   if(bt.Has(name)){definition=bt[name];var existing=(BlockTableRecord)tx.GetObject(definition,OpenMode.ForRead);var children=existing.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Entity>().ToList();string actual=AICad.Contracts.Json.Hash(children.Select(e=>GridStore.EntityHash(e,tx)).ToArray());var dictionary=existing.ExtensionDictionary.IsNull?null:(DBDictionary)tx.GetObject(existing.ExtensionDictionary,OpenMode.ForRead);string saved=dictionary!=null&&dictionary.Contains("QA_COLUMN_SHAPE")?(string)((Xrecord)tx.GetObject(dictionary.GetAt("QA_COLUMN_SHAPE"),OpenMode.ForRead)).Data.AsArray()[0].Value:"";if(saved!=actual||existing.Origin!=Point3d.Origin||existing.Units!=UnitsValue.Millimeters)throw new CadException("COLUMN_CONFLICT","Định nghĩa cột đã bị sửa: "+name);}
   else{bt.UpgradeOpen();var record=new BlockTableRecord{Name=name,Origin=Point3d.Origin,Units=UnitsValue.Millimeters};definition=bt.Add(record);tx.AddNewlyCreatedDBObject(record,true);var edge=Outline(new(){B=c.B,H=c.H,Circle=c.Circle,Center=new()});edge.Layer="0";edge.ColorIndex=256;record.AppendEntity(edge);tx.AddNewlyCreatedDBObject(edge,true);var children=new List<Entity>{edge};
    if(p.ColumnHatch){var hatch=new Hatch();hatch.SetDatabaseDefaults(db);hatch.Layer=lib.ColumnHatchLayer!.Name;record.AppendEntity(hatch);tx.AddNewlyCreatedDBObject(hatch,true);hatch.Associative=true;hatch.HatchStyle=HatchStyle.Normal;ConfigureHatch(hatch,lib.ColumnHatch??throw new CadException("COLUMN_HATCH","Chọn hatch cột từ kho."));hatch.AppendLoop(HatchLoopTypes.External,new ObjectIdCollection(new[]{edge.ObjectId}));hatch.EvaluateHatch(true);var order=(DrawOrderTable)tx.GetObject(record.DrawOrderTableId,OpenMode.ForWrite);order.MoveToBottom(new ObjectIdCollection(new[]{hatch.ObjectId}));children.Add(hatch);}
    if(record.ExtensionDictionary.IsNull)record.CreateExtensionDictionary();var dictionary=(DBDictionary)tx.GetObject(record.ExtensionDictionary,OpenMode.ForWrite);var metadata=new Xrecord{Data=new ResultBuffer(new TypedValue(1,AICad.Contracts.Json.Hash(children.Select(e=>GridStore.EntityHash(e,tx)).ToArray())))};dictionary.SetAt("QA_COLUMN_SHAPE",metadata);tx.AddNewlyCreatedDBObject(metadata,true);
   }
   Append(new BlockReference(new Point3d(c.Center.X,c.Center.Y,0),definition){Rotation=c.Angle},lib.ColumnLayer!.Name);
  }
  var loops=WallLoops(geometry);try{
   var boundaryIds=new List<ObjectId>();foreach(var loop in loops)boundaryIds.Add(Append(loop,lib.WallLayer!.Name));
   if(p.Walls&&p.WallHatch&&boundaryIds.Count>0){var values=lib.WallHatch??throw new CadException("HATCH_REQUIRED","Chọn hatch tường.");var hatch=new Hatch();Append(hatch,lib.HatchLayer!.Name);hatch.Associative=true;hatch.HatchStyle=HatchStyle.Normal;
    ConfigureHatch(hatch,values);
    foreach(var id in boundaryIds)hatch.AppendLoop(HatchLoopTypes.External,new ObjectIdCollection(new[]{id}));hatch.EvaluateHatch(true);var order=(DrawOrderTable)tx.GetObject(model.DrawOrderTableId,OpenMode.ForWrite);order.MoveToBottom(new ObjectIdCollection(new[]{hatch.ObjectId}));
   }
  }finally{foreach(var loop in loops)if(loop.ObjectId.IsNull)loop.Dispose();}
  return ids;
 }
}
