using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;

namespace Rainmaker.Axis;

internal sealed class ColumnFootprint
{
 internal string Key="";internal Point2 Center=new();internal double B,H,Angle;internal bool Circle;
 internal List<Point2> Corners()=>new[]{new Point2(-B/2,-H/2),new Point2(B/2,-H/2),new Point2(B/2,H/2),new Point2(-B/2,H/2)}.Select(p=>new Point2(Center.X+p.X*Math.Cos(Angle)-p.Y*Math.Sin(Angle),Center.Y+p.X*Math.Sin(Angle)+p.Y*Math.Cos(Angle))).ToList();
}
internal sealed class WallFootprint
{
 internal string Key="";internal List<Point2> Corners=new();
 internal Point2 A=new(),B=new();
}
internal sealed class ConstructionGeometry
{
 internal List<ColumnFootprint> Columns=new();internal List<WallFootprint> Walls=new();
 internal static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v)&&Math.Abs(v)<=10000000;
 internal static string[] Nodes(AxisGridParameters p)=>Enumerable.Range(0,p.XBays.Length+1).SelectMany(x=>Enumerable.Range(0,p.YBays.Length+1).Select(y=>$"{x}:{y}")).ToArray();
 internal static string[] Segments(AxisGridParameters p)=>Enumerable.Range(0,p.XBays.Length).SelectMany(x=>Enumerable.Range(0,p.YBays.Length+1).Select(y=>$"X:{x}:{y}")).Concat(Enumerable.Range(0,p.XBays.Length+1).SelectMany(x=>Enumerable.Range(0,p.YBays.Length).Select(y=>$"Y:{x}:{y}"))).ToArray();
 internal static ConstructionGeometry Make(AxisGridParameters p,Point2 origin)
 {
  var result=new ConstructionGeometry();if(p.Single){if(p.Columns||p.Walls)throw new ArgumentException("Cột/tường cần chế độ Lưới trục.");return result;}
  var xs=AxisGridLayout.Positions(p.XBays);var ys=AxisGridLayout.Positions(p.YBays);
  if(p.Columns){var valid=Nodes(p);if(p.ColumnNodes.Distinct().Count()!=p.ColumnNodes.Length||p.ColumnNodes.Any(k=>!valid.Contains(k)))throw new ArgumentException("Chọn giao điểm cột hợp lệ; lưới đổi có thể làm mất vị trí đã chọn.");
   foreach(var key in p.ColumnNodes){var v=ColumnOptions.At(p,key);if(!AxisLibrary.ValidDistance(v.B)||!AxisLibrary.ValidDistance(v.H)||!Finite(v.Rotation)||!Finite(v.OffsetX)||!Finite(v.OffsetY)||!Finite(v.WallInset)||v.WallInset<0||!new[]{"Rectangle","Square","Circle"}.Contains(v.Shape)||!ColumnOptions.Anchors.Contains(v.Anchor))throw new ArgumentException("Thông số cột không hợp lệ tại "+key);
    var ij=key.Split(':').Select(int.Parse).ToArray();double b=v.B,h=v.Shape=="Rectangle"?v.H:b,angle=v.Rotation*Math.PI/180;
    double dx=v.Anchor=="Left"||v.Anchor=="Corner"?b/2:v.Anchor=="Right"?-b/2:0,dy=v.Anchor=="Bottom"||v.Anchor=="Corner"?h/2:v.Anchor=="Top"?-h/2:0;
    double wx=0,wy=0;if(v.Anchor.StartsWith("Wall",StringComparison.Ordinal)){string direction=v.Anchor.Substring(4);if(direction=="Auto"){wx=ij[0]==0?v.WallInset:ij[0]==p.XBays.Length?-v.WallInset:0;wy=ij[1]==0?v.WallInset:ij[1]==p.YBays.Length?-v.WallInset:0;}else{wx=direction.Contains("Left")?-v.WallInset:direction.Contains("Right")?v.WallInset:0;wy=direction.Contains("Top")?v.WallInset:direction.Contains("Bottom")?-v.WallInset:0;}}
    if(p.ColumnFaceAnchors&&v.Anchor.StartsWith("Wall",StringComparison.Ordinal)){
     double ex=v.Shape=="Circle"?b/2:(Math.Abs(b*Math.Cos(angle))+Math.Abs(h*Math.Sin(angle)))/2,ey=v.Shape=="Circle"?b/2:(Math.Abs(b*Math.Sin(angle))+Math.Abs(h*Math.Cos(angle)))/2;
     string direction=v.Anchor.Substring(4);int sx=direction=="Auto"?(ij[0]==0?1:ij[0]==p.XBays.Length?-1:0):direction.Contains("Left")?-1:direction.Contains("Right")?1:0,sy=direction=="Auto"?(ij[1]==0?1:ij[1]==p.YBays.Length?-1:0):direction.Contains("Top")?1:direction.Contains("Bottom")?-1:0;
     wx=sx*(ex-v.WallInset);wy=sy*(ey-v.WallInset);
     if(direction=="Auto"&&p.Envelope!=null){wx=sx*(ex-(ij[0]==0?p.Envelope.Left:p.Envelope.Right));wy=sy*(ey-(ij[1]==0?p.Envelope.Bottom:p.Envelope.Top));}
    }
    result.Columns.Add(new(){Key=key,B=b,H=h,Circle=v.Shape=="Circle",Angle=angle,Center=new(origin.X+xs[ij[0]]+v.OffsetX+wx+dx*Math.Cos(angle)-dy*Math.Sin(angle),origin.Y+ys[ij[1]]+v.OffsetY+wy+dx*Math.Sin(angle)+dy*Math.Cos(angle))});
   }
  }
  if(p.Walls){var valid=Segments(p);if(p.WallSegments.Distinct().Count()!=p.WallSegments.Length||p.WallSegments.Any(k=>!valid.Contains(k)))throw new ArgumentException("Chọn đoạn tường hợp lệ; lưới đổi có thể làm mất đoạn đã chọn.");
   foreach(var key in p.WallSegments){var v=WallOptions.At(p,key);if(!AxisLibrary.ValidDistance(v.Thickness)||!Finite(v.Offset)||!new[]{"Center","Left","Right"}.Contains(v.Alignment))throw new ArgumentException("Thông số tường không hợp lệ tại "+key);var parts=key.Split(':');int i=int.Parse(parts[1]),j=int.Parse(parts[2]);double half=v.Thickness/2,shift=v.Offset+(v.Alignment=="Left"?half:v.Alignment=="Right"?-half:0);
    if(parts[0]=="X"){double y=origin.Y+ys[j]+shift,x=origin.X+xs[i],end=origin.X+xs[i+1];result.Walls.Add(new(){Key=key,A=new(x,y),B=new(end,y),Corners=new(){new(x-half,y-half),new(end+half,y-half),new(end+half,y+half),new(x-half,y+half)}});}
    else{double x=origin.X+xs[i]+(p.DirectedWalls?-shift:shift),y=origin.Y+ys[j],end=origin.Y+ys[j+1];result.Walls.Add(new(){Key=key,A=new(x,y),B=new(x,end),Corners=new(){new(x-half,y-half),new(x+half,y-half),new(x+half,end+half),new(x-half,end+half)}});}
   }
   if(p.CleanWallJunctions)CleanJunctions(p,result.Walls,origin,xs,ys);
  }return result;
 }
 // Butt ends and the real intersecting wall faces replace the old square half-width caps.
 // Centered perimeter segments share the default outer face even when their widths differ.
 static void CleanJunctions(AxisGridParameters p,List<WallFootprint> walls,Point2 origin,double[] xs,double[] ys)
 {
  var endpoints=new Dictionary<string,List<WallFootprint>>();
  foreach(var w in walls){var a=w.Key.Split(':');int i=int.Parse(a[1]),j=int.Parse(a[2]);bool horizontal=a[0]=="X";var v=WallOptions.At(p,w.Key);double half=v.Thickness/2;double shift=v.Offset+(v.Alignment=="Left"?half:v.Alignment=="Right"?-half:0);
   if(v.Alignment=="Center"){double delta=(p.WallThickness-v.Thickness)/2;if(horizontal){if(j==0)shift+=p.Envelope!=null?half-p.Envelope.Bottom:-delta;else if(j==p.YBays.Length)shift+=p.Envelope!=null?p.Envelope.Top-half:delta;}else{if(i==0)shift+=p.Envelope!=null?p.Envelope.Left-half:delta;else if(i==p.XBays.Length)shift+=p.Envelope!=null?half-p.Envelope.Right:-delta;}}
   w.A=horizontal?new(origin.X+xs[i],origin.Y+ys[j]+shift):new(origin.X+xs[i]-shift,origin.Y+ys[j]);w.B=horizontal?new(origin.X+xs[i+1],w.A.Y):new(w.A.X,origin.Y+ys[j+1]);
   foreach(string key in new[]{$"{i}:{j}",horizontal?$"{i+1}:{j}":$"{i}:{j+1}"}){if(!endpoints.TryGetValue(key,out var list))endpoints[key]=list=new();list.Add(w);}
  }
  foreach(var w in walls){var a=w.Key.Split(':');int i=int.Parse(a[1]),j=int.Parse(a[2]);bool horizontal=a[0]=="X";double half=WallOptions.At(p,w.Key).Thickness/2;
   double start=horizontal?w.A.X:w.A.Y,end=horizontal?w.B.X:w.B.Y;
   var keys=new[]{$"{i}:{j}",horizontal?$"{i+1}:{j}":$"{i}:{j+1}"};
   for(int side=0;side<2;side++){var across=endpoints[keys[side]].Where(o=>o.Key[0]!=w.Key[0]).ToArray();if(across.Length==0){if(endpoints[keys[side]].Count>1){if(side==0)start-=.001;else end+=.001;}continue;}
    double face=side==0?across.Min(o=>(horizontal?o.A.X:o.A.Y)-WallOptions.At(p,o.Key).Thickness/2):across.Max(o=>(horizontal?o.A.X:o.A.Y)+WallOptions.At(p,o.Key).Thickness/2);if(side==0)start=face;else end=face;
   }
   w.Corners=horizontal?new(){new(start,w.A.Y-half),new(end,w.A.Y-half),new(end,w.A.Y+half),new(start,w.A.Y+half)}:new(){new(w.A.X-half,start),new(w.A.X+half,start),new(w.A.X+half,end),new(w.A.X-half,end)};
  }
 }
}
