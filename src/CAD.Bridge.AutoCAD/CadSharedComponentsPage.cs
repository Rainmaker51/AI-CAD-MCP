using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AICad.Contracts;
namespace AICad.Bridge;
internal sealed partial class CadToolPalette {
 FrameworkElement SharedComponentsPage(){
  var root=new StackPanel{Margin=new Thickness(14)};var store=new CadSharedComponentStore(plugin.Config.DataRoot);
  root.Children.Add(Label("Thành phần dùng chung",17,true));root.Children.Add(Label("Cài riêng Reader để các add-in đọc kho. Thành phần này không có nút Ribbon."));
  var state=Label("");var path=Label("");root.Children.Add(state);root.Children.Add(path);
  void Refresh(){try{var selected=store.Read();state.Text=selected==null?"Reader · Chưa cài":"Reader · "+selected.Version;path.Text=selected==null?"Chọn DLL từ gói Reader đã bàn giao.":store.Demand(selected);path.ToolTip=path.Text;}catch(Exception error){state.Text=error.Message;path.Text="";}}
  var actions=new WrapPanel{Margin=new Thickness(0,12,0,0)};
  actions.Children.Add(Button("Cài / cập nhật Reader",()=>Guard(()=>{
   var dialog=new Microsoft.Win32.OpenFileDialog{Title="Chọn Rainmaker.Library.Reader.dll",Filter="Library Reader|Rainmaker.Library.Reader.dll",CheckFileExists=true};if(dialog.ShowDialog()!=true)return;
   var current=store.Read();var installed=store.Install(dialog.FileName,current?.Revision??0);Refresh();Say("Đã cài Reader "+installed.Version+". Nếu Reader đã nạp, mở lại AutoCAD để dùng bản mới.");
  }),true));actions.Children.Add(Button("Làm mới",()=>Guard(Refresh)));root.Children.Add(actions);
  root.Children.Add(Label("Mỗi máy cài Reader riêng một lần. Khi dữ liệu kho đổi, add-in đọc lại dữ liệu ở lần triển khai tiếp theo; không cần cài lại Reader."));
  root.Children.Add(Label("Khi Reader đã được nạp, cập nhật phiên bản DLL cần mở lại AutoCAD. Không cài Reader trong danh sách .NET Add-in hoặc NETLOAD thủ công."));
  Refresh();return new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
 }
}
