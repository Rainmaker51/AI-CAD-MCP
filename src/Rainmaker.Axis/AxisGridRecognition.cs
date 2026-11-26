using System;
using System.Collections.Generic;
using System.Linq;

namespace Rainmaker.Axis;

internal readonly struct AxisSegment
{
 internal readonly double X1,Y1,X2,Y2;
 internal AxisSegment(double x1,double y1,double x2,double y2){X1=x1;Y1=y1;X2=x2;Y2=y2;}
}
internal sealed class RecognizedAxes
{
 internal double[] X=Array.Empty<double>(),Y=Array.Empty<double>();
}
internal static class AxisGridRecognition
{
 // Numerical join tolerance in WCS millimetres, never a wall-thickness snap.
 internal const double Tolerance=0.01;
 internal static RecognizedAxes Read(IEnumerable<AxisSegment> source)
 {
  var segments=source.ToArray();
  if(segments.Length>10000)throw new ArgumentException("Tối đa 10.000 đoạn trục trong một lần nhận.");
  var horizontal=new List<(double at,double start,double end)>();var vertical=new List<(double at,double start,double end)>();
  foreach(var s in segments){
   if(new[]{s.X1,s.Y1,s.X2,s.Y2}.Any(v=>double.IsNaN(v)||double.IsInfinity(v)))throw new ArgumentException("Tọa độ trục không hợp lệ.");
   double dx=Math.Abs(s.X2-s.X1),dy=Math.Abs(s.Y2-s.Y1);
   if(dx<=Tolerance&&dy<=Tolerance)continue;
   if(dx<=Tolerance)vertical.Add(((s.X1+s.X2)/2,Math.Min(s.Y1,s.Y2),Math.Max(s.Y1,s.Y2)));
   else if(dy<=Tolerance)horizontal.Add(((s.Y1+s.Y2)/2,Math.Min(s.X1,s.X2),Math.Max(s.X1,s.X2)));
   else throw new ArgumentException("Chỉ nhận đoạn thẳng ngang/đứng theo WCS XY; không nhận đoạn chéo hoặc cong.");
  }
  List<(double at,List<(double start,double end)> ranges)> Groups(List<(double at,double start,double end)> list){
   var groups=new List<(double at,List<(double start,double end)> ranges)>();
   foreach(var s in list.OrderBy(s=>s.at)){
    if(groups.Count==0||s.at-groups[groups.Count-1].at>Tolerance)groups.Add((s.at,new List<(double,double)>()));
    groups[groups.Count-1].ranges.Add((s.start,s.end));
   }return groups;
  }
  var xs=Groups(vertical);var ys=Groups(horizontal);
  if(xs.Count<2||ys.Count<2)throw new ArgumentException("Cần ít nhất hai trục đứng và hai trục ngang giao nhau.");
  if(xs.Count>50||ys.Count>50)throw new ArgumentException("Mỗi phương tối đa 50 trục.");
  bool Covers(List<(double start,double end)> ranges,double from,double to){
   double reached=from;
   foreach(var r in ranges.OrderBy(r=>r.start)){
    if(r.end<from-Tolerance)continue;
    if(r.start>reached+Tolerance)return false;
    reached=Math.Max(reached,r.end);if(reached>=to-Tolerance)return true;
   }return false;
  }
  if(xs.Any(g=>!Covers(g.ranges,ys.First().at,ys.Last().at))||ys.Any(g=>!Covers(g.ranges,xs.First().at,xs.Last().at)))
   throw new ArgumentException("Các đoạn chưa nối đủ thành lưới chữ nhật. Nối đoạn bị hở hoặc chỉ chọn một lưới đầy đủ; không tự kéo dài qua khoảng trống.");
  return new(){X=xs.Select(g=>g.at).ToArray(),Y=ys.Select(g=>g.at).ToArray()};
 }
}
