using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AICad.Contracts;
using AICad.LibraryScanner;
using Newtonsoft.Json.Linq;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;

internal sealed partial class QaBrowserWindow
{
    readonly WrapPanel layoutActions=new();
    readonly List<Button> layoutButtons=new();
    readonly TextBlock layoutStatus=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,8)};
    internal Expander LayoutSection {get;private set;}=null!;
    bool cadMessageReceived;

    Expander BuildLayoutSection()
    {
        var content=new StackPanel();content.Children.Add(layoutStatus);content.Children.Add(layoutActions);
        foreach(var action in new[]{("Nhận mẫu vào nhóm","QALIBADOPT"),("Chuyển mẫu sang nhóm","QALIBASSIGN"),("Xếp gọn nhóm","QALIBPACK"),("Di chuyển cả nhóm","QALIBMOVEGROUP"),("Chỉnh khung nhóm","QALIBRESIZE"),("Đến nhóm trên CAD","QALIBZOOM"),("Khởi tạo bố trí DWG","QALIBINIT")})
        {
            string command=action.Item2;
            var button=Button(action.Item1,()=>{BeginCadCommand(command);return Task.CompletedTask;});
            button.Tag=command;layoutActions.Children.Add(button);layoutButtons.Add(button);
        }
        layoutActions.Children.Add(Button("Làm mới bố trí",()=>{RefreshCadContext();return Task.CompletedTask;}));
        layoutActions.Children.Add(Button("Vị trí sử dụng / hướng dẫn CAD",ShowCadUsage));
        LayoutSection=new Expander{Header="Bố trí trên CAD",IsExpanded=false,Margin=new Thickness(0,0,0,8),Content=new ScrollViewer{Content=content,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};
        LayoutSection.Expanded+=(_,_)=>RefreshCadContext();
        return LayoutSection;
    }
    bool SameSource(string document)=>Source!=""&&string.Equals(Path.GetFullPath(document),Path.GetFullPath(Source),StringComparison.OrdinalIgnoreCase);
    string SelectedPath=>group=="*"?"":GroupPath(group);
    string SelectionCaption=>group=="*"?"Tất cả nhóm — chọn khung trên CAD khi chạy":group==""?"Chưa phân nhóm":GroupPath(group);

    internal void RefreshCadContext()
    {
        foreach(var button in layoutActions.Children.OfType<Button>().Where(b=>b.Tag as string=="hatch-merge"))button.Visibility=kind=="hatches"?Visibility.Visible:Visibility.Collapsed;
        foreach(var button in layoutButtons)button.IsEnabled=false;
        layoutStatus.Text=LibraryFrames.KindName(kind)+" · "+SelectionCaption;
        if(!persistUi)return;
        try
        {
            var doc=Commands.Document;
            if(!SameSource(doc.Name)){layoutStatus.Text+="\nMở DWG nguồn để dùng công cụ bố trí. Bản vẽ hiện tại: "+Path.GetFileName(doc.Name);return;}
            using var locked=doc.LockDocument();using var tx=doc.Database.TransactionManager.StartOpenCloseTransaction();
            bool managed=Commands.IsLibrary(doc.Database,tx);
            foreach(var button in layoutButtons)button.IsEnabled=(string)button.Tag=="QALIBINIT"||managed;
            if(!managed)layoutStatus.Text+="\nDWG nguồn chưa khởi tạo bố trí. Dùng Khởi tạo bố trí DWG trên chính file này.";
            else if(group!="*"&&!LibraryFrames.Model(doc.Database,tx).OfType<Autodesk.AutoCAD.DatabaseServices.BlockReference>().Any(b=>LibraryFrames.Info(b,tx)?.Kind==kind&&LibraryFrames.Info(b,tx)?.Path==SelectedPath))
            {foreach(var button in layoutButtons)if((string)button.Tag!="QALIBINIT")button.IsEnabled=false;layoutStatus.Text+="\nKhung nhóm không còn trên CAD. Dùng Xóa nhóm hoặc Quét thay đổi để đối chiếu; QA không tự dựng lại khung đã xóa.";}
            else layoutStatus.Text+="\nDùng chung nhóm đang chọn. Bảng tạm ẩn khi chọn trên CAD; Esc hủy rồi trở lại bảng này.";
        }
        catch(Exception error){layoutStatus.Text+="\n"+error.Message;}
    }
    void BeginCadCommand(string command)
    {
        DemandLive();
        var doc=Commands.Document;
        if(!SameSource(doc.Name))throw new CadException("LIBRARY_SOURCE","Mở đúng DWG nguồn trước khi bố trí.");
        string selected="";
        if(command!="QALIBINIT")
        {
            using var locked=doc.LockDocument();using var tx=doc.Database.TransactionManager.StartOpenCloseTransaction();
            if(!Commands.IsLibrary(doc.Database,tx))throw new CadException("LIBRARY_DOCUMENT","Khởi tạo bố trí trên DWG nguồn trước.");
            if(group!="*")selected=LibraryFrames.Model(doc.Database,tx).OfType<Autodesk.AutoCAD.DatabaseServices.BlockReference>().Select(b=>LibraryFrames.Info(b,tx)).SingleOrDefault(n=>n?.Kind==kind&&n.Path==SelectedPath)?.Id??throw new CadException("LIBRARY_GROUP","Khung nhóm không còn trên CAD. Dùng Xóa nhóm hoặc Quét thay đổi để đối chiếu.");
        }
        cadMessageReceived=false;status.Text="Đang thao tác trên CAD. Nhấn Esc để hủy và quay lại Thư viện QA.";
        var plugin=LibraryPlugin.Current??throw new InvalidOperationException("Thư viện QA chưa được Manager nạp.");
        plugin.BeginLayout(command,new LayoutCommandContext(Source,kind,selected,group!="*"));
    }
    internal async void FinishCadCommand()
    {
        QaBlockInsert.Pending=null;
        if(lifetime.IsCancellationRequested)return;
        Show();Activate();
        if(!cadMessageReceived)status.Text="Đã kết thúc thao tác CAD. Lưu Ctrl+S; dùng Quét thay đổi / đối chiếu để tiếp nhận vào kho.";
        if(kind!=Commands.Kind){kind=Commands.Kind;group="*";await Run(Load);}
        try
        {
            var doc=Commands.Document;
            if(SameSource(doc.Name))
            {
                if(client!=null)await Run(RefreshLive);
                using var locked=doc.LockDocument();using var tx=doc.Database.TransactionManager.StartOpenCloseTransaction();
                var node=LibraryFrames.Nodes(doc.Database,tx).SingleOrDefault(n=>n.Id==Commands.SelectedGroup&&n.Kind==kind);
                if(node!=null)
                {
                    var match=(state["groups"] as JArray??new()).OfType<JObject>().SingleOrDefault(g=>GroupPath(g.Value<string>("Id")!)==node.Path);
                    if(node.Path=="")group=group==""?"":"*";
                    else if(match!=null)group=match.Value<string>("Id")!;
                    else status.Text="Nhóm CAD đã chọn: "+node.Path+". Nhóm chưa có trong danh mục đã duyệt; lưu và đọc kiểm tra DWG để cập nhật vào kho.";
                    SelectTreeGroup(tree,group);RenderList();
                }
            }
        }
        catch(Exception error){status.Text=error.Message;}
        RefreshCadContext();
    }
    static bool SelectTreeGroup(ItemsControl parent,string selected)
    {
        foreach(var item in parent.Items.OfType<TreeViewItem>())
        {
            if((string)item.Tag==selected){item.IsSelected=true;item.BringIntoView();return true;}
            if(SelectTreeGroup(item,selected)){item.IsExpanded=true;return true;}
        }
        return false;
    }
    internal void Message(string message){cadMessageReceived=true;status.Text=message;}
    internal async void ReloadFromCad()=>await Run(RefreshLive);
    internal async void SelectKind(string requested)
    {
        if(busy||kind==requested||!LibrarySyncRules.Kinds.Contains(requested))return;
        kind=requested;group="*";await Run(Load);
    }
}
