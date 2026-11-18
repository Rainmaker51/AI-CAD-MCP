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

public sealed class NativeTests
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
                nodes=LibraryFrames.Nodes(db,tx);nodes.Single(n=>n.Id==target.Id).Manual=true;LibraryFrames.Save(db,tx,nodes);try{LibraryFrames.PackGroup(db,tx,nodes.Single(n=>n.Id==target.Id),items);throw new System.Exception("Manual group packed");}catch(CadException e)when(e.Code=="LIBRARY_MANUAL"){}
                Check(items.All(i=>LibrarySyncNative.ReadDrawing(db,tx).Single(n=>n.Id==i.Id).GroupEditedInCad),"CAD group edits not marked for app import");
                tx.Commit();
            }
            db.SaveAs(Path.Combine(root,"library-manager-test.dwg"),DwgVersion.AC1032);
            using(var saved=new Database(false,true))
            {
                saved.ReadDwgFile(Path.Combine(root,"library-manager-test.dwg"),FileOpenMode.OpenForReadAndAllShare,true,"");saved.CloseInput(true);HostApplicationServices.WorkingDatabase=saved;using var savedTx=saved.TransactionManager.StartOpenCloseTransaction();Check(LibraryFrames.Nodes(saved,savedTx).Count==16&&LibraryFrames.Nodes(saved,savedTx).Any(n=>n.Manual),"Group tree or manual mode not persisted");
            }
            HostApplicationServices.WorkingDatabase=db;
            AICad.Contracts.Json.Atomic(Path.Combine(root,"result.json"),new{passed=true,kinds=5,frames=16,editableSamples=true,attributesPreserved=true,resizeGrips=3,definitionUnchanged=true,manualMode=true,cadGroupsSyncable=true});
        }
        catch(System.Exception error){AICad.Contracts.Json.Atomic(Path.Combine(root,"result.json"),new{passed=false,error=error.ToString()});}
        finally{HostApplicationServices.WorkingDatabase=previous;}
    }
}
