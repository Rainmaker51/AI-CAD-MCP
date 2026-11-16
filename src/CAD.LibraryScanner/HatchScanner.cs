using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
[assembly:CommandClass(typeof(AICad.LibraryScanner.HatchScanner))]
namespace AICad.LibraryScanner;
public sealed class HatchScanner {
 [CommandMethod("AICADHATCHSCAN",CommandFlags.Modal)]
 public void Run(){
  string root=Environment.GetEnvironmentVariable("AICAD_LIBRARY_JOB")??"";
  if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-library-job")))return;
  try{
   using var db=new Database(false,true);db.ReadDwgFile(Path.Combine(root,"source.dwg"),FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);
   using var tx=db.TransactionManager.StartTransaction();var table=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);
   var entries=new Dictionary<string,JObject>();var warnings=new JArray();int total=0;
   foreach(ObjectId blockId in table){var block=(BlockTableRecord)tx.GetObject(blockId,OpenMode.ForRead);if(block.IsFromExternalReference||block.IsFromOverlayReference)continue;
    foreach(ObjectId objectId in block){if(tx.GetObject(objectId,OpenMode.ForRead) is not Hatch hatch)continue;total++;
     try{
      var settings=new{pattern=hatch.PatternName,type=hatch.PatternType.ToString(),scale=hatch.PatternScale,angle=hatch.PatternAngle,layer=hatch.Layer,color=(int)hatch.ColorIndex,spacing=hatch.PatternSpace,doublePattern=hatch.PatternDouble};
      string id=JsonUtil.Hash(settings);
      if(entries.TryGetValue(id,out var existing)){existing["count"]=existing.Value<int>("count")+1;var locations=(JArray)existing["locations"]!;if(!locations.Values<string>().Contains(block.Name))locations.Add(block.Name);continue;}
      if(entries.Count>=500)throw new InvalidOperationException("Vượt 500 cấu hình hatch.");
      var item=JObject.FromObject(settings);item["id"]=id;item["count"]=1;item["locations"]=new JArray(block.Name);item["handle"]=hatch.Handle.ToString();
      item["engineSupported"]=(settings.pattern=="ANSI31"||settings.pattern=="SOLID")&&settings.scale>=1&&settings.scale<=1000&&Math.Abs(settings.angle)<1e-9;
      try{
       var bounds=hatch.GeometricExtents;double w=Math.Max(1,bounds.MaxPoint.X-bounds.MinPoint.X),h=Math.Max(1,bounds.MaxPoint.Y-bounds.MinPoint.Y);
       item["width"]=w;item["height"]=h;
       var shapes=new StringBuilder();var exploded=new DBObjectCollection();int lines=0;
       try{if(!hatch.IsSolidFill)hatch.Explode(exploded);foreach(DBObject obj in exploded){if(obj is Line line&&lines++<8000){string F(double v)=>v.ToString("0.###",CultureInfo.InvariantCulture);shapes.Append("<polyline points=\""+F(line.StartPoint.X)+","+F(-line.StartPoint.Y)+" "+F(line.EndPoint.X)+","+F(-line.EndPoint.Y)+"\"/>");}}}
       finally{foreach(DBObject obj in exploded)obj.Dispose();}
       string image=id+".png";
       if(hatch.IsSolidFill){using var bitmap=new System.Drawing.Bitmap(240,180);using var g=System.Drawing.Graphics.FromImage(bitmap);g.Clear(System.Drawing.Color.FromArgb(247,249,252));g.FillRectangle(System.Drawing.Brushes.SlateGray,12,12,216,156);bitmap.Save(Path.Combine(root,image),System.Drawing.Imaging.ImageFormat.Png);}
       else if(shapes.Length>0)BlockPreviews.Thumbnail("<svg>"+shapes+"</svg>",Path.Combine(root,image),bounds.MinPoint.X,bounds.MaxPoint.Y,w,h);
       else throw new InvalidOperationException("Không đọc được nét hatch để tạo ảnh.");
       item["imageFile"]=image;item["previewTruncated"]=lines>8000;
      }catch(System.Exception e){item["previewError"]=e.Message;}
      entries.Add(id,item);
     }catch(System.Exception e){warnings.Add(new JObject{["handle"]=hatch.Handle.ToString(),["error"]=e.Message});}
    }
   }
   JsonUtil.Atomic(Path.Combine(root,"hatches.json"),new{schemaVersion=1,sourceHash=JsonUtil.FileHash(Path.Combine(root,"source.dwg")),entityCount=total,items=entries.Values.ToArray(),warnings});
  }catch(System.Exception e){JsonUtil.Atomic(Path.Combine(root,"hatches.json"),new{error=e.Message});}
 }
}
