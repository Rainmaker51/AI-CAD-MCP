using System;
using System.Linq;

namespace Rainmaker.Axis;
internal sealed class AxisEnvelope
{
 public double Width=6000,Height=15000,Left=110,Right=110,Bottom=110,Top=110;
 public double SpanX=>Width-Left-Right;
 public double SpanY=>Height-Bottom-Top;
 internal const double Tolerance=.000001;
 internal void Validate(){bool Valid(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n)&&n>=0&&n<=10000000;if(!new[]{Width,Height,Left,Right,Bottom,Top}.All(Valid)||Width<=0||Height<=0||SpanX<=0||SpanY<=0)throw new ArgumentException("Phủ bì phải lớn hơn tổng hai khoảng mép → tim; nhập số hữu hạn, không âm.");}
 internal static double Remaining(double span,double[] bays){double left=span-bays.Sum();return Math.Abs(left)<=Tolerance?0:left;}
 internal double RemainingX(double[] bays)=>Remaining(SpanX,bays);
 internal double RemainingY(double[] bays)=>Remaining(SpanY,bays);
 internal static double[] Divide(double span,int count){if(count<1||count>99||double.IsNaN(span)||double.IsInfinity(span)||span<=0)throw new ArgumentException("Số nhịp chia đều phải từ 1 đến 99.");var values=Enumerable.Repeat(span/count,count).ToArray();values[count-1]=span-values.Take(count-1).Sum();AxisGridLayout.Positions(values);return values;}
 internal void DemandMatch(double[] x,double[] y){Validate();if(RemainingX(x)!=0||RemainingY(y)!=0)throw new ArgumentException("Tổng nhịp chưa khớp phủ bì: hoàn tất nhịp cuối hoặc sửa phần vượt trước khi ghi CAD.");}
}
