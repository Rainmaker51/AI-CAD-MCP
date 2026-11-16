using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Security;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Xml.Linq;
[assembly:CommandClass(typeof(AICad.LibraryScanner.BlockPreviews))]
namespace AICad.LibraryScanner;
public sealed class BlockPreviews {
 // Fixed-size PNGs keep CAD geometry out of the JSON response entirely.
 internal static void Thumbnail(string svg,string output,double minX,double maxY,double width,double height){
  using var bitmap=new Bitmap(240,180);using var graphics=Graphics.FromImage(bitmap);
  graphics.Clear(Color.FromArgb(247,249,252));graphics.SmoothingMode=SmoothingMode.AntiAlias;
  float scale=(float)Math.Min(216/width,156/height),ox=(240-(float)width*scale)/2,oy=(180-(float)height*scale)/2;
  using var pen=new Pen(Color.FromArgb(24,49,73),.9f);
  foreach(var shape in XDocument.Parse(svg).Descendants().Where(e=>e.Name.LocalName=="polyline")){
   var points=((string?)shape.Attribute("points")??"").Split(' ').Where(s=>s.Length>0).Select(s=>s.Split(',')).Select(p=>new PointF(ox+(float)(double.Parse(p[0],CultureInfo.InvariantCulture)-minX)*scale,oy+(float)(double.Parse(p[1],CultureInfo.InvariantCulture)+maxY)*scale)).ToArray();
   if(points.Length>1)graphics.DrawLines(pen,points);
  }
  bitmap.Save(output,ImageFormat.Png);
 }
 [CommandMethod("AICADBLOCKPREVIEWS",CommandFlags.Modal)]
 public void Run(){
  string root=Environment.GetEnvironmentVariable("AICAD_LIBRARY_JOB")??"";
  if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-library-job")))return;
  var results=new JObject();
  try{
   using var db=new Database(false,true);db.ReadDwgFile(Path.Combine(root,"source.dwg"),FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);
   var previous=HostApplicationServices.WorkingDatabase;
   try{HostApplicationServices.WorkingDatabase=db;
    using var tx=db.TransactionManager.StartTransaction();var table=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);
    foreach(var asset in JsonUtil.Read<List<BlockAsset>>(File.ReadAllText(Path.Combine(root,"blocks.json"))))try{
     var n=asset.NativeSource!;if(!table.Has(n.BlockName))throw new InvalidOperationException("Không tìm thấy block nguồn.");
     var ms=(BlockTableRecord)tx.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
     using var reference=new BlockReference(Point3d.Origin,table[n.BlockName]);ms.AppendEntity(reference);tx.AddNewlyCreatedDBObject(reference,true);
     if(reference.IsDynamicBlock&&asset.Usage!=null)foreach(DynamicBlockReferenceProperty prop in reference.DynamicBlockReferencePropertyCollection){
      if(prop.ReadOnly||!asset.Usage.Values.TryGetValue(prop.PropertyName,out var value))continue;
      prop.Value=value.ToObject(prop.Value.GetType())!;
     }
     var shapes=new StringBuilder();var layers=new HashSet<string>();int count=0,skipped=0;Extents3d bounds=new();bool any=false;
     string F(double d)=>d.ToString("0.####",CultureInfo.InvariantCulture);
     void Include(Point3d p){if(!any){bounds=new Extents3d(p,p);any=true;}else bounds.AddPoint(p);}
     void Draw(Entity e,int depth){
      // Exploded dynamic blocks can include entities from other visibility states.
      if(!e.Visible)return;
      if(depth>16||count>=12000){skipped++;return;}count++;layers.Add(e.Layer);
      if(e is DBText text){var p=text.Position;Include(p);try{bounds.AddExtents(text.GeometricExtents);}catch{}shapes.Append("<text x=\""+F(p.X)+"\" y=\""+F(-p.Y)+"\" font-size=\""+F(Math.Max(1,text.Height))+"\" fill=\"#183149\" stroke=\"none\" transform=\"rotate("+F(-text.Rotation*180/Math.PI)+" "+F(p.X)+" "+F(-p.Y)+")\">"+SecurityElement.Escape(text.TextString)+"</text>");return;}
      if(e is Curve curve){
       try{int samples=e is Line?1:e is Polyline pl?Math.Min(300,Math.Max(2,pl.NumberOfVertices*3)):32;var pts=new List<string>();
        for(int i=0;i<=samples;i++){var p=curve.GetPointAtParameter(curve.StartParam+(curve.EndParam-curve.StartParam)*i/samples);Include(p);pts.Add(F(p.X)+","+F(-p.Y));}
        shapes.Append("<polyline points=\""+string.Join(" ",pts)+"\"/>");return;
       }catch{skipped++;return;}
      }
      var children=new DBObjectCollection();
      try{e.Explode(children);if(children.Count==0)skipped++;foreach(DBObject child in children)try{if(child is Entity ent)Draw(ent,depth+1);}finally{child.Dispose();}}
      catch{skipped++;}
     }
     Draw(reference,0);reference.Erase();
     if(!any)throw new InvalidOperationException("Không trích xuất được hình xem trước.");
     double w=Math.Max(1,bounds.MaxPoint.X-bounds.MinPoint.X),h=Math.Max(1,bounds.MaxPoint.Y-bounds.MinPoint.Y),pad=Math.Max(w,h)*.07;
     string svg="<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\""+F(bounds.MinPoint.X-pad)+" "+F(-bounds.MaxPoint.Y-pad)+" "+F(w+2*pad)+" "+F(h+2*pad)+"\"><g fill=\"none\" stroke=\"#183149\" stroke-width=\""+F(Math.Max(w,h)/500)+"\" stroke-linejoin=\"round\">"+shapes+"</g></svg>";
     string imageFile=JsonUtil.Hash(asset.Id)+".png";
     Thumbnail(svg,Path.Combine(root,imageFile),bounds.MinPoint.X,bounds.MaxPoint.Y,w,h);
     results[asset.Id]=JObject.FromObject(new{imageFile,imageWidth=240,imageHeight=180,width=w,height=h,internalLayers=layers.OrderBy(x=>x).ToArray(),entities=count,skipped,complete=skipped==0,values=asset.Usage?.Values});
    }catch(System.Exception e){results[asset.Id]=new JObject{["error"]=e.Message};}
   }finally{HostApplicationServices.WorkingDatabase=previous;}
  }catch(System.Exception e){results["error"]=e.Message;}
  JsonUtil.Atomic(Path.Combine(root,"previews.json"),results);
 }
}
