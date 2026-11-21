using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json;

namespace Rainmaker.Axis;

internal sealed class GridCatalogRow
{
 public string Name {get;set;}="";
 public string Summary {get;set;}="";
 public string Status {get;set;}="";
 internal GridDefinition? Definition;
 internal string Id="";
}
internal sealed class CadScene
{
 internal readonly List<List<Point2>> Paths=new();
 internal readonly List<(Point2 Point,string Text)> Labels=new();
 internal int Unreadable;
}
internal static partial class GridStore
{
 internal static List<GridCatalogRow> List(Database db,bool verify=true)
 {
  using var tx=db.TransactionManager.StartOpenCloseTransaction();var folder=Folder(db,tx,false);var rows=new List<GridCatalogRow>();if(folder==null)return rows;
  foreach(DBDictionaryEntry entry in folder){var row=new GridCatalogRow{Id=entry.Key,Name="Lưới "+entry.Key.Substring(0,Math.Min(6,entry.Key.Length))};rows.Add(row);try{var d=JsonConvert.DeserializeObject<GridDefinition>(Text((Xrecord)tx.GetObject(entry.Value,OpenMode.ForRead)))??throw new Exception("Không đọc được dữ liệu");if(d.SchemaVersion!=1&&d.SchemaVersion!=2)throw new Exception("Phiên bản dữ liệu chưa hỗ trợ");row.Definition=d;row.Name=string.IsNullOrWhiteSpace(d.Name)?row.Name:d.Name;row.Summary=$"{d.Parameters.XLabels.Length}×{d.Parameters.YLabels.Length} trục · {d.Parameters.XBays.Sum():0.##}×{d.Parameters.YBays.Sum():0.##} mm · {(d.Parameters.Columns?d.Parameters.ColumnNodes.Length:0)} cột/{(d.Parameters.Walls?d.Parameters.WallSegments.Length:0)} tường";row.Status=verify?ChangeStatus(Changes(db,d)):"Chọn để đối chiếu";}catch(Exception e){row.Definition=null;row.Status="Dữ liệu lỗi: "+e.Message;}}
  return rows.OrderBy(r=>r.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
 }
 internal static string ChangeStatus(List<string> changes)=>changes.Any(c=>c.EndsWith("đã xóa",StringComparison.Ordinal))?"Thiếu thành viên":changes.Count>0?"Đã sửa tay":"Khớp";
 internal static GridDefinition Rename(Database db,GridDefinition old,string name)
 {
  name=name.Trim();if(name.Length==0||name.Length>100||name.Any(char.IsControl))throw new CadException("GRID_NAME","Tên lưới cần 1–100 ký tự.");using var tx=db.TransactionManager.StartTransaction();var folder=Folder(db,tx,false)??throw new CadException("GRID_METADATA","Không tìm thấy danh mục lưới.");if(!folder.Contains(old.Id))throw new CadException("GRID_METADATA","Lưới đã bị xóa.");var d=JsonConvert.DeserializeObject<GridDefinition>(Text((Xrecord)tx.GetObject(folder.GetAt(old.Id),OpenMode.ForRead)))!;if(d.Revision!=old.Revision)throw new CadException("GRID_CHANGED","Dữ liệu lưới đã đổi; chọn lại trước khi đổi tên.");d.Name=name;d.Parameters.GridName=name;d.Revision++;Put(folder,d.Id,JsonConvert.SerializeObject(d),tx);tx.Commit();return d;
 }
 internal static ObjectId[] LiveMembers(Database db,GridDefinition d)=>d.Members.Keys.Select(k=>HandleId(db,k)).Where(id=>!id.IsNull&&id.IsValid&&!id.IsErased).ToArray();
 internal static CadScene Capture(Database db,GridDefinition d)
 {
  using var tx=db.TransactionManager.StartOpenCloseTransaction();var scene=new CadScene();int visited=0;Point2 P(Point3d point,Matrix3d transform){var p=point.TransformBy(transform);return new(p.X-d.X,p.Y-d.Y);}
  void Add(Entity e,Matrix3d transform,int depth=0){
   if(++visited>20000){scene.Unreadable++;return;}
   if(e is Hatch||e is AttributeDefinition)return;
   if(e is BlockReference block){if(depth>=6){scene.Unreadable++;return;}var record=(BlockTableRecord)tx.GetObject(block.BlockTableRecord,OpenMode.ForRead);foreach(ObjectId id in record)if(tx.GetObject(id,OpenMode.ForRead) is Entity child)Add(child,transform*block.BlockTransform,depth+1);foreach(ObjectId id in block.AttributeCollection){var a=(AttributeReference)tx.GetObject(id,OpenMode.ForRead);scene.Labels.Add((P(a.Position,transform),a.TextString));}return;}
   if(e is AlignedDimension dimension){bool horizontal=Math.Abs(dimension.XLine1Point.Y-dimension.XLine2Point.Y)<1e-6;var a=dimension.XLine1Point;var b=dimension.XLine2Point;var line=dimension.DimLinePoint;var x=horizontal?new Point3d(a.X,line.Y,0):new Point3d(line.X,a.Y,0);var y=horizontal?new Point3d(b.X,line.Y,0):new Point3d(line.X,b.Y,0);scene.Paths.Add(new(){P(a,transform),P(x,transform),P(y,transform),P(b,transform)});return;}
   if(e is DBText text){scene.Labels.Add((P(text.Position,transform),text.TextString));return;}
   if(e is Curve curve){var points=new List<Point2>();if(curve is Polyline poly){int count=Math.Min(poly.NumberOfVertices,10000);for(int i=0;i<count;i++){points.Add(P(poly.GetPoint3dAt(i),transform));if(poly.GetBulgeAt(i)!=0&&(i<poly.NumberOfVertices-1||poly.Closed))for(int k=1;k<12;k++)points.Add(P(poly.GetPointAtParameter(i+k/12.0),transform));}if(poly.NumberOfVertices>count)scene.Unreadable++;if(poly.Closed&&points.Count>0)points.Add(points[0]);}else{int steps=e is Line?1:48;for(int i=0;i<=steps;i++)points.Add(P(curve.GetPointAtParameter(curve.StartParam+(curve.EndParam-curve.StartParam)*i/steps),transform));}scene.Paths.Add(points);}
  }
  foreach(var id in LiveMembers(db,d)){try{Add((Entity)tx.GetObject(id,OpenMode.ForRead),Matrix3d.Identity);}catch{scene.Unreadable++;}}return scene;
 }
 internal static void Locate(Autodesk.AutoCAD.ApplicationServices.Document doc,GridDefinition d)
 {
  var ids=LiveMembers(doc.Database,d);if(ids.Length==0)throw new CadException("GRID_EMPTY","Lưới không còn thành viên trong DWG.");using var tx=doc.Database.TransactionManager.StartOpenCloseTransaction();Extents3d? extents=null;foreach(var id in ids){try{var bounds=((Entity)tx.GetObject(id,OpenMode.ForRead)).GeometricExtents;if(extents==null)extents=bounds;else{var value=extents.Value;value.AddExtents(bounds);extents=value;}}catch{}}
  doc.Editor.SetImpliedSelection(ids);if(extents==null)return;using var view=doc.Editor.GetCurrentView();var transform=Matrix3d.PlaneToWorld(view.ViewDirection);transform=Matrix3d.Displacement(view.Target-Point3d.Origin)*transform;transform=Matrix3d.Rotation(-view.ViewTwist,view.ViewDirection,view.Target)*transform;var box=extents.Value;box.TransformBy(transform.Inverse());double ratio=view.Width/view.Height;view.Height=Math.Max(1,box.MaxPoint.Y-box.MinPoint.Y)*1.15;view.Width=Math.Max(1,box.MaxPoint.X-box.MinPoint.X)*1.15;if(view.Width/view.Height>ratio)view.Height=view.Width/ratio;else view.Width=view.Height*ratio;view.CenterPoint=new Point2d((box.MinPoint.X+box.MaxPoint.X)/2,(box.MinPoint.Y+box.MaxPoint.Y)/2);doc.Editor.SetCurrentView(view);
 }
}
