using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using AICad.Bridge;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

namespace Rainmaker.Axis;

internal sealed partial class AxisGridDialog
{
 readonly AxisPreview preview=new(){MinHeight=360};
 readonly ComboBox axisLayer=new(),headLayer=new(),headBlock=new(),dimLayer=new(),bayDim=new(),totalDim=new(),ends=new();
 readonly TextBox attribute=new(),dimOffset=new(),dimRowGap=new();
 readonly TabControl libraryTabs=new();
 readonly CheckBox top=new(){Content="Trên",IsChecked=true},bottom=new(){Content="Dưới",IsChecked=true},left=new(){Content="Trái",IsChecked=true},right=new(){Content="Phải",IsChecked=true};
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(220)};
 readonly TextBlock libraryNote=new(){TextWrapping=TextWrapping.Wrap};
 readonly TextBlock attributeHints=new(){TextWrapping=TextWrapping.Wrap,FontSize=12};
 readonly TextBox sourceDetails=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap,FontSize=12,MinHeight=90,MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
 AxisLibrary? library;AxisLibrarySettings settings=new();AxisResolvedLibrary? resolved;bool ready;
 void Build(double currentScale)
 {
  Title="Rainmaker · Tạo trục 1.3.1";Width=1120;Height=820;MinWidth=860;MinHeight=580;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;FontFamily=new FontFamily("Segoe UI");FontSize=14;
  var assembly=typeof(Plugin).Assembly;using(var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith(".CadToolTheme.xaml"))))Resources=(ResourceDictionary)XamlReader.Load(stream!);
  using(var pickerStream=typeof(AxisGridDialog).Assembly.GetManifestResourceStream("Rainmaker.Axis.AxisResourcePicker.xaml"))Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(pickerStream!));
  SetResourceReference(BackgroundProperty,"ToolBackground");SetResourceReference(ForegroundProperty,"ToolText");
  UseLayoutRounding=true;SnapsToDevicePixels=true;
  var root=new DockPanel{Margin=new Thickness(22)};root.SetResourceReference(Panel.BackgroundProperty,"ToolBackground");var header=new StackPanel{Margin=new Thickness(0,0,0,16)};
  header.Children.Add(new TextBlock{Text="Tạo lưới trục",FontSize=24,FontWeight=FontWeights.SemiBold});header.Children.Add(Muted("Nhập nhịp → chọn tài nguyên kho → xem bố trí → đặt vào CAD",new Thickness(0,6,0,0)));DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  var footer=new StackPanel{Margin=new Thickness(0,16,0,0)};AutomationProperties.SetLiveSetting(error,AutomationLiveSetting.Polite);footer.Children.Add(error);
  var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};
  var save=new Button{Content="Lưu cài đặt thư viện",Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,8,10,0)};
  save.Click+=(_,__)=>{try{var p=Read();var s=ReadSettings();(library??throw new ArgumentException("Chưa đọc được kho.")).Save(s,p.Single);settings=s;error.Foreground=new SolidColorBrush(Color.FromRgb(134,239,172));error.Text="Đã lưu lựa chọn theo ID trong kho.";}catch(Exception e){error.Foreground=new SolidColorBrush(Color.FromRgb(253,164,175));error.Text=e.Message;}};
  actions.Children.Add(save);actions.Children.Add(new Button{Content="Hủy",IsCancel=true,MinWidth=80,Margin=new Thickness(0,8,10,0)});create.Margin=new Thickness(0,8,0,0);create.SetResourceReference(StyleProperty,"ToolPrimaryButton");create.Click+=(_,__)=>Accept();actions.Children.Add(create);footer.Children.Add(actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
  var columns=new Grid();columns.ColumnDefinitions.Add(new(){Width=new GridLength(400)});columns.ColumnDefinitions.Add(new(){Width=new GridLength(20)});columns.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
  var tabs=libraryTabs;var form=new StackPanel{Margin=new Thickness(10)};
  var gridForm=new StackPanel();gridForm.Children.Add(Field("Nhịp X (mm)",xBays,"Ví dụ 4000 5000 hoặc 3*4000"));gridForm.Children.Add(Field("Nhịp Y (mm)",yBays,"Mỗi nhịp thêm một trục."));gridForm.Children.Add(Field("Ký hiệu bắt đầu trục đứng",xStart,"Ví dụ 1 hoặc A"));gridForm.Children.Add(Field("Ký hiệu bắt đầu trục ngang",yStart,"Ví dụ A hoặc 1"));
  var singleForm=new StackPanel();singleForm.Children.Add(Field("Ký hiệu trục",singleLabel,"Chọn hai đầu trục khi đặt vào CAD."));modes.Items.Add(new TabItem{Header="Lưới trục",Content=gridForm});modes.Items.Add(new TabItem{Header="Một trục",Content=singleForm});form.Children.Add(modes);form.Children.Add(Field("Tỷ lệ bố trí 1/…",scale,"Điều chỉnh độ nhô và khoảng đặt dim; không ghi đè tỷ lệ của dim style thường."));
  tabs.Items.Add(new TabItem{Header="Thông số",Content=Scroll(form)});
  var resources=new StackPanel{Margin=new Thickness(10)};
  resources.Children.Add(Muted("Gõ mã hoặc chọn tên. Lựa chọn được lưu theo ID ổn định, không theo tên.",new Thickness(0,0,0,12)));
  foreach(var pair in new[]{("Layer tim trục",axisLayer),("Block đầu trục",headBlock),("Layer đầu trục",headLayer),("Dim nhịp",bayDim),("Dim tổng",totalDim),("Layer dim",dimLayer)}){resources.Children.Add(new TextBlock{Text=pair.Item1,Margin=new Thickness(0,8,0,5)});var box=pair.Item2;box.Style=(Style)Resources["AxisResourcePicker"];box.IsEditable=true;box.IsTextSearchEnabled=true;box.DisplayMemberPath="Label";TextSearch.SetTextPath(box,"Label");box.MinHeight=38;AutomationProperties.SetName(box,pair.Item1);resources.Children.Add(box);box.SelectionChanged+=(_,__)=>Schedule();box.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler((_,__)=>Schedule()));}
  resources.Children.Add(Field("Tag thuộc tính ký hiệu",attribute,"Đúng tag ATT của block, ví dụ KI-HIEU-TRUC."));resources.Children.Add(attributeHints);resources.Children.Add(libraryNote);resources.Children.Add(new Expander{Header="Nguồn và ID đã chọn",Content=sourceDetails,Margin=new Thickness(0,12,0,0)});tabs.Items.Add(new TabItem{Header="Thư viện",Content=Scroll(resources)});
  var placement=new StackPanel{Margin=new Thickness(10)};placement.Children.Add(Field("Trục → dim nhịp (mm)",dimOffset,"Khoảng cách từ trục biên đến đường dim nhịp, theo mm thực trong bản vẽ."));placement.Children.Add(Field("Dim nhịp → dim tổng (mm)",dimRowGap,"Khoảng cách giữa hai hàng dim. Mặc định 600 mm."));placement.Children.Add(new TextBlock{Text="Các phía đặt dim",FontWeight=FontWeights.SemiBold});foreach(var check in new[]{bottom,top,left,right}){check.Margin=new Thickness(0,8,0,8);placement.Children.Add(check);check.Checked+=(_,__)=>Schedule();check.Unchecked+=(_,__)=>Schedule();}
  placement.Children.Add(new TextBlock{Text="Đầu ký hiệu trục",Margin=new Thickness(0,16,0,8)});ends.ItemsSource=new[]{"Both","Start","End"};ends.SelectedIndex=0;placement.Children.Add(ends);placement.Children.Add(Muted("Both: cả hai đầu · Start: đầu dưới/trái · End: đầu trên/phải.",new Thickness(0,8,0,0)));ends.SelectionChanged+=(_,__)=>Schedule();tabs.Items.Add(new TabItem{Header="Bố trí",Content=Scroll(placement)});columns.Children.Add(tabs);
  var previewPanel=new DockPanel();var previewHeader=new StackPanel();previewHeader.Children.Add(new TextBlock{Text="Xem trước bố trí",FontWeight=FontWeights.SemiBold,FontSize=17});dimensionNote=Muted("Sơ đồ bố trí theo mm; đầu trục, font và mũi tên giản lược. Khi vẽ dùng đúng tài nguyên đã chọn.",new Thickness(0,8,0,10));previewHeader.Children.Add(dimensionNote);previewHeader.Children.Add(summary);var zoom=new WrapPanel{Margin=new Thickness(0,10,0,10)};
  foreach(var caption in new[]{"−","Vừa khung","+"}){var button=new Button{Content=caption,Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,0,6,0)};button.Click+=(_,__)=>{preview.Zoom=caption=="Vừa khung"?1:Math.Max(.3,Math.Min(4,preview.Zoom*(caption=="+"?1.2:1/1.2)));preview.InvalidateVisual();};zoom.Children.Add(button);}previewHeader.Children.Add(zoom);DockPanel.SetDock(previewHeader,Dock.Top);previewPanel.Children.Add(previewHeader);previewPanel.Children.Add(preview);Grid.SetColumn(previewPanel,2);columns.Children.Add(previewPanel);root.Children.Add(columns);Content=root;
  xBays.Text=saved[0];yBays.Text=saved[1];xStart.Text=saved[2];yStart.Text=saved[3];singleLabel.Text=saved[4];scale.Text=saved[5]==""?((int)(currentScale>=2&&currentScale<=1000?Math.Round(currentScale):60)).ToString():saved[5];
  try{
   library=new AxisLibrary(Plugin.Current!.Config.ContentRoot);settings=library.Load();
   foreach(var box in new[]{axisLayer,headLayer,dimLayer})box.ItemsSource=library.Options.Where(o=>o.Kind=="layers").OrderBy(o=>o.Code).ToList();headBlock.ItemsSource=library.Options.Where(o=>o.Kind=="blocks").OrderBy(o=>o.Code).ToList();foreach(var box in new[]{bayDim,totalDim})box.ItemsSource=library.Options.Where(o=>o.Kind=="dims").OrderBy(o=>o.Code).ToList();
   void Select(ComboBox box,string id)=>box.SelectedItem=library.Options.FirstOrDefault(o=>o.Id==id);
   Select(axisLayer,settings.AxisLayerId);Select(headLayer,settings.HeadLayerId);Select(dimLayer,settings.DimLayerId);Select(headBlock,settings.HeadBlockId);Select(bayDim,settings.BayDimId);Select(totalDim,settings.TotalDimId);attribute.Text=settings.AttributeTag;dimOffset.Text=settings.DimOffset.ToString(System.Globalization.CultureInfo.InvariantCulture);dimRowGap.Text=settings.DimRowGap.ToString(System.Globalization.CultureInfo.InvariantCulture);top.IsChecked=settings.Top;bottom.IsChecked=settings.Bottom;left.IsChecked=settings.Left;right.IsChecked=settings.Right;ends.SelectedItem=settings.HeadEnds;
  }catch(Exception e){libraryNote.Text=e.Message;}
  foreach(var field in new[]{xBays,yBays,xStart,yStart,singleLabel,scale,attribute,dimOffset,dimRowGap})field.TextChanged+=(_,__)=>Schedule();modes.SelectionChanged+=(_,__)=>Schedule();timer.Tick+=(_,__)=>{timer.Stop();UpdateState();};Closed+=(_,__)=>timer.Stop();ready=true;UpdateState();
 }
 static ScrollViewer Scroll(object content)=>new(){Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
 void Schedule(){if(!ready)return;timer.Stop();timer.Start();}
 AxisLibrarySettings ReadSettings()
 {
  string Pick(ComboBox box,string kind){var option=box.SelectedItem as AxisLibraryOption;if(option!=null&&box.Text==option.Label)return option.Id;return library?.Options.FirstOrDefault(o=>o.Kind==kind&&o.Code.Equals(box.Text.Trim(),StringComparison.OrdinalIgnoreCase))?.Id??"";}
  double Distance(TextBox input){if(!double.TryParse(input.Text.Trim().Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double value)||!AxisLibrary.ValidDistance(value))throw new ArgumentException("Khoảng cách dim phải lớn hơn 0 và không vượt quá 10.000.000 mm.");return value;}
  return new(){DimOffset=Distance(dimOffset),DimRowGap=Distance(dimRowGap),Revision=settings.Revision,AxisLayerId=Pick(axisLayer,"layers"),HeadLayerId=Pick(headLayer,"layers"),DimLayerId=Pick(dimLayer,"layers"),HeadBlockId=Pick(headBlock,"blocks"),BayDimId=Pick(bayDim,"dims"),TotalDimId=Pick(totalDim,"dims"),AttributeTag=attribute.Text,Top=top.IsChecked==true,Bottom=bottom.IsChecked==true,Left=left.IsChecked==true,Right=right.IsChecked==true,HeadEnds=ends.SelectedItem as string??"Both"};
 }
 void UpdateState()
 {
  if(!ready)return;resolved=null;sourceDetails.Text="";error.Foreground=new SolidColorBrush(Color.FromRgb(253,164,175));if(library!=null)libraryNote.Text="";
  try{
   var p=Read();summary.Text=p.Single?$"Trục {p.SingleLabel} · đoạn 4.000 mm minh họa, lấy chiều dài thật khi chọn điểm":$"{p.XLabels.Length} trục đứng · {p.YLabels.Length} trục ngang\n{p.XBays.Sum():N0} × {p.YBays.Sum():N0} mm";
   var s=ReadSettings();
   var option=library?.Options.FirstOrDefault(o=>o.Id==s.HeadBlockId&&o.Kind=="blocks");attributeHints.Text="Tag có sẵn: "+string.Join(", ",(option?.Sync.Values["attributes"] as JArray??new()).OfType<JObject>().Where(a=>a.Value<bool>("constant")==false).Select(a=>a.Value<string>("tag")));
   bayDim.IsEnabled=totalDim.IsEnabled=!p.Single;
   // Layout remains visible before choosing styles, but drawing is blocked until selection is valid.
   var dummy=new AxisResolvedLibrary{Settings=s,Template=new(){Id="PREVIEW"},AxisLayer=new(){Name="Axis"},HeadLayer=new(){Name="Head"},DimLayer=new(){Name="Dim"},Head=new(){Id="HEAD",NativeSource=new(){LabelAttribute="LABEL"}}};
   preview.Update(RainmakerAxis.ConfiguredPlan(dummy,p,new Autodesk.AutoCAD.Geometry.Point3d(0,0,0)));
   resolved=(library??throw new ArgumentException(libraryNote.Text)).Resolve(s,p.Single);error.Text="";create.IsEnabled=true;create.Content=p.Single?"Chọn hai đầu trục":"Chọn điểm đặt lưới";
   libraryNote.Text=p.Single?"Đã chọn block và layer theo ID.":$"Dim nhịp: {resolved.BayDim}\nDim tổng: {resolved.TotalDim}\nGiữ nguyên style nguồn; tỷ lệ Dim thường: {resolved.BayParameters["scale"]} / {resolved.TotalParameters["scale"]}.";
   sourceDetails.Text=$"Nguồn: {s.SourceId} · Template v{resolved.Template.Version}\nLayer trục: {s.AxisLayerId}\nBlock: {s.HeadBlockId}\nLayer đầu trục: {s.HeadLayerId}\nLayer dim: {s.DimLayerId}\nDim nhịp: {s.BayDimId}\nDim tổng: {s.TotalDimId}\nDWG nguồn Dim: {resolved.DimFile}";
  }catch(Exception e){error.Text=e.Message;create.IsEnabled=false;if(e is ArgumentException){preview.Update(null);summary.Text="Điền thông số hợp lệ để xem lưới.";}}
 }
}
