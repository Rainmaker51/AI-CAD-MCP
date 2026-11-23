using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Newtonsoft.Json.Linq;

namespace Rainmaker.Library;

internal sealed partial class QaBrowserWindow
{
    readonly WrapPanel hatchControls=new(){Margin=new Thickness(0,0,0,6)};
    readonly ComboBox hatchMode=new(){ItemsSource=new[]{"Theo loại hatch","Danh sách"},SelectedIndex=0,MinWidth=145,Margin=new Thickness(0,0,8,0)};
    readonly ComboBox hatchPattern=new(){MinWidth=130,Margin=new Thickness(0,0,8,0)};
    bool bindingHatch,expandHatches;
    void BuildHatchControls(DockPanel middle)
    {
        DockPanel.SetDock(hatchControls,Dock.Top);middle.Children.Insert(1,hatchControls);
        hatchControls.Children.Add(new TextBlock{Text="Hiển thị",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
        hatchControls.Children.Add(hatchMode);hatchControls.Children.Add(hatchPattern);
        hatchControls.Children.Add(Button("Thu gọn",()=>{expandHatches=false;RenderList();return System.Threading.Tasks.Task.CompletedTask;}));
        hatchControls.Children.Add(Button("Mở rộng",()=>{expandHatches=true;RenderList();return System.Threading.Tasks.Task.CompletedTask;}));
        System.Windows.Automation.AutomationProperties.SetName(hatchMode,"Cách hiển thị hatch");
        System.Windows.Automation.AutomationProperties.SetName(hatchPattern,"Tên loại hatch");
        hatchMode.SelectionChanged+=(_,_)=>{if(!bindingHatch)RenderList();};
        hatchPattern.SelectionChanged+=(_,_)=>{if(!bindingHatch)RenderList();};
        BindHatchControls();
    }
    void BindHatchControls()
    {
        hatchControls.Visibility=kind=="hatches"?Visibility.Visible:Visibility.Collapsed;
        bindingHatch=true;string selected=hatchPattern.SelectedItem as string??"Tất cả loại hatch";
        var patterns=Rows.Select(Pattern).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).Prepend("Tất cả loại hatch").ToArray();
        hatchPattern.ItemsSource=patterns;hatchPattern.SelectedItem=patterns.Contains(selected)?selected:patterns[0];bindingHatch=false;
    }
    static string Pattern(JObject row)=>row["profile"]?.Value<string>("pattern")??"Không rõ mẫu";
    bool MatchesPattern(JObject row)=>kind!="hatches"||hatchPattern.SelectedIndex<=0||Pattern(row)==hatchPattern.SelectedItem as string;
    static string HatchSummary(JObject row)
    {
        var p=row["profile"]!;double degrees=(p.Value<double?>("angle")??0)*180/Math.PI;
        string color=p["rgb"]?.Type is JTokenType.Integer?"RGB "+p["rgb"]:"ACI "+p["color"];
        return $"{Pattern(row)} · Scale {p["scale"]} · {degrees:0.##}° · {color}";
    }
    void SetHatchView(List<ListBoxItem> items)
    {
        if(kind!="hatches"||hatchMode.SelectedIndex!=0){list.ItemsSource=items;return;}
        var view=new ListCollectionView(items);view.GroupDescriptions.Add(new HatchGroup());
        var expander=new FrameworkElementFactory(typeof(Expander));expander.SetValue(Expander.IsExpandedProperty,expandHatches);expander.SetValue(FrameworkElement.MarginProperty,new Thickness(0,5,0,5));
        var header=new FrameworkElementFactory(typeof(TextBlock));header.SetBinding(TextBlock.TextProperty,new Binding("Name"));header.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);
        expander.SetValue(HeaderedContentControl.HeaderTemplateProperty,new DataTemplate{VisualTree=header});expander.SetBinding(HeaderedContentControl.HeaderProperty,new Binding());
        var presenter=new FrameworkElementFactory(typeof(ItemsPresenter));expander.AppendChild(presenter);
        var style=new Style(typeof(GroupItem));style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(GroupItem)){VisualTree=expander}));
        list.GroupStyle.Add(new GroupStyle{ContainerStyle=style,Panel=new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)))});
        list.ItemsPanel=new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));list.ItemsSource=view;
    }
    sealed class HatchGroup:GroupDescription
    {
        public override object GroupNameFromItem(object item,int level,CultureInfo culture)=>Pattern((JObject)((ListBoxItem)item).Tag);
    }
}
