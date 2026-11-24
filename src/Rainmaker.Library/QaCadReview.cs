using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AICad.Contracts;
using AICad.LibraryScanner;
using Newtonsoft.Json.Linq;

namespace Rainmaker.Library;
internal sealed class QaCadReview:Window
{
    internal JObject Plan {get;private set;}=new();
    internal sealed class Pick
    {
        public string Key{get;set;}="";public string Text{get;set;}="";
    }
    internal sealed class Row
    {
        public bool Selected{get;set;}
        public string Kind{get;set;}="";public string Caption{get;set;}="";public string Name{get;set;}="";public string Before{get;set;}="";public string After{get;set;}="";
        public string KindName=>LibraryFrames.KindName(Kind);
        public string Group{get;set;}="";public string BindId{get;set;}="";public string MergeId{get;set;}="";
        public List<Pick> Bindings{get;set;}=new();public List<Pick> Merges{get;set;}=new();
        internal JObject Difference=null!;
        internal string Id=>Difference.Value<string>("Id")!;
        internal bool Missing=>Difference["Cad"] is not JObject||Difference.Value<string>("Status")=="CAD_SAMPLE_MISSING";
        internal bool DefinitionOnly=>Difference.Value<string>("Status")=="CAD_DEFINITION_ONLY";
    }
    internal readonly List<Row> Rows;
    static string Caption(JObject d)
    {
        string status=d.Value<string>("Status")??"";
        if(status=="CAD_DEFINITION_ONLY"||status=="CAD_SAMPLE_MISSING")return "Chỉ còn định nghĩa / thiếu mẫu";
        if(d["Cad"] is not JObject)return "Đã mất trong CAD";
        if(d["App"] is not JObject)return "Mới trong CAD";
        if(d["Cad"]!.Value<string>("Name")!=d["App"]!.Value<string>("Name"))return "Đổi tên";
        return status=="MATCH"?"Không thay đổi":"Đã sửa / cần đối chiếu";
    }
    internal QaCadReview(JObject review,Dictionary<string,string> defaultGroups)
    {
        Title="Quét CAD → xác nhận → cập nhật kho và trình bày";Width=1280;Height=780;MinWidth=880;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Resources=Theme.Load();SetResourceReference(BackgroundProperty,"ToolBackground");SetResourceReference(ForegroundProperty,"ToolText");
        var differences=review["differences"]!.OfType<JObject>().ToList();
        Rows=differences.Select(d=>{var i=(JObject)(d["Cad"] is JObject?d["Cad"]!:d["App"]!);string id=d.Value<string>("Id")!;return new Row{Difference=d,Kind=i.Value<string>("Kind")!,Caption=Caption(d),Name=i.Value<string>("Name")!,Group=defaultGroups.TryGetValue(id,out var g)?g:i.Value<string>("Group")??"",Before=d["App"]?.ToString()??"Chưa có",After=d["Cad"]?.ToString()??"Không còn",Selected=d.Value<string>("Status") is not ("MATCH" or "CAD_DEFINITION_ONLY")};}).ToList();
        foreach(var row in Rows)
        {
            row.Bindings.Add(new(){Key="",Text="Giữ ID hiện tại"});
            if(row.Difference["App"] is not JObject)row.Bindings.AddRange(Rows.Where(r=>r.Kind==row.Kind&&r.Difference["Cad"] is not JObject).Select(r=>new Pick{Key=r.Id,Text=r.Name+" · "+r.Id.Substring(0,8)}));
            row.Merges.Add(new(){Key="",Text="Không gộp danh mục"});
            if(row.Kind=="hatches"&&!row.Missing)row.Merges.AddRange(Rows.Where(r=>r.Kind=="hatches"&&r.Id!=row.Id&&!r.Missing&&r.Difference["App"] is JObject).Select(r=>new Pick{Key=r.Id,Text=r.Name+" · "+r.Id.Substring(0,8)}));
        }
        var root=new DockPanel{Margin=new Thickness(16)};Content=root;
        var help=new TextBlock{Text="Chỉ nhận thông số từ DWG đã lưu. Chọn từng mục; để trống mục cần rà sau. Nhóm nhập theo đường dẫn Cha / Con.\nLiên kết ID chỉ dùng khi đã xác minh cùng tài nguyên; gộp hatch chỉ đổi danh mục/ID khi cấu hình trên CAD đã giống nhau.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(help,Dock.Top);root.Children.Add(help);
        var footer=new StackPanel();var error=new TextBlock{TextWrapping=TextWrapping.Wrap};footer.Children.Add(error);var actions=new WrapPanel{Margin=new Thickness(0,10,0,0)};footer.Children.Add(actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var grid=new DataGrid{ItemsSource=Rows,AutoGenerateColumns=false,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Single};root.Children.Add(grid);
        grid.Columns.Add(new DataGridCheckBoxColumn{Header="Nhận",Binding=new System.Windows.Data.Binding("Selected")});
        foreach(var field in new[]{("Loại","KindName"),("Trạng thái","Caption"),("Tài nguyên","Name"),("Nhóm / đường dẫn","Group")})grid.Columns.Add(new DataGridTextColumn{Header=field.Item1,Binding=new System.Windows.Data.Binding(field.Item2),IsReadOnly=field.Item2!="Group",Width=field.Item2=="Name"?240:170});
        void Choices(string header,string collection,string selected)
        {
            var combo=new FrameworkElementFactory(typeof(ComboBox));combo.SetBinding(ItemsControl.ItemsSourceProperty,new System.Windows.Data.Binding(collection));combo.SetValue(ComboBox.DisplayMemberPathProperty,"Text");combo.SetValue(ComboBox.SelectedValuePathProperty,"Key");combo.SetBinding(ComboBox.SelectedValueProperty,new System.Windows.Data.Binding(selected){Mode=System.Windows.Data.BindingMode.TwoWay,UpdateSourceTrigger=System.Windows.Data.UpdateSourceTrigger.PropertyChanged});grid.Columns.Add(new DataGridTemplateColumn{Header=header,CellTemplate=new DataTemplate{VisualTree=combo},Width=230});
        }
        Choices("Liên kết ID cũ khi mất dấu","Bindings","BindId");Choices("Gộp danh mục hatch về","Merges","MergeId");
        void Button(string text,Action action){var b=new Button{Content=text,Margin=new Thickness(0,0,8,0),Padding=new Thickness(10,7,10,7)};b.Click+=(_,_)=>action();actions.Children.Add(b);}
        Button("Xem thông số hai phía",()=>{if(grid.SelectedItem is Row item){var view=new Window{Title="Đối chiếu · "+item.Name,Width=780,Height=600,Owner=this,Resources=Theme.Load(),Content=new TextBox{Text="Kho:\n"+item.Before+"\n\nCAD:\n"+item.After,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};view.ShowDialog();}});
        Button("Chọn mục có thay đổi",()=>{foreach(var r in Rows)r.Selected=r.Difference.Value<string>("Status") is not ("MATCH" or "CAD_DEFINITION_ONLY");grid.Items.Refresh();});
        Button("Xác nhận cập nhật",()=>{try{grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);Plan=BuildPlan();DialogResult=true;}catch(Exception e){error.Text=e.Message;}});
        Button("Hủy",()=>{DialogResult=false;});
    }
    internal JObject BuildPlan()
    {
        var selected=Rows.Where(r=>r.Selected).ToList();if(selected.Count==0)throw new InvalidOperationException("Chọn ít nhất một mục cần tiếp nhận hoặc trình bày lại.");
        var destinations=new HashSet<string>();
        foreach(var r in selected)
        {
            if(r.Group.Any(char.IsControl))throw new InvalidOperationException("Đường dẫn nhóm không chứa ký tự điều khiển.");
            var parts=r.Group.Split('/').Select(p=>p.Trim()).Where(p=>p!="").ToArray();if(parts.Any(p=>p.Length>140))throw new InvalidOperationException("Tên một nhóm dài tối đa 140 ký tự.");r.Group=string.Join(" / ",parts);
            if(r.DefinitionOnly)throw new InvalidOperationException("Block "+r.Name+" chỉ còn định nghĩa. Chèn mẫu trên CAD hoặc PURGE; QA không tự tạo block chèn.");
            if(r.BindId!=""&&(!r.Bindings.Any(p=>p.Key==r.BindId)||!destinations.Add(r.BindId)))throw new InvalidOperationException("ID liên kết không hợp lệ hoặc bị chọn cho nhiều mục.");
            if(r.MergeId!=""&&(!r.Merges.Any(p=>p.Key==r.MergeId)||selected.Any(other=>other.Id==r.MergeId&&other.MergeId!="")))throw new InvalidOperationException("Chọn một hatch đích giữ lại, không gộp vòng hoặc gộp dây chuyền.");
        }
        var rebound=new HashSet<string>(selected.Where(r=>r.BindId!="").Select(r=>r.BindId));
        return new JObject{["rows"]=new JArray(selected.Where(r=>!rebound.Contains(r.Id)).Select(r=>new JObject{["id"]=r.Id,["kind"]=r.Kind,["name"]=r.Name,["action"]=r.Missing?"remove":"cad",["new"]=r.Difference["App"] is not JObject&&r.BindId=="",["bindId"]=r.BindId,["mergeId"]=r.MergeId,["group"]=r.BindId!=""&&r.Group==""?Rows.Single(old=>old.Id==r.BindId).Group:r.Group,["native"]=r.Difference["Cad"]?.DeepClone(),["app"]=r.BindId!=""?Rows.Single(old=>old.Id==r.BindId).Difference["App"]?.DeepClone():r.Difference["App"]?.DeepClone()})),["deferred"]=Rows.Count(r=>!r.Selected&&r.Difference.Value<string>("Status") is not ("MATCH" or "CAD_DEFINITION_ONLY"))};
    }
}
