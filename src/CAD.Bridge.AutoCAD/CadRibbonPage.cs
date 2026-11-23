using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AICad.Contracts;

namespace AICad.Bridge;

internal sealed partial class CadToolPalette
{
    sealed class RibbonPage
    {
        readonly CadToolPalette owner;
        readonly ListBox list=new(){DisplayMemberPath="Name",MinHeight=130};
        readonly TextBox name=new(){MaxLength=60},order=new(){MaxLength=4,Text="10"};
        readonly CheckBox visible=new(){Content="Hiện tab trên ribbon",IsChecked=true};
        readonly TextBlock use=Label("");CadToolIndex index=new();
        string selected="";internal FrameworkElement Root;
        internal RibbonPage(CadToolPalette owner)
        {
            this.owner=owner;var panel=new StackPanel{Margin=new Thickness(14)};
            panel.Children.Add(Label("Quản lý tab ribbon",18,true));
            panel.Children.Add(Label("Tạo tab cho add-in, đổi tên hoặc ẩn tab. Chỉ các tab do CAD Manager tạo được quản lý ở đây."));
            panel.Children.Add(list);list.SelectionChanged+=(_,_)=>Select();
            panel.Children.Add(Label("Tên tab"));panel.Children.Add(name);System.Windows.Automation.AutomationProperties.SetName(name,"Tên tab ribbon");
            panel.Children.Add(Label("Thứ tự (0–9999)"));panel.Children.Add(order);panel.Children.Add(visible);panel.Children.Add(use);
            var actions=new WrapPanel{Margin=new Thickness(0,14,0,0)};
            actions.Children.Add(Button("Tab mới",()=>{list.SelectedItem=null;selected="";name.Text="";order.Text=Math.Min(9999,index.Tabs.Count*10).ToString();visible.IsChecked=true;visible.IsEnabled=true;use.Text="Nhập tên rồi Lưu tab.";name.Focus();}));
            actions.Children.Add(Button("Lưu tab",()=>owner.Guard(Save),true));
            actions.Children.Add(Button("Xóa tab",()=>owner.Guard(Delete)));
            actions.Children.Add(Button("Làm mới",Refresh));panel.Children.Add(actions);
            panel.Children.Add(Label("Xóa tab không xóa DLL hay lệnh. Add-in và nút lệnh của tab đó được chuyển về tab mặc định Rainmaker. Tab mặc định luôn được giữ để mở Manager."));
            panel.Children.Add(Label("Đưa add-in vào tab: trang .NET Add-in → chọn tab đích → Chuyển toàn bộ nút. Dùng Chỉ ghim lệnh mở bảng để giữ một nút cho mỗi add-in. Có thể bỏ ghim hàng loạt và hoàn tác trong cùng trang."));
            Root=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Refresh();
        }
        internal void Refresh()=>owner.Guard(()=>
        {
            string keep=selected;index=owner.catalog.Read();list.ItemsSource=index.Tabs.OrderBy(t=>t.Order).ThenBy(t=>t.Name).ToList();list.SelectedItem=index.Tabs.FirstOrDefault(t=>t.Id==keep)??index.Tabs.First(t=>t.Id==CadToolCatalog.DefaultTabId);Select();
        });
        void Select()
        {
            if(list.SelectedItem is not CadRibbonTab tab)return;selected=tab.Id;name.Text=tab.Name;order.Text=tab.Order.ToString();visible.IsChecked=tab.Enabled;visible.IsEnabled=tab.Id!=CadToolCatalog.DefaultTabId;
            int tools=index.Items.Count(t=>t.TabId==tab.Id&&!t.DeletedUtc.HasValue);int pins=index.Items.Sum(t=>t.RibbonPins.Count(p=>p.TabId==tab.Id));use.Text=tools+" add-in mặc định · "+pins+" nút chỉ định riêng"+(tab.Id==CadToolCatalog.DefaultTabId?" · Tab mặc định":"");
        }
        void Save()
        {
            if(!int.TryParse(order.Text,out int position))throw new CadException("RIBBON_TAB","Nhập thứ tự từ 0 đến 9999.");
            selected=owner.catalog.SaveTab(index.Revision,selected,name.Text,position,visible.IsChecked==true);Refresh();owner.addins.Refresh();owner.plugin.Ribbon?.Invalidate();owner.Say("Đã lưu tab ribbon. Giao diện cập nhật khi AutoCAD rảnh.");
        }
        void Delete()
        {
            if(selected=="")throw new CadException("RIBBON_TAB","Chọn tab cần xóa.");owner.catalog.DeleteTab(index.Revision,selected);selected=CadToolCatalog.DefaultTabId;Refresh();owner.addins.Refresh();owner.plugin.Ribbon?.Invalidate();owner.Say("Đã xóa tab; các add-in được giữ và chuyển về tab mặc định.");
        }
    }
}
