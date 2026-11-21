using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

namespace Rainmaker.Axis;

internal sealed partial class AxisGridDialog
{
 string loadError="",selectedGridStatus="Chọn để đối chiếu",resolvedKey="";
 AxisResolvedLibrary? resolvedSnapshot;
 internal double LastGridLoadMilliseconds;
 StackPanel BuildLibrary()
 {
  var panel=new StackPanel{Margin=new Thickness(8)};panel.Children.Add(Muted("Lựa chọn lưu theo ID; mỗi lần nạp bảng đọc cấu hình kho mới. Hatch cột/tường độc lập.",new Thickness(0,0,0,8)));
  var pairs=new[]{("Layer tim trục",axisLayer),("Block đầu trục",headBlock),("Layer đầu trục",headLayer),("Layer dim",dimLayer),("Dim nhịp",bayDim),("Dim tổng",totalDim),("Layer cột",columnLayer),("Layer nét tường",wallLayer),("Hatch trong cột",columnHatch),("Layer hatch cột",columnHatchLayer),("Hatch tường",wallHatch),("Layer hatch tường",hatchLayer)};
  foreach(var pair in pairs){var box=pair.Item2;box.Style=(Style)Resources["AxisResourcePicker"];box.IsEditable=true;box.IsTextSearchEnabled=true;box.DisplayMemberPath="Label";TextSearch.SetTextPath(box,"Label");System.Windows.Automation.AutomationProperties.SetName(box,pair.Item1);box.SelectionChanged+=(_,__)=>{if(!loading){draftDirty=true;Schedule();}};box.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler((_,__)=>{if(!loading){draftDirty=true;Schedule();}}));}
  for(int i=0;i<pairs.Length;i+=2)panel.Children.Add(Pairs((pairs[i].Item1,pairs[i].Item2),(pairs[i+1].Item1,pairs[i+1].Item2)));
  panel.Children.Add(Pairs(("Tag ATT đầu trục",attribute)));panel.Children.Add(attributeHints);panel.Children.Add(libraryNote);panel.Children.Add(new Expander{Header="Thông tin nguồn / ID",Content=sourceDetails});return panel;
 }
 void BindResources()
 {
  foreach(var box in new[]{axisLayer,headLayer,dimLayer,columnLayer,wallLayer,hatchLayer,columnHatchLayer})box.ItemsSource=library!.Options.Where(o=>o.Kind=="layers").OrderBy(o=>o.Code).ToList();headBlock.ItemsSource=library!.Options.Where(o=>o.Kind=="blocks").OrderBy(o=>o.Code).ToList();foreach(var box in new[]{bayDim,totalDim})box.ItemsSource=library.Options.Where(o=>o.Kind=="dims").OrderBy(o=>o.Code).ToList();foreach(var box in new[]{wallHatch,columnHatch})box.ItemsSource=library.Options.Where(o=>o.Kind=="hatches").OrderBy(o=>o.Code).ToList();
 }
 void PopulateResourceSelection()
 {
  void Select(ComboBox box,string id)=>box.SelectedItem=library?.Options.FirstOrDefault(o=>o.Id==id);
  Select(axisLayer,settings.AxisLayerId);Select(headLayer,settings.HeadLayerId);Select(dimLayer,settings.DimLayerId);Select(headBlock,settings.HeadBlockId);Select(bayDim,settings.BayDimId);Select(totalDim,settings.TotalDimId);Select(columnLayer,settings.ColumnLayerId);Select(wallLayer,settings.WallLayerId);Select(hatchLayer,settings.HatchLayerId);Select(wallHatch,settings.WallHatchId);Select(columnHatch,settings.ColumnHatchId);Select(columnHatchLayer,settings.ColumnHatchLayerId);
  attribute.Text=settings.AttributeTag;dimOffset.Text=N(settings.DimOffset);dimRowGap.Text=N(settings.DimRowGap);top.IsChecked=settings.Top;bottom.IsChecked=settings.Bottom;left.IsChecked=settings.Left;right.IsChecked=settings.Right;ends.SelectedItem=settings.HeadEnds;
 }
 AxisLibrarySettings ReadSettings()
 {
  string Pick(ComboBox box,string kind){var option=box.SelectedItem as AxisLibraryOption;if(option!=null&&box.Text==option.Label)return option.Id;return library?.Options.FirstOrDefault(o=>o.Kind==kind&&o.Code.Equals(box.Text.Trim(),StringComparison.OrdinalIgnoreCase))?.Id??"";}
  return new(){Revision=settings.Revision,AxisLayerId=Pick(axisLayer,"layers"),HeadLayerId=Pick(headLayer,"layers"),DimLayerId=Pick(dimLayer,"layers"),HeadBlockId=Pick(headBlock,"blocks"),BayDimId=Pick(bayDim,"dims"),TotalDimId=Pick(totalDim,"dims"),ColumnLayerId=Pick(columnLayer,"layers"),WallLayerId=Pick(wallLayer,"layers"),HatchLayerId=Pick(hatchLayer,"layers"),WallHatchId=Pick(wallHatch,"hatches"),ColumnHatchId=Pick(columnHatch,"hatches"),ColumnHatchLayerId=Pick(columnHatchLayer,"layers"),AttributeTag=attribute.Text.Trim(),Top=top.IsChecked==true,Bottom=bottom.IsChecked==true,Left=left.IsChecked==true,Right=right.IsChecked==true,HeadEnds=ends.SelectedItem as string??"Both",DimOffset=Number(dimOffset),DimRowGap=Number(dimRowGap)};
 }
 void SaveLibrary(){try{var p=Read();var s=ReadSettings();var r=library!.Resolve(s,p.Single);library.ResolveConstruction(r,p);library.Save(s,p.Single);settings=s;error.Foreground=Brushes.LightGreen;error.Text="Đã lưu lựa chọn thư viện theo ID.";}catch(Exception e){error.Foreground=Brushes.LightPink;error.Text=e.Message;}}
 void LoadDefinition(GridDefinition definition)
 {
  var loadWatch=System.Diagnostics.Stopwatch.StartNew();bool before=loading;loading=true;loadError="";editingNote.Text="Đang nạp lưới…";if(IsLoaded)Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render,new Action(()=>{}));try{
   var p=ConstructionDraft.Copy(definition.Parameters);ConstructionDraft.MigrateDirectedWalls(p);Editing=definition;var global=library?.Load();settings=JObject.FromObject(definition.Library).ToObject<AxisLibrarySettings>()!;settings.Revision=global?.Revision??settings.Revision;if(string.IsNullOrEmpty(settings.ColumnHatchId)&&global!=null)settings.ColumnHatchId=global.ColumnHatchId;if(string.IsNullOrEmpty(settings.ColumnHatchLayerId)&&global!=null)settings.ColumnHatchLayerId=global.ColumnHatchLayerId;PopulateResourceSelection();
   LoadEnvelope(p.Envelope);gridName.Text=string.IsNullOrWhiteSpace(definition.Name)?"Lưới "+definition.Id.Substring(0,6):definition.Name;heading.Text="Sửa · "+gridName.Text;xBays.Text=string.Join(" ",p.XBays.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));yBays.Text=string.Join(" ",p.YBays.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));xStart.Text=string.Join(" ",p.XLabels);yStart.Text=string.Join(" ",p.YLabels);scale.Text=p.Scale.ToString();singleLabel.Text=p.SingleLabel;modes.SelectedIndex=0;gridMode.IsChecked=true;singleMode.IsEnabled=false;
   defaultColumn=ColumnOptions.Default(p);defaultWall=WallOptions.Default(p);columnOverrides=p.ColumnOverrides.ToDictionary(k=>k.Key,k=>k.Value.Copy());wallOverrides=p.WallOverrides.ToDictionary(k=>k.Key,k=>k.Value.Copy());selectedNodes=p.ColumnNodes.ToArray();selectedSegments=p.WallSegments.ToArray();pickedNodes.Clear();pickedSegments.Clear();dirtyColumns.Clear();dirtyWalls.Clear();columnsInitialized=p.Columns||p.ColumnNodes.Length>0;wallsInitialized=p.Walls||p.WallSegments.Length>0;columnsOn.IsChecked=p.Columns;wallsOn.IsChecked=p.Walls;hatchOn.IsChecked=p.WallHatch;fixedDim.IsChecked=p.FixedDimExtensions;dimExtension.Text=N(p.DimExtensionLength);preview.Fit();SyncPicked();LoadEditors();
   var db=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;var changes=GridStore.Changes(db,definition);selectedGridStatus=GridStore.ChangeStatus(changes);var row=gridList.Items.OfType<GridCatalogRow>().FirstOrDefault(r=>r.Id==definition.Id);if(row!=null){row.Status=selectedGridStatus;gridList.Items.Refresh();}preview.Existing=GridStore.Capture(db,definition);showExisting.IsEnabled=true;editingNote.Text=definition.Adopted?"Lưới cũ · chưa lưu nhận quản lý. Kiểm tra tỷ lệ và tài nguyên trước khi ghi.":"ID "+definition.Id.Substring(0,8)+" · lần "+definition.Revision+(changes.Count>0?" · "+changes.Count+" thành viên khác hiện trạng CAD; màu cam là hình đang có.":" · Khớp dữ liệu DWG.");if(preview.Existing.Unreadable>0)editingNote.Text+=" Có thành viên không đọc được hình xem trước.";draftDirty=false;ResetHistory();
  }catch(Exception e){loadError="Không nạp được lưới: "+e.Message;preview.Existing=null;preview.Update(null);error.Text=loadError;create.IsEnabled=false;throw;}finally{loading=before;LastGridLoadMilliseconds=loadWatch.Elapsed.TotalMilliseconds;}if(ready)UpdateState();
 }
 void UpdateState()
 {
  if(!ready||loading)return;resolved=null;error.Foreground=Brushes.LightPink;if(loadError.Length>0){error.Text=loadError;create.IsEnabled=false;preview.Update(null);return;}
  bool envelopeMatches=RefreshEnvelope();try{var p=Read();var s=ReadSettings();RecordHistory(p,s);bayResults.Visibility=p.Envelope==null?Visibility.Visible:Visibility.Collapsed;bayResults.Text="X = "+string.Join(" · ",p.XBays.Select(N))+"\nY = "+string.Join(" · ",p.YBays.Select(N));dimExtension.IsEnabled=fixedDim.IsChecked==true;if(!p.Single)Selections(p);var geometry=ConstructionGeometry.Make(p,new());summary.Text=p.Single?"Một trục · chọn hai điểm CAD":$"{p.XLabels.Length}×{p.YLabels.Length} trục · {p.XBays.Sum():N0}×{p.YBays.Sum():N0} mm\n{geometry.Columns.Count} cột · {geometry.Walls.Count} đoạn tường · chọn {pickedNodes.Count} cột/{pickedSegments.Count} đoạn";
   var dummy=new AxisResolvedLibrary{Settings=s,Template=new(){Id="PREVIEW"},AxisLayer=new(){Name="Axis"},HeadLayer=new(){Name="Head"},DimLayer=new(){Name="Dim"},Head=new(){Id="HEAD",NativeSource=new(){LabelAttribute="LABEL"}}};preview.ShowExisting=showExisting.IsChecked==true;preview.Update(RainmakerAxis.ConfiguredPlan(dummy,p,new(0,0,0)),geometry,p);
   var option=library?.Options.FirstOrDefault(o=>o.Id==s.HeadBlockId);attributeHints.Text="Tag: "+string.Join(", ",(option?.Sync.Values["attributes"] as JArray??new()).OfType<JObject>().Where(a=>!a.Value<bool>("constant")).Select(a=>a.Value<string>("tag")));bayDim.IsEnabled=totalDim.IsEnabled=!p.Single;
   string resourceKey=Newtonsoft.Json.JsonConvert.SerializeObject(new{s,p.Single,p.Columns,p.ColumnHatch,p.Walls,p.WallHatch});if(resolvedSnapshot==null||resourceKey!=resolvedKey){var fresh=(library??throw new ArgumentException(libraryNote.Text)).Resolve(s,p.Single);library.ResolveConstruction(fresh,p);resolvedSnapshot=fresh;resolvedKey=resourceKey;}resolved=resolvedSnapshot;create.IsEnabled=envelopeMatches;create.Content=Editing!=null?"Ghi cập nhật lưới":p.Single?"Chọn hai đầu trục":"Chọn điểm đặt lưới";error.Text=envelopeMatches?"":"Hoàn tất nhịp còn lại hoặc giảm phần vượt trước khi ghi CAD.";libraryNote.Text=p.Single?"Tài nguyên đã phân giải theo ID.":$"Dim nhịp: {resolved.BayDim}\nDim tổng: {resolved.TotalDim}";if(resolved.ColumnHatch!=null)libraryNote.Text+="\nHatch cột: "+resolved.ColumnHatch.ToString(Newtonsoft.Json.Formatting.None);if(resolved.WallHatch!=null)libraryNote.Text+="\nHatch tường: "+resolved.WallHatch.ToString(Newtonsoft.Json.Formatting.None);sourceDetails.Text=$"Nguồn {s.SourceId} · Template v{resolved.Template.Version}\n{JObject.FromObject(s)}\nDWG dim: {resolved.DimFile}";
  }catch(Exception e){error.Text=e.Message;create.IsEnabled=false;if(e is ArgumentException){preview.Update(null);summary.Text="Điền thông số hợp lệ để xem trước.";}}
 }
}
