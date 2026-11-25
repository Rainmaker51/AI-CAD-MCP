using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Library;
public sealed partial class NativeTests
{
    static void VerifyLiveAndInsertion(string root)
    {
        var working=HostApplicationServices.WorkingDatabase;
        try
        {
            using var source=new Database(true,true){Insunits=UnitsValue.Millimeters};HostApplicationServices.WorkingDatabase=source;
            ObjectId blockId,usedId,hatchId;List<LibrarySyncItem> initialHints;
            using(var tx=source.TransactionManager.StartTransaction())
            {
                var blocks=(BlockTable)tx.GetObject(source.BlockTableId,OpenMode.ForWrite);var block=new BlockTableRecord{Name="QA_INSERT_TEST",Units=UnitsValue.Millimeters};blocks.Add(block);tx.AddNewlyCreatedDBObject(block,true);blockId=block.ObjectId;
                var circle=new Circle(Point3d.Origin,Vector3d.ZAxis,123.45678901234567);block.AppendEntity(circle);tx.AddNewlyCreatedDBObject(circle,true);
                var att=new AttributeDefinition{Tag="LABEL",TextString="SOURCE",Height=20,Position=new Point3d(150,0,0)};block.AppendEntity(att);tx.AddNewlyCreatedDBObject(att,true);
                var unused=new BlockTableRecord{Name="QA_DEFINITION_ONLY",Units=UnitsValue.Millimeters};blocks.Add(unused);tx.AddNewlyCreatedDBObject(unused,true);var line=new Line(Point3d.Origin,new Point3d(100,0,0));unused.AppendEntity(line);tx.AddNewlyCreatedDBObject(line,true);
                var layer=(LayerTable)tx.GetObject(source.LayerTableId,OpenMode.ForWrite);var unusedLayer=new LayerTableRecord{Name="QA_UNUSED_LAYER"};layer.Add(unusedLayer);tx.AddNewlyCreatedDBObject(unusedLayer,true);
                var model=(BlockTableRecord)tx.GetObject(blocks[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var reference=new BlockReference(Point3d.Origin,blockId);model.AppendEntity(reference);tx.AddNewlyCreatedDBObject(reference,true);usedId=reference.ObjectId;
                var hatch=new Hatch{ColorIndex=8};model.AppendEntity(hatch);tx.AddNewlyCreatedDBObject(hatch,true);hatch.SetHatchPattern(HatchPatternType.PreDefined,"ANSI31");hatch.PatternScale=100;
                var loop=new HatchLoop(HatchLoopTypes.External|HatchLoopTypes.Polyline);foreach(var point in new[]{new Point2d(0,0),new Point2d(500,0),new Point2d(500,500),new Point2d(0,500),new Point2d(0,0)})loop.Polyline.Add(new BulgeVertex(point,0));hatch.AppendLoop(loop);hatch.EvaluateHatch(true);hatchId=hatch.ObjectId;
                initialHints=LibrarySyncNative.ReadDrawing(source,tx);
                var selected=initialHints.Where(i=>i.Name=="QA_INSERT_TEST"||i.Kind=="hatches").ToList();
                foreach(var item in selected){Entity sample=item.Kind=="blocks"?reference:hatch;LibrarySyncNative.AssignGroup(source,tx,item,"",sample);LibraryFrames.AddCell(source,tx,item,sample);}
                LibraryFrames.Build(source,tx,selected);LibraryFrames.Separate(source,tx,selected);tx.Commit();
            }
            string file=Path.Combine(root,"live-source.dwg");source.SaveAs(file,DwgVersion.AC1032);string originalHash=JsonUtil.FileHash(file);
            var config=new InstallConfig{DataRoot=Path.Combine(root,"live-data")};
            var saved=QaCadSnapshot.CaptureSaved(config,file,initialHints);
            var hints=saved["items"]!.ToObject<List<LibrarySyncItem>>()!;
            var opened=QaCadSnapshot.Capture(config,file,hints,source);
            JsonUtil.Atomic(Path.Combine(root,"cross-runtime.json"),opened);
            Check(opened.Value<string>("signature")==saved.Value<string>("signature"),"Open clone and saved DWG signatures differ");
            QaCadSnapshot.Previews(config,opened,new[]{new JObject{["id"]=hints.Single(i=>i.Name=="QA_INSERT_TEST").Id,["name"]="QA_INSERT_TEST",["profile"]=new JObject(),["liveStatus"]="PRESENT"}});
            Check(File.Exists(Path.Combine(QaCadSnapshot.Folder(config,opened),JsonUtil.Hash(hints.Single(i=>i.Name=="QA_INSERT_TEST").Id)+".png")),"Live thumbnail not generated from CAD snapshot");
            Check(opened["items"]!.Any(i=>i.Value<string>("Name")=="QA_UNUSED_LAYER"),"Unused layer omitted from live CAD");
            string unusedId=hints.Single(i=>i.Name=="QA_DEFINITION_ONLY").Id;Check(opened["presence"]![unusedId]!.Value<bool>("samplePresent")==false,"Unused definition mistaken for visible sample");
            using(var tx=source.TransactionManager.StartTransaction()){tx.GetObject(usedId,OpenMode.ForWrite).Erase();tx.Commit();}
            var deleted=QaCadSnapshot.Capture(config,file,hints,source);string id=hints.Single(i=>i.Name=="QA_INSERT_TEST").Id;
            Check(deleted["presence"]![id]!.Value<bool>("samplePresent")==false,"Deleted gallery sample resurrected in live read");
            Check(JsonUtil.FileHash(file)==originalHash,"Browsing overwrote saved source");
            using(var tx=source.TransactionManager.StartTransaction())
            {
                ((BlockTableRecord)tx.GetObject(blockId,OpenMode.ForWrite)).Name="QA_RENAMED_INSERT";((Hatch)tx.GetObject(hatchId,OpenMode.ForWrite)).PatternScale=300;tx.Commit();
            }
            var renamed=QaCadSnapshot.Capture(config,file,hints,source);
            Check(renamed["items"]!.Single(i=>i.Value<string>("Name")=="QA_RENAMED_INSERT").Value<string>("Id")==id,"Untagged block rename changed stable identity in live clone");
            Check(renamed["items"]!.Single(i=>i.Value<string>("Kind")=="hatches").Value<string>("Id")==hints.Single(i=>i.Kind=="hatches").Id,"Edited hatch lost identity in live clone");
            string snapshot=Path.Combine(config.DataRoot,"library-sync",opened.Value<string>("id")!,"source.dwg");
            using var target=new Database(true,true){Insunits=UnitsValue.Millimeters};HostApplicationServices.WorkingDatabase=target;
            var request=new QaInsertRequest{Snapshot=snapshot,Name="QA_INSERT_TEST",Scale=2,Degrees=90,Attributes=new(){["LABEL"]="KEPT"}};
            ObjectId inserted=QaBlockInsert.Insert(target,request,new Point3d(1000,2000,0));
            using(var tx=target.TransactionManager.StartOpenCloseTransaction())
            {
                var reference=(BlockReference)tx.GetObject(inserted,OpenMode.ForRead);Check(reference.Position==new Point3d(1000,2000,0)&&Math.Abs(reference.ScaleFactors.X-2)<1e-8&&Math.Abs(reference.Rotation-Math.PI/2)<1e-8,"Insertion transform incorrect");
                Check(reference.AttributeCollection.Cast<ObjectId>().Select(a=>(AttributeReference)tx.GetObject(a,OpenMode.ForRead)).Single().TextString=="KEPT","ATT input lost on insertion");
                Check(!LibraryFrames.Initialized(target,tx),"Insertion initialized the destination as a library");
            }
            QaBlockInsert.Insert(target,request,Point3d.Origin);
            request.Replace=true;QaBlockInsert.Insert(target,request,new Point3d(3000,0,0));
            using(var tx=target.TransactionManager.StartOpenCloseTransaction())
            {
                var table=(BlockTable)tx.GetObject(target.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForRead);
                Check(model.Cast<ObjectId>().Select(i=>tx.GetObject(i,OpenMode.ForRead)).OfType<BlockReference>().Count(b=>b.BlockTableRecord==table[request.Name])==3,"Same-name update lost existing references");
            }
            target.SaveAs(Path.Combine(root,"inserted.dwg"),DwgVersion.AC1032);
            using(var reopened=new Database(false,true)){reopened.ReadDwgFile(Path.Combine(root,"inserted.dwg"),FileOpenMode.OpenForReadAndAllShare,true,"");reopened.CloseInput(true);using var tx=reopened.TransactionManager.StartOpenCloseTransaction();Check(((BlockTable)tx.GetObject(reopened.BlockTableId,OpenMode.ForRead)).Has(request.Name),"Inserted definition lost on reopen");}
            JsonUtil.Atomic(Path.Combine(root,"live-insert.json"),new{passed=true,sourceUntouched=true,openSavedEquivalent=true,unusedDefinitions=true,erasedSamples=true,insertionTransform=true,attributes=true,keepExisting=true,replaceExisting=true,reopen=true});
        }
        finally{HostApplicationServices.WorkingDatabase=working;}
    }
    static void VerifyHatchView(string root)
    {
        var window=new QaBrowserWindow(test:true);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            typeof(QaBrowserWindow).GetField("kind",flags)!.SetValue(window,"hatches");
            var rows=new JArray();foreach(var pair in new[]{("ANSI31",100),("ANSI31",300),("SOLID",1)})rows.Add(new JObject{["id"]=Guid.NewGuid().ToString("N"),["name"]=pair.Item1,["displayName"]=pair.Item1,["code"]="HAT-TEST",["management"]=JObject.FromObject(new LibraryLabel()),["profile"]=new JObject{["pattern"]=pair.Item1,["scale"]=pair.Item2,["angle"]=0,["color"]=8},["image"]=""});
            window.Bind(new JObject{["source"]="C:/fixture.dwg",["groups"]=new JArray(),["items"]=rows});
            var list=(ListBox)typeof(QaBrowserWindow).GetField("list",flags)!.GetValue(window)!;
            var view=(ListCollectionView)list.ItemsSource;Check(view.Groups!.Count==2&&view.Cast<object>().Count()==3,"Hatch variants were merged or pattern grouping failed");
            var rootView=(FrameworkElement)window.Content;rootView.Measure(new Size(1400,820));rootView.Arrange(new Rect(0,0,1400,820));rootView.UpdateLayout();
            Check(!Descendants(rootView).OfType<Expander>().Where(e=>e.Header is CollectionViewGroup).Any(e=>e.IsExpanded),"Hatch groups did not start collapsed");
            var combo=(ComboBox)typeof(QaBrowserWindow).GetField("hatchPattern",flags)!.GetValue(window)!;combo.SelectedItem="ANSI31";
            Check(((ListCollectionView)list.ItemsSource).Count==2,"Pattern filter lost variants");
            JsonUtil.Atomic(Path.Combine(root,"hatch-view.json"),new{passed=true,groups=2,variants=3,collapsed=true,patternFilter=true});
        }
        finally{window.Close();}
    }
    static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
    {
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++){var child=System.Windows.Media.VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
}
