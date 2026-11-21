using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Rainmaker.Axis;

internal sealed partial class AxisGridDialog
{
 bool loadingCatalog;
 FrameworkElement BuildCatalog()
 {
  gridList.EnableColumnVirtualization=false;gridList.EnableRowVirtualization=false;gridList.ColumnHeaderHeight=24;
  var headerStyle=new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader),(Style)Resources[typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)]);headerStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(6,2,6,2)));headerStyle.Setters.Add(new Setter(Control.FontSizeProperty,11.0));gridList.ColumnHeaderStyle=headerStyle;
  gridList.SizeChanged+=(_,__)=>{double width=Math.Max(200,gridList.ActualWidth-20);if(gridList.Columns.Count==3){gridList.Columns[0].Width=width*.24;gridList.Columns[1].Width=width*.5;gridList.Columns[2].Width=width*.26;}};
  var panel=new StackPanel();gridList.Columns.Add(new DataGridTextColumn{Header="Lưới",Binding=new Binding("Name"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});gridList.Columns.Add(new DataGridTextColumn{Header="Thông tin",Binding=new Binding("Summary"),Width=new DataGridLength(2,DataGridLengthUnitType.Star)});gridList.Columns.Add(new DataGridTextColumn{Header="Trạng thái",Binding=new Binding("Status"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});gridList.SelectionChanged+=(_,__)=>SelectCatalogRow();panel.Children.Add(gridList);var row=new Grid();row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var name=Pairs(("Tên lưới đang tạo/sửa",gridName));row.Children.Add(name);int col=1;foreach(var button in new[]{B("Đổi tên",RenameGrid),B("Đến lưới trên CAD",()=>{if(Editing==null){error.Text="Chọn lưới trước.";return;}RequestAction("Locate");})}){button.VerticalAlignment=VerticalAlignment.Bottom;button.Margin=new Thickness(6,0,0,3);button.Height=gridName.MinHeight;Grid.SetColumn(button,col++);row.Children.Add(button);}panel.Children.Add(row);return G("Các lưới đã nhận dạng · DWG hiện tại",panel);
 }
 void LoadGridList(){loadingCatalog=true;try{var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc==null)return;var rows=GridStore.List(doc.Database,false);foreach(var row in rows)if(row.Id==Editing?.Id)row.Status=selectedGridStatus;gridList.ItemsSource=rows;gridList.SelectedItem=rows.FirstOrDefault(r=>r.Id==Editing?.Id);if(gridList.SelectedItem!=null)gridList.ScrollIntoView(gridList.SelectedItem);}finally{loadingCatalog=false;}}
 void RenameGrid()
 {
  try{if(Editing==null||Editing.Adopted)throw new ArgumentException("Chọn lưới đã lưu trước khi đổi tên.");var current=Read();var original=ConstructionDraft.Copy(Editing.Parameters);ConstructionDraft.MigrateDirectedWalls(original);current.GridName=original.GridName="";bool same=Newtonsoft.Json.JsonConvert.SerializeObject(current)==Newtonsoft.Json.JsonConvert.SerializeObject(original);var db=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;AICad.Bridge.NativeWriteGuard.CheckDatabase(db);Editing=GridStore.Rename(db,Editing,gridName.Text);heading.Text="Sửa · "+Editing.Name;LoadGridList();if(same)draftDirty=false;editingNote.Text="Đã đổi tên, giữ nguyên GridId và các đối tượng. Các chỉnh sửa hình học khác vẫn là bản nháp.";}catch(Exception e){error.Text=e.Message;}
 }
 bool CanDiscardDraft(){if(!draftDirty)return true;return MessageBox.Show("Bản nháp chưa ghi vào CAD. Bỏ bản nháp để chuyển thao tác/lưới?","Bản nháp đang sửa",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
 void RequestAction(string action,bool discard=true){try{if(discard&&!CanDiscardDraft())return;Action=action;DialogResult=true;}catch(Exception e){error.Text=e.Message;}}
 void SelectCatalogRow()
 {
  if(loadingCatalog||loading)return;var row=gridList.SelectedItem as GridCatalogRow;if(row==null)return;
  if(row.Definition==null){loadError=row.Status;preview.Existing=null;preview.Update(null);create.IsEnabled=false;error.Text=row.Status;return;}if(row.Id==Editing?.Id&&loadError.Length==0)return;
  try{if(!CanDiscardDraft()){loadingCatalog=true;gridList.SelectedItem=gridList.Items.OfType<GridCatalogRow>().FirstOrDefault(r=>r.Id==Editing?.Id);loadingCatalog=false;return;}LoadDefinition(row.Definition);}catch(Exception e){error.Text=e.Message;}
 }
}
