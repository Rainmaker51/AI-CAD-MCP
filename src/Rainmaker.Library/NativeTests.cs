using System;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly:CommandClass(typeof(Rainmaker.Library.NativeTests))]
namespace Rainmaker.Library;

public sealed partial class NativeTests
{
    [CommandMethod("QALIBUIVERIFY")]
    public void VerifyUi()
    {
        string root=Environment.GetEnvironmentVariable("QALIB_TEST_ROOT")??"";if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".library-manager-test")))return;
        LibraryPlugin.Current!.Show();EventHandler? handler=null;handler=(_,_)=>
        {
            var ribbon=Autodesk.Windows.ComponentManager.Ribbon;if(ribbon==null)return;
            Autodesk.AutoCAD.ApplicationServices.Core.Application.Idle-=handler;
            try
            {
                var buttons=LibraryPlugin.Current.ManagedCommands.Select(p=>p.Label).ToArray();Check(!ribbon.Tabs.Any(t=>t.Id=="RAINMAKER_LIBRARY"),"Library created an unmanaged ribbon");
                Check(new[]{"Nhận vào nhóm","Chuyển nhóm","Xếp gọn nhóm","Chỉnh khung","Tạo nhóm","Đổi tên nhóm","Xóa nhóm rỗng"}.All(buttons.Contains),"Required ribbon actions missing");
                var view=LibraryPlugin.Current.RootView!;view.UpdateLayout();int width=Math.Max(350,(int)view.ActualWidth),height=Math.Max(600,(int)view.ActualHeight);var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(root,"palette.png")))encoder.Save(output);
                AICad.Contracts.Json.Atomic(Path.Combine(root,"ui-result.json"),new{passed=true,standaloneRibbon=false,buttons=buttons.Length,requiredActions=true});
            }
            catch(System.Exception error){AICad.Contracts.Json.Atomic(Path.Combine(root,"ui-result.json"),new{passed=false,error=error.ToString()});}
        };Autodesk.AutoCAD.ApplicationServices.Core.Application.Idle+=handler;
    }
    static void Check(bool condition,string message){if(!condition)throw new System.Exception(message);}
    [CommandMethod("QALIBMANAGERTEST")]
    public void Run()
    {
        string root=Environment.GetEnvironmentVariable("QALIB_TEST_ROOT")??"";
        if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".library-manager-test")))throw new InvalidOperationException("Isolated test marker required.");
        var previous=HostApplicationServices.WorkingDatabase;
        try
        {
            EditProfiles();
            VerifyLayoutContext(root);
            RenderBrowser(root);
            VerifyLiveAndInsertion(root);
            VerifyHatchView(root);
            VerifyStandalone(root);
            VerifyLayerRename(root);
            VerifyCadFirst(root);
            VerifyMutations(root);
            VerifyCadPublication(root);
            using var db=new Database(true,true){Insunits=UnitsValue.Millimeters};HostApplicationServices.WorkingDatabase=db;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var layers=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForWrite);var layer=new LayerTableRecord{Name="QA_LIB_TEST",Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,3),LineWeight=LineWeight.LineWeight025};layers.Add(layer);tx.AddNewlyCreatedDBObject(layer,true);
                var texts=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForWrite);var textStyle=new TextStyleTableRecord{Name="QA_LIB_TEXT",FileName="tahoma.ttf",XScale=1};texts.Add(textStyle);tx.AddNewlyCreatedDBObject(textStyle,true);
                var dims=(DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForWrite);var dimStyle=new DimStyleTableRecord{Name="QA_LIB_DIM",Dimtxsty=textStyle.ObjectId,Dimtxt=200,Dimasz=100,Dimscale=1};dims.Add(dimStyle);tx.AddNewlyCreatedDBObject(dimStyle,true);
                var blocks=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);var definition=new BlockTableRecord{Name="QA_LIB_BLOCK",Units=UnitsValue.Millimeters};blocks.Add(definition);tx.AddNewlyCreatedDBObject(definition,true);var circle=new Circle(Point3d.Origin,Vector3d.ZAxis,1000);definition.AppendEntity(circle);tx.AddNewlyCreatedDBObject(circle,true);var attribute=new AttributeDefinition{Tag="KEEP_TEXT",TextString="GỐC",Height=150,Position=new Point3d(1200,0,0)};definition.AppendEntity(attribute);tx.AddNewlyCreatedDBObject(attribute,true);
                var model=(BlockTableRecord)tx.GetObject(blocks[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                T Append<T>(T entity)where T:Entity{entity.LayerId=layer.ObjectId;model.AppendEntity(entity);tx.AddNewlyCreatedDBObject(entity,true);return entity;}
                var block=Append(new BlockReference(Point3d.Origin,definition.ObjectId));var value=new AttributeReference();value.SetAttributeFromBlock(attribute,block.BlockTransform);value.TextString="GIỮ CHỮ";block.AttributeCollection.AppendAttribute(value);tx.AddNewlyCreatedDBObject(value,true);
                var line=Append(new Line(new Point3d(0,-5000,0),new Point3d(5000,-5000,0)));var text=Append(new DBText{TextString="Mẫu chữ",Height=200,TextStyleId=textStyle.ObjectId,Position=new Point3d(0,-10000,0)});
                var dim=Append(new RotatedDimension(0,new Point3d(0,-15000,0),new Point3d(3000,-15000,0),new Point3d(0,-14000,0),"",dimStyle.ObjectId));dim.RecomputeDimensionBlock(true);
                var hatch=Append(new Hatch{ColorIndex=4});hatch.SetHatchPattern(HatchPatternType.PreDefined,"ANSI31");hatch.PatternScale=100;hatch.PatternAngle=.3;var loop=new HatchLoop(HatchLoopTypes.External|HatchLoopTypes.Polyline);foreach(var point in new[]{new Point2d(0,-20000),new Point2d(3000,-20000),new Point2d(3000,-22000),new Point2d(0,-22000),new Point2d(0,-20000)})loop.Polyline.Add(new BulgeVertex(point,0));hatch.AppendLoop(loop);hatch.EvaluateHatch(true);
                var all=LibrarySyncNative.ReadDrawing(db,tx);var items=new[]{all.Single(i=>i.Kind=="blocks"&&i.Name==definition.Name),all.Single(i=>i.Kind=="layers"&&i.Name==layer.Name),all.Single(i=>i.Kind=="hatches"&&i.Handle==hatch.Handle.ToString()),all.Single(i=>i.Kind=="texts"&&i.Name==textStyle.Name),all.Single(i=>i.Kind=="dims"&&i.Name==dimStyle.Name)}.ToList();
                var samples=new Entity[]{block,line,hatch,text,dim};var nodes=LibraryFrames.Nodes(db,tx);for(int i=0;i<items.Count;i++){LibraryFrames.Ensure(nodes,items[i].Kind,"Nhóm / Nhóm con");LibrarySyncNative.AssignGroup(db,tx,items[i],"Nhóm / Nhóm con",samples[i]);LibraryFrames.AddCell(db,tx,items[i],samples[i]);}LibraryFrames.Ensure(nodes,"blocks","Nhóm trống");LibraryFrames.Save(db,tx,nodes);LibraryFrames.Build(db,tx,items);LibraryFrames.Separate(db,tx,items);
                Check(LibraryFrames.Model(db,tx).OfType<BlockReference>().Count(b=>LibraryFrames.Info(b,tx)!=null)==16,"Missing root, parent, child or empty ATT frames");
                Check(LibrarySyncNative.ReadDrawing(db,tx).All(i=>!i.Name.StartsWith("AICAD_LIB_")),"Frame definition leaked into resource catalog");
                var target=nodes.Single(n=>n.Kind=="blocks"&&n.Path=="Nhóm / Nhóm con");ObjectId originalId=block.ObjectId;string beforeHash=LibrarySyncNative.ReadDrawing(db,tx).Single(i=>i.Id==items[0].Id).ContentSignature();
                LibraryFrames.PackGroup(db,tx,target,items);LibraryFrames.Separate(db,tx,items);Check(!originalId.IsErased&&value.TextString=="GIỮ CHỮ","Packing replaced sample or reset attributes");
                var frame=LibraryFrames.Model(db,tx).OfType<BlockReference>().Single(b=>LibraryFrames.Info(b,tx)?.Id==target.Id);var position=block.Position;LibraryFrames.Resize(frame,tx,40000,12000);Check(block.Position==position,"Resizing border moved sample");
                var grips=new FrameGrips();var points=new Point3dCollection();grips.GetGripPoints(frame,points,new IntegerCollection(),new IntegerCollection());Check(points.Count==3,"Missing frame resize grips");grips.MoveGripPointsAt(frame,new IntegerCollection(new[]{1}),new Vector3d(1000,-1000,0));Check(Math.Abs(frame.ScaleFactors.X-41000)<.01&&block.Position==position,"Grip changed sample or incorrect frame size");
                var attributePosition=value.Position;LibraryFrames.MoveGroup(db,tx,target,new Vector3d(1000,-2000,0),items);Check(block.Position==position+new Vector3d(1000,-2000,0)&&value.Position==attributePosition+new Vector3d(1000,-2000,0)&&value.TextString=="GIỮ CHỮ","Group move lost sample/attribute state");
                Check(LibrarySyncNative.ReadDrawing(db,tx).Single(i=>i.Id==items[0].Id).ContentSignature()==beforeHash,"Layout changed definition data");
                nodes=LibraryFrames.Nodes(db,tx);nodes.Single(n=>n.Id==target.Id).Manual=true;LibraryFrames.Save(db,tx,nodes);
                Check(LibraryFrames.Nodes(db,tx).All(n=>!n.Manual),"Legacy layout flag was not retired");
                var sampleBounds=samples.Select(e=>e.GeometricExtents).ToArray();
                var keptAttribute=value.Position;
                LibrarySyncNative.ArrangeDrawing(db,tx,items);
                Check(samples.Select((e,i)=>e.GeometricExtents.MinPoint==sampleBounds[i].MinPoint&&e.GeometricExtents.MaxPoint==sampleBounds[i].MaxPoint).All(v=>v)&&value.Position==keptAttribute,"Sync moved an existing sample or its attributes");
                var newDefinition=new BlockTableRecord{Name="QA_LIB_NEW",Units=UnitsValue.Millimeters};
                blocks.Add(newDefinition);tx.AddNewlyCreatedDBObject(newDefinition,true);
                var newShape=new Line(Point3d.Origin,new Point3d(1500,800,0));
                newDefinition.AppendEntity(newShape);tx.AddNewlyCreatedDBObject(newShape,true);
                var newItem=LibrarySyncNative.ReadDrawing(db,tx).Single(i=>i.Kind=="blocks"&&i.Name=="QA_LIB_NEW");
                LibrarySyncNative.AssignGroup(db,tx,newItem,target.Path);items.Add(newItem);
                LibrarySyncNative.ArrangeDrawing(db,tx,items);
                Check(samples.Select((e,i)=>e.GeometricExtents.MinPoint==sampleBounds[i].MinPoint&&e.GeometricExtents.MaxPoint==sampleBounds[i].MaxPoint).All(v=>v),"Adding a sample moved existing resources");
                var newSample=LibraryFrames.Model(db,tx).OfType<BlockReference>().Single(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==newItem.Id);
                var newPosition=newSample.Position;
                int glyphCount=LibraryFrames.Model(db,tx).Count(e=>LibrarySyncNative.SampleMetadata(e,tx)!=null);
                LibrarySyncNative.ArrangeDrawing(db,tx,items);
                Check(newSample.Position==newPosition&&glyphCount==LibraryFrames.Model(db,tx).Count(e=>LibrarySyncNative.SampleMetadata(e,tx)!=null),"Retry duplicated or moved samples");
                var otherBounds=samples.Skip(1).Select(e=>e.GeometricExtents).ToArray();
                LibraryFrames.PackGroup(db,tx,LibraryFrames.Nodes(db,tx).Single(n=>n.Id==target.Id),items);
                Check(samples.Skip(1).Select((e,i)=>e.GeometricExtents.MinPoint==otherBounds[i].MinPoint).All(v=>v),"Packing one group moved another group");
                Check(items.All(i=>LibrarySyncNative.ReadDrawing(db,tx).Single(n=>n.Id==i.Id).GroupEditedInCad),"CAD group edits not marked for app import");
                // Layout-only must never turn an erased sample's surviving definition into a new insertion.
                var retained=samples.Skip(2).Select(e=>new{Entity=e,Min=e.GeometricExtents.MinPoint,Max=e.GeometricExtents.MaxPoint}).ToArray();var keptNewPosition=newSample.Position;
                block.UpgradeOpen();block.Erase();line.UpgradeOpen();line.Erase();
                int layerAnnotations=LibraryFrames.Model(db,tx).Count(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==items[1].Id);
                var layout=LibrarySyncNative.ArrangeExistingDrawing(db,tx,items,"blocks");
                Check(layout.MissingSampleIds.SequenceEqual(new[]{items[0].Id})&&layout.ExistingSamples==1,"Layout did not report the erased block sample");
                Check(!definition.IsErased&&!LibraryFrames.Model(db,tx).OfType<BlockReference>().Any(b=>b.BlockTableRecord==definition.ObjectId),"Layout recreated an erased block from its surviving definition");
                Check(!LibraryFrames.Model(db,tx).Any(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==items[0].Id),"Orphaned block annotation remained after layout");
                Check(LibraryFrames.Model(db,tx).Count(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==items[1].Id)==layerAnnotations&&line.IsErased,"Block-only layout changed another kind or restored its erased sample");
                Check(retained.All(e=>e.Entity.GeometricExtents.MinPoint==e.Min&&e.Entity.GeometricExtents.MaxPoint==e.Max)&&newSample.Position==keptNewPosition,"Layout-only moved surviving samples");
                LibrarySyncNative.ArrangeExistingDrawing(db,tx,items,"blocks");
                Check(!LibraryFrames.Model(db,tx).OfType<BlockReference>().Any(b=>b.BlockTableRecord==definition.ObjectId),"Repeated layout restored a deleted sample");
                AICad.Contracts.Json.Atomic(Path.Combine(root,"layout-test-items.json"),items);
                foreach(var entity in LibraryFrames.Model(db,tx).Where(e=>LibrarySyncNative.SampleMetadata(e,tx)!=null).ToArray()){entity.UpgradeOpen();entity.Erase();}
                var emptyLayout=LibrarySyncNative.ArrangeExistingDrawing(db,tx,items,"all");
                Check(emptyLayout.MissingSampleIds.Count==items.Count&&emptyLayout.ExistingSamples==0&&!LibraryFrames.Model(db,tx).Any(e=>LibrarySyncNative.SampleMetadata(e,tx)!=null),"An empty gallery triggered sample regeneration");
                tx.Commit();
            }
            db.SaveAs(Path.Combine(root,"library-manager-test.dwg"),DwgVersion.AC1032);
            using(var saved=new Database(false,true))
            {
                saved.ReadDwgFile(Path.Combine(root,"library-manager-test.dwg"),FileOpenMode.OpenForReadAndAllShare,true,"");saved.CloseInput(true);HostApplicationServices.WorkingDatabase=saved;using var savedTx=saved.TransactionManager.StartOpenCloseTransaction();Check(LibraryFrames.Nodes(saved,savedTx).Count==16&&LibraryFrames.Nodes(saved,savedTx).All(n=>!n.Manual),"Group tree or legacy flag migration differs");
            }
            HostApplicationServices.WorkingDatabase=db;
            AICad.Contracts.Json.Atomic(Path.Combine(root,"result.json"),new{passed=true,kinds=5,frames=16,editableSamples=true,attributesPreserved=true,resizeGrips=3,definitionUnchanged=true,legacyFlagRetired=true,syncPreservesPositions=true,newSamplesAppend=true,retryStable=true,explicitPacking=true,cadGroupsSyncable=true,layoutNeverRestoresErasedSamples=true,layoutScopePreserved=true,emptyLayoutNeverBootstraps=true});
        }
        catch(System.Exception error){AICad.Contracts.Json.Atomic(Path.Combine(root,"result.json"),new{passed=false,error=error.ToString()});}
        finally{HostApplicationServices.WorkingDatabase=previous;}
    }
    static void EditProfiles()
    {
        var previous=HostApplicationServices.WorkingDatabase;
        try
        {
            using var db=new Database(true,true){Insunits=UnitsValue.Millimeters};HostApplicationServices.WorkingDatabase=db;
            using var tx=db.TransactionManager.StartTransaction();
            var hints=new System.Collections.Generic.List<LibrarySyncItem>();
            LibrarySyncItem Add(string kind,string name,object values)
            {
                var item=new LibrarySyncItem{Id=Guid.NewGuid().ToString("N"),Kind=kind,Name=name,Values=Newtonsoft.Json.Linq.JObject.FromObject(values)};
                LibrarySyncNative.EditResource(db,tx,item,hints);hints.Add(item);return item;
            }
            var layer=Add("layers","QA_EDIT",new{color=7,lineWeight=25,lineType="Continuous",plottable=true});
            layer.Name="QA_RENAMED";layer.Values["color"]=3;LibrarySyncNative.EditResource(db,tx,layer,hints);
            var text=Add("texts","QA_EDIT_TEXT",new{font="tahoma.ttf",bigFont="",height=0,widthFactor=0.8,obliqueAngle=0,vertical=false});
            var dim=Add("dims","QA_EDIT_DIM",new{textStyle=text.Name,annotative=false,textHeight=2.5,arrowSize=2.5,scale=60,linearFactor=1,precision=0,extensionOffset=1,extensionBeyond=1,baselineSpacing=7,textGap=1,rounding=0,unitFormat=2,angularPrecision=0,zeroSuppression=0});
            var hatch=Add("hatches","QA_HATCH",new{pattern="ANSI31",type="PreDefined",scale=300,angle=0.5,layer=layer.Name,color=8,spacing=1,doublePattern=false});
            var read=LibrarySyncNative.ReadDrawing(db,tx,hints);
            Check(read.Single(i=>i.Id==layer.Id).Name=="QA_RENAMED"&&read.Single(i=>i.Id==layer.Id).Values.Value<int>("color")==3,"QA editor lost layer identity on rename");
            Check(Math.Abs(read.Single(i=>i.Id==text.Id).Values.Value<double>("widthFactor")-0.8)<1e-9,"QA text profile not applied");
            Check(read.Single(i=>i.Id==dim.Id).Name=="QA_EDIT_DIM"&&read.Single(i=>i.Id==dim.Id).Values.Value<double>("scale")==60,"QA dim name/scale changed");
            Check(read.Single(i=>i.Id==hatch.Id).Values.Value<double>("scale")==300&&read.Single(i=>i.Id==hatch.Id).Values.Value<int>("color")==8,"QA hatch profile not applied");
            tx.Abort();
        }
        finally{HostApplicationServices.WorkingDatabase=previous;}
    }
    static void RenderBrowser(string root)
    {
        var window=new QaBrowserWindow(test:true);
        try
        {
            var row=new Newtonsoft.Json.Linq.JObject{["resourceId"]="QA_TEST",["metadataId"]="test",["id"]=Guid.NewGuid().ToString("N"),["code"]="BLK-0001",["name"]="QA_KT_CUA",["displayName"]="Cửa thư viện",["management"]=Newtonsoft.Json.Linq.JObject.FromObject(new LibraryLabel{Purpose="Mẫu cửa kiến trúc",Reviewed=true}),["profile"]=new Newtonsoft.Json.Linq.JObject{["Loại"]="Block động",["Đơn vị"]="mm"},["image"]=""};
            string parent=Guid.NewGuid().ToString("N"),child=Guid.NewGuid().ToString("N");
            row["management"]!["GroupId"]=child;
            var duplicate=(Newtonsoft.Json.Linq.JObject)row.DeepClone();duplicate["resourceId"]="QA_DOOR_PROFILE";duplicate["profile"]!["Usage"]=new Newtonsoft.Json.Linq.JObject{["Role"]="door",["Reviewed"]=true};
            var cards=QaBrowserWindow.ResourceRows(new[]{duplicate,row},"blocks").ToList();
            Check(cards.Count==1&&cards[0].Value<string>("name")=="QA_KT_CUA","QA must show one native card, not legacy usage profiles");
            Check(duplicate["profile"]!["Usage"]!.Value<string>("Role")=="door","Collapsing cards must preserve old profile data");
            window.Bind(new Newtonsoft.Json.Linq.JObject{["source"]="C:/Library/ISO_QuangAnh_ThuVien.dwg",["revision"]=1,["version"]="1.2.26",["groups"]=Newtonsoft.Json.Linq.JArray.FromObject(new[]{new LibraryGroup{Id=parent,Name="Kiến trúc"},new LibraryGroup{Id=child,Name="Cửa",ParentId=parent}}),["items"]=new Newtonsoft.Json.Linq.JArray(row),["authority"]=new Newtonsoft.Json.Linq.JObject{["editor"]="QA"}});
            var tree=(System.Windows.Controls.TreeView)typeof(QaBrowserWindow).GetField("tree",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(window)!;
            typeof(QaBrowserWindow).GetMethod("SelectTreeGroup",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,new object[]{tree,child});
            var layoutStatus=(System.Windows.Controls.TextBlock)typeof(QaBrowserWindow).GetField("layoutStatus",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(window)!;
            Check(layoutStatus.Text.Contains("Kiến trúc / Cửa"),"Layout controls did not follow the shared library group selection");
            Check(!window.LayoutSection.IsExpanded,"Layout section must start collapsed");
            Check(typeof(LibraryPlugin).GetMethod("ShowLayout",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)==null,"A separate layout window remains reachable");
            Check(typeof(LibraryPlugin).GetFields(System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).All(f=>f.Name!="palette"),"A second main palette is still allocated");
            window.CatalogFallback(new Newtonsoft.Json.Linq.JObject{["source"]="C:/Library/ISO_QuangAnh_ThuVien.dwg",["groups"]=Newtonsoft.Json.Linq.JArray.FromObject(new[]{new LibraryGroup{Id=parent,Name="Kiến trúc"},new LibraryGroup{Id=child,Name="Cửa",ParentId=parent}}),["items"]=new Newtonsoft.Json.Linq.JArray(row)});
            var catalogList=(System.Windows.Controls.ListBox)typeof(QaBrowserWindow).GetField("list",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(window)!;
            Check(catalogList.Items.Count==1&&tree.Items.Count==3,"Failed CAD read hid the published catalog or groups");
            bool readOnly=false;try{typeof(QaBrowserWindow).GetMethod("DemandLive",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(window,null);}catch(System.Reflection.TargetInvocationException error)when(error.InnerException is InvalidOperationException){readOnly=true;}
            Check(readOnly,"Catalog-only fallback allowed editing");
            window.LayoutSection.IsExpanded=true;
            var rootView=(System.Windows.FrameworkElement)window.Content;
            rootView.Measure(new System.Windows.Size(1400,820));rootView.Arrange(new System.Windows.Rect(0,0,1400,820));rootView.UpdateLayout();
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1400,820,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(rootView);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(root,"browser.png"));encoder.Save(output);
        }
        finally{window.Close();}
    }
    static void VerifyLayoutContext(string root)
    {
        string kind=Commands.Kind,group=Commands.SelectedGroup;
        try
        {
            string file=Commands.Document.Name;
            var context=new LayoutCommandContext(file,"layers","selected-test-group",true);
            context.DemandSource(file.ToUpperInvariant());
            bool rejected=false;try{context.DemandSource(Path.Combine(root,"other.dwg"));}catch(CadException error)when(error.Code=="LIBRARY_SOURCE"){rejected=true;}
            Check(rejected,"Queued layout allowed a different drawing");
            bool invoked=false;
            Commands.PendingLayout=new LayoutCommandContext(Path.Combine(root,"other.dwg"),"layers","selected-test-group",true);
            Commands.Guard((doc,tx)=>invoked=true,true);
            Check(!invoked&&Commands.PendingLayout==null,"A queued layout action executed on the wrong drawing or retained its pending context");
            Commands.PendingLayout=context;
            Commands.Guard((doc,tx)=>{invoked=Commands.Kind=="layers"&&Commands.SelectedGroup=="selected-test-group";throw new CadException("CANCELLED","Fixture canceled");},true);
            Check(invoked&&Commands.PendingLayout==null,"Layout lost the captured kind/group or cancellation left a pending context");
        }
        finally{Commands.Kind=kind;Commands.SelectedGroup=group;Commands.PendingLayout=null;}
    }
}
