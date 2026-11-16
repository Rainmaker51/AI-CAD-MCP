using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
[assembly:CommandClass(typeof(AICad.LibraryScanner.StyleCatalogScanner))]
namespace AICad.LibraryScanner;
public sealed class StyleCatalogScanner {
 [CommandMethod("AICADSTYLECATALOG",CommandFlags.Modal)]
 public void Run(){
  string root=Environment.GetEnvironmentVariable("AICAD_LIBRARY_JOB")??"";if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-library-job")))return;
  try{
   using var db=new Database(false,true);db.ReadDwgFile(Path.Combine(root,"source.dwg"),FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);using var tx=db.TransactionManager.StartTransaction();
   var texts=new JArray();var dims=new JArray();var annotations=new JArray();var hatchPositions=new JArray();
   string TextStyle(ObjectId id)=>((TextStyleTableRecord)tx.GetObject(id,OpenMode.ForRead)).Name;
   foreach(ObjectId id in (TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForRead)){
    var s=(TextStyleTableRecord)tx.GetObject(id,OpenMode.ForRead);texts.Add(JObject.FromObject(new{id=JsonUtil.Hash(s.Name),name=s.Name,font=s.FileName,bigFont=s.BigFontFileName,height=s.TextSize,widthFactor=s.XScale,obliqueAngle=s.ObliquingAngle,vertical=s.IsVertical,backwards=s.FlagBits,handle=s.Handle.ToString()}));
   }
   foreach(ObjectId id in (DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForRead)){
    var s=(DimStyleTableRecord)tx.GetObject(id,OpenMode.ForRead);dims.Add(JObject.FromObject(new{id=JsonUtil.Hash(s.Name),name=s.Name,textStyle=TextStyle(s.Dimtxsty),textHeight=s.Dimtxt,arrowSize=s.Dimasz,scale=s.Dimscale,linearFactor=s.Dimlfac,precision=s.Dimdec,extensionOffset=s.Dimexo,extensionBeyond=s.Dimexe,baselineSpacing=s.Dimdli,textGap=s.Dimgap,rounding=s.Dimrnd,unitFormat=s.Dimlunit,angularPrecision=s.Dimadec,zeroSuppression=s.Dimzin,handle=s.Handle.ToString()}));
   }
   foreach(ObjectId bid in (BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead)){
    var b=(BlockTableRecord)tx.GetObject(bid,OpenMode.ForRead);if(b.IsFromExternalReference||b.IsFromOverlayReference)continue;
    foreach(ObjectId id in b){if(tx.GetObject(id,OpenMode.ForRead) is not Entity e)continue;try{
     string? content=e is DBText t?t.TextString:e is MText m?m.Text:null;
     if(content!=null&&annotations.Count<15000){var ex=e.GeometricExtents;annotations.Add(JObject.FromObject(new{text=content,block=b.Name,handle=e.Handle.ToString(),layer=e.Layer,minX=ex.MinPoint.X,minY=ex.MinPoint.Y,maxX=ex.MaxPoint.X,maxY=ex.MaxPoint.Y,style=e is DBText dt?TextStyle(dt.TextStyleId):e is MText mt?TextStyle(mt.TextStyleId):""}));}
     if(e is Hatch h){var ex=h.GeometricExtents;var settings=new{pattern=h.PatternName,type=h.PatternType.ToString(),scale=h.PatternScale,angle=h.PatternAngle,layer=h.Layer,color=(int)h.ColorIndex,spacing=h.PatternSpace,doublePattern=h.PatternDouble};hatchPositions.Add(JObject.FromObject(new{id=JsonUtil.Hash(settings),pattern=h.PatternName,block=b.Name,handle=h.Handle.ToString(),minX=ex.MinPoint.X,minY=ex.MinPoint.Y,maxX=ex.MaxPoint.X,maxY=ex.MaxPoint.Y}));}
    }catch{} }
   }
   JsonUtil.Atomic(Path.Combine(root,"styles.json"),new{sourceHash=JsonUtil.FileHash(Path.Combine(root,"source.dwg")),texts,dims,annotations,hatchPositions});
  }catch(System.Exception e){JsonUtil.Atomic(Path.Combine(root,"styles.json"),new{error=e.Message});}
 }
}
