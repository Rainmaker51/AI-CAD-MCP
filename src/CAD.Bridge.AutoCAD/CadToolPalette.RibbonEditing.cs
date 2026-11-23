using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;

namespace AICad.Bridge;
internal sealed partial class CadToolPalette
{
 sealed partial class ToolPage
 {
  readonly DataGrid ribbonList=new(){AutoGenerateColumns=false,SelectionMode=DataGridSelectionMode.Extended,SelectionUnit=DataGridSelectionUnit.FullRow,MinHeight=120,MaxHeight=240,CanUserAddRows=false,CanUserDeleteRows=false};
  readonly ComboBox mainCommand=new();
  Button? undoRibbon;
  List<PinDraft> pinDrafts=new();
  string mainTool="";
  public sealed class PinDraft
  {
   public string Command{get;set;}="";
   public bool Pinned{get;set;}
   public string Label{get;set;}="";
   public string Group{get;set;}="";
   public string TabId{get;set;}="";
   internal CadRibbonPin Original=new();
   internal CadRibbonPin Pin(){var p=JsonUtil.Read<CadRibbonPin>(JsonUtil.Write(Original));p.Command=Command;p.Label=Label;p.Group=Group;p.TabId=TabId;return p;}
  }
  sealed class TabChoice(string id,string name){public string Id=>id;public string Name=>name;}
  void BuildRibbonManagement(Panel panel)
  {
   void Pair(FrameworkElement field,Button button){var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star),MinWidth=100});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});field.Margin=new Thickness(0,0,6,6);row.Children.Add(field);Grid.SetColumn(button,1);row.Children.Add(button);panel.Children.Add(row);}
   Pair(toolTab,Button("Chuyển toàn bộ nút",()=>owner.Guard(MoveAll)));
   panel.Children.Add(Label("Lệnh mở bảng chính"));
   System.Windows.Automation.AutomationProperties.SetName(mainCommand,"Lệnh mở bảng chính của add-in");
   Pair(mainCommand,Button("Chỉ ghim lệnh mở bảng",()=>owner.Guard(PinMain),true));
   panel.Children.Add(Label("Các nút Ribbon · sửa checkbox, tên, tab hoặc nhóm rồi Lưu các nút"));
   var check=new FrameworkElementFactory(typeof(CheckBox));check.SetValue(FrameworkElement.StyleProperty,owner.theme["ToolEnabledCheckBox"]);check.SetBinding(ToggleButton.IsCheckedProperty,new Binding(nameof(PinDraft.Pinned)){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
   check.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty,new Binding(nameof(PinDraft.Command)){StringFormat="Ghim {0}"});
   ribbonList.Columns.Add(new DataGridTemplateColumn{Header="Ghim",Width=45,CellTemplate=new DataTemplate{VisualTree=check}});
   ribbonList.Columns.Add(new DataGridTextColumn{Header="Lệnh",Binding=new Binding(nameof(PinDraft.Command)),IsReadOnly=true,Width=140,ElementStyle=(Style)owner.theme["ToolCellText"]});
   ribbonList.Columns.Add(new DataGridTextColumn{Header="Tên nút",Binding=new Binding(nameof(PinDraft.Label)),Width=120});
   ribbonList.Columns.Add(new DataGridComboBoxColumn{Header="Tab Ribbon",SelectedValueBinding=new Binding(nameof(PinDraft.TabId)){Mode=BindingMode.TwoWay},SelectedValuePath="Id",DisplayMemberPath="Name",Width=150});
   ribbonList.Columns.Add(new DataGridTextColumn{Header="Nhóm (trống = theo add-in)",Binding=new Binding(nameof(PinDraft.Group)),Width=150});
   System.Windows.Automation.AutomationProperties.SetName(ribbonList,"Danh sách lệnh để ghim hoặc bỏ ghim Ribbon");panel.Children.Add(ribbonList);
   var actions=new WrapPanel();actions.Children.Add(Button("Ghim mục chọn",()=>owner.Guard(()=>SetChecked(true))));actions.Children.Add(Button("Bỏ ghim mục chọn",()=>owner.Guard(()=>SetChecked(false))));actions.Children.Add(Button("Lưu các nút",()=>owner.Guard(SaveRibbonList),true));
   actions.Children.Add(Button("Bỏ ghim tất cả",()=>owner.Guard(()=>ApplyLayout(Demand().TabId,new List<CadRibbonPin>(),"UNPIN_ALL","Đã bỏ ghim tất cả nút của add-in; công cụ vẫn được giữ."))));
   undoRibbon=Button("Hoàn tác Ribbon gần nhất",()=>owner.Guard(()=>{var tool=Demand();owner.catalog.UndoRibbon(tool.Id,index.Revision);FinishRibbon("Đã hoàn tác cấu hình Ribbon gần nhất.");}));actions.Children.Add(undoRibbon);panel.Children.Add(actions);
   panel.Children.Add(Label("Bỏ ghim chỉ ẩn nút, không xóa add-in. Lệnh chưa được nạp vẫn có thể cấu hình nếu đã có trong danh sách lưu."));
  }
  void BindRibbonList()
  {
   if(selected==null){ribbonList.ItemsSource=null;mainCommand.ItemsSource=null;if(undoRibbon!=null)undoRibbon.IsEnabled=false;return;}
   string? previous=mainTool==selected.Id?mainCommand.SelectedItem as string:null;mainTool=selected.Id;
   mainCommand.ItemsSource=selected.Commands;
   string? suggested=selected.Commands.FirstOrDefault(c=>c=="QALIBRARY")??selected.Commands.FirstOrDefault(c=>c==CadToolCatalog.AxisCommand)??(selected.RibbonPins.Count==1?selected.RibbonPins[0].Command:selected.Commands.Count==1?selected.Commands[0]:null);
   mainCommand.SelectedItem=previous!=null&&selected.Commands.Contains(previous)?previous:suggested;
   loaded.TryGetValue(selected.Id,out var descriptors);
   pinDrafts=selected.Commands.Concat(selected.RibbonPins.Select(p=>p.Command)).Distinct(StringComparer.OrdinalIgnoreCase).Select((command,i)=>
   {
    var existing=selected.RibbonPins.FirstOrDefault(p=>p.Command==command);
    var p=existing??descriptors?.FirstOrDefault(d=>d.Name==command)?.SuggestedPin??new CadRibbonPin{Command=command,Label=command,Order=i*10};
    return new PinDraft{Command=command,Pinned=existing!=null,Label=p.Label,Group=p.Group,TabId=p.TabId,Original=p};
   }).ToList();
   var choices=new List<TabChoice>{new("","Theo tab add-in · "+(index.Tabs.FirstOrDefault(t=>t.Id==selected.TabId)?.Name??"Rainmaker"))};choices.AddRange(index.Tabs.OrderBy(t=>t.Order).Select(t=>new TabChoice(t.Id,t.Name+(!t.Enabled&&t.Id!=CadToolCatalog.DefaultTabId?" (ẩn)":""))));
   ((DataGridComboBoxColumn)ribbonList.Columns[3]).ItemsSource=choices;ribbonList.ItemsSource=pinDrafts;
   if(undoRibbon!=null)undoRibbon.IsEnabled=owner.catalog.CanUndoRibbon(selected.Id);
  }
  void CommitRibbonDraft(){ribbonList.CommitEdit(DataGridEditingUnit.Cell,true);ribbonList.CommitEdit(DataGridEditingUnit.Row,true);}
  void SetChecked(bool value){CommitRibbonDraft();var rows=ribbonList.SelectedItems.Cast<PinDraft>().ToArray();if(rows.Length==0)throw new CadException("TOOL_COMMAND","Chọn một hoặc nhiều dòng lệnh.");foreach(var row in rows)row.Pinned=value;ribbonList.Items.Refresh();}
  void SaveRibbonList(){CommitRibbonDraft();ApplyLayout(Demand().TabId,pinDrafts.Where(p=>p.Pinned).Select(p=>p.Pin()),"EDIT_LIST","Đã lưu danh sách nút Ribbon.");}
  void PinMain()
  {
   var tool=Demand();string command=mainCommand.SelectedItem as string??throw new CadException("TOOL_COMMAND","Chọn lệnh mở bảng chính trước.");
   var row=pinDrafts.Single(p=>p.Command==command);var pin=row.Pin();pin.TabId="";
   if(string.IsNullOrWhiteSpace(pin.Label)||pin.Label==command)pin.Label=tool.Name;
   ApplyLayout((toolTab.SelectedItem as CadRibbonTab)?.Id??tool.TabId,new[]{pin},"MAIN_ONLY","Đã chỉ ghim một nút mở bảng: "+command);
  }
  void MoveAll()
  {
   var tool=Demand();string target=(toolTab.SelectedItem as CadRibbonTab)?.Id??throw new CadException("RIBBON_TAB","Chọn tab đích.");
   owner.catalog.MoveRibbon(tool.Id,index.Revision,target);FinishRibbon("Đã chuyển "+tool.RibbonPins.Count+" nút sang "+index.Tabs.Single(t=>t.Id==target).Name+".",target);
  }
  void ApplyLayout(string tab,IEnumerable<CadRibbonPin> pins,string reason,string message){var tool=Demand();owner.catalog.SetRibbonLayout(tool.Id,index.Revision,tab,pins,reason);FinishRibbon(message,tab);}
  void FinishRibbon(string message,string? tab=null){Refresh();owner.plugin.Ribbon?.Invalidate();owner.plugin.Ribbon?.Update();if(tab!=null)owner.plugin.Ribbon?.ActivateTab(tab);owner.Say(message);}
  sealed class LocationConverter(Func<CadToolIndex> current):IValueConverter
  {
   public object Convert(object value,Type type,object parameter,CultureInfo culture)=>value is CadToolEntry tool?CadToolRibbonSetup.Location(current(),tool):"";
   public object ConvertBack(object value,Type type,object parameter,CultureInfo culture)=>throw new NotSupportedException();
  }
 }
}
