using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
[assembly:CommandClass(typeof(AICad.LibraryScanner.Scanner))]
namespace AICad.LibraryScanner;
public sealed class Scanner {
 static string Alias(string name){string s=Regex.Replace(name,@"[^\p{L}\p{N}_-]","_");if(s.Length>60)s=s.Substring(0,60);return string.IsNullOrWhiteSpace(s)?"ITEM":s;}
 [CommandMethod("AICADLIBRARYSCAN",CommandFlags.Modal|CommandFlags.NoUndoMarker)]
 public void Run(){string root=Environment.GetEnvironmentVariable("AICAD_LIBRARY_JOB")??"";if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-library-job")))throw new InvalidOperationException("Isolated library job required.");try{
  string source=Path.Combine(root,"source.dwg");if(new FileInfo(source).Length>20*1024*1024)throw new InvalidOperationException("Source exceeds 20 MB.");
  using var db=new Database(false,true);db.ReadDwgFile(source,FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);
  var result=new LibraryAnalysis{Id=Path.GetFileName(root),SourceHash=JsonUtil.FileHash(source),Units=db.Insunits.ToString()};
  using var tx=db.TransactionManager.StartTransaction();
  var layers=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForRead);foreach(ObjectId id in layers.Cast<ObjectId>().Take(500)){
   var l=(LayerTableRecord)tx.GetObject(id,OpenMode.ForRead);short c=l.Color.ColorIndex;bool supported=c>=1&&c<=255&&!l.IsDependent;
   var lineType=(LinetypeTableRecord)tx.GetObject(l.LinetypeObjectId,OpenMode.ForRead);result.Layers.Add(new(){Dashes=Enumerable.Range(0,lineType.NumDashes).Select(i=>lineType.DashLengthAt(i)).ToList(),PatternLength=lineType.PatternLength,LineType=lineType.Name,LineWeight=(int)l.LineWeight,Rgb=l.Color.ColorMethod==Autodesk.AutoCAD.Colors.ColorMethod.ByColor?(l.Color.Red<<16|l.Color.Green<<8|l.Color.Blue):null,Plottable=l.IsPlottable,Name=l.Name,SuggestedId=Alias(l.Name),Color=c,Supported=supported,Reason=supported?"":"Layer phụ thuộc hoặc dùng màu không phải ACI; màu trong block gốc vẫn được giữ."});
  }
  var text=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForRead);foreach(ObjectId id in text.Cast<ObjectId>().Take(100)){
   var t=(TextStyleTableRecord)tx.GetObject(id,OpenMode.ForRead);string f=Path.GetFileName(t.FileName??"");if(f.Length>0&&Path.GetExtension(f)=="")f+=".shx";bool fontAvailable=File.Exists(Path.Combine(@"C:\Program Files\Autodesk\AutoCAD 2021\Fonts",f))||File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),f));bool supported=fontAvailable&&Regex.IsMatch(f,@"^[a-zA-Z0-9_-]{1,60}\.(shx|ttf)$",RegexOptions.IgnoreCase)&&string.IsNullOrWhiteSpace(t.BigFontFileName);
   result.TextStyles.Add(new(){Name=t.Name,Font=f,TextHeight=t.TextSize>=10&&t.TextSize<=500?t.TextSize:100,Supported=supported,Reason=supported?"Chỉ lấy font và chiều cao chữ; các tham số khác dùng chuẩn của app.":"Font chưa có trên máy hoặc có bigfont chưa hỗ trợ cho chữ mới; định nghĩa chữ trong block gốc vẫn được giữ."});
  }
  var dims=(DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForRead);foreach(ObjectId id in dims.Cast<ObjectId>().Take(100)){
   var t=(DimStyleTableRecord)tx.GetObject(id,OpenMode.ForRead);bool supported=t.Dimtxt>=10&&t.Dimtxt<=500&&t.Dimasz>=1&&t.Dimasz<=300&&Math.Abs(t.Dimlfac-1)<1e-9&&Math.Abs(t.Dimscale-1)<1e-9;
   result.DimensionStyles.Add(new(){Name=t.Name,TextHeight=t.Dimtxt,ArrowSize=t.Dimasz,Supported=supported,Reason=supported?"Lấy chiều cao chữ và mũi tên cho kích thước mới.":"App tạo kích thước theo mm, tỷ lệ 1; style nguồn có chiều cao/tỷ lệ chưa phù hợp."});
  }
  var table=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var definitions=table.Cast<ObjectId>().Select(id=>(BlockTableRecord)tx.GetObject(id,OpenMode.ForRead)).Where(b=>!b.IsLayout&&!b.IsAnonymous).ToList();
  if(definitions.Count>2000)throw new InvalidOperationException("Thư viện vượt 2.000 block có tên; hãy chia nhỏ DWG trước khi nhập.");
  foreach(var b in definitions)result.Blocks.Add(Analyze(b,db,tx,false));
  var ms=(BlockTableRecord)tx.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForRead);if(ms.Cast<ObjectId>().Any())result.Blocks.Insert(0,Analyze(ms,db,tx,true));
  foreach(var item in result.Blocks.Where(item=>item.Dynamic)){var definition=definitions.Single(b=>b.Name==item.Name);ProbeDynamic(item,definition,db,tx);}
  if(layers.Cast<ObjectId>().Count()>500)result.Warnings.Add("Danh sách layer được giới hạn ở 500 layer.");
  if(result.TextStyles.Any(t=>!t.Supported))result.Warnings.Add("Một số font/style chữ nguồn chưa có trên máy hoặc dùng bigfont. Cần cài đủ font gốc để block hiển thị đúng.");result.Warnings.Add("Hình thu nhỏ là sơ đồ 2D. Block gốc, màu, layer, style và thuộc tính được sao chép bằng API native; xem trước đầy đủ ở AutoCAD.");
  int previewBudget=1200;foreach(var block in result.Blocks){block.Preview=block.Preview.Take(Math.Max(0,previewBudget)).ToList();previewBudget-=block.Preview.Count;block.PreviewComplete=block.Preview.Count==block.EntityCount;}
  JsonUtil.Atomic(Path.Combine(root,"analysis.json"),result);
 }catch(System.Exception e){JsonUtil.Atomic(Path.Combine(root,"scan-error.json"),new{code="CAD_SCAN_FAILED",message=e.Message});}}
 static void ProbeDynamic(LibraryBlock item,BlockTableRecord definition,Database db,Transaction tx){var previous=HostApplicationServices.WorkingDatabase;BlockReference? probe=null;try{HostApplicationServices.WorkingDatabase=db;var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);probe=new BlockReference(Point3d.Origin,definition.ObjectId);ms.AppendEntity(probe);tx.AddNewlyCreatedDBObject(probe,true);foreach(DynamicBlockReferenceProperty property in probe.DynamicBlockReferencePropertyCollection){var value=property.Value;item.DynamicProperties.Add(new(){Name=property.PropertyName,Description=property.Description,Units=property.UnitsType.ToString(),ValueType=value?.GetType().Name??"",ReadOnly=property.ReadOnly,Visible=property.VisibleInCurrentVisibilityState,DefaultValue=value==null?JValue.CreateNull():JToken.FromObject(value),AllowedValues=property.GetAllowedValues().Take(128).Select(JToken.FromObject).ToList()});}foreach(DynamicBlockReferenceProperty property in probe.DynamicBlockReferencePropertyCollection){if(property.ReadOnly||property.Value is not string)continue;var states=property.GetAllowedValues().OfType<string>().Take(64).ToList();foreach(string state in states){property.Value=state;var bounds=probe.GeometricExtents;if(Math.Abs(bounds.MinPoint.Z)>1e-6||Math.Abs(bounds.MaxPoint.Z)>1e-6)continue;item.VisibilityStates.Add(new(){Property=property.PropertyName,Value=state,Min=new(bounds.MinPoint.X,bounds.MinPoint.Y),Max=new(bounds.MaxPoint.X,bounds.MaxPoint.Y)});}}}catch(System.Exception e){item.DynamicError=e.Message;}finally{if(probe!=null&&!probe.IsErased)probe.Erase();HostApplicationServices.WorkingDatabase=previous;}}
 static LibraryBlock Analyze(BlockTableRecord b,Database db,Transaction tx,bool model){
  var item=new LibraryBlock{Name=model?"Toàn bộ Model":b.Name,SuggestedId=model?"MODEL_DETAIL":Alias(b.Name),ModelSpace=model,Dynamic=b.IsDynamicBlock,Supported=true};
  try{
   if(b.IsFromExternalReference||b.IsFromOverlayReference)throw new InvalidOperationException("Xref cần được bind trong bản vẽ nguồn trước khi nhập.");
   int count=0;void Check(BlockTableRecord record,HashSet<ObjectId> stack,int depth){if(depth>16||!stack.Add(record.ObjectId))throw new InvalidOperationException("Block lồng quá sâu hoặc vòng tham chiếu.");if(record.IsFromExternalReference||record.IsFromOverlayReference)throw new InvalidOperationException("Có xref lồng chưa bind.");foreach(ObjectId id in record){if(++count>10000)throw new InvalidOperationException("Block vượt 10.000 đối tượng.");var e=tx.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;if(!item.InternalLayers.Contains(e.Layer))item.InternalLayers.Add(e.Layer);if(e is ProxyEntity||e is RasterImage||e is UnderlayReference)throw new InvalidOperationException("Có proxy, ảnh hoặc underlay phụ thuộc tài nguyên ngoài.");if(e is AttributeDefinition)item.Attributes++;if(e is BlockReference nested)Check((BlockTableRecord)tx.GetObject(nested.BlockTableRecord,OpenMode.ForRead),stack,depth+1);}stack.Remove(record.ObjectId);}
   Check(b,new HashSet<ObjectId>(),0);item.EntityCount=count;if(count==0)throw new InvalidOperationException("Block trống.");
   Extents3d ex;
   if(model){bool any=false;ex=new Extents3d();foreach(ObjectId id in b){if(tx.GetObject(id,OpenMode.ForRead) is Entity e){try{var x=e.GeometricExtents;if(!any){ex=x;any=true;}else ex.AddExtents(x);}catch(Autodesk.AutoCAD.Runtime.Exception){}}}if(!any)throw new InvalidOperationException("Không đọc được phạm vi model.");ex.TransformBy(Matrix3d.Displacement(Point3d.Origin-db.Insbase));}
   else {using var reference=new BlockReference(Point3d.Origin,b.ObjectId);ex=reference.GeometricExtents;foreach(ObjectId child in b)if(tx.GetObject(child,OpenMode.ForRead) is AttributeDefinition ad){try{var ax=ad.GeometricExtents;ax.TransformBy(reference.BlockTransform);ex.AddExtents(ax);}catch(Autodesk.AutoCAD.Runtime.Exception){}}}
   if(Math.Abs(ex.MinPoint.Z)>1e-6||Math.Abs(ex.MaxPoint.Z)>1e-6)throw new InvalidOperationException("Tài nguyên 3D chưa hỗ trợ trong luồng triển khai 2D.");
   item.Min=new(ex.MinPoint.X,ex.MinPoint.Y);item.Max=new(ex.MaxPoint.X,ex.MaxPoint.Y);
   if(Math.Max(Math.Abs(item.Min.X),Math.Abs(item.Max.X))>1e8||Math.Max(Math.Abs(item.Min.Y),Math.Abs(item.Max.Y))>1e8)throw new InvalidOperationException("Tọa độ vượt giới hạn thư viện.");
   Point3d origin=model?db.Insbase:b.Origin;
   foreach(ObjectId id in b){if(item.Preview.Count>=8)break;var e=tx.GetObject(id,OpenMode.ForRead) as Entity;Operation? op=null;Point2 P(Point3d p)=>new(p.X-origin.X,p.Y-origin.Y);
    if(e is Line line)op=new(){Kind="draw_line",Points=new(){P(line.StartPoint),P(line.EndPoint)}};
    else if(e is Polyline pl&&Enumerable.Range(0,pl.NumberOfVertices).All(i=>Math.Abs(pl.GetBulgeAt(i))<1e-9)&&pl.NumberOfVertices<=100)op=new(){Kind="draw_polyline",Closed=pl.Closed,Points=Enumerable.Range(0,pl.NumberOfVertices).Select(i=>P(pl.GetPoint3dAt(i))).ToList()};
    else if(e is Circle c)op=new(){Kind="draw_circle",Radius=c.Radius,Points=new(){P(c.Center)}};
    else if(e is Arc a&&Math.Abs((a.EndAngle-a.StartAngle+Math.PI*2)%(Math.PI*2)-Math.PI/2)<1e-6)op=new(){Kind="draw_arc",Radius=a.Radius,Rotation=a.StartAngle,Points=new(){P(a.Center)}};
    if(op!=null)item.Preview.Add(op);
   }
   item.PreviewComplete=item.Preview.Count==count;
  }catch(System.Exception e){item.Supported=false;item.Reason=e.Message;}
  return item;
 }
}
