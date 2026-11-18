using System;
using System.Drawing;
using System.Linq;
using System.Windows.Input;
using AICad.LibraryScanner;
using AICad.Contracts;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autodesk.Windows;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;

public sealed class LibraryPlugin:IExtensionApplication,ICadManagedAddin
{
    internal static LibraryPlugin? Current;
    PaletteSet? palette;LibraryView? view;FrameGrips? grips;string document="";Action<string>? managedDispatch;
    public IReadOnlyList<CadRibbonPin> ManagedCommands=>new[]{
        Pin("QALIBBLOCK","Block","Loại thư viện",0),Pin("QALIBLAYER","Layer","Loại thư viện",1),Pin("QALIBHATCH","Hatch","Loại thư viện",2),Pin("QALIBTEXT","Text","Loại thư viện",3),Pin("QALIBDIM","Dim","Loại thư viện",4),
        Pin("QALIBADOPT","Nhận vào nhóm","Mẫu thư viện",0),Pin("QALIBASSIGN","Chuyển nhóm","Mẫu thư viện",1),Pin("QALIBPACK","Xếp gọn nhóm","Mẫu thư viện",2),Pin("QALIBMOVEGROUP","Di chuyển cả nhóm","Mẫu thư viện",3),
        Pin("QALIBRESIZE","Chỉnh khung","Khung và nhóm",0),Pin("QALIBNEWGROUP","Tạo nhóm","Khung và nhóm",1),Pin("QALIBRENAME","Đổi tên nhóm","Khung và nhóm",2),Pin("QALIBDELETEGROUP","Xóa nhóm rỗng","Khung và nhóm",3),Pin("QALIBKEEP","Giữ bố trí","Khung và nhóm",4),
        Pin("QALIBRARY","Bảng thư viện","Quản lý thư viện",0),Pin("QALIBZOOM","Đến nhóm","Quản lý thư viện",1),Pin("QALIBREFRESH","Làm mới","Quản lý thư viện",2),Pin("QALIBINIT","Khởi tạo DWG thư viện","Quản lý thư viện",3)
    };
    static CadRibbonPin Pin(string command,string label,string group,int order)=>new(){Command=command,Label=label,Group=group,Order=order,Icon="Công cụ",Description=label+" · Thư viện CAD"};
    public void Initialize()=>Start(null);
    public void InitializeManaged(Action<string> dispatch)=>Start(dispatch);
    void Start(Action<string>? dispatch){Current=this;managedDispatch=dispatch;grips=new FrameGrips();Overrule.AddOverrule(RXObject.GetClass(typeof(BlockReference)),grips,false);Overrule.Overruling=true;AcApp.Idle+=Idle;}
    public void Terminate(){AcApp.Idle-=Idle;if(grips!=null)Overrule.RemoveOverrule(RXObject.GetClass(typeof(BlockReference)),grips);if(System.Diagnostics.Process.GetCurrentProcess().ProcessName.Equals("acad",StringComparison.OrdinalIgnoreCase))DisposePalette();managedDispatch=null;if(Current==this)Current=null;}
    // CAD Core has no PaletteSet host. Keep GUI type resolution outside its lifecycle path.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    void DisposePalette(){palette?.Dispose();palette=null;view=null;}
    void Idle(object? sender,EventArgs e)
    {
        string name=AcApp.DocumentManager.MdiActiveDocument?.Name??"";if(palette?.Visible==true&&name!=document){document=name;Refresh();}
    }
    internal static void Send(string command)
    {
        if(Current?.managedDispatch is Action<string> dispatch)dispatch(command);
        else AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(command+" ",true,false,false);
    }
    internal void Show()
    {
        if(palette==null){view=new LibraryView();palette=new PaletteSet("Thư viện QA · Block / Layer / Hatch / Text / Dim",new Guid("2c450968-2b64-4f9e-9cce-799a57879ab3")){Style=PaletteSetStyles.ShowAutoHideButton|PaletteSetStyles.ShowCloseButton|PaletteSetStyles.ShowPropertiesMenu,MinimumSize=new Size(350,400),Size=new Size(440,760),DockEnabled=DockSides.Left|DockSides.Right};palette.AddVisual("Quản lý",view);}
        palette.Visible=true;Refresh();
    }
    internal void Refresh()
    {
        if(view==null)return;try
        {
            var doc=Commands.Document;document=doc.Name;using var locked=doc.LockDocument();using var tx=doc.Database.TransactionManager.StartTransaction();
            bool managed=Commands.IsLibrary(doc.Database,tx);view.Update(doc.Name,managed,managed?LibraryFrames.Nodes(doc.Database,tx):new(),managed?LibraryFrames.ManagedItems(doc.Database,tx):new());tx.Abort();
        }catch(System.Exception error){Message(error.Message);}
    }
    internal void Message(string value)=>view?.Message(value);
    internal System.Windows.FrameworkElement? RootView=>view;
}

/// <summary>Grip extension resizes the border only; membership is managed by ID, never by a stretch selection set.</summary>
public sealed class FrameGrips:GripOverrule
{
    public FrameGrips(){SetExtensionDictionaryEntryFilter(LibraryFrames.FrameKey);}
    public override void GetGripPoints(Entity entity,Point3dCollection points,Autodesk.AutoCAD.Geometry.IntegerCollection snapModes,Autodesk.AutoCAD.Geometry.IntegerCollection geometryIds)
    {
        if(entity is not BlockReference frame){base.GetGripPoints(entity,points,snapModes,geometryIds);return;}
        var origin=frame.Position;points.Add(origin+new Vector3d(frame.ScaleFactors.X,0,0));points.Add(origin+new Vector3d(frame.ScaleFactors.X,-frame.ScaleFactors.Y,0));points.Add(origin+new Vector3d(0,-frame.ScaleFactors.Y,0));
    }
    public override void MoveGripPointsAt(Entity entity,Autodesk.AutoCAD.Geometry.IntegerCollection indices,Vector3d offset)
    {
        if(entity is not BlockReference frame){base.MoveGripPointsAt(entity,indices,offset);return;}
        double width=frame.ScaleFactors.X,height=frame.ScaleFactors.Y;foreach(int i in indices){if(i==0||i==1)width+=offset.X;if(i==1||i==2)height-=offset.Y;}
        width=Math.Max(2000,Math.Min(2000000,width));height=Math.Max(1500,Math.Min(2000000,height));frame.ScaleFactors=new Scale3d(width,height,1);
        // CAD uses transient clones while dragging. Persist settings only on a resident frame.
        if(frame.ObjectId.IsNull)return;
        using var tx=frame.Database.TransactionManager.StartTransaction();LibraryFrames.Resize(frame,tx,width,height);tx.Commit();
    }
}
