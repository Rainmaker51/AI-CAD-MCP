using System;
using System.Drawing;
using System.Linq;
using System.Windows.Input;
using AICad.LibraryScanner;
using AICad.Contracts;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autodesk.Windows;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;

public sealed class LibraryPlugin:IExtensionApplication,ICadManagedAddin
{
    internal static LibraryPlugin? Current;
    QaBrowserWindow? browser;FrameGrips? grips;string document="";Action<string>? managedDispatch;
    bool layoutRunning,restoreBrowser;
    Document? layoutDocument;string layoutCommand="";
    public IReadOnlyList<CadRibbonPin> ManagedCommands=>new[]{
        Pin("QALIBBLOCK","Block","Loại thư viện",0),Pin("QALIBLAYER","Layer","Loại thư viện",1),Pin("QALIBHATCH","Hatch","Loại thư viện",2),Pin("QALIBTEXT","Text","Loại thư viện",3),Pin("QALIBDIM","Dim","Loại thư viện",4),
        Pin("QALIBADOPT","Nhận vào nhóm","Mẫu thư viện",0),Pin("QALIBASSIGN","Chuyển nhóm","Mẫu thư viện",1),Pin("QALIBPACK","Xếp gọn nhóm","Mẫu thư viện",2),Pin("QALIBMOVEGROUP","Di chuyển cả nhóm","Mẫu thư viện",3),
        Pin("QALIBRESIZE","Chỉnh khung","Khung và nhóm",0),Pin("QALIBNEWGROUP","Tạo nhóm","Khung và nhóm",1),Pin("QALIBRENAME","Đổi tên nhóm","Khung và nhóm",2),Pin("QALIBDELETEGROUP","Xóa nhóm rỗng","Khung và nhóm",3),
        Pin("QALIBRARY","Thư viện QA","Quản lý thư viện",0),Pin("QALIBZOOM","Đến nhóm","Quản lý thư viện",1),Pin("QALIBREFRESH","Làm mới","Quản lý thư viện",2),Pin("QALIBINIT","Khởi tạo DWG thư viện","Quản lý thư viện",3),Pin("QALIBINSERT","Chèn block","Thư viện QA",4)
    };
    static CadRibbonPin Pin(string command,string label,string group,int order)=>new(){Command=command,Label=label,Group=group,Order=order,Icon="Công cụ",Description=label+" · Thư viện CAD"};
    public void Initialize()=>Start(null);
    public void InitializeManaged(Action<string> dispatch)=>Start(dispatch);
    void Start(Action<string>? dispatch){Current=this;managedDispatch=dispatch;grips=new FrameGrips();Overrule.AddOverrule(RXObject.GetClass(typeof(BlockReference)),grips,false);Overrule.Overruling=true;AcApp.Idle+=Idle;AcApp.DocumentManager.DocumentToBeDestroyed+=DocumentClosing;}
    public void Terminate(){AcApp.Idle-=Idle;AcApp.DocumentManager.DocumentToBeDestroyed-=DocumentClosing;DetachLayoutEvents();Commands.PendingLayout=null;QaBlockInsert.Pending=null;if(grips!=null)Overrule.RemoveOverrule(RXObject.GetClass(typeof(BlockReference)),grips);if(System.Diagnostics.Process.GetCurrentProcess().ProcessName.Equals("acad",StringComparison.OrdinalIgnoreCase))DisposeBrowser();managedDispatch=null;if(Current==this)Current=null;}
    // CAD Core has no GUI host. Keep WPF resolution outside its lifecycle path.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    void DisposeBrowser(){browser?.Close();browser=null;layoutRunning=restoreBrowser=false;Commands.PendingLayout=null;}
    void Idle(object? sender,EventArgs e)
    {
        if(restoreBrowser&&(AcApp.DocumentManager.MdiActiveDocument==null||Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE"))==0)){restoreBrowser=false;layoutRunning=false;DetachLayoutEvents();browser?.FinishCadCommand();}
        string name=AcApp.DocumentManager.MdiActiveDocument?.Name??"";if(browser?.IsVisible==true&&name!=document){document=name;Refresh();}
    }
    internal static void Send(string command)
    {
        if(Current?.managedDispatch is Action<string> dispatch)dispatch(command);
        else AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(command+" ",true,false,false);
    }
    internal void Show()
    {
        if(layoutRunning)return;
        if(browser==null){browser=new QaBrowserWindow();browser.Closed+=(_,_)=>browser=null;AcApp.ShowModelessWindow(browser);}
        else{browser.Show();browser.Activate();browser.SelectKind(Commands.Kind);browser.ReloadFromCad();}
    }
    internal void BeginLayout(string command,LayoutCommandContext context)
    {
        if(layoutRunning)throw new InvalidOperationException("Hoàn tất hoặc nhấn Esc để hủy thao tác CAD đang chạy.");
        layoutDocument=Commands.Document;layoutCommand=command;
        layoutDocument.CommandEnded+=CommandFinished;layoutDocument.CommandCancelled+=CommandFinished;layoutDocument.CommandFailed+=CommandFinished;
        layoutRunning=true;Commands.PendingLayout=context;browser?.Hide();
        try{Send(command);}catch{Commands.PendingLayout=null;layoutRunning=false;DetachLayoutEvents();browser?.FinishCadCommand();throw;}
    }
    internal void CompleteLayout(){if(layoutRunning)restoreBrowser=true;}
    void CommandFinished(object sender,CommandEventArgs args)
    {
        if(!layoutRunning||!new[]{layoutCommand,"AICADTOOLRUN","AICADTOOLSESSION"}.Contains(args.GlobalCommandName,StringComparer.OrdinalIgnoreCase))return;
        if(Commands.PendingLayout!=null){Commands.PendingLayout=null;Message("Lệnh CAD đã kết thúc trước khi thực thi. Xem thông báo trong dòng lệnh rồi thử lại.");}
        CompleteLayout();
    }
    void DocumentClosing(object sender,DocumentCollectionEventArgs args){if(layoutRunning&&args.Document==layoutDocument){Commands.PendingLayout=null;Message("DWG đã đóng. Mở lại DWG nguồn để tiếp tục bố trí.");CompleteLayout();DetachLayoutEvents();}}
    void DetachLayoutEvents(){if(layoutDocument!=null){layoutDocument.CommandEnded-=CommandFinished;layoutDocument.CommandCancelled-=CommandFinished;layoutDocument.CommandFailed-=CommandFinished;layoutDocument=null;}layoutCommand="";}
    internal void Refresh()
    {
        browser?.RefreshCadContext();
    }
    internal void RefreshLibrary()=>browser?.ReloadFromCad();
    internal void Message(string value)=>browser?.Message(value);
    internal System.Windows.FrameworkElement? RootView=>browser;
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
