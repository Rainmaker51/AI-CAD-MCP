using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using AICad.Contracts;
using AICad.LibraryScanner;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;

internal static class Theme
{
    internal static ResourceDictionary Load(){var assembly=typeof(Theme).Assembly;using var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith("CadToolTheme.xaml")));return (ResourceDictionary)XamlReader.Load(stream!);}
    internal static Button Button(string text,string command)=>new(){Content=text,MinHeight=38,Margin=new Thickness(0,4,0,4),HorizontalContentAlignment=HorizontalAlignment.Left,Tag=command};
}

internal sealed class LibraryView:UserControl
{
    readonly ComboBox kind=new(){ItemsSource=LibrarySyncRules.Kinds.Select(k=>new KindItem(k)).ToArray(),DisplayMemberPath="Name",Margin=new Thickness(0,8,0,8)};
    readonly TreeView groups=new(){MinHeight=150,MaxHeight=260};readonly ListBox items=new(){DisplayMemberPath="Name",MinHeight=100,MaxHeight=220};
    readonly TextBlock source=new(){TextWrapping=TextWrapping.Wrap},summary=new(){TextWrapping=TextWrapping.Wrap},error=new(){TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(253,164,175))};
    bool updating;
    internal LibraryView()
    {
        Resources=Theme.Load();FontFamily=new FontFamily("Segoe UI");FontSize=13;SetResourceReference(ForegroundProperty,"ToolText");
        groups.SetResourceReference(BackgroundProperty,"ToolInput");groups.SetResourceReference(ForegroundProperty,"ToolText");groups.SetResourceReference(BorderBrushProperty,"ToolBorder");
        var root=new StackPanel{Margin=new Thickness(14)};var border=new Border{Child=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};border.SetResourceReference(Border.BackgroundProperty,"ToolBackground");Content=border;
        root.Children.Add(new TextBlock{Text="THƯ VIỆN QA",FontSize=20,FontWeight=FontWeights.SemiBold});root.Children.Add(source);root.Children.Add(kind);root.Children.Add(summary);root.Children.Add(error);
        root.Children.Add(new TextBlock{Text="Nhóm và nhóm con",Margin=new Thickness(0,12,0,6),FontWeight=FontWeights.SemiBold});root.Children.Add(groups);
        foreach(var action in new[]{("Nhận mẫu vào nhóm","QALIBADOPT"),("Chuyển mẫu sang nhóm khác","QALIBASSIGN"),("Xếp gọn nhóm","QALIBPACK"),("Di chuyển cả nhóm","QALIBMOVEGROUP"),("Kéo/chỉnh phạm vi khung","QALIBRESIZE"),("Tạo nhóm con","QALIBNEWGROUP"),("Đổi tên nhóm","QALIBRENAME"),("Xóa nhóm rỗng","QALIBDELETEGROUP"),("Bật/tắt giữ bố trí","QALIBKEEP")})
        {
            var button=Theme.Button(action.Item1,action.Item2);button.Click+=(_,_)=>LibraryPlugin.Send((string)button.Tag);root.Children.Add(button);
        }
        root.Children.Add(new TextBlock{Text="Mẫu trong nhóm",Margin=new Thickness(0,12,0,6),FontWeight=FontWeights.SemiBold});root.Children.Add(items);
        foreach(var action in new[]{("Làm mới từ DWG","QALIBREFRESH"),("Khởi tạo quản lý trên DWG này","QALIBINIT")}){var button=Theme.Button(action.Item1,action.Item2);button.Click+=(_,_)=>LibraryPlugin.Send((string)button.Tag);root.Children.Add(button);}
        root.Children.Add(new TextBlock{Text="Ctrl+S lưu DWG trước khi đồng bộ app. Các mẫu vẫn sửa trực tiếp trong Model Space. Kéo khung chỉ đổi phạm vi; dùng Di chuyển cả nhóm để chuyển cả nội dung.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)});
        kind.SelectionChanged+=(_,_)=>{if(updating||kind.SelectedItem is not KindItem selected)return;Commands.Kind=selected.Key;Commands.SelectedGroup="";LibraryPlugin.Current?.Refresh();};
        groups.SelectedItemChanged+=(_,_)=>{if(groups.SelectedItem is TreeViewItem row&&row.Tag is LibraryFrameNode node){Commands.SelectedGroup=node.Id;Commands.Kind=node.Kind;ShowItems(node);}};
    }
    List<LibrarySyncItem> resources=new();
    void ShowItems(LibraryFrameNode node)=>items.ItemsSource=resources.Where(i=>i.Kind==node.Kind&&LibraryFrames.Under(i.Group,node.Path)).OrderBy(i=>i.Name).ToList();
    internal void Update(string file,bool managed,List<LibraryFrameNode> nodes,List<LibrarySyncItem> all)
    {
        updating=true;resources=all;source.Text=Path.GetFileName(file);error.Text=managed?"":"Chọn đúng DWG thư viện rồi nhấn Khởi tạo để dùng các lệnh quản lý.";kind.SelectedItem=((KindItem[])kind.ItemsSource).First(k=>k.Key==Commands.Kind);summary.Text=LibraryFrames.KindName(Commands.Kind)+": "+all.Count(i=>i.Kind==Commands.Kind)+" tài nguyên · "+nodes.Count(n=>n.Kind==Commands.Kind&&n.Path!="")+" nhóm";groups.Items.Clear();
        TreeViewItem Row(LibraryFrameNode node)
        {
            var row=new TreeViewItem{Header=(node.Path==""?LibraryFrames.KindName(node.Kind):node.Path.Split('/').Last().Trim())+(node.Manual?" · Giữ bố trí":""),Tag=node,IsExpanded=true};
            row.SetResourceReference(ForegroundProperty,"ToolText");
            row.MouseDoubleClick+=(_,e)=>{e.Handled=true;Commands.Kind=node.Kind;Commands.SelectedGroup=node.Id;LibraryPlugin.Send("QALIBZOOM");};
            foreach(var child in nodes.Where(n=>n.Kind==node.Kind&&n.Path!=""&&n.Parent==node.Path).OrderBy(n=>n.Path))row.Items.Add(Row(child));
            if(node.Id==Commands.SelectedGroup)row.IsSelected=true;return row;
        }
        var root=nodes.FirstOrDefault(n=>n.Kind==Commands.Kind&&n.Path=="");if(root!=null){groups.Items.Add(Row(root));ShowItems(nodes.FirstOrDefault(n=>n.Id==Commands.SelectedGroup)??root);}else items.ItemsSource=null;updating=false;
    }
    internal void Message(string text)=>error.Text=text;
    sealed class KindItem(string key){public string Key=>key;public string Name=>LibraryFrames.KindName(key);public override string ToString()=>Name;}
}

internal sealed class GroupDialog:Window
{
    readonly ComboBox choices;internal LibraryFrameNode? Group=>choices.SelectedItem as LibraryFrameNode;
    internal GroupDialog(string title,List<LibraryFrameNode> groups,string selected)
    {
        Title=title;Width=520;Height=245;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;Resources=Theme.Load();SetResourceReference(BackgroundProperty,"ToolBackground");SetResourceReference(ForegroundProperty,"ToolText");
        var panel=new StackPanel{Margin=new Thickness(22)};Content=panel;panel.Children.Add(new TextBlock{Text=title,FontSize=20,FontWeight=FontWeights.SemiBold});choices=new ComboBox{ItemsSource=groups.OrderBy(g=>g.Path).ToList(),DisplayMemberPath="Label",Margin=new Thickness(0,16,0,12),MinHeight=38};choices.SelectedItem=groups.FirstOrDefault(g=>g.Id==selected)??groups.FirstOrDefault();panel.Children.Add(choices);
        var ok=new Button{Content="Chọn nhóm",MinHeight=38,IsDefault=true};ok.Click+=(_,_)=>DialogResult=Group!=null;panel.Children.Add(ok);
    }
}
internal sealed class NameDialog:Window
{
    readonly TextBox value;NameDialog(string title,string label,string initial)
    {
        Title=title;Width=480;Height=230;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;Resources=Theme.Load();SetResourceReference(BackgroundProperty,"ToolBackground");SetResourceReference(ForegroundProperty,"ToolText");var panel=new StackPanel{Margin=new Thickness(22)};Content=panel;panel.Children.Add(new TextBlock{Text=label});value=new TextBox{Text=initial,MaxLength=80,MinHeight=38,Margin=new Thickness(0,12,0,12)};panel.Children.Add(value);var ok=new Button{Content="Lưu tên nhóm",MinHeight=38,IsDefault=true};ok.Click+=(_,_)=>DialogResult=true;panel.Children.Add(ok);
    }
    internal static string? Ask(string title,string label,string initial=""){var dialog=new NameDialog(title,label,initial);return AcApp.ShowModalWindow(dialog)==true?dialog.value.Text.Trim():null;}
}
