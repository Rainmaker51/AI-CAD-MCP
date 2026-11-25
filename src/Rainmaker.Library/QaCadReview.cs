using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
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
        public override string ToString()=>Text;
    }
    internal sealed class Row:INotifyPropertyChanged
    {
        bool selected;
        public event PropertyChangedEventHandler? PropertyChanged;
        public bool Selected{get=>selected;set{if(selected==value)return;selected=value;PropertyChanged?.Invoke(this,new(nameof(Selected)));}}
        public string Kind{get;set;}="";public string Caption{get;set;}="";public string Name{get;set;}="";public string Before{get;set;}="";public string After{get;set;}="";
        public string KindName=>LibraryFrames.KindName(Kind);
        public string Group{get;set;}="";public string BindId{get;set;}="";public string MergeId{get;set;}="";
        public List<Pick> Bindings{get;set;}=new();public List<Pick> Merges{get;set;}=new();
        internal JObject Difference=null!;
        internal string Id=>Difference.Value<string>("Id")!;
        internal bool Missing=>Difference["Cad"] is not JObject||Difference.Value<string>("Status")=="CAD_SAMPLE_MISSING";
        internal bool OutOfScope=>Difference.Value<string>("Status")=="CAD_OUT_OF_SCOPE";
        internal bool DefinitionOnly=>Difference.Value<string>("Status")=="CAD_DEFINITION_ONLY";
        internal bool IsGroup=>Difference.Value<string>("EntityType")=="group";
        public bool CanLink=>!IsGroup&&!OutOfScope;
        public string Code=>Difference.Value<string>("Code")??"";
        public bool CanSelect=>(!OutOfScope||Kind=="hatches"&&Difference["App"] is JObject)&&!DefinitionOnly;
    }
    internal readonly List<Row> Rows;
    internal readonly ComboBox KindFilter=new(){MinWidth=160};
    internal readonly ComboBox StatusFilter=new(){MinWidth=260};
    internal readonly ComboBox SelectionFilter=new(){MinWidth=170};
    internal readonly TextBox SearchFilter=new(){MinWidth=220};
    internal ListCollectionView Filtered=null!;
    internal readonly TextBlock SelectionSummary=new(){Margin=new Thickness(0,6,0,8)};
    static string Caption(JObject d)
    {
        string status=d.Value<string>("Status")??"";
        if(status=="CAD_OUT_OF_SCOPE")return "Ngoài khung quét · giữ danh mục";
        if(status=="GROUP_NEW")return "Nhóm mới trên CAD";
        if(status=="GROUP_MISSING")return "Khung nhóm đã bị xóa";
        if(status=="GROUP_CHANGED")return "Đổi tên / chuyển nhóm";
        if(status=="GROUP_MATCH")return "Nhóm không thay đổi";
        if(status=="CAD_DEFINITION_ONLY"||status=="CAD_SAMPLE_MISSING")return "Chỉ còn định nghĩa / thiếu mẫu";
        if(d["Cad"] is not JObject)return "Đã mất trong CAD";
        if(d["App"] is not JObject)return "Mới trong CAD";
        if(d["Cad"]!.Value<string>("Name")!=d["App"]!.Value<string>("Name"))return "Đổi tên";
        return status=="MATCH"?"Không thay đổi":"Đã sửa / cần đối chiếu";
    }
    internal QaCadReview(JObject review,Dictionary<string,string> defaultGroups)
    {
        Title="Quét CAD → xác nhận → cập nhật kho và trình bày";Width=1440;Height=850;MinWidth=880;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Resources=Theme.Load();SetResourceReference(BackgroundProperty,"ToolBackground");SetResourceReference(ForegroundProperty,"ToolText");
        var differences=review["differences"]!.OfType<JObject>().Concat((review["groupDifferences"] as JArray??new()).OfType<JObject>()).ToList();
        Rows=differences.Select(d=>{var i=(JObject)(d["Cad"] is JObject?d["Cad"]!:d["App"]!);string id=d.Value<string>("Id")!;return new Row{Difference=d,Kind=i.Value<string>("Kind")!,Caption=Caption(d),Name=i.Value<string>("Name")!,Group=d["Cad"]?.Value<bool?>("GroupEditedInCad")==true?i.Value<string>("Group")??"":defaultGroups.TryGetValue(id,out var g)?g:i.Value<string>("Group")??"",Before=d["App"]?.ToString()??"Chưa có",After=d["Cad"]?.ToString()??"Không còn",Selected=d.Value<string>("Status") is not ("MATCH" or "CAD_DEFINITION_ONLY" or "CAD_OUT_OF_SCOPE" or "GROUP_MATCH")};}).ToList();
        foreach(var row in Rows)
        {
            row.Bindings.Add(new(){Key="",Text="Giữ ID hiện tại"});
            if(!row.IsGroup&&row.Difference["App"] is not JObject)row.Bindings.AddRange(Rows.Where(r=>!r.IsGroup&&r.Kind==row.Kind&&r.Difference["Cad"] is not JObject).Select(r=>new Pick{Key=r.Id,Text=r.Name+" · "+r.Id.Substring(0,Math.Min(8,r.Id.Length))}));
            row.Merges.Add(new(){Key="",Text="Không gộp danh mục"});
            if(!row.IsGroup&&row.Kind=="hatches"&&!row.Missing&&!row.OutOfScope)row.Merges.AddRange(Rows.Where(r=>!r.IsGroup&&!r.OutOfScope&&r.Kind=="hatches"&&r.Id!=row.Id&&!r.Missing&&r.Difference["App"] is JObject).Select(r=>new Pick{Key=r.Id,Text=r.Name+" · "+r.Id.Substring(0,8)}));
        }
        var root=new DockPanel{Margin=new Thickness(16)};root.SetResourceReference(Panel.BackgroundProperty,"ToolBackground");Content=root;
        var help=new TextBlock{Text="Chỉ nhận thông số từ DWG đã lưu. Chọn từng mục; để trống mục cần rà sau. Nhóm nhập theo đường dẫn Cha / Con.\nLiên kết ID chỉ dùng khi đã xác minh cùng tài nguyên; gộp hatch chỉ đổi danh mục/ID khi cấu hình trên CAD đã giống nhau.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(help,Dock.Top);root.Children.Add(help);
        var filters=new WrapPanel();DockPanel.SetDock(filters,Dock.Top);root.Children.Add(filters);
        void Field(string label,Control control){var panel=new StackPanel{Margin=new Thickness(0,0,10,4)};panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,0,0,4)});panel.Children.Add(control);filters.Children.Add(panel);System.Windows.Automation.AutomationProperties.SetName(control,label);}
        Field("Loại thư viện",KindFilter);Field("Trạng thái",StatusFilter);Field("Tình trạng chọn",SelectionFilter);Field("Tìm mã, tên, nhóm",SearchFilter);
        KindFilter.ItemsSource=new[]{new Pick{Key="",Text="Tất cả loại"}}.Concat(LibrarySyncRules.Kinds.Select(k=>new Pick{Key=k,Text=LibraryFrames.KindName(k)+" ("+Rows.Count(r=>r.Kind==k)+")"})).ToList();
        StatusFilter.ItemsSource=new[]{new Pick{Key="",Text="Tất cả trạng thái"}}.Concat(Rows.GroupBy(r=>r.Difference.Value<string>("Status")??"").Select(g=>new Pick{Key=g.Key,Text=Caption(g.First().Difference)+" ("+g.Count()+")"})).ToList();
        SelectionFilter.ItemsSource=new[]{new Pick{Key="",Text="Tất cả"},new Pick{Key="selected",Text="Đã tích chọn"},new Pick{Key="unselected",Text="Chưa tích chọn"}};
        KindFilter.SelectedIndex=StatusFilter.SelectedIndex=SelectionFilter.SelectedIndex=0;
        DockPanel.SetDock(SelectionSummary,Dock.Top);root.Children.Add(SelectionSummary);
        var footer=new StackPanel();var error=new TextBlock{TextWrapping=TextWrapping.Wrap};footer.Children.Add(error);var actions=new WrapPanel{Margin=new Thickness(0,10,0,0)};footer.Children.Add(actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        Filtered=new ListCollectionView(Rows);Filtered.Filter=o=>Matches((Row)o);
        var grid=new DataGrid{ItemsSource=Filtered,AutoGenerateColumns=false,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Single};ScrollViewer.SetHorizontalScrollBarVisibility(grid,ScrollBarVisibility.Auto);root.Children.Add(grid);
        var check=new FrameworkElementFactory(typeof(CheckBox));check.SetBinding(CheckBox.IsCheckedProperty,new Binding("Selected"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});check.SetBinding(IsEnabledProperty,new Binding("CanSelect"));
        grid.Columns.Add(new DataGridTemplateColumn{Header="Nhận",CellTemplate=new DataTemplate{VisualTree=check},Width=55});
        foreach(var field in new[]{("Loại","KindName"),("Trạng thái","Caption"),("Tài nguyên / nhóm","Name"),("Nhóm / đường dẫn","Group")}){var textStyle=new Style(typeof(TextBlock));textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));textStyle.Setters.Add(new Setter(ToolTipProperty,new Binding(field.Item2)));grid.Columns.Add(new DataGridTextColumn{Header=field.Item1,Binding=new Binding(field.Item2),ElementStyle=textStyle,IsReadOnly=field.Item2!="Group",Width=field.Item2 is "Name" or "Caption"?260:170});}
        void Choices(string header,string collection,string selected)
        {
            var combo=new FrameworkElementFactory(typeof(ComboBox));combo.SetBinding(IsEnabledProperty,new Binding("CanLink"));combo.SetBinding(ItemsControl.ItemsSourceProperty,new System.Windows.Data.Binding(collection));combo.SetValue(ComboBox.DisplayMemberPathProperty,"Text");combo.SetValue(ComboBox.SelectedValuePathProperty,"Key");combo.SetBinding(ComboBox.SelectedValueProperty,new System.Windows.Data.Binding(selected){Mode=System.Windows.Data.BindingMode.TwoWay,UpdateSourceTrigger=System.Windows.Data.UpdateSourceTrigger.PropertyChanged});grid.Columns.Add(new DataGridTemplateColumn{Header=header,CellTemplate=new DataTemplate{VisualTree=combo},Width=230});
        }
        Choices("Liên kết ID cũ khi mất dấu","Bindings","BindId");Choices("Gộp danh mục hatch về","Merges","MergeId");
        void Refresh(){grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);Filtered.Refresh();UpdateSummary();}
        KindFilter.SelectionChanged+=(_,_)=>Refresh();StatusFilter.SelectionChanged+=(_,_)=>Refresh();SelectionFilter.SelectionChanged+=(_,_)=>Refresh();SearchFilter.TextChanged+=(_,_)=>Refresh();
        bool refreshQueued=false;
        foreach(var row in Rows)row.PropertyChanged+=(_,_)=>{if(refreshQueued)return;refreshQueued=true;Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{refreshQueued=false;Filtered.Refresh();UpdateSummary();}));};
        UpdateSummary();
        void Button(string text,Action action){var b=new Button{Content=text,Margin=new Thickness(0,0,8,0),Padding=new Thickness(10,7,10,7)};b.Click+=(_,_)=>action();actions.Children.Add(b);}
        Button("Xem thông số hai phía",()=>{if(grid.SelectedItem is Row item){var view=new Window{Title="Đối chiếu · "+item.Name,Width=780,Height=600,Owner=this,Resources=Theme.Load(),Content=new TextBox{Text="Kho:\n"+item.Before+"\n\nCAD:\n"+item.After,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};view.ShowDialog();}});
        Button("Chọn các mục đang hiển thị",()=>{foreach(var r in Filtered.Cast<Row>().ToArray())if(r.CanSelect)r.Selected=true;Refresh();});
        Button("Bỏ chọn các mục đang hiển thị",()=>{foreach(var r in Filtered.Cast<Row>().ToArray())r.Selected=false;Refresh();});
        Button("Xóa bộ lọc",()=>{KindFilter.SelectedIndex=StatusFilter.SelectedIndex=SelectionFilter.SelectedIndex=0;SearchFilter.Clear();Refresh();});
        Button("Chọn hatch ngoài khung để loại khỏi kho",()=>{foreach(var row in Filtered.Cast<Row>().Where(r=>r.OutOfScope&&r.Kind=="hatches"&&r.Difference["App"] is JObject).ToArray())row.Selected=true;Refresh();error.Text="Các hatch ngoài khung được chọn sẽ bị loại khỏi danh mục sử dụng; giữ nguyên hatch trong CAD. Liên kết đang dùng được kiểm tra trước khi loại.";});
        Button("Xác nhận cập nhật",()=>{try{grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);Plan=BuildPlan();DialogResult=true;}catch(Exception e){error.Text=e.Message;}});
        Button("Hủy",()=>{DialogResult=false;});
    }
    internal bool Matches(Row row)
    {
        string Key(ComboBox combo)=>(combo.SelectedItem as Pick)?.Key??"";
        return (Key(KindFilter)==""||row.Kind==Key(KindFilter))&&(Key(StatusFilter)==""||row.Difference.Value<string>("Status")==Key(StatusFilter))&&(Key(SelectionFilter)==""||row.Selected==(Key(SelectionFilter)=="selected"))&&(SearchFilter.Text.Trim()==""||(row.Code+" "+row.Name+" "+row.Group+" "+row.Before+" "+row.After).IndexOf(SearchFilter.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)>=0);
    }
    void UpdateSummary()=>SelectionSummary.Text=$"Hiển thị {Filtered.Count}/{Rows.Count} mục · Đã tích chọn {Rows.Count(r=>r.Selected)} mục, gồm {Rows.Count(r=>r.Selected&&!Matches(r))} mục đang ẩn bởi bộ lọc.";
    internal JObject BuildPlan()
    {
        var all=Rows.Where(r=>r.Selected).ToList();var groupChanges=new JArray(all.Where(r=>r.IsGroup&&r.Difference.Value<string>("Status")!="GROUP_MATCH").Select(r=>r.Difference["GroupRequest"]!.DeepClone()));var selected=all.Where(r=>!r.IsGroup).ToList();if(all.Count==0)throw new InvalidOperationException("Chọn ít nhất một mục cần tiếp nhận hoặc trình bày lại.");
        var destinations=new HashSet<string>();
        foreach(var r in selected)
        {
            if(r.Group.Any(char.IsControl))throw new InvalidOperationException("Đường dẫn nhóm không chứa ký tự điều khiển.");
            var parts=r.Group.Split('/').Select(p=>p.Trim()).Where(p=>p!="").ToArray();if(parts.Any(p=>p.Length>140))throw new InvalidOperationException("Tên một nhóm dài tối đa 140 ký tự.");r.Group=string.Join(" / ",parts);
            if(r.OutOfScope&&(r.Kind!="hatches"||r.Difference["App"] is not JObject))throw new InvalidOperationException("Chỉ loại được hatch ngoài khung đã có hồ sơ trong kho. Các loại khác cần đưa mẫu về khung để đối chiếu.");
            if(r.DefinitionOnly)throw new InvalidOperationException("Block "+r.Name+" chỉ còn định nghĩa. Chèn mẫu trên CAD hoặc PURGE; QA không tự tạo block chèn.");
            if(r.BindId!=""&&(!r.Bindings.Any(p=>p.Key==r.BindId)||!destinations.Add(r.BindId)))throw new InvalidOperationException("ID liên kết không hợp lệ hoặc bị chọn cho nhiều mục.");
            if(r.MergeId!=""&&(!r.Merges.Any(p=>p.Key==r.MergeId)||selected.Any(other=>other.Id==r.MergeId&&other.MergeId!="")))throw new InvalidOperationException("Chọn một hatch đích giữ lại, không gộp vòng hoặc gộp dây chuyền.");
        }
        var rebound=new HashSet<string>(selected.Where(r=>r.BindId!="").Select(r=>r.BindId));
        return new JObject{["groupChanges"]=groupChanges,["rows"]=new JArray(selected.Where(r=>!rebound.Contains(r.Id)).Select(r=>new JObject{["id"]=r.Id,["kind"]=r.Kind,["name"]=r.Name,["action"]=r.OutOfScope?"exclude":r.Missing?"remove":"cad",["new"]=r.Difference["App"] is not JObject&&r.BindId=="",["bindId"]=r.BindId,["mergeId"]=r.MergeId,["group"]=r.BindId!=""&&r.Group==""?Rows.Single(old=>old.Id==r.BindId).Group:r.Group,["native"]=r.Difference["Cad"]?.DeepClone(),["app"]=r.BindId!=""?Rows.Single(old=>old.Id==r.BindId).Difference["App"]?.DeepClone():r.Difference["App"]?.DeepClone()})),["deferred"]=Rows.Count(r=>!r.Selected&&r.Difference.Value<string>("Status") is not ("MATCH" or "CAD_DEFINITION_ONLY" or "CAD_OUT_OF_SCOPE" or "GROUP_MATCH"))};
    }
}
