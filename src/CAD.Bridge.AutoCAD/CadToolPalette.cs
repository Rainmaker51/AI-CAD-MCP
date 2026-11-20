using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Markup;
using System.Reflection;
using System.Windows.Automation;
using AICad.Contracts;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;
namespace AICad.Bridge;
internal sealed partial class CadToolPalette:IDisposable {
 readonly ResourceDictionary theme;readonly Plugin plugin;readonly CadToolCatalog catalog;readonly CadToolRuntime runtime;readonly PaletteSet palette;readonly TabControl tabs=new();readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};readonly ToolPage addins;
 sealed class Pending {internal string Id="",Command="",Package="";internal bool Load,Reload,ResolveCommand,Ribbon;internal Document Document=null!;}
 Pending? pending;
 static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(230,237,247)),Surface=new SolidColorBrush(Color.FromRgb(16,23,32));
 internal CadToolPalette(Plugin plugin){
  this.plugin=plugin;catalog=new CadToolCatalog(plugin.Config.ContentRoot);runtime=new CadToolRuntime(plugin.Config,QueueManaged);
  palette=new PaletteSet("CAD TOOL MANAGER",new Guid("462d0f71-9acd-40d3-a902-b0e8cd019fdc")){MinimumSize=new System.Drawing.Size(420,640),Size=new System.Drawing.Size(620,860),Style=PaletteSetStyles.ShowCloseButton|PaletteSetStyles.ShowAutoHideButton|PaletteSetStyles.ShowPropertiesMenu};
  var root=new DockPanel{Background=Surface,Margin=new Thickness(0),LastChildFill=true,UseLayoutRounding=true,SnapsToDevicePixels=true};var assembly=Assembly.GetExecutingAssembly();using(var theme=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith(".CadToolTheme.xaml"))))root.Resources=(ResourceDictionary)XamlReader.Load(theme!);this.theme=root.Resources;root.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty,new FontFamily("Segoe UI"));var header=new StackPanel{Margin=new Thickness(14,12,14,8)};header.Children.Add(Label("CAD TOOL MANAGER",20,true));var subtitle=Label("AutoCAD 2021  /  Quản lí công cụ dùng chung",12);header.Children.Add(subtitle);root.SizeChanged+=(_,__)=>subtitle.Visibility=root.ActualHeight<740?Visibility.Collapsed:Visibility.Visible;DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  var foot=new Border{Padding=new Thickness(12,8,12,8),Background=new SolidColorBrush(Color.FromRgb(25,35,49)),Child=status};status.Foreground=Ink;status.MaxHeight=32;status.TextTrimming=TextTrimming.CharacterEllipsis;DockPanel.SetDock(foot,Dock.Bottom);root.Children.Add(foot);root.Children.Add(tabs);
  addins=new ToolPage(this);
  tabs.Items.Add(new TabItem{Header=".NET Add-in",Content=addins.Root});
  tabs.Items.Add(new TabItem{Header="MCP",Content=McpPage()});var ribbonPage=new RibbonPage(this);tabs.Items.Add(new TabItem{Header="Ribbon",Content=ribbonPage.Root});tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs&&tabs.SelectedIndex==2)ribbonPage.Refresh();};palette.AddVisual("Quản lí công cụ",root);Say("Chọn công cụ để chạy trên DWG đang mở, hoặc thêm DLL .NET Framework 4.8.");
 }
 static TextBlock Label(string text,double size=12,bool bold=false)=>new(){Text=text,Foreground=Ink,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,5)};
 static Button Button(string text,Action click,bool primary=false){var button=new Button{Content=text,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,6,6),MinHeight=34};if(primary)button.SetResourceReference(FrameworkElement.StyleProperty,"ToolPrimaryButton");AutomationProperties.SetName(button,text);button.Click+=(_,__)=>click();return button;}
 void Guard(Action work){try{work();}catch(System.Exception e){Say("Lỗi: "+e.Message);AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nCAD Tool Manager: "+e.Message);}}
 void Say(string message){status.Text=message;status.ToolTip=message;try{string folder=Path.Combine(plugin.Config.DataRoot,"tool-manager");Directory.CreateDirectory(folder);File.AppendAllText(Path.Combine(folder,"activity.jsonl"),new JObject{["utc"]=DateTime.UtcNow,["message"]=message}.ToString(Newtonsoft.Json.Formatting.None)+Environment.NewLine);}catch(IOException){}}
 internal void Show(int tab=0){tabs.SelectedIndex=tab;addins.Refresh();palette.Visible=true;}
 internal void Synchronize(CadToolIndex index)=>runtime.Synchronize(index);
 internal void ExecutePending(){
  var work=pending??throw new CadException("TOOL_COMMAND","Không có công cụ chờ chạy.");pending=null;
  Guard(()=>{
   var document=AcApp.DocumentManager.MdiActiveDocument;if(document!=work.Document)throw new CadException("WRONG_DOCUMENT","DWG đã thay đổi. Chọn và chạy lại công cụ.");
   var tool=FindTool(work.Id);if(tool.PackageHash!=work.Package)throw new CadException("TOOL_CHANGED","Gói công cụ đã được cập nhật. Làm mới và nạp lại.");
   if(work.Ribbon)DemandRibbonPin(tool,work.Command);
   if(work.ResolveCommand){
    var discovered=runtime.Load(tool).Commands;
    addins.Loaded(tool,discovered);
    var command=discovered.SingleOrDefault(c=>string.Equals(c.Name,work.Command,StringComparison.OrdinalIgnoreCase))
     ??throw new CadException("TOOL_COMMAND","Lệnh này không còn trong DLL vừa cập nhật.");
    work.Command=command.Name;
    if((command.Flags&CommandFlags.Session)==0){
     work.ResolveCommand=false;
     pending=work;
     try{document.SendStringToExecute("AICADTOOLRUN\n",true,false,false);}catch{pending=null;throw;}
     return;
    }
   }
   if(work.Load){var loaded=runtime.Load(tool,work.Reload);addins.Loaded(tool,loaded.Commands);var index=catalog.Read();Say("Đã nạp "+tool.Name+" · "+loaded.Commands.Count+" lệnh. "+CadToolRibbonSetup.Status(index,index.Items.Single(t=>t.Id==tool.Id)));}
   else{runtime.Run(tool,work.Command);Say("Đã thực thi "+work.Command+" · "+Path.GetFileName(document.Name));}
  });
 }
 CadToolEntry FindTool(string id)=>catalog.Read().Items.SingleOrDefault(t=>t.Id==id&&t.Kind=="dotnet")
  ??throw new CadException("TOOL_MISSING","Công cụ không còn trong danh mục.");
 static void DemandRibbonPin(CadToolEntry tool,string command){
  if(!tool.RibbonPins.Any(p=>string.Equals(p.Command,command,StringComparison.OrdinalIgnoreCase))||!tool.Commands.Contains(command,StringComparer.OrdinalIgnoreCase))
   throw new CadException("TOOL_COMMAND","Lệnh này đã bỏ ghim hoặc không còn trong danh mục.");
 }
 internal void QueueRibbon(string id,string command)=>Guard(()=>{
  var tool=FindTool(id);
  DemandRibbonPin(tool,command);
  Queue(tool,command,false,false,true,true);
 });
 void QueueManaged(string id,string command)=>Guard(()=>{var tool=FindTool(id);Queue(tool,command,false,false,true,false,true);});
 void Queue(CadToolEntry tool,string command,bool load,bool reload,bool session=false,bool ribbon=false,bool resolveCommand=false){Guard(()=>{
  if(pending!=null||Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE"))!=0)throw new CadException("TOOL_BUSY","Kết thúc lệnh AutoCAD hiện tại rồi thử lại.");
  var document=AcApp.DocumentManager.MdiActiveDocument??throw new CadException("DOCUMENT_REQUIRED","Mở DWG đích trước khi nạp/chạy.");catalog.Demand(tool);pending=new Pending{Id=tool.Id,Command=command,Package=tool.PackageHash,Load=load,Reload=reload,Ribbon=ribbon,ResolveCommand=ribbon||resolveCommand,Document=document};
  try{document.SendStringToExecute(session||load?"AICADTOOLSESSION\n":"AICADTOOLRUN\n",true,false,false);}catch{pending=null;throw;}Say("Đang chờ AutoCAD xử lí "+tool.Name+"…");
 });}
 FrameworkElement McpPage(){
  var grid=new Grid{Margin=new Thickness(12,0,12,12)};foreach(var height in new[]{GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto})grid.RowDefinitions.Add(new RowDefinition{Height=height});
  var intro=new StackPanel();intro.Children.Add(Label("Kết nối trợ lí AI",17,true));intro.Children.Add(Label("Chọn ứng dụng và sao chép cấu hình kết nối.",12));var connection=Label("");intro.Children.Add(connection);grid.Children.Add(intro);
  var choice=new StackPanel{Margin=new Thickness(0,8,0,10)};choice.Children.Add(Label("Ứng dụng kết nối"));var clients=new ComboBox{ItemsSource=new[]{"Claude Desktop · JSON","Codex · TOML"},SelectedIndex=0};AutomationProperties.SetName(clients,"Ứng dụng kết nối MCP");choice.Children.Add(clients);Grid.SetRow(choice,1);grid.Children.Add(choice);
  var configBox=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=150,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas"),FontSize=13};AutomationProperties.SetName(configBox,"Cấu hình MCP để sao chép");Grid.SetRow(configBox,2);grid.Children.Add(configBox);
  var actions=new WrapPanel{Margin=new Thickness(0,12,0,0)};actions.Children.Add(Button("Sao chép cấu hình",()=>Guard(()=>{Clipboard.SetText(configBox.Text);Say("Đã sao chép cấu hình MCP. Gộp vào cấu hình ứng dụng bạn chọn.");}),true));
  void Refresh(){Guard(()=>{var config=CadToolMcpConfig.Create(plugin.Config);configBox.Text=clients.SelectedIndex==0?config["claudeDesktop"]!.ToString():config.Value<string>("codexToml");string host=Path.Combine(plugin.Config.DataRoot,"host.json");int sessions=Directory.Exists(Path.Combine(plugin.Config.DataRoot,"sessions"))?Directory.GetFiles(Path.Combine(plugin.Config.DataRoot,"sessions"),"*.json").Count(f=>(DateTime.UtcNow-File.GetLastWriteTimeUtc(f)).TotalSeconds<20):0;connection.Text=(config.Value<bool>("installed")?"Server đã cài đặt":"Server chưa được cài đặt")+"  ·  "+sessions+" phiên AutoCAD trực tuyến\n"+(File.Exists(host)?"Host có cấu hình phiên cục bộ.":"Host chưa khởi động.");});}
  clients.SelectionChanged+=(_,__)=>Refresh();actions.Children.Add(Button("Làm mới",Refresh));actions.Children.Add(Button("Mở AI CAD",()=>Guard(()=>{var host=JObject.Parse(File.ReadAllText(Path.Combine(plugin.Config.DataRoot,"host.json")));var uri=new Uri(host.Value<string>("url")!);if(uri.Scheme!="http"||uri.Host!="127.0.0.1")throw new CadException("UNTRUSTED_HOST","Host không phải loopback.");Process.Start(new ProcessStartInfo(new Uri(uri,"/?page=work#token="+host.Value<string>("token")).ToString()){UseShellExecute=true});})));Grid.SetRow(actions,3);grid.Children.Add(actions);
  var help=new Expander{Header="Cách kết nối",Content=Label("1. Gộp cấu hình vào ứng dụng đã chọn.\n2. Kết nối lại MCP trong Codex hoặc Claude Desktop.\n3. Kiểm tra trạng thái kết nối tại ứng dụng đó.\n\nMCP tra cứu danh mục công cụ và dùng luồng duyệt CAD hiện có. Nạp/chạy DLL được thực hiện tại AutoCAD.",12)};Grid.SetRow(help,4);grid.Children.Add(help);Refresh();return grid;
 }
 sealed class ToolPage {
  readonly CadToolPalette owner;readonly DataGrid list=new(){AutoGenerateColumns=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Single,MinHeight=110};readonly TextBox search=new(),name=new(),group=new(),notes=new(){AcceptsReturn=true,MinHeight=58,TextWrapping=TextWrapping.Wrap},path=new(){IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,FontFamily=new FontFamily("Consolas"),FontSize=12};readonly ComboBox commands=new();
  readonly ComboBox pinIcon=new(){ItemsSource=RainmakerRibbon.Icons};
  readonly ComboBox toolTab=new(){DisplayMemberPath="Name"},pinTab=new(){DisplayMemberPath="Name"};
  readonly TextBox pinName=new(){MaxLength=60},pinGroup=new(){MaxLength=60},pinDescription=new(){MaxLength=500},pinOrder=new(){MaxLength=4};
  readonly TextBlock ribbonState=Label("");readonly Button pinAll;
  readonly CheckBox pinned=new(){Content="Ghim lệnh đã chọn"};
  readonly CheckBox inheritGroup=new(){Content="Dùng nhóm công cụ trên Ribbon"};
  readonly Expander ribbonEditor=new(){Header="Ribbon · Tab và nút lệnh"};readonly CheckBox enabled=new(){Content="Cho phép nạp/chạy"};readonly CheckBox trash=new(){Content="Xem thùng rác"};readonly TextBlock count=Label(""),empty=Label(""),state=Label("");readonly Border details;readonly RowDefinition detailRow=new(){Height=new GridLength(0)};readonly StackPanel runArea=new();readonly Button add,update,save,load,run,remove,location;CadToolIndex index=new();CadToolEntry? selected;readonly Dictionary<string,List<CadToolCommand>> loaded=new();readonly Dictionary<string,string> loadedPackages=new();internal FrameworkElement Root;
  internal ToolPage(CadToolPalette owner){this.owner=owner;
   var grid=new Grid{Margin=new Thickness(12,0,12,10)};grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star),MinHeight=110});grid.RowDefinitions.Add(detailRow);grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var toolbar=new WrapPanel();add=Button("Thêm file",()=>owner.Guard(()=>Import(false)));update=Button("Cập nhật file",()=>owner.Guard(()=>Import(true)));toolbar.Children.Add(add);toolbar.Children.Add(update);toolbar.Children.Add(Button("Làm mới",Refresh));grid.Children.Add(toolbar);
   var filter=new StackPanel{Margin=new Thickness(0,3,0,8)};filter.Children.Add(Label("Tìm công cụ"));AutomationProperties.SetName(search,"Tìm tên, nhóm, công dụng hoặc tên lệnh");search.ToolTip="Tìm theo tên, nhóm, công dụng hoặc tên lệnh";filter.Children.Add(search);var filterFoot=new DockPanel();DockPanel.SetDock(trash,Dock.Right);filterFoot.Children.Add(trash);filterFoot.Children.Add(count);filter.Children.Add(filterFoot);Grid.SetRow(filter,1);grid.Children.Add(filter);search.TextChanged+=(_,__)=>Refresh();trash.Checked+=(_,__)=>Refresh();trash.Unchecked+=(_,__)=>Refresh();
   list.Columns.Add(new DataGridTextColumn{Header="Công cụ",ElementStyle=(Style)owner.theme["ToolCellText"],Binding=new Binding("Name"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});list.Columns.Add(new DataGridTextColumn{Header="Nhóm công cụ",ElementStyle=(Style)owner.theme["ToolCellText"],Binding=new Binding("Group"),Width=new DataGridLength(0.6,DataGridLengthUnitType.Star)});list.Columns.Add(CreateEnabledColumn());AutomationProperties.SetName(list,"Danh sách add-in .NET");list.SizeChanged+=(_,e)=>{if(!e.WidthChanged)return;double available=Math.Max(240,list.ActualWidth-60);list.Columns[0].Width=new DataGridLength(Math.Floor(available*0.62));list.Columns[1].Width=new DataGridLength(Math.Floor(available*0.38));};list.SelectionChanged+=(_,__)=>Select();var listing=new Grid();listing.Children.Add(list);empty.TextAlignment=TextAlignment.Center;empty.HorizontalAlignment=HorizontalAlignment.Center;empty.VerticalAlignment=VerticalAlignment.Center;empty.Margin=new Thickness(20,38,20,10);empty.IsHitTestVisible=false;listing.Children.Add(empty);Grid.SetRow(listing,2);grid.Children.Add(listing);
   var form=new StackPanel();form.Children.Add(Label("Hồ sơ công cụ",14,true));var identity=new Grid();identity.ColumnDefinitions.Add(new ColumnDefinition());identity.ColumnDefinitions.Add(new ColumnDefinition());var a=new StackPanel{Margin=new Thickness(0,0,8,0)};a.Children.Add(Label("Tên hiển thị"));a.Children.Add(name);identity.Children.Add(a);var b=new StackPanel();b.Children.Add(Label("Nhóm công cụ"));b.Children.Add(group);Grid.SetColumn(b,1);identity.Children.Add(b);form.Children.Add(identity);AutomationProperties.SetName(name,"Tên hiển thị công cụ");AutomationProperties.SetName(group,"Nhóm công cụ");form.Children.Add(Label("Công dụng / ghi chú"));form.Children.Add(notes);AutomationProperties.SetName(notes,"Công dụng và ghi chú công cụ");form.Children.Add(enabled);
   AutomationProperties.SetName(path,"Đường dẫn file công cụ và SHA256");form.Children.Add(new Expander{Header="File nguồn và SHA256",Content=path});
   AddRibbonField(form,"Tab mặc định của add-in",toolTab);
   AutomationProperties.SetName(ribbonState,"Trạng thái nút Ribbon");form.Children.Add(ribbonState);
   pinAll=Button("Ghim tất cả lệnh lên Ribbon",()=>owner.Guard(PinAll));
   pinAll.ToolTip="Thêm nút còn thiếu vào tab đã chọn, giữ tên và cấu hình các nút đã ghim. Không chạy DLL.";form.Children.Add(pinAll);
   var ribbonFields=new StackPanel();var ribbonCommands=new ComboBox();
   ribbonCommands.SetBinding(ItemsControl.ItemsSourceProperty,new Binding(nameof(ItemsControl.ItemsSource)){Source=commands});
   ribbonCommands.SetBinding(Selector.SelectedItemProperty,new Binding(nameof(Selector.SelectedItem)){Source=commands,Mode=BindingMode.TwoWay});
   AddRibbonField(ribbonFields,"Lệnh cần cấu hình",ribbonCommands);
   ribbonFields.Children.Add(Label("Dùng danh sách lệnh đã lưu để chỉnh Ribbon. Add-in mới chưa có lệnh cần Nạp / nạp lại một lần."));ribbonFields.Children.Add(pinned);
   ribbonFields.Children.Add(Label("Tạo/đổi tên/xóa tab trong trang Ribbon. Có thể chỉ định tab riêng cho từng lệnh."));
   AddRibbonField(ribbonFields,"Tab của lệnh",pinTab);
   AddRibbonField(ribbonFields,"Tên nút",pinName);ribbonFields.Children.Add(inheritGroup);AddRibbonField(ribbonFields,"Nhóm trên Ribbon",pinGroup);
   ribbonFields.Children.Add(Label("Dùng nhóm công cụ để hai nơi đồng bộ tên. Bỏ chọn để đặt nhóm Ribbon riêng."));
   inheritGroup.Checked+=(_,__)=>UpdatePinGroup();inheritGroup.Unchecked+=(_,__)=>UpdatePinGroup();group.TextChanged+=(_,__)=>{if(inheritGroup.IsChecked==true)UpdatePinGroup();};
   AddRibbonField(ribbonFields,"Công dụng của lệnh",pinDescription);AddRibbonField(ribbonFields,"Biểu tượng",pinIcon);
   AddRibbonField(ribbonFields,"Thứ tự trong nhóm (0–9999)",pinOrder);
   ribbonFields.Children.Add(Button("Lưu nút Ribbon",()=>owner.Guard(SavePin)));
   ribbonEditor.Content=ribbonFields;form.Children.Add(ribbonEditor);var minor=new WrapPanel{Margin=new Thickness(0,10,0,0)};save=Button("Lưu hồ sơ",()=>owner.Guard(Save));location=Button("Vị trí file",()=>owner.Guard(()=>Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+owner.catalog.PathFor(Demand())+"\""){UseShellExecute=true})));remove=Button("Xóa khỏi danh mục",()=>owner.Guard(()=>{var t=Demand();owner.catalog.Update(t.Id,index.Revision,t.Name,t.Group,t.Notes,t.Enabled,t.Commands,t.DeletedUtc.HasValue?"restore":"delete");if(!t.DeletedUtc.HasValue)owner.runtime.Disable(t.Id);Refresh();}));remove.Foreground=new SolidColorBrush(Color.FromRgb(253,164,175));minor.Children.Add(save);minor.Children.Add(location);minor.Children.Add(remove);form.Children.Add(minor);details=new Border{Padding=new Thickness(10),Margin=new Thickness(0,10,0,0),Background=new SolidColorBrush(Color.FromRgb(25,35,49)),CornerRadius=new CornerRadius(6),Child=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Visibility=Visibility.Collapsed};Grid.SetRow(details,3);grid.Children.Add(details);
   runArea.Margin=new Thickness(0,10,0,0);runArea.Children.Add(state);AutomationProperties.SetName(commands,"Chọn lệnh để chạy");commands.SelectionChanged+=(_,__)=>{ReadPin();UpdateActions();};runArea.Children.Add(commands);var primaryActions=new WrapPanel{Margin=new Thickness(0,8,0,0)};run=Button("Chạy lệnh",()=>owner.Guard(()=>{var t=Demand();string c=commands.SelectedItem?.ToString()??"";if(c=="")throw new CadException("TOOL_COMMAND","Chọn lệnh trước khi chạy.");bool session=loaded.TryGetValue(t.Id,out var values)&&values.Any(v=>v.Name==c&&(v.Flags&CommandFlags.Session)!=0);owner.Queue(t,c,false,false,session||!loaded.ContainsKey(t.Id),resolveCommand:!loaded.ContainsKey(t.Id));}),true);load=Button("Nạp / nạp lại",()=>owner.Guard(()=>owner.Queue(Demand(),"",true,true)));primaryActions.Children.Add(run);primaryActions.Children.Add(load);runArea.Children.Add(primaryActions);Grid.SetRow(runArea,4);grid.Children.Add(runArea);Root=grid;Refresh();
  }
  void UpdateActions(){if(run==null)return;UpdateRibbonState();bool has=selected!=null,active=has&&selected!.Enabled&&!selected.DeletedUtc.HasValue;ribbonEditor.IsEnabled=has&&!selected!.DeletedUtc.HasValue;update.IsEnabled=has;save.IsEnabled=has;remove.IsEnabled=has;location.IsEnabled=has;load.IsEnabled=active;commands.IsEnabled=active&&commands.Items.Count>0;run.IsEnabled=active&&commands.SelectedItem!=null;runArea.Visibility=has?Visibility.Visible:Visibility.Collapsed;details.Visibility=has?Visibility.Visible:Visibility.Collapsed;detailRow.Height=has?new GridLength(1.15,GridUnitType.Star):new GridLength(0);if(has){state.Text=selected!.DeletedUtc.HasValue?"Trong thùng rác · Khôi phục để sử dụng":!selected.Enabled?"Đang tắt · Bật và lưu hồ sơ để sử dụng":commands.Items.Count==0?"Chọn Nạp / nạp lại để đọc danh sách lệnh.":"Lệnh để chạy · DWG đang chọn";remove.Content=selected.DeletedUtc.HasValue?"Khôi phục":"Xóa khỏi danh mục";}add.SetResourceReference(FrameworkElement.StyleProperty,has?(object)typeof(Button):"ToolPrimaryButton");}
  void UpdateRibbonState(){ribbonState.Text=selected==null?"":CadToolRibbonSetup.Status(index,selected);pinAll.IsEnabled=selected!=null&&!selected.DeletedUtc.HasValue&&selected.Commands.Count>0;}
  CadToolEntry Demand()=>selected??throw new CadException("TOOL_REQUIRED","Chọn công cụ trong danh sách.");
  internal void Refresh(){owner.plugin.Ribbon?.Invalidate();owner.Guard(()=>{string? id=selected?.Id;string? command=commands.SelectedItem as string;index=owner.catalog.Read();string q=search.Text.Trim();var items=index.Items.Where(t=>t.Kind=="dotnet"&&t.DeletedUtc.HasValue==(trash.IsChecked==true)&&(q==""||(t.Name+" "+t.Group+" "+t.Notes+" "+string.Join(" ",t.Commands)).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(t=>t.Group).ThenBy(t=>t.Name).ToList();list.ItemsSource=items;list.SelectedItem=items.FirstOrDefault(t=>t.Id==id)??items.FirstOrDefault();if(selected?.Id==id&&command!=null&&commands.Items.Contains(command))commands.SelectedItem=command;count.Text=items.Count+" công cụ";empty.Visibility=items.Count==0?Visibility.Visible:Visibility.Collapsed;empty.Text=trash.IsChecked==true?"Thùng rác trống.":q!=""?"Không có kết quả phù hợp.\nThử đổi từ khóa hoặc bộ lọc.":"Chưa có công cụ.\nChọn Thêm file để nhập DLL .NET Framework 4.8.";if(items.Count==0){selected=null;name.Text=group.Text=notes.Text=path.Text="";commands.ItemsSource=null;}UpdateActions();});}
  void Select(){selected=list.SelectedItem as CadToolEntry;if(selected==null){UpdateActions();return;}if(loadedPackages.TryGetValue(selected.Id,out string package)&&package!=selected.PackageHash)loaded.Remove(selected.Id);toolTab.ItemsSource=index.Tabs.OrderBy(t=>t.Order).ThenBy(t=>t.Name).ToList();toolTab.SelectedItem=index.Tabs.FirstOrDefault(t=>t.Id==selected.TabId)??index.Tabs.First(t=>t.Id==CadToolCatalog.DefaultTabId);name.Text=selected.Name;group.Text=selected.Group;notes.Text=selected.Notes;enabled.IsChecked=selected.Enabled;path.Text=owner.catalog.PathFor(selected)+"\nSHA256: "+selected.FileHash;commands.ItemsSource=loaded.TryGetValue(selected.Id,out var c)?c.Select(x=>x.Name).ToList():selected.Commands.ToList();if(commands.Items.Count>0)commands.SelectedIndex=0;ReadPin();UpdateActions();}
  DataGridTemplateColumn CreateEnabledColumn(){
   var check=new FrameworkElementFactory(typeof(CheckBox));
   check.SetValue(FrameworkElement.StyleProperty,owner.theme["ToolEnabledCheckBox"]);
   check.SetBinding(ToggleButton.IsCheckedProperty,new Binding(nameof(CadToolEntry.Enabled)){Mode=BindingMode.OneWay});
   check.SetBinding(AutomationProperties.NameProperty,new Binding(nameof(CadToolEntry.Name)){StringFormat="Bật hoặc tắt {0}"});
   check.AddHandler(ButtonBase.ClickEvent,new RoutedEventHandler((sender,_)=>ToggleEnabled((CheckBox)sender)));
   return new DataGridTemplateColumn{Header="Bật",HeaderStyle=(Style)owner.theme["ToolEnabledHeader"],Width=55,CellTemplate=new DataTemplate{VisualTree=check}};
  }
  void ToggleEnabled(CheckBox check){
   if(!(check.DataContext is CadToolEntry tool))return;
   bool value=check.IsChecked==true;
   owner.Guard(()=>{
    try{owner.catalog.SetEnabled(tool.Id,index.Revision,value);}
    catch{check.SetCurrentValue(ToggleButton.IsCheckedProperty,tool.Enabled);throw;}
    tool.Enabled=value;
    if(!value)owner.runtime.Disable(tool.Id);
    index.Revision++;
    if(selected==tool){enabled.IsChecked=value;UpdateActions();}
    owner.plugin.Ribbon?.Invalidate();
    owner.Say((value?"Đã bật ":"Đã tắt ")+tool.Name+".");
   });
  }
  static void AddRibbonField(Panel panel,string label,FrameworkElement field){
   panel.Children.Add(Label(label));AutomationProperties.SetName(field,label);panel.Children.Add(field);
  }
  void ReadPin(){
   string? command=commands.SelectedItem as string;
   var pin=selected?.RibbonPins.FirstOrDefault(p=>string.Equals(p.Command,command,StringComparison.OrdinalIgnoreCase));
   pinned.IsChecked=pin!=null;pinName.Text=pin?.Label??command??"";inheritGroup.IsChecked=string.IsNullOrWhiteSpace(pin?.Group);pinGroup.Text=pin?.Group??"";UpdatePinGroup();

   var pinTabs=new List<CadRibbonTab>{new(){Id="",Name="Theo tab của add-in"}};pinTabs.AddRange(index.Tabs.OrderBy(t=>t.Order));pinTab.ItemsSource=pinTabs;pinTab.SelectedItem=pinTabs.FirstOrDefault(t=>t.Id==pin?.TabId)??pinTabs[0];
   pinDescription.Text=pin?.Description??"";pinOrder.Text=(pin?.Order??100).ToString();
   pinIcon.SelectedItem=RainmakerRibbon.Icons.Contains(pin?.Icon)?pin!.Icon:RainmakerRibbon.Icons[0];
  }
  void UpdatePinGroup(){pinGroup.IsEnabled=inheritGroup.IsChecked!=true;if(!pinGroup.IsEnabled)pinGroup.Text=group.Text;}
  void SavePin(){
   var tool=Demand();
   string command=commands.SelectedItem as string??throw new CadException("TOOL_COMMAND","Chọn lệnh cần ghim.");
   bool keep=pinned.IsChecked==true;
   int order=100;
   if(keep&&!int.TryParse(pinOrder.Text,out order))throw new CadException("TOOL_RIBBON","Nhập thứ tự từ 0 đến 9999.");
   owner.catalog.SetRibbonPin(tool.Id,index.Revision,keep?new CadRibbonPin{Command=command,Label=pinName.Text,Group=inheritGroup.IsChecked==true?"":pinGroup.Text,
     Description=pinDescription.Text,Icon=pinIcon.SelectedItem as string??"",Order=order,TabId=(pinTab.SelectedItem as CadRibbonTab)?.Id??""}:new CadRibbonPin{Command=command},keep);
   Refresh();
   owner.Say(keep?"Đã lưu nút trên tab ribbon đã chọn.":"Đã bỏ ghim lệnh khỏi ribbon.");
  }
  void Save(){var t=Demand();owner.catalog.Update(t.Id,index.Revision,name.Text,group.Text,notes.Text,enabled.IsChecked==true,t.Commands,tabId:(toolTab.SelectedItem as CadRibbonTab)?.Id??CadToolCatalog.DefaultTabId);if(enabled.IsChecked!=true)owner.runtime.Disable(t.Id);Refresh();owner.Say("Đã lưu hồ sơ và tab ribbon của add-in.");}
  void PinAll(){var t=Demand();loaded.TryGetValue(t.Id,out var values);CadToolRibbonSetup.PinAll(owner.catalog,index,t,(toolTab.SelectedItem as CadRibbonTab)?.Id??t.TabId,values);Refresh();owner.Say("Đã ghim các lệnh; giữ cấu hình nút cũ. "+CadToolRibbonSetup.Status(index,Demand()));}
  void Import(bool replace){var old=replace?Demand():null;var dialog=new Microsoft.Win32.OpenFileDialog{Title=replace?"Cập nhật công cụ":"Thêm công cụ",Filter=".NET Framework 4.8|*.dll",CheckFileExists=true};if(dialog.ShowDialog()!=true)return;var item=owner.catalog.Import(dialog.FileName,index.Revision,old?.Id);Refresh();list.SelectedItem=((IEnumerable<CadToolEntry>)list.ItemsSource).FirstOrDefault(t=>t.Id==item.Id);owner.Say("Đã cài "+item.Name+" vào _Installed, lưu lịch sử trong _Development và dọn bản cũ. Chờ máy còn lại đồng bộ đủ trước khi mở CAD. Chưa chạy mã công cụ.");}
  internal void Loaded(CadToolEntry entry,List<CadToolCommand> values){
   loaded[entry.Id]=values;loadedPackages[entry.Id]=entry.PackageHash;
   var current=owner.catalog.Read();var tool=current.Items.Single(t=>t.Id==entry.Id);if(tool.PackageHash!=entry.PackageHash)throw new CadException("TOOL_CHANGED","Công cụ đã đổi trong lúc nạp.");
   var names=values.Select(c=>c.Name).ToList();if(!tool.Commands.SequenceEqual(names))owner.catalog.Update(tool.Id,current.Revision,tool.Name,tool.Group,tool.Notes,tool.Enabled,names);Refresh();
  }
 }
 public void Dispose(){runtime.Dispose();palette.Dispose();}
}
