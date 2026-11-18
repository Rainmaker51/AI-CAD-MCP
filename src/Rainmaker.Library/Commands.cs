using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(Rainmaker.Library.LibraryPlugin))]
[assembly: CommandClass(typeof(Rainmaker.Library.Commands))]
namespace Rainmaker.Library;

public sealed class Commands
{
    internal static string Kind="blocks";
    internal static string SelectedGroup="";
    internal static Document Document=>AcApp.DocumentManager.MdiActiveDocument??throw new CadException("DOCUMENT_REQUIRED","Mở DWG thư viện trước.");
    internal static bool IsLibrary(Database db,Transaction tx)=>LibraryFrames.Initialized(db,tx);
    internal static void Guard(Action<Document,Transaction> action,bool initialize=false)
    {
        try
        {
            var doc=Document;using var locked=doc.LockDocument();using var tx=doc.Database.TransactionManager.StartTransaction();
            if(!initialize&&!IsLibrary(doc.Database,tx))throw new CadException("LIBRARY_DOCUMENT","DWG này chưa được quản lý như thư viện. Dùng Khởi tạo trên đúng file thư viện trước.");
            action(doc,tx);tx.Commit();doc.Editor.Regen();LibraryPlugin.Current?.Refresh();
        }
        catch(Autodesk.AutoCAD.Runtime.Exception error){Document.Editor.WriteMessage("\nThư viện: "+error.Message);LibraryPlugin.Current?.Message(error.Message);}
        catch(System.Exception error){AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nThư viện: "+error.Message);LibraryPlugin.Current?.Message(error.Message);}
    }
    [CommandMethod("QALIBRARY")]
    public void Open()=>LibraryPlugin.Current?.Show();
    [CommandMethod("QALIBBLOCK")]
    public void Blocks(){Kind="blocks";Open();}
    [CommandMethod("QALIBLAYER")]
    public void Layers(){Kind="layers";Open();}
    [CommandMethod("QALIBHATCH")]
    public void Hatches(){Kind="hatches";Open();}
    [CommandMethod("QALIBTEXT")]
    public void Texts(){Kind="texts";Open();}
    [CommandMethod("QALIBDIM")]
    public void Dims(){Kind="dims";Open();}
    [CommandMethod("QALIBREFRESH")]
    public void Refresh()=>LibraryPlugin.Current?.Refresh();
    [CommandMethod("QALIBZOOM",CommandFlags.Modal|CommandFlags.UsePickSet)]
    public void Zoom()=>Guard((doc,tx)=>
    {
        var node=LibraryFrames.Nodes(doc.Database,tx).FirstOrDefault(n=>n.Id==SelectedGroup)??PickFrame(doc,tx);
        var frame=LibraryFrames.Model(doc.Database,tx).OfType<BlockReference>().First(b=>LibraryFrames.Info(b,tx)?.Id==node.Id);
        using var view=doc.Editor.GetCurrentView();double aspect=view.Height>0?view.Width/view.Height:1.6;view.Target=Point3d.Origin;view.ViewDirection=Vector3d.ZAxis;view.ViewTwist=0;
        view.CenterPoint=new Point2d(frame.Position.X+frame.ScaleFactors.X/2,frame.Position.Y-frame.ScaleFactors.Y/2);view.Height=Math.Max(frame.ScaleFactors.Y+2000,(frame.ScaleFactors.X+2000)/aspect);view.Width=view.Height*aspect;doc.Editor.SetCurrentView(view);
    });
    [CommandMethod("QALIBINIT")]
    public void Initialize()=>Guard((doc,tx)=>
    {
        if(doc.Database.Insunits!=UnitsValue.Millimeters)throw new CadException("UNITS","Thư viện cần đặt đơn vị mm trước khi khởi tạo.");
        var items=LibraryFrames.ManagedItems(doc.Database,tx);var nodes=LibraryFrames.Nodes(doc.Database,tx);foreach(string kind in LibrarySyncRules.Kinds)LibraryFrames.Ensure(nodes,kind,"");LibraryFrames.Save(doc.Database,tx,nodes);
        LibrarySyncNative.ArrangeDrawing(doc.Database,tx,items);doc.Editor.WriteMessage("\nĐã tạo 5 khu quản lý thư viện. Nội dung chưa nhận vào nhóm được giữ nguyên.");
    },true);

    static LibraryFrameNode ChooseGroup(Database db,Transaction tx,string title)
    {
        var nodes=LibraryFrames.Nodes(db,tx);LibraryFrames.Ensure(nodes,Kind,"");var dialog=new GroupDialog(title,nodes.Where(n=>n.Kind==Kind).ToList(),SelectedGroup);
        if(AcApp.ShowModalWindow(dialog)!=true||dialog.Group==null)throw new CadException("CANCELLED","Đã hủy.");SelectedGroup=dialog.Group.Id;return dialog.Group;
    }
    internal static LibraryFrameNode PickFrame(Document doc,Transaction tx)
    {
        var implied=doc.Editor.SelectImplied();if(implied.Status==PromptStatus.OK)foreach(ObjectId id in implied.Value.GetObjectIds())if(LibraryFrames.Info(tx.GetObject(id,OpenMode.ForRead),tx) is LibraryFrameNode selected)return LibraryFrames.Nodes(doc.Database,tx).FirstOrDefault(n=>n.Id==selected.Id)??selected;
        var options=new PromptEntityOptions("\nChọn khung nhóm ATT: ");options.SetRejectMessage("\nChọn khung nhóm thư viện.");options.AddAllowedClass(typeof(BlockReference),true);
        var choice=doc.Editor.GetEntity(options);if(choice.Status!=PromptStatus.OK)throw new CadException("CANCELLED","Đã hủy.");var node=LibraryFrames.Info(tx.GetObject(choice.ObjectId,OpenMode.ForRead),tx)??throw new CadException("LIBRARY_FRAME","Đối tượng này không phải khung thư viện.");
        Kind=node.Kind;SelectedGroup=node.Id;return LibraryFrames.Nodes(doc.Database,tx).FirstOrDefault(n=>n.Id==node.Id)??node;
    }
    static string EffectiveName(Entity entity,Transaction tx,string kind)
    {
        if(kind=="layers")return entity.Layer;
        if(kind=="blocks"&&entity is BlockReference block&&LibraryFrames.Info(block,tx)==null)return ((BlockTableRecord)tx.GetObject(block.IsDynamicBlock?block.DynamicBlockTableRecord:block.BlockTableRecord,OpenMode.ForRead)).Name;
        if(kind=="texts"&&(entity is DBText||entity is MText))return ((TextStyleTableRecord)tx.GetObject(entity is DBText text?text.TextStyleId:((MText)entity).TextStyleId,OpenMode.ForRead)).Name;
        if(kind=="dims"&&entity is Dimension dim)return ((DimStyleTableRecord)tx.GetObject(dim.DimensionStyle,OpenMode.ForRead)).Name;
        return "";
    }
    static void Adopt(Document doc,Transaction tx,bool requireManaged)
    {
        var selection=doc.Editor.SelectImplied();if(selection.Status!=PromptStatus.OK)selection=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn mẫu "+LibraryFrames.KindName(Kind)+" cần nhận/chuyển nhóm: "});
        if(selection.Status!=PromptStatus.OK)throw new CadException("CANCELLED","Đã hủy.");var target=ChooseGroup(doc.Database,tx,requireManaged?"Chuyển nhóm":"Nhận vào nhóm");
        var all=LibrarySyncNative.ReadDrawing(doc.Database,tx);var chosen=new Dictionary<string,Tuple<LibrarySyncItem,Entity>>();
        foreach(ObjectId id in selection.Value.GetObjectIds())
        {
            if(tx.GetObject(id,OpenMode.ForRead) is not Entity entity||LibraryFrames.Info(entity,tx)!=null)throw new CadException("LIBRARY_SELECTION","Chọn mẫu tài nguyên, không chọn khung nhóm.");
            var sample=LibrarySyncNative.SampleMetadata(entity,tx);string name=EffectiveName(entity,tx,Kind);
            var item=all.FirstOrDefault(i=>i.Kind==Kind&&(sample?.Id==i.Id||Kind!="hatches"&&i.Name==name||Kind=="hatches"&&i.Handle==entity.Handle.ToString()));
            if(item==null&&entity is Hatch hatch&&Kind=="hatches")item=all.FirstOrDefault(i=>i.Kind==Kind&&i.Values.Value<string>("pattern")==hatch.PatternName&&Math.Abs(i.Values.Value<double>("scale")-hatch.PatternScale)<1e-7&&Math.Abs(i.Values.Value<double>("angle")-hatch.PatternAngle)<1e-7&&i.Values.Value<int>("color")==hatch.ColorIndex);
            if(item==null)throw new CadException("LIBRARY_SELECTION","Mẫu không phù hợp loại "+LibraryFrames.KindName(Kind)+". Block cần có định nghĩa; Text/Dim cần đúng đối tượng chữ/kích thước.");
            if(requireManaged&&!LibrarySyncNative.Managed(doc.Database,tx,item))throw new CadException("LIBRARY_UNMANAGED","Mẫu chưa được quản lý; dùng Nhận vào nhóm trước.");
            if(!chosen.ContainsKey(item.Id))chosen.Add(item.Id,Tuple.Create(item,entity));
        }
        var additions=new List<LibrarySyncItem>();
        foreach(var chosenItem in chosen.Values)
        {
            var item=chosenItem.Item1;var entity=chosenItem.Item2;bool hasCell=LibraryFrames.Model(doc.Database,tx).OfType<Polyline>().Any(p=>LibrarySyncNative.SampleMetadata(p,tx)?.Id==item.Id);
            if(!hasCell&&Kind=="layers")
            {
                var bt=(BlockTable)tx.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var point=entity.GeometricExtents.MinPoint;
                var preview=new Line(point,point+new Vector3d(5200,0,0)){LayerId=entity.LayerId,LinetypeScale=100};ms.AppendEntity(preview);tx.AddNewlyCreatedDBObject(preview,true);entity=preview;
            }
            bool relocate=!hasCell||item.Group!=target.Path;
            LibrarySyncNative.AssignGroup(doc.Database,tx,item,target.Path,hasCell?null:entity);if(!hasCell)LibraryFrames.AddCell(doc.Database,tx,item,entity);
            if(relocate)additions.Add(item);
        }
        var managed=LibraryFrames.ManagedItems(doc.Database,tx);
        LibraryFrames.AppendToGroup(doc.Database,tx,target,managed,additions);
        LibraryFrames.Clear(doc.Database,tx);LibraryFrames.Build(doc.Database,tx,managed);
        doc.Editor.WriteMessage("\nĐã nhận/chuyển "+chosen.Count+" tài nguyên vào "+target.Label+". Ctrl+S để lưu trước khi đồng bộ app.");
    }
    [CommandMethod("QALIBADOPT",CommandFlags.Modal|CommandFlags.UsePickSet)]public void Add()=>Guard((doc,tx)=>Adopt(doc,tx,false));
    [CommandMethod("QALIBASSIGN",CommandFlags.Modal|CommandFlags.UsePickSet)]public void Assign()=>Guard((doc,tx)=>Adopt(doc,tx,true));
    [CommandMethod("QALIBPACK",CommandFlags.Modal|CommandFlags.UsePickSet)]public void Pack()=>Guard((doc,tx)=>{var group=PickFrame(doc,tx);var items=LibraryFrames.ManagedItems(doc.Database,tx);LibraryFrames.PackGroup(doc.Database,tx,group,items);LibraryFrames.Separate(doc.Database,tx,items);});
    [CommandMethod("QALIBMOVEGROUP",CommandFlags.Modal|CommandFlags.UsePickSet)]public void Move()=>Guard((doc,tx)=>
    {
        var group=PickFrame(doc,tx);var frame=LibraryFrames.Model(doc.Database,tx).OfType<BlockReference>().First(b=>LibraryFrames.Info(b,tx)?.Id==group.Id);
        var point=doc.Editor.GetPoint(new PromptPointOptions("\nChọn vị trí mới cho góc trên trái của nhóm: "){BasePoint=frame.Position,UseBasePoint=true});if(point.Status!=PromptStatus.OK)return;
        var items=LibraryFrames.ManagedItems(doc.Database,tx);LibraryFrames.MoveGroup(doc.Database,tx,group,point.Value-frame.Position,items);LibraryFrames.Clear(doc.Database,tx);LibraryFrames.Build(doc.Database,tx,items);LibraryFrames.Separate(doc.Database,tx,items);
    });
    [CommandMethod("QALIBRESIZE",CommandFlags.Modal|CommandFlags.UsePickSet)]public void Resize()=>Guard((doc,tx)=>
    {
        var group=PickFrame(doc,tx);var frame=LibraryFrames.Model(doc.Database,tx).OfType<BlockReference>().First(b=>LibraryFrames.Info(b,tx)?.Id==group.Id);
        var point=doc.Editor.GetCorner(new PromptCornerOptions("\nChọn góc dưới phải của khung: ",frame.Position));if(point.Status!=PromptStatus.OK)return;
        if(point.Value.X<=frame.Position.X||point.Value.Y>=frame.Position.Y)throw new CadException("LIBRARY_SIZE","Chọn góc ở dưới và bên phải khung.");LibraryFrames.Resize(frame,tx,point.Value.X-frame.Position.X,frame.Position.Y-point.Value.Y);
    });
    [CommandMethod("QALIBNEWGROUP")]public void NewGroup()=>Guard((doc,tx)=>
    {
        var parent=ChooseGroup(doc.Database,tx,"Chọn nhóm cha");var name=NameDialog.Ask("Tạo nhóm con","Tên nhóm mới");if(name==null)return;ValidateName(name);
        string path=parent.Path==""?name:parent.Path+" / "+name;var nodes=LibraryFrames.Nodes(doc.Database,tx);if(nodes.Any(n=>n.Kind==Kind&&n.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))throw new CadException("GROUP_EXISTS","Tên nhóm đã có trong nhóm cha.");
        SelectedGroup=LibraryFrames.Ensure(nodes,Kind,path).Id;LibraryFrames.Save(doc.Database,tx,nodes);var items=LibraryFrames.ManagedItems(doc.Database,tx);LibraryFrames.Clear(doc.Database,tx);LibraryFrames.Build(doc.Database,tx,items);LibraryFrames.Separate(doc.Database,tx,items);
    });
    static void ValidateName(string name){if(string.IsNullOrWhiteSpace(name)||name.Length>80||name.Contains('/')||name.Any(char.IsControl))throw new CadException("GROUP_NAME","Tên nhóm 1–80 ký tự, không chứa dấu / hoặc xuống dòng.");}
    [CommandMethod("QALIBRENAME",CommandFlags.Modal|CommandFlags.UsePickSet)]public void Rename()=>Guard((doc,tx)=>
    {
        var group=PickFrame(doc,tx);if(group.Path=="")throw new CadException("GROUP_ROOT","Giữ tên của 5 khu gốc.");var name=NameDialog.Ask("Đổi tên nhóm","Tên mới",group.Path.Split('/').Last().Trim());if(name==null)return;ValidateName(name);
        string newPath=group.Parent==""?name:group.Parent+" / "+name;var nodes=LibraryFrames.Nodes(doc.Database,tx);if(nodes.Any(n=>n.Kind==group.Kind&&n.Id!=group.Id&&n.Path.Equals(newPath,StringComparison.OrdinalIgnoreCase)))throw new CadException("GROUP_EXISTS","Tên nhóm đã có.");
        var items=LibraryFrames.ManagedItems(doc.Database,tx);foreach(var item in items.Where(i=>i.Kind==group.Kind&&LibraryFrames.Under(i.Group,group.Path)))LibrarySyncNative.AssignGroup(doc.Database,tx,item,newPath+item.Group.Substring(group.Path.Length));
        foreach(var node in nodes.Where(n=>n.Kind==group.Kind&&LibraryFrames.Under(n.Path,group.Path))){node.Path=newPath+node.Path.Substring(group.Path.Length);node.Parent=node.Path.Contains(" / ")?node.Path.Substring(0,node.Path.LastIndexOf(" / ",StringComparison.Ordinal)):"";}
        LibraryFrames.Save(doc.Database,tx,nodes);LibraryFrames.Clear(doc.Database,tx);LibraryFrames.Build(doc.Database,tx,items);
    });
    [CommandMethod("QALIBDELETEGROUP",CommandFlags.Modal|CommandFlags.UsePickSet)]public void DeleteGroup()=>Guard((doc,tx)=>
    {
        var group=PickFrame(doc,tx);var nodes=LibraryFrames.Nodes(doc.Database,tx);var items=LibraryFrames.ManagedItems(doc.Database,tx);
        if(group.Path==""||nodes.Any(n=>n.Kind==group.Kind&&n.Parent==group.Path)||items.Any(i=>i.Kind==group.Kind&&LibraryFrames.Under(i.Group,group.Path)))throw new CadException("GROUP_NOT_EMPTY","Chỉ xóa nhóm rỗng. Chuyển mẫu và nhóm con sang nơi khác trước; 5 khu gốc luôn được giữ.");
        nodes.RemoveAll(n=>n.Id==group.Id);LibraryFrames.Save(doc.Database,tx,nodes);LibraryFrames.Clear(doc.Database,tx);LibraryFrames.Build(doc.Database,tx,items);
    });
}
