using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rainmaker.Axis;

internal sealed class ConstructionRow
{
 public string Key {get;set;}="";
 public string Label {get;set;}="";
}
internal sealed partial class AxisGridDialog
{
 ColumnOptions defaultColumn=new(){Anchor="WallAuto"};WallOptions defaultWall=new();
 Dictionary<string,ColumnOptions> columnOverrides=new();Dictionary<string,WallOptions> wallOverrides=new();
 string[] selectedNodes=Array.Empty<string>(),selectedSegments=Array.Empty<string>();
 HashSet<string> pickedNodes=new(),pickedSegments=new(),dirtyColumns=new(),dirtyWalls=new();
 readonly Dictionary<string,Control> columnFields=new(),wallFields=new();
 readonly TextBlock columnCaption=new(){TextWrapping=TextWrapping.Wrap,FontSize=11},wallCaption=new(){TextWrapping=TextWrapping.Wrap,FontSize=11};
 readonly Dictionary<ListBox,string> rowKeys=new();
 bool columnsInitialized,wallsInitialized,loadingEditors,loadingRows,draftDirty;
 Expander BuildColumnEditor()
 {
  var panel=new StackPanel();panel.Children.Add(columnsOn);panel.Children.Add(columnCaption);columnShape.ItemsSource=new[]{"Rectangle","Square","Circle"};columnAnchor.ItemsSource=ColumnOptions.Anchors;
  var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding{Converter=new AnchorLabelConverter()});columnAnchor.ItemTemplate=new DataTemplate{VisualTree=text};
  panel.Children.Add(Pairs(("Hình cột",columnShape),("Tim → mép ngoài · mm",columnInset)));panel.Children.Add(Pairs(("Neo",columnAnchor)));panel.Children.Add(Pairs(("B / D · mm",columnB),("H · mm",columnH),("Xoay · độ",columnRotation)));panel.Children.Add(Pairs(("Lệch X · mm",columnDx),("Lệch Y · mm",columnDy)));
  foreach(var pair in new (string,Control)[]{("Shape",columnShape),("Anchor",columnAnchor),("B",columnB),("H",columnH),("Rotation",columnRotation),("OffsetX",columnDx),("OffsetY",columnDy),("WallInset",columnInset)})BindEdit(columnFields,pair.Item1,pair.Item2,true);
  var actions=new WrapPanel();actions.Children.Add(B("Tất cả",()=>ChooseNodes(false)));actions.Children.Add(B("Biên",()=>ChooseNodes(true)));actions.Children.Add(B("Bỏ cột",()=>RemovePicked("Columns")));actions.Children.Add(B("Khôi phục",()=>RestorePicked("Columns")));actions.Children.Add(B("Bỏ vị trí mất",DropOrphans));panel.Children.Add(actions);SetupRows(columnRows,true);panel.Children.Add(columnRows);panel.ToolTip="Neo tim tường theo hướng lưới, không xoay theo tiết diện. Biên tự động căn mép ngoài cột theo mép tường. 110 là khoảng tim tường đến mép ngoài, không phải độ dịch tim cột. Lệch X/Y cộng thêm. Xc theo B, Yc theo H. Chọn nhiều: chỉ ô được sửa mới áp dụng.";return G("Cột tự tạo",panel);
 }
 Expander BuildWallEditor()
 {
  var panel=new StackPanel();panel.Children.Add(wallsOn);panel.Children.Add(wallCaption);wallAlignment.ItemsSource=new[]{"Center","Left","Right"};panel.Children.Add(Pairs(("Dày · mm",wallThickness),("Căn tường",wallAlignment)));panel.Children.Add(Pairs(("Lệch thêm · mm",wallOffset)));panel.Children.Add(hatchOn);
  foreach(var pair in new (string,Control)[]{("Thickness",wallThickness),("Offset",wallOffset),("Alignment",wallAlignment)})BindEdit(wallFields,pair.Item1,pair.Item2,false);
  var actions=new WrapPanel();actions.Children.Add(B("Chọn biên",ChooseWalls));actions.Children.Add(B("Bỏ tường",()=>RemovePicked("Walls")));actions.Children.Add(B("Thêm lại",()=>RestorePicked("Walls")));actions.Children.Add(B("Bỏ vị trí mất",DropOrphans));panel.Children.Add(actions);SetupRows(wallRows,false);panel.Children.Add(wallRows);panel.ToolTip="Center: tim; Left/Right: đặt tường về bên trái/phải mũi hướng. Lệch dương về trái. Đoạn X hướng sang phải; đoạn Y hướng lên trên.";return G("Tường kín và hatch",panel);
 }
 void BindEdit(Dictionary<string,Control> fields,string key,Control control,bool column)
 {
  fields[key]=control;void Changed(){if(!ready||loading||loadingEditors)return;(column?dirtyColumns:dirtyWalls).Add(key);draftDirty=true;Schedule();}
  if(control is TextBox text)text.TextChanged+=(_,__)=>Changed();else if(control is ComboBox box)box.SelectionChanged+=(_,__)=>Changed();
 }
 void SetupRows(ListBox box,bool column){box.DisplayMemberPath="Label";box.SelectionMode=SelectionMode.Extended;box.Height=70;box.Margin=new Thickness(0,4,0,2);box.SetResourceReference(BackgroundProperty,"ToolInput");box.SetResourceReference(ForegroundProperty,"ToolText");box.SetResourceReference(BorderBrushProperty,"ToolBorder");box.SelectionChanged+=(_,__)=>{if(loadingRows||loading)return;var keys=box.SelectedItems.OfType<ConstructionRow>().Select(r=>r.Key).ToArray();SelectPreview(column?"Columns":"Walls",keys);};}
 void CommitEditors()
 {
  void Apply<T>(T value,Dictionary<string,Control> fields,HashSet<string> dirty){foreach(var key in dirty){var field=typeof(T).GetField(key,BindingFlags.Instance|BindingFlags.Public)!;object v=fields[key] is TextBox text?Number(text):(object?)((ComboBox)fields[key]).SelectedItem??throw new ArgumentException("Chọn giá trị cho "+key);field.SetValue(value,v);}}
  if(dirtyColumns.Count>0){if(pickedNodes.Count==0){var copy=defaultColumn.Copy();Apply(copy,columnFields,dirtyColumns);defaultColumn=copy;}else foreach(var key in pickedNodes){var copy=columnOverrides.TryGetValue(key,out var old)?old.Copy():defaultColumn.Copy();Apply(copy,columnFields,dirtyColumns);columnOverrides[key]=copy;}dirtyColumns.Clear();}
  if(dirtyWalls.Count>0){if(pickedSegments.Count==0){var copy=defaultWall.Copy();Apply(copy,wallFields,dirtyWalls);defaultWall=copy;}else foreach(var key in pickedSegments){var copy=wallOverrides.TryGetValue(key,out var old)?old.Copy():defaultWall.Copy();Apply(copy,wallFields,dirtyWalls);wallOverrides[key]=copy;}dirtyWalls.Clear();}
 }
 void LoadEditors()
 {
  loadingEditors=true;try{
   void Fill<T>(Dictionary<string,Control> fields,IEnumerable<T> values,TextBlock caption,string title){var all=values.ToArray();var mixed=new List<string>();foreach(var pair in fields){var field=typeof(T).GetField(pair.Key)!;var objects=all.Select(v=>field.GetValue(v)).Distinct().ToArray();bool same=objects.Length==1;string text=same?(objects[0] is double d?N(d):objects[0]?.ToString()??""):"";if(!same)mixed.Add(pair.Key);if(pair.Value is TextBox box)box.Text=text;else ((ComboBox)pair.Value).SelectedItem=same?objects[0]:null;pair.Value.ToolTip=same?pair.Value.ToolTip:"Nhiều giá trị — chỉ thay khi bạn nhập/chọn giá trị mới.";}caption.Text=title+(mixed.Count==0?"":" · ô trống: nhiều giá trị");}
   Fill(columnFields,pickedNodes.Count==0?new[]{defaultColumn}:pickedNodes.Select(k=>columnOverrides.TryGetValue(k,out var c)?c:defaultColumn),columnCaption,pickedNodes.Count==0?"Mặc định cho cột mới":"Đang chọn "+pickedNodes.Count+" cột");
   Fill(wallFields,pickedSegments.Count==0?new[]{defaultWall}:pickedSegments.Select(k=>wallOverrides.TryGetValue(k,out var w)?w:defaultWall),wallCaption,pickedSegments.Count==0?"Mặc định cho đoạn mới":"Đang chọn "+pickedSegments.Count+" đoạn tường");
  }finally{loadingEditors=false;}
 }
 bool SelectPreview(string kind,string[] keys)
 {
  try{CheckpointHistory();CommitEditors();if(kind=="Columns"){pickedNodes=new(keys);var next=selectedNodes.Union(keys).ToArray();draftDirty|=next.Length!=selectedNodes.Length;selectedNodes=next;}else{pickedSegments=new(keys);var next=selectedSegments.Union(keys).ToArray();draftDirty|=next.Length!=selectedSegments.Length;selectedSegments=next;}LoadEditors();SyncPicked();CheckpointHistory();Schedule();return true;}catch(Exception e){error.Text=e.Message;return false;}
 }
 void SyncPicked(){preview.PickedColumns=new(pickedNodes);preview.PickedWalls=new(pickedSegments);preview.InvalidateVisual();}
 void ClearPicked(){try{CommitEditors();pickedNodes.Clear();pickedSegments.Clear();LoadEditors();SyncPicked();Schedule();}catch(Exception e){error.Text=e.Message;}}
 void RemovePicked(string kind){try{CheckpointHistory();CommitEditors();if(kind=="Columns")selectedNodes=selectedNodes.Except(pickedNodes).ToArray();else selectedSegments=selectedSegments.Except(pickedSegments).ToArray();draftDirty=true;CheckpointHistory();Schedule();}catch(Exception e){error.Text=e.Message;}}
 void RestorePicked(string kind){CheckpointHistory();if(kind=="Columns")selectedNodes=selectedNodes.Union(pickedNodes).ToArray();else selectedSegments=selectedSegments.Union(pickedSegments).ToArray();draftDirty=true;CheckpointHistory();Schedule();}
 void DropOrphans(){try{CheckpointHistory();CommitEditors();var p=new AxisGridParameters{XBays=AxisGridLayout.ParseBays(xBays.Text),YBays=AxisGridLayout.ParseBays(yBays.Text)};var nodes=new HashSet<string>(ConstructionGeometry.Nodes(p));var segments=new HashSet<string>(ConstructionGeometry.Segments(p));selectedNodes=selectedNodes.Where(nodes.Contains).ToArray();selectedSegments=selectedSegments.Where(segments.Contains).ToArray();foreach(var key in columnOverrides.Keys.Where(k=>!nodes.Contains(k)).ToArray())columnOverrides.Remove(key);foreach(var key in wallOverrides.Keys.Where(k=>!segments.Contains(k)).ToArray())wallOverrides.Remove(key);pickedNodes.IntersectWith(nodes);pickedSegments.IntersectWith(segments);dirtyColumns.Clear();dirtyWalls.Clear();LoadEditors();SyncPicked();draftDirty=true;CheckpointHistory();Schedule();}catch(Exception e){error.Text=e.Message;}}
 void ChooseNodes(bool boundary){try{var p=Read();var keys=ConstructionGeometry.Nodes(p).Where(k=>{var a=k.Split(':').Select(int.Parse).ToArray();return !boundary||a[0]==0||a[1]==0||a[0]==p.XBays.Length||a[1]==p.YBays.Length;}).ToArray();SelectPreview("Columns",keys);}catch(Exception e){error.Text=e.Message;}}
 void ChooseWalls(){try{SelectPreview("Walls",ConstructionDraft.Perimeter(Read()));}catch(Exception e){error.Text=e.Message;}}
 void EnableColumns(){if(loading||!ready)return;try{if(!columnsInitialized){selectedNodes=ConstructionGeometry.Nodes(Read());columnsInitialized=true;}draftDirty=true;CheckpointHistory();Schedule();}catch(Exception e){error.Text=e.Message;}}
 void EnableWalls(){if(loading||!ready)return;try{if(!wallsInitialized){selectedSegments=ConstructionDraft.Perimeter(Read());wallsInitialized=true;}draftDirty=true;CheckpointHistory();Schedule();}catch(Exception e){error.Text=e.Message;}}
 void ReadConstruction(AxisGridParameters p)
 {
  CommitEditors();defaultColumn.SetDefault(p);p.WallThickness=defaultWall.Thickness;p.WallOffset=defaultWall.Offset;p.WallAlignment=defaultWall.Alignment;p.ColumnOverrides=columnOverrides.ToDictionary(x=>x.Key,x=>x.Value.Copy());p.WallOverrides=wallOverrides.ToDictionary(x=>x.Key,x=>x.Value.Copy());
  // Preserve a non-center default for subsequently added segments.
  if(defaultWall.Alignment!="Center")foreach(var key in selectedSegments.Where(k=>!p.WallOverrides.ContainsKey(k)))p.WallOverrides[key]=defaultWall.Copy();
  p.Columns=columnsOn.IsChecked==true;p.Walls=wallsOn.IsChecked==true;p.ColumnHatch=p.Columns;p.WallHatch=hatchOn.IsChecked==true;p.ColumnNodes=selectedNodes.ToArray();p.WallSegments=selectedSegments.ToArray();p.DirectedWalls=true;p.CleanWallJunctions=true;p.ColumnFaceAnchors=true;p.FixedDimExtensions=fixedDim.IsChecked==true;p.DimExtensionLength=Number(dimExtension);if(p.FixedDimExtensions&&!AxisLibrary.ValidDistance(p.DimExtensionLength))throw new ArgumentException("Khoảng cách từ trục đến chân dim phải lớn hơn 0.");p.GridName=gridName.Text.Trim();if(p.GridName.Length>100)throw new ArgumentException("Tên lưới tối đa 100 ký tự.");p.ShowColumns=showColumns.IsChecked==true;p.ShowWalls=showWalls.IsChecked==true;p.ShowHatch=showHatch.IsChecked==true;
 }
 void Selections(AxisGridParameters p)
 {
  loadingRows=true;try{void Fill(ListBox box,string[] keys,bool column){var picked=column?pickedNodes:pickedSegments;var active=column?selectedNodes:selectedSegments;string rowsKey=string.Join("|",p.XLabels)+"/"+string.Join("|",p.YLabels)+"/"+string.Join("|",active);if(!rowKeys.TryGetValue(box,out var previous)||previous!=rowsKey){var generated=keys.Select(k=>{var a=k.Split(':');int i=int.Parse(a[column?0:1]),j=int.Parse(a[column?1:2]);return new ConstructionRow{Key=k,Label=(active.Contains(k)?"● ":"○ ")+(column?$"{p.XLabels[i]}/{p.YLabels[j]}":a[0]=="X"?$"{p.XLabels[i]}→{p.XLabels[i+1]} / {p.YLabels[j]}":$"{p.YLabels[j]}→{p.YLabels[j+1]} / {p.XLabels[i]}")};}).ToArray();box.ItemsSource=generated;rowKeys[box]=rowsKey;}var rows=box.Items.OfType<ConstructionRow>().ToArray();var current=box.SelectedItems.OfType<ConstructionRow>().Select(r=>r.Key);if(new HashSet<string>(current).SetEquals(picked))return;box.SelectedItems.Clear();foreach(var row in rows.Where(r=>picked.Contains(r.Key)))box.SelectedItems.Add(row);}Fill(columnRows,ConstructionGeometry.Nodes(p),true);Fill(wallRows,ConstructionGeometry.Segments(p),false);}finally{loadingRows=false;}
 }
}
