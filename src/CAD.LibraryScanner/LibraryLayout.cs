using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;
[assembly:CommandClass(typeof(AICad.LibraryScanner.LibraryLayout))]
namespace AICad.LibraryScanner;
public sealed class LibraryLayout {
 [CommandMethod("AICADLIBRARYLAYOUT",CommandFlags.Modal)]
 public void Run(){
  string job=Environment.GetEnvironmentVariable("AICAD_LIBRARY_JOB")??"";if(!Path.IsPathRooted(job)||!File.Exists(Path.Combine(job,".aicad-library-job")))return;
  try{
   var plan=JsonUtil.Read<JObject>(File.ReadAllText(Path.Combine(job,"layout.json")));string output=Path.Combine(job,"ISO_QuangAnh_ThuVien.dwg");if(File.Exists(output))throw new InvalidOperationException("Output already exists.");
   using var db=new Database(false,true);db.ReadDwgFile(Path.Combine(job,"source.dwg"),FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);var previous=HostApplicationServices.WorkingDatabase;HostApplicationServices.WorkingDatabase=db;
   var report=new JObject();var failures=new JArray();int managedCount=0,otherCount=0,hatchCount=0,layerCount=0,textCount=0,dimCount=0;
   try{
    // Bring in missing native definitions from approved asset DWGs only.
    foreach(var donor in ((JArray)plan["donors"]!).OfType<JObject>()){
     using var tx0=db.TransactionManager.StartTransaction();var bt0=(BlockTable)tx0.GetObject(db.BlockTableId,OpenMode.ForRead);string name=donor.Value<string>("name")!;if(bt0.Has(name))continue;
     using var source=new Database(false,true);source.ReadDwgFile(donor.Value<string>("path")!,FileOpenMode.OpenForReadAndAllShare,true,"");source.CloseInput(true);using var st=source.TransactionManager.StartTransaction();var sb=(BlockTable)st.GetObject(source.BlockTableId,OpenMode.ForRead);if(!sb.Has(name))throw new InvalidOperationException("Missing donor block: "+name);var ids=new ObjectIdCollection{sb[name]};var map=new IdMapping();source.WblockCloneObjects(ids,db.BlockTableId,map,DuplicateRecordCloning.Ignore,false);tx0.Commit();
    }
    using(var tx=db.TransactionManager.StartTransaction()){
     var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var original=ms.Cast<ObjectId>().ToArray();var sourceHandles=original.Select(id=>id.Handle.ToString()).ToArray();
     var lt=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForWrite);ObjectId Layer(string name,short color){if(lt.Has(name))return lt[name];using var l=new LayerTableRecord{Name=name,Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,color)};var id=lt.Add(l);tx.AddNewlyCreatedDBObject(l,true);return id;}
     var guides=Layer("AICAD_LIB_LAYOUT",8);var labels=Layer("AICAD_LIB_LABEL",7);
     var styles=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForWrite);string labelStyle="AICAD_LIBRARY_LABEL";ObjectId styleId;
     if(styles.Has(labelStyle))styleId=styles[labelStyle];else{using var st=new TextStyleTableRecord{Name=labelStyle,FileName="arial.ttf"};styleId=styles.Add(st);tx.AddNewlyCreatedDBObject(st,true);}
     void Add(Entity e){ms.AppendEntity(e);tx.AddNewlyCreatedDBObject(e,true);}
     void Text(string value,double x,double y,double height=200,ObjectId? nativeStyle=null){var t=new DBText{TextString=value.Length>240?value.Substring(0,240):value,Position=new Point3d(x,y,0),Height=height,TextStyleId=nativeStyle??styleId,LayerId=labels};Add(t);}
     void Box(double x,double y,double w,double h){var p=new Polyline();p.AddVertexAt(0,new Point2d(x,y),0,0,0);p.AddVertexAt(1,new Point2d(x+w,y),0,0,0);p.AddVertexAt(2,new Point2d(x+w,y-h),0,0,0);p.AddVertexAt(3,new Point2d(x,y-h),0,0,0);p.Closed=true;p.LayerId=guides;Add(p);}
     void Title(string title,double x,double y){Text(title,x,y,420);}
     var views=(ViewTable)tx.GetObject(db.ViewTableId,OpenMode.ForWrite);
     void View(string name,double x,double y,double w,double h){if(views.Has(name))return;var v=new ViewTableRecord{Name=name,CenterPoint=new Point2d(x+w/2,y-h/2),Width=w,Height=h,Target=Point3d.Origin,ViewDirection=Vector3d.ZAxis};views.Add(v);tx.AddNewlyCreatedDBObject(v,true);}
     // Keep all original model objects intact, relocated together to an archive zone.
     bool hasBounds=false;var sourceBounds=new Extents3d();foreach(var id in original)try{var e=(Entity)tx.GetObject(id,OpenMode.ForRead);var ex=e.GeometricExtents;if(!hasBounds){sourceBounds=ex;hasBounds=true;}else sourceBounds.AddExtents(ex);}catch{}
     if(hasBounds){var shift=Matrix3d.Displacement(new Vector3d(450000-sourceBounds.MinPoint.X,-sourceBounds.MaxPoint.Y,0));foreach(var id in original){var e=tx.GetObject(id,OpenMode.ForWrite) as Entity;if(e!=null)e.TransformBy(shift);}Title("06 | NỘI DUNG DWG GỐC — GIỮ ĐỂ ĐỐI CHIẾU",450000,1500);}
     var allNamed=bt.Cast<ObjectId>().Select(id=>(BlockTableRecord)tx.GetObject(id,OpenMode.ForRead)).Where(b=>!b.IsLayout&&!b.IsAnonymous&&!b.IsFromExternalReference).ToArray();
     var active=((JArray)plan["blocks"]!).OfType<JObject>().ToArray();var activeNames=new HashSet<string>(active.Select(b=>b["NativeSource"]!.Value<string>("BlockName")!));
     Title("ISO QUANG ANH — THƯ VIỆN ĐƯỢC APP QUẢN LÝ",0,3000);Text("Bản bố trí để rà soát. Block mẫu tỷ lệ 1:1; định nghĩa gốc và visibility được giữ nguyên.",0,2100,230);Text("Khu đang dùng: X=0 | Ngoài danh mục: X=180000 | DWG gốc lưu đối chiếu: X=450000",0,1500,230);
     double Gallery(IEnumerable<JObject> assets,double x,double y,string title,bool managed){
      var refs=new List<(JObject data,BlockReference reference,Extents3d bounds)>();
      Title(title,x,y);y-=1000;
      foreach(var asset in assets)try{
       string name=asset["NativeSource"]!.Value<string>("BlockName")!;if(!bt.Has(name))throw new InvalidOperationException("Missing block "+name);
       var r=new BlockReference(Point3d.Origin,bt[name]);Add(r);
       if(r.IsDynamicBlock&&asset["Usage"]?["Values"] is JObject values)foreach(DynamicBlockReferenceProperty p in r.DynamicBlockReferencePropertyCollection){if(p.ReadOnly||!values.TryGetValue(p.PropertyName,out var v))continue;p.Value=v.ToObject(p.Value.GetType())!;}
       var ex=r.GeometricExtents;refs.Add((asset,r,ex));
      }catch(System.Exception e){failures.Add(new JObject{["id"]=asset["Id"]?.DeepClone(),["error"]=e.Message});}
      if(refs.Count==0){Text("Không có tài nguyên trong nhóm.",x,y,180);return y-1000;}
      double cw=Math.Max(6000,refs.Max(r=>r.bounds.MaxPoint.X-r.bounds.MinPoint.X)+2000),ch=Math.Max(5000,refs.Max(r=>r.bounds.MaxPoint.Y-r.bounds.MinPoint.Y)+2600);int columns=Math.Max(1,Math.Min(4,(int)(130000/cw)));
      for(int i=0;i<refs.Count;i++){var entry=refs[i];double cx=x+(i%columns)*cw,cy=y-(i/columns)*ch;Box(cx,cy,cw-400,ch-400);Text(entry.data.Value<string>("Id")!,cx+300,cy-350,190);Text(entry.data["NativeSource"]!.Value<string>("BlockName")!,cx+300,cy-700,150);entry.reference.TransformBy(Matrix3d.Displacement(new Vector3d(cx+900-entry.bounds.MinPoint.X,cy-1500-entry.bounds.MaxPoint.Y,0)));if(managed)managedCount++;else otherCount++;}
      return y-Math.Ceiling((double)refs.Count/columns)*ch-2000;
     }
     double managedY=0;int groupIndex=0;foreach(var g in active.GroupBy(a=>a.Value<string>("group")??"Chưa phân nhóm")){double top=managedY;managedY=Gallery(g,0,managedY,"01 | BLOCK ĐANG DÙNG / "+g.Key,true);View("01_BLOCK_GROUP_"+(++groupIndex),-1000,top+1000,110000,top-managedY+2000);}View("01_APP_BLOCKS",-2000,5000,135000,-managedY+6000);
     var retiredNames=new HashSet<string>(((JArray)plan["retiredBlockNames"]!).Values<string>()!);
     var others=allNamed.Where(b=>!activeNames.Contains(b.Name)).Select(b=>new JObject{["Id"]=b.Name,["NativeSource"]=new JObject{["BlockName"]=b.Name},["status"]=retiredNames.Contains(b.Name)?"THÙNG RÁC / ĐÃ LOẠI KHỎI APP":"CHƯA QUẢN LÝ / BLOCK PHỤ THUỘC"}).ToArray();
     double otherY=0;foreach(var g in others.GroupBy(b=>b.Value<string>("status")))otherY=Gallery(g,180000,otherY,"05 | "+g.Key,false);
     View("05_OTHER_BLOCKS",178000,2000,135000,-otherY+4000);if(hasBounds)View("06_SOURCE_ARCHIVE",448000,2000,sourceBounds.MaxPoint.X-sourceBounds.MinPoint.X+4000,sourceBounds.MaxPoint.Y-sourceBounds.MinPoint.Y+4000);
     double y=managedY-1000,hatchTop=y;Title("02 | HATCH ĐANG DÙNG — THEO CÔNG DỤNG",0,y);y-=1000;int hi=0;
     // Source handles remain valid in this copied database, even after relocation.
     foreach(var h in ((JArray)plan["hatches"]!).OfType<JObject>().OrderBy(h=>h["management"]!.Value<string>("Purpose")))try{
      double x=(hi%4)*12000,top=y-(hi/4)*8500;Box(x,top,11600,8100);string purpose=h["management"]!.Value<string>("Purpose")??"";Text(purpose,x+300,top-450,180);Text(h.Value<string>("pattern")+" | Scale "+h.Value<double>("scale")+" | Góc "+Math.Round(h.Value<double>("angle")*180/Math.PI,2)+"°",x+300,top-850,170);
      var id=db.GetObjectId(false,new Handle(Convert.ToInt64(h.Value<string>("handle"),16)),0);var src=(Hatch)tx.GetObject(id,OpenMode.ForRead);var clone=(Hatch)src.Clone();Add(clone);clone.Associative=false;var ex=clone.GeometricExtents;clone.TransformBy(Matrix3d.Displacement(new Vector3d(x+800-ex.MinPoint.X,top-1800-ex.MaxPoint.Y,0)));hi++;hatchCount++;
     }catch(System.Exception e){failures.Add(new JObject{["hatch"]=h["id"]?.DeepClone(),["error"]=e.Message});}
     y-=Math.Ceiling(hi/4.0)*8500+2000;View("02_APP_HATCH",-1000,hatchTop+1000,51000,hatchTop-y+1000);double layerTop=y;Title("03 | LAYER ĐANG QUẢN LÝ",0,y);y-=900;
     foreach(var layer in ((JArray)plan["layers"]!).OfType<JObject>()){
      string name=layer.Value<string>("Name")!;if(!lt.Has(name)){
       var spec=layer.ToObject<LayerAsset>()!;Layer(name,spec.Color);var created=(LayerTableRecord)tx.GetObject(lt[name],OpenMode.ForWrite);if(spec.Appearance!=null){created.LineWeight=(LineWeight)spec.Appearance.LineWeight;created.IsPlottable=spec.Appearance.Plottable;var lineTypes=(LinetypeTable)tx.GetObject(db.LinetypeTableId,OpenMode.ForRead);if(!lineTypes.Has(spec.Appearance.LineType))throw new InvalidOperationException("Missing linetype "+spec.Appearance.LineType);created.LinetypeObjectId=lineTypes[spec.Appearance.LineType];if(spec.Appearance.Rgb is int rgb)created.Color=Autodesk.AutoCAD.Colors.Color.FromRgb((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);}
      }var line=new Line(new Point3d(300,y-150,0),new Point3d(4500,y-150,0)){LayerId=lt[name]};Add(line);Text(name,5200,y-200,190);var l=(LayerTableRecord)tx.GetObject(lt[name],OpenMode.ForRead);Text("ACI "+l.Color.ColorIndex+" | "+l.LineWeight+" | Print "+l.IsPlottable,19000,y-200,170);y-=650;layerCount++;
     }
     View("03_APP_LAYERS",-1000,layerTop+1000,50000,layerTop-y+2000);y-=2000;double textTop=y;Title("04A | TEXT STYLE ĐANG QUẢN LÝ",0,y);y-=1000;
     foreach(var item in ((JArray)plan["texts"]!).OfType<JObject>()){
      string name=item.Value<string>("name")!;if(!styles.Has(name))continue;Text(name,300,y,180);Text("Aa Bb 0123456789 - Text style",16000,y,220,styles[name]);Text(item.Value<string>("font")??"",34000,y,170);y-=1100;textCount++;
     }
     View("04_APP_TEXT",-1000,textTop+1000,60000,textTop-y+2000);y-=2000;double dimTop=y;Title("04B | DIM STYLE ĐANG QUẢN LÝ",0,y);y-=1500;var dimTable=(DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForRead);
     foreach(var item in ((JArray)plan["dims"]!).OfType<JObject>()){
      string name=item.Value<string>("name")!;if(!dimTable.Has(name))continue;Text(name,300,y,180);var dim=new RotatedDimension(0,new Point3d(15000,y-1600,0),new Point3d(19000,y-1600,0),new Point3d(17000,y-400,0),"",dimTable[name]){LayerId=labels};Add(dim);dim.RecomputeDimensionBlock(true);y-=6500;dimCount++;
     }
     db.Insunits=UnitsValue.Millimeters;
     View("04_APP_DIM",-1000,dimTop+1000,60000,dimTop-y+2000);var vp=(ViewportTable)tx.GetObject(db.ViewportTableId,OpenMode.ForRead);foreach(ObjectId id in vp){var v=(ViewportTableRecord)tx.GetObject(id,OpenMode.ForWrite);if(v.Name=="*Active"){v.CenterPoint=new Point2d(45000,-15000);v.Height=45000;v.Width=75000;v.Target=Point3d.Origin;v.ViewDirection=Vector3d.ZAxis;}}
     report["originalModelEntities"]=original.Length;report["originalHandles"]=JArray.FromObject(sourceHandles);report["managedBlocks"]=managedCount;report["otherBlocks"]=otherCount;report["hatches"]=hatchCount;report["layers"]=layerCount;report["texts"]=textCount;report["dims"]=dimCount;report["failures"]=failures;
     if(failures.Count>0)throw new InvalidOperationException("Layout contains failures: "+failures.ToString());
     tx.Commit();
    }
    db.SaveAs(output,DwgVersion.AC1032);
   }finally{HostApplicationServices.WorkingDatabase=previous;}
   // Reopen the actual saved drawing and prove the original entities survived.
   using(var verify=new Database(false,true)){verify.ReadDwgFile(output,FileOpenMode.OpenForReadAndAllShare,true,"");verify.CloseInput(true);using var tx=verify.TransactionManager.StartTransaction();foreach(string handle in report["originalHandles"]!.Values<string>()){var id=verify.GetObjectId(false,new Handle(Convert.ToInt64(handle,16)),0);if(id.IsNull||id.IsErased)throw new InvalidOperationException("Lost original entity "+handle);}var bt=(BlockTable)tx.GetObject(verify.BlockTableId,OpenMode.ForRead);report["savedBlockDefinitions"]=bt.Cast<ObjectId>().Count();report["verified"]=true;}
   report["output"]=output;JsonUtil.Atomic(Path.Combine(job,"layout-result.json"),report);
  }catch(System.Exception e){JsonUtil.Atomic(Path.Combine(job,"layout-error.json"),new{message=e.Message,stack=e.StackTrace});}
 }
}
