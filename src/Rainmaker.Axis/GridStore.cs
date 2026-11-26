using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Axis;

internal sealed class GridDefinition
{
 public int SchemaVersion=2,Revision;
 public int MemberHashVersion=1;
 public string Name="";
 public string Id=Guid.NewGuid().ToString("N");
 public AxisGridParameters Parameters=new();public AxisLibrarySettings Library=new();
 public double X,Y;
 public string[] XAxisIds=Array.Empty<string>(),YAxisIds=Array.Empty<string>();
 public Dictionary<string,string> Members=new();
 [JsonIgnore] internal bool Adopted;
 [JsonIgnore] internal int IgnoredSelectionCount;
}
internal static partial class GridStore
{
 const string Root="QA_AXIS_GRIDS",Tag="QA_AXIS_GRID";
 internal static string EntityHash(Entity e,Transaction tx)
 {
  double[] P(Point3d p)=>new[]{Math.Round(p.X,7),Math.Round(p.Y,7),Math.Round(p.Z,7)};
  double[] P2(Point2d p)=>new[]{Math.Round(p.X,7),Math.Round(p.Y,7)};
  object Shape(Entity obj,int depth=0){
   if(obj is Line line)return new{a=P(line.StartPoint),b=P(line.EndPoint)};
   if(obj is Polyline poly)return new{poly.Closed,vertices=Enumerable.Range(0,poly.NumberOfVertices).Select(i=>new{p=P2(poly.GetPoint2dAt(i)),b=poly.GetBulgeAt(i)}).ToArray()};
   if(obj is Circle circle)return new{center=P(circle.Center),circle.Radius};
   if(obj is Arc arc)return new{center=P(arc.Center),arc.Radius,arc.StartAngle,arc.EndAngle};
   if(obj is BlockReference block){var attributes=block.AttributeCollection.Cast<ObjectId>().Select(id=>(AttributeReference)tx.GetObject(id,OpenMode.ForRead)).Select(a=>new{a.Tag,a.TextString,a.Height,a.Rotation}).ToArray();var definition=(BlockTableRecord)tx.GetObject(block.IsDynamicBlock?block.DynamicBlockTableRecord:block.BlockTableRecord,OpenMode.ForRead);var dynamicValues=block.IsDynamicBlock?block.DynamicBlockReferencePropertyCollection.Cast<DynamicBlockReferenceProperty>().Select(v=>v.PropertyName+":"+Convert.ToString(v.Value,CultureInfo.InvariantCulture)).ToArray():Array.Empty<string>();return new{dynamicValues,position=P(block.Position),block.Rotation,scale=new[]{block.ScaleFactors.X,block.ScaleFactors.Y,block.ScaleFactors.Z},definition.Name,basePoint=P(definition.Origin),definition.Units,attributes,shape=depth<2?definition.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Entity>().Select(child=>Shape(child,depth+1)).ToArray():Array.Empty<object>()};}
   if(obj is AlignedDimension d)return new{a=P(d.XLine1Point),b=P(d.XLine2Point),c=P(d.DimLinePoint),d.DimensionText,d.Dimtxt,d.Dimasz,d.Dimscale,style=d.DimensionStyle.Handle.ToString(),text=d.UsingDefaultTextPosition?null:P(d.TextPosition)};
   if(obj is Hatch h)return new{h.PatternName,h.PatternScale,h.PatternAngle,h.PatternSpace,h.PatternDouble,h.NumberOfLoops,h.Associative,loops=Enumerable.Range(0,h.NumberOfLoops).Select(i=>{var loop=h.GetLoopAt(i);return loop.IsPolyline?string.Join(";",loop.Polyline.Cast<BulgeVertex>().Select(v=>JsonConvert.SerializeObject(new{p=P2(v.Vertex),v.Bulge}))):loop.LoopType.ToString();}).ToArray()};
   if(obj is DBText text)return new{text.TextString,text.Height,p=P(text.Position),text.Rotation};
   return obj.GetType().Name;
  }
  return JsonUtil.Hash(new{type=e.GetType().Name,e.Layer,color=new{method=e.Color.ColorMethod.ToString(),e.ColorIndex,rgb=e.Color.ColorMethod==Autodesk.AutoCAD.Colors.ColorMethod.ByColor?new[]{(int)e.Color.Red,e.Color.Green,e.Color.Blue}:null},shape=Shape(e)});
 }
 static string MemberHash(Entity e,Transaction tx,int version)=>version<2?EntityHash(e,tx):version<3?JsonUtil.Hash(new{geometry=EntityHash(e,tx),extension=e is Dimension d?new{d.DimfxlenOn,d.Dimfxlen}:null}):JsonUtil.Hash(new{geometry=EntityHash(e,tx),extension=e is Dimension dim?new{dim.DimfxlenOn,dim.Dimfxlen,dim.Dimexo}:null});
 static string Text(Xrecord record)=>string.Concat(record.Data.AsArray().Select(v=>v.Value as string));
 static void Put(DBDictionary dict,string key,string text,Transaction tx){Xrecord record;if(dict.Contains(key)){record=(Xrecord)tx.GetObject(dict.GetAt(key),OpenMode.ForWrite);}else{dict.UpgradeOpen();record=new Xrecord();dict.SetAt(key,record);tx.AddNewlyCreatedDBObject(record,true);}var chunks=Enumerable.Range(0,(text.Length+199)/200).Select(i=>new TypedValue(1,text.Substring(i*200,Math.Min(200,text.Length-i*200))));record.Data=new ResultBuffer(chunks.ToArray());}
 internal static ObjectId HandleId(Database db,string handle){try{return db.GetObjectId(false,new Handle(long.Parse(handle,NumberStyles.HexNumber,CultureInfo.InvariantCulture)),0);}catch{return ObjectId.Null;}}
 static DBDictionary? Folder(Database db,Transaction tx,bool create){var root=(DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);if(root.Contains(Root))return (DBDictionary)tx.GetObject(root.GetAt(Root),OpenMode.ForRead);if(!create)return null;root.UpgradeOpen();var folder=new DBDictionary();root.SetAt(Root,folder);tx.AddNewlyCreatedDBObject(folder,true);return folder;}
 internal static GridDefinition? Read(Database db,ObjectId member)
 {
  using var tx=db.TransactionManager.StartOpenCloseTransaction();var e=(Entity)tx.GetObject(member,OpenMode.ForRead);if(e.ExtensionDictionary.IsNull)return null;var dict=(DBDictionary)tx.GetObject(e.ExtensionDictionary,OpenMode.ForRead);if(!dict.Contains(Tag))return null;string id=Text((Xrecord)tx.GetObject(dict.GetAt(Tag),OpenMode.ForRead));var folder=Folder(db,tx,false);if(folder==null||!folder.Contains(id))throw new CadException("GRID_METADATA","Metadata lưới thiếu. Không nhận quản lý tự động.");var result=JsonConvert.DeserializeObject<GridDefinition>(Text((Xrecord)tx.GetObject(folder.GetAt(id),OpenMode.ForRead)))!;if(result.SchemaVersion!=1&&result.SchemaVersion!=2)throw new CadException("GRID_METADATA","Phiên bản dữ liệu lưới không hỗ trợ.");return result;
 }
 internal static List<string> Changes(Database db,GridDefinition definition){using var tx=db.TransactionManager.StartOpenCloseTransaction();var changes=new List<string>();foreach(var pair in definition.Members){var id=HandleId(db,pair.Key);if(id.IsNull||id.IsErased||!id.IsValid){changes.Add(pair.Key+": đã xóa");continue;}var e=(Entity)tx.GetObject(id,OpenMode.ForRead);if(MemberHash(e,tx,definition.MemberHashVersion)!=pair.Value)changes.Add(pair.Key+": đã sửa tay");}return changes;}
 internal static GridDefinition Apply(Database db,AxisResolvedLibrary library,AxisGridParameters p,Point3d origin,GridDefinition? old=null,bool overwrite=false,bool replaceDims=false)
 {
  p.Envelope?.DemandMatch(p.XBays,p.YBays);
  if(old!=null&&!overwrite&&Changes(db,old).Count>0)throw new CadException("GRID_CHANGED","Lưới có đối tượng đã chỉnh tay/xóa; cần xem khác biệt trước khi cập nhật.");
  using var tx=db.TransactionManager.StartTransaction();var folder=Folder(db,tx,true)!;
  if(old==null){foreach(DBDictionaryEntry entry in folder){var other=JsonConvert.DeserializeObject<GridDefinition>(Text((Xrecord)tx.GetObject(entry.Value,OpenMode.ForRead)))!;if(Math.Abs(other.X-origin.X)<1e-5&&Math.Abs(other.Y-origin.Y)<1e-5)throw new CadException("GRID_DUPLICATE","Đã có lưới tại điểm này. Chọn lại lưới để cập nhật.");}}
  else if(!old.Adopted){if(!folder.Contains(old.Id))throw new CadException("GRID_CHANGED","Lưới đã bị xóa.");var current=JsonConvert.DeserializeObject<GridDefinition>(Text((Xrecord)tx.GetObject(folder.GetAt(old.Id),OpenMode.ForRead)))!;if(current.Revision!=old.Revision)throw new CadException("GRID_CHANGED","Lưới đã được cập nhật; chọn lại lưới.");}
  if(old!=null){var members=old.Members.Keys.Select(handle=>HandleId(db,handle)).Where(id=>!id.IsNull&&id.IsValid&&!id.IsErased).Select(id=>(Entity)tx.GetObject(id,OpenMode.ForRead)).ToArray();foreach(var e in members.OrderBy(e=>e is Hatch?0:1)){e.UpgradeOpen();e.Erase();}}
  var ids=RainmakerAxis.WriteConfigured(db,library,p,origin,null,replaceDims);ids.AddRange(ConstructionWriter.Write(db,tx,library,p,origin));
  var result=new GridDefinition{MemberHashVersion=3,Id=old?.Id??Guid.NewGuid().ToString("N"),Revision=(old?.Revision??0)+1,Name=p.GridName.Trim(),Parameters=p,Library=library.Settings,X=origin.X,Y=origin.Y};
  string[] Axes(int count,string[]? previous)=>Enumerable.Range(0,count).Select(i=>previous!=null&&i<previous.Length?previous[i]:Guid.NewGuid().ToString("N")).ToArray();result.XAxisIds=Axes(p.XLabels.Length,old?.XAxisIds);result.YAxisIds=Axes(p.YLabels.Length,old?.YAxisIds);
  foreach(var id in ids){var e=(Entity)tx.GetObject(id,OpenMode.ForWrite);if(e.ExtensionDictionary.IsNull)e.CreateExtensionDictionary();var dict=(DBDictionary)tx.GetObject(e.ExtensionDictionary,OpenMode.ForWrite);Put(dict,Tag,result.Id,tx);result.Members[e.Handle.ToString()]=MemberHash(e,tx,result.MemberHashVersion);}
  Put(folder,result.Id,JsonConvert.SerializeObject(result),tx);tx.Commit();return result;
 }
 internal static GridDefinition Adopt(Database db,ObjectId[] selection,AxisLibrarySettings settings)
 {
  using var tx=db.TransactionManager.StartOpenCloseTransaction();
  if(!db.TileMode)throw new CadException("GRID_ADOPT","Chỉ nhận lưới trong Model.");
  if(selection.Length>10000)throw new CadException("GRID_ADOPT","Tối đa 10.000 đối tượng trong một lần nhận.");
  var entities=selection.Distinct().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Entity>().Where(e=>e.OwnerId==db.CurrentSpaceId).ToArray();
  var curves=entities.Where(e=>e is Line||e is Polyline||e is Polyline2d).ToArray();var segments=new List<AxisSegment>();
  void Segment(Point3d a,Point3d b,double bulge=0){
   if(Math.Abs(a.Z)>1e-6||Math.Abs(b.Z)>1e-6||Math.Abs(bulge)>1e-10)throw new CadException("GRID_ADOPT","Chỉ nhận Line/Polyline thẳng trên WCS XY; bỏ đoạn cong hoặc có cao độ.");
   segments.Add(new(a.X,a.Y,b.X,b.Y));
  }
  foreach(var e in curves){
   if(Read(db,e.ObjectId)!=null)throw new CadException("GRID_ADOPT","Đối tượng đã thuộc lưới quản lý. Dùng Sửa lưới để tránh nhận chồng.");
   if(e is Line line)Segment(line.StartPoint,line.EndPoint);
   else if(e is Polyline poly){
    if(poly.NumberOfVertices<2)throw new CadException("GRID_ADOPT","Polyline cần ít nhất hai đỉnh.");
    for(int i=0;i<poly.NumberOfVertices-(poly.Closed?0:1);i++)Segment(poly.GetPoint3dAt(i),poly.GetPoint3dAt((i+1)%poly.NumberOfVertices),poly.GetBulgeAt(i));
   }else if(e is Polyline2d old){
    var vertices=old.Cast<ObjectId>().Select(id=>(Vertex2d)tx.GetObject(id,OpenMode.ForRead)).ToArray();
    if(old.PolyType!=Poly2dType.SimplePoly||vertices.Length<2)throw new CadException("GRID_ADOPT","Chỉ nhận Polyline 2D thẳng, không fit/spline.");
    for(int i=0;i<vertices.Length-(old.Closed?0:1);i++)Segment(vertices[i].Position,vertices[(i+1)%vertices.Length].Position,vertices[i].Bulge);
   }
  }
  RecognizedAxes grid;try{grid=AxisGridRecognition.Read(segments);}catch(ArgumentException error){throw new CadException("GRID_ADOPT",error.Message);}
  var xs=grid.X;var ys=grid.Y;const double tolerance=AxisGridRecognition.Tolerance;
  var members=curves.ToList();
  // Optional old heads/dims are accepted only when tied to this grid. Other
  // selected entities stay outside Members and are never erased on Apply.
  bool Near(double value,double[] positions)=>positions.Any(v=>Math.Abs(v-value)<=tolerance);
  var heads=entities.OfType<BlockReference>().Where(b=>settings.AttributeTag.Trim()!=""&&
   (Near(b.Position.X,xs)||Near(b.Position.Y,ys))&&b.AttributeCollection.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<AttributeReference>().Any(a=>a.Tag.Trim()==settings.AttributeTag.Trim())).ToArray();
  members.AddRange(heads);
  if(entities.OfType<Dimension>().Any()){
   var index=new AxisLibrary(AICad.Bridge.Plugin.Current!.Config.ContentRoot);string layer=index.Demand("layers",settings.DimLayerId).Name;
   members.AddRange(entities.OfType<AlignedDimension>().Where(d=>d.Layer==layer&&
    (Math.Abs(d.XLine1Point.Y-d.XLine2Point.Y)<=tolerance&&Near(d.XLine1Point.X,xs)&&Near(d.XLine2Point.X,xs)&&Near(d.XLine1Point.Y,ys)||
     Math.Abs(d.XLine1Point.X-d.XLine2Point.X)<=tolerance&&Near(d.XLine1Point.Y,ys)&&Near(d.XLine2Point.Y,ys)&&Near(d.XLine1Point.X,xs))));
  }
  foreach(var e in members)if(Read(db,e.ObjectId)!=null)throw new CadException("GRID_ADOPT","Có đối tượng đã thuộc lưới quản lý. Dùng Sửa lưới.");
  string[] Labels(double[] positions,bool vertical)=>positions.Select((v,i)=>{
   var labels=heads.Where(b=>Math.Abs((vertical?b.Position.X:b.Position.Y)-v)<=tolerance).SelectMany(b=>b.AttributeCollection.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<AttributeReference>()).Where(a=>a.Tag.Trim()==settings.AttributeTag.Trim()).Select(a=>a.TextString.Trim()).Where(t=>t!="").Distinct().ToArray();
   if(labels.Length>1)throw new CadException("GRID_ADOPT","Hai đầu của cùng trục có ký hiệu khác nhau; chỉnh lại trước khi nhận.");
   return labels.FirstOrDefault()??(vertical?(i+1).ToString():AxisGridLayout.Labels("A",positions.Length)[i]);
  }).ToArray();
  var p=new AxisGridParameters{Scale=60,XBays=xs.Zip(xs.Skip(1),(a,b)=>b-a).ToArray(),YBays=ys.Zip(ys.Skip(1),(a,b)=>b-a).ToArray(),XLabels=Labels(xs,true),YLabels=Labels(ys,false)};
  return new(){Adopted=true,IgnoredSelectionCount=selection.Distinct().Count()-members.Count,Parameters=p,Library=settings,X=xs[0],Y=ys[0],Members=members.ToDictionary(e=>e.Handle.ToString(),e=>EntityHash(e,tx))};
 }
}