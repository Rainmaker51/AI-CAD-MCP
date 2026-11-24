using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Library;
public sealed partial class NativeTests
{
    static void VerifyCadFirst(string evidence)
    {
        using var db=new Database(true,true);var previous=HostApplicationServices.WorkingDatabase;HostApplicationServices.WorkingDatabase=db;
        try
        {
            List<LibrarySyncItem> selected;ObjectId hatchId,blockSample;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var layers=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForWrite);var layer=new LayerTableRecord{Name="CAD_FIRST_LAYER",Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,6),LineWeight=LineWeight.LineWeight035};layers.Add(layer);tx.AddNewlyCreatedDBObject(layer,true);
                var texts=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForWrite);var style=new TextStyleTableRecord{Name="CAD_FIRST_TEXT",FileName="tahoma.ttf",TextSize=250,XScale=.8};texts.Add(style);tx.AddNewlyCreatedDBObject(style,true);
                var dims=(DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForWrite);var dim=new DimStyleTableRecord{Name="CAD_FIRST_DIM"};dims.Add(dim);tx.AddNewlyCreatedDBObject(dim,true);dim.Dimtxsty=style.ObjectId;dim.Dimtxt=250;dim.Dimscale=1;
                var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);var block=new BlockTableRecord{Name="CAD_FIRST_BLOCK",Units=UnitsValue.Millimeters};bt.Add(block);tx.AddNewlyCreatedDBObject(block,true);var shape=new Line(Point3d.Origin,new Point3d(1000,1000,0));block.AppendEntity(shape);tx.AddNewlyCreatedDBObject(shape,true);
                var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var reference=new BlockReference(new Point3d(4000,3000,0),block.ObjectId){Rotation=.3};ms.AppendEntity(reference);tx.AddNewlyCreatedDBObject(reference,true);blockSample=reference.ObjectId;
                var hatch=new Hatch{ColorIndex=3};block.AppendEntity(hatch);tx.AddNewlyCreatedDBObject(hatch,true);hatch.SetHatchPattern(HatchPatternType.PreDefined,"ANSI31");hatch.PatternScale=100;var loop=new HatchLoop(HatchLoopTypes.External|HatchLoopTypes.Polyline);foreach(var p in new[]{new Point2d(0,0),new Point2d(1000,0),new Point2d(1000,800),new Point2d(0,800)})loop.Polyline.Add(new BulgeVertex(p,0));hatch.AppendLoop(loop);hatch.EvaluateHatch(true);hatchId=hatch.ObjectId;
                selected=LibrarySyncNative.ReadDrawing(db,tx).Where(i=>i.Name.StartsWith("CAD_FIRST_",StringComparison.Ordinal)||i.Kind=="hatches").ToList();Check(selected.Count==5,"CAD-first fixture missing a resource kind");tx.Commit();
            }
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var current=LibrarySyncNative.ReadDrawing(db,tx,selected);var before=current.Where(i=>selected.Any(s=>s.Id==i.Id)).ToDictionary(i=>i.Id,i=>i.Values.ToString());double area=((Hatch)tx.GetObject(hatchId,OpenMode.ForRead)).Area;
                foreach(var i in selected)i.Group="CAD first / "+i.Kind;
                LibrarySyncNative.PresentFromCad(db,tx,selected,new HashSet<string>(selected.Select(i=>i.Id)),selected);
                var after=LibrarySyncNative.ReadDrawing(db,tx,selected);
                foreach(var i in selected){var actual=after.Single(a=>a.Id==i.Id);Check(actual.Values.ToString()==before[i.Id],"Presentation overwrote native values: "+i.Kind);Check(LibraryFrames.Model(db,tx).OfType<MText>().Any(l=>LibrarySyncNative.SampleMetadata(l,tx)?.Id==i.Id),"Missing generated label: "+i.Kind);}
                Check(Math.Abs(((Hatch)tx.GetObject(hatchId,OpenMode.ForRead)).Area-area)<1e-7,"Nested hatch boundary changed");Check(Math.Abs(((BlockReference)tx.GetObject(blockSample,OpenMode.ForRead)).Rotation-.3)<1e-7,"Block variant rotation changed");tx.Commit();
            }
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var read=LibrarySyncNative.ReadDrawing(db,tx,selected);var layer=read.Single(i=>i.Kind=="layers"&&i.Name=="CAD_FIRST_LAYER");
                var sample=LibraryFrames.Model(db,tx).OfType<Line>().Single(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==layer.Id);tx.GetObject(sample.ObjectId,OpenMode.ForWrite).Erase();
                LibrarySyncNative.PresentFromCad(db,tx,new(){layer},new(),read);Check(!LibraryFrames.Model(db,tx).OfType<Line>().Any(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==layer.Id),"Presentation restored erased sample");tx.Commit();
            }
            var differences=new JArray(selected.Select(i=>new JObject{["Id"]=i.Id,["Status"]="CAD_NEW",["Cad"]=JObject.FromObject(i),["App"]=null}));
            var dialog=new QaCadReview(new JObject{["differences"]=differences},new());
            try{dialog.Rows[0].Selected=false;var plan=dialog.BuildPlan();Check(plan["rows"]!.Count()==4&&plan.Value<int>("deferred")==1,"Review ignored deferred item");}finally{dialog.Close();}
            var oldIdentity=selected.Single(i=>i.Kind=="layers").Copy();var newIdentity=oldIdentity.Copy();newIdentity.Id=Guid.NewGuid().ToString("N");newIdentity.Name="CAD_RENAMED_WITHOUT_TAG";
            var bindingReview=new QaCadReview(new JObject{["differences"]=new JArray(new JObject{["Id"]=oldIdentity.Id,["Status"]="CAD_MISSING",["App"]=JObject.FromObject(oldIdentity)},new JObject{["Id"]=newIdentity.Id,["Status"]="CAD_NEW",["Cad"]=JObject.FromObject(newIdentity)})},new());
            try{bindingReview.Rows.Single(r=>r.Id==newIdentity.Id).BindId=oldIdentity.Id;var plan=bindingReview.BuildPlan();Check(plan["rows"]!.Count()==1&&plan["rows"]![0]!.Value<string>("bindId")==oldIdentity.Id,"Explicit identity binding also removed the old ID");}finally{bindingReview.Close();}
            var definitionReview=new QaCadReview(new JObject{["differences"]=new JArray(new JObject{["Id"]=oldIdentity.Id,["Status"]="CAD_DEFINITION_ONLY",["Cad"]=JObject.FromObject(oldIdentity)})},new());
            try{definitionReview.Rows[0].Selected=true;bool blocked=false;try{definitionReview.BuildPlan();}catch(InvalidOperationException){blocked=true;}Check(blocked,"Definition-only block was accepted as a new sample");}finally{definitionReview.Close();}
            var window=new QaBrowserWindow(test:true);
            try
            {
                System.Collections.Generic.IEnumerable<System.Windows.DependencyObject> Walk(System.Windows.DependencyObject node)
                {yield return node;foreach(var child in System.Windows.LogicalTreeHelper.GetChildren(node).OfType<System.Windows.DependencyObject>())foreach(var descendant in Walk(child))yield return descendant;}
                var buttons=Walk(window).OfType<System.Windows.Controls.Button>().Select(b=>b.Content as string??"").ToList();
                Check(!new[]{"Thêm mẫu / kiểu","Sửa thông số trong DWG","Xóa khỏi CAD và kho","Gộp hatch từ danh sách"}.Any(buttons.Contains),"QA still exposes native resource editing");
                Check(buttons.Contains("Quét thay đổi / đối chiếu")&&buttons.Contains("Xử lý cập nhật"),"CAD-first scan/recovery controls missing");
            }finally{window.Close();}
            JsonUtil.Atomic(Path.Combine(evidence,"cad-first.json"),new{passed=true,fiveKinds=true,nativeValuesPreserved=true,nestedHatchPreview=true,blockVariantPreserved=true,erasedSampleNotRestored=true,reviewSelection=true,explicitIdentityBinding=true,definitionOnlyGuard=true,nativeEditingRemoved=true,scanAndRecoveryControls=true});
        }
        finally{HostApplicationServices.WorkingDatabase=previous;}
    }
}
