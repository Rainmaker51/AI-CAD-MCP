using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rainmaker.Axis;
internal sealed partial class AxisGridDialog
{
 readonly CheckBox envelopeOn=new(){Content="Nhập theo phủ bì"};
 readonly TextBox envelopeWidth=new(){Text="6000"},envelopeHeight=new(){Text="15000"},envelopeLeft=new(){Text="110"},envelopeRight=new(){Text="110"},envelopeBottom=new(){Text="110"},envelopeTop=new(){Text="110"},divideXCount=new(){Text="3",Width=38},divideYCount=new(){Text="3",Width=38};
 readonly TextBlock remainingX=new(){TextWrapping=TextWrapping.Wrap,FontSize=12},remainingY=new(){TextWrapping=TextWrapping.Wrap,FontSize=12};
 readonly StackPanel envelopeFields=new();
 FrameworkElement BuildEnvelope()
 {
  var panel=new StackPanel();panel.Children.Add(envelopeOn);envelopeFields.Children.Add(Pairs(("Phủ bì X · mm",envelopeWidth),("Phủ bì Y · mm",envelopeHeight)));
  var edges=new StackPanel();edges.Children.Add(Pairs(("Trái → tim · mm",envelopeLeft),("Phải → tim · mm",envelopeRight)));edges.Children.Add(Pairs(("Dưới → tim · mm",envelopeBottom),("Trên → tim · mm",envelopeTop)));envelopeFields.Children.Add(new Expander{Header="Mép ngoài → tim trục · mặc định 110",Content=edges,IsExpanded=false});panel.Children.Add(envelopeFields);
  foreach(var t in new[]{envelopeWidth,envelopeHeight,envelopeLeft,envelopeRight,envelopeBottom,envelopeTop})t.TextChanged+=(_,__)=>{if(ready&&!loading){draftDirty=true;Schedule();}};
  envelopeOn.Checked+=(_,__)=>{if(ready&&!loading){draftDirty=true;Schedule();}};envelopeOn.Unchecked+=(_,__)=>{if(ready&&!loading){draftDirty=true;Schedule();}};
  return panel;
 }
 FrameworkElement BuildBayTools(bool x)
 {
  var panel=new StackPanel{Margin=new Thickness(0,2,0,4)};var row=new WrapPanel();var count=x?divideXCount:divideYCount;count.MinHeight=28;count.ToolTip="Số nhịp chia đều của riêng phương "+(x?"X":"Y")+" (1–99)";row.Children.Add(count);var divide=B("Chia đều",()=>DivideEnvelope(x));divide.ToolTip="Thay nhịp của phương này bằng các nhịp bằng nhau theo phủ bì đã trừ hai đầu. Có thể Hoàn tác.";var fill=B("Điền cuối",()=>FillEnvelope(x));fill.ToolTip="Giữ các nhịp đã nhập, thêm phần còn lại thành nhịp cuối; không trừ hai đầu lần nữa.";row.Children.Add(divide);row.Children.Add(fill);panel.Children.Add(row);panel.Children.Add(x?remainingX:remainingY);envelopeTools.Add(row);return panel;
 }
 readonly System.Collections.Generic.List<FrameworkElement> envelopeTools=new();
 AxisEnvelope? ReadEnvelope(){if(modes.SelectedIndex==1||envelopeOn.IsChecked!=true)return null;var p=new AxisEnvelope{Width=Number(envelopeWidth),Height=Number(envelopeHeight),Left=Number(envelopeLeft),Right=Number(envelopeRight),Bottom=Number(envelopeBottom),Top=Number(envelopeTop)};p.Validate();return p;}
 void LoadEnvelope(AxisEnvelope? profile){envelopeOn.IsChecked=profile!=null;var p=profile??new AxisEnvelope();envelopeWidth.Text=N(p.Width);envelopeHeight.Text=N(p.Height);envelopeLeft.Text=N(p.Left);envelopeRight.Text=N(p.Right);envelopeBottom.Text=N(p.Bottom);envelopeTop.Text=N(p.Top);}
 bool RefreshEnvelope()
 {
  bool enabled=envelopeOn.IsChecked==true&&modes.SelectedIndex!=1;envelopeOn.IsEnabled=modes.SelectedIndex!=1;envelopeFields.IsEnabled=enabled;envelopeFields.Visibility=enabled?Visibility.Visible:Visibility.Collapsed;foreach(var tool in envelopeTools){tool.IsEnabled=enabled;tool.Visibility=enabled?Visibility.Visible:Visibility.Collapsed;}
  if(!enabled){remainingX.Text=remainingY.Text="";return true;}
  try{var p=ReadEnvelope()!;bool Match(TextBlock label,string text,double span,string direction){var bays=string.IsNullOrWhiteSpace(text)?Array.Empty<double>():AxisGridLayout.ParseBays(text);double remaining=AxisEnvelope.Remaining(span,bays);label.Foreground=remaining<0?Brushes.LightPink:remaining==0?Brushes.LightGreen:Brushes.Khaki;label.Text=$"{direction}: tim {N(span)} · đã nhập {N(bays.Sum())}\n"+(remaining<0?$"Vượt {N(-remaining)} mm":remaining==0?"Khớp phủ bì":$"Còn cho nhịp cuối: {N(remaining)} mm");return remaining==0;}bool x=Match(remainingX,xBays.Text,p.SpanX,"X");bool y=Match(remainingY,yBays.Text,p.SpanY,"Y");return x&&y;}
  catch(Exception e){remainingX.Foreground=remainingY.Foreground=Brushes.LightPink;remainingX.Text=e.Message;remainingY.Text="";return false;}
 }
 void FillEnvelope(bool x){try{CheckpointHistory();var p=ReadEnvelope()??throw new ArgumentException("Bật Nhập theo phủ bì trước.");var box=x?xBays:yBays;var values=string.IsNullOrWhiteSpace(box.Text)?Array.Empty<double>():AxisGridLayout.ParseBays(box.Text);double left=AxisEnvelope.Remaining(x?p.SpanX:p.SpanY,values);if(left<=0)throw new ArgumentException(left==0?"Đã khớp, không cần thêm nhịp cuối.":"Nhịp đã vượt phủ bì; giảm nhịp trước khi điền cuối.");var next=values.Concat(new[]{left}).ToArray();AxisGridLayout.Positions(next);box.Text=string.Join(" ",next.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));UpdateState();}catch(Exception e){error.Text=e.Message;}}
 void DivideEnvelope(bool x){try{CheckpointHistory();var p=ReadEnvelope()??throw new ArgumentException("Bật Nhập theo phủ bì trước.");var count=x?divideXCount:divideYCount;if(!int.TryParse(count.Text,NumberStyles.None,CultureInfo.InvariantCulture,out int n))throw new ArgumentException("Nhập số nhịp nguyên 1–99.");var bays=AxisEnvelope.Divide(x?p.SpanX:p.SpanY,n);(x?xBays:yBays).Text=string.Join(" ",bays.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));UpdateState();}catch(Exception e){error.Text=e.Message;}}
}
