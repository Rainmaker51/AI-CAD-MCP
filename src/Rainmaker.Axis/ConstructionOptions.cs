using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Rainmaker.Axis;

internal sealed class ColumnOptions
{
 public string Shape="Rectangle",Anchor="Center";
 public double B=300,H=300,Rotation,OffsetX,OffsetY,WallInset=110;
 public ColumnOptions Copy()=>JsonConvert.DeserializeObject<ColumnOptions>(JsonConvert.SerializeObject(this))!;
 internal static ColumnOptions Default(AxisGridParameters p)=>new(){Shape=p.ColumnShape,Anchor=p.ColumnAnchor,B=p.ColumnB,H=p.ColumnH,Rotation=p.ColumnRotation,OffsetX=p.ColumnOffsetX,OffsetY=p.ColumnOffsetY,WallInset=p.ColumnWallInset};
 internal static ColumnOptions At(AxisGridParameters p,string key)=>p.ColumnOverrides.TryGetValue(key,out var value)?value:Default(p);
 internal void SetDefault(AxisGridParameters p){p.ColumnShape=Shape;p.ColumnAnchor=Anchor;p.ColumnB=B;p.ColumnH=H;p.ColumnRotation=Rotation;p.ColumnOffsetX=OffsetX;p.ColumnOffsetY=OffsetY;p.ColumnWallInset=WallInset;}
 internal static readonly string[] Anchors={"Center","WallAuto","WallLeft","WallRight","WallTop","WallBottom","WallTopLeft","WallTopRight","WallBottomLeft","WallBottomRight","Left","Right","Bottom","Top","Corner"};
 internal static string AnchorLabel(string key)=>key switch{"Center"=>"Center · Đúng tim","WallAuto"=>"Tim tường · Biên tự động","WallLeft"=>"Tim tường · Trái","WallRight"=>"Tim tường · Phải","WallTop"=>"Tim tường · Trên","WallBottom"=>"Tim tường · Dưới","WallTopLeft"=>"Tim tường · Trên trái","WallTopRight"=>"Tim tường · Trên phải","WallBottomLeft"=>"Tim tường · Dưới trái","WallBottomRight"=>"Tim tường · Dưới phải","Left"=>"Mép trái cột","Right"=>"Mép phải cột","Bottom"=>"Mép dưới cột","Top"=>"Mép trên cột","Corner"=>"Góc dưới trái cột",_=>key};
}
internal sealed class WallOptions
{
 public double Thickness=220,Offset;
 public string Alignment="Center";
 public WallOptions Copy()=>new(){Thickness=Thickness,Offset=Offset,Alignment=Alignment};
 internal static WallOptions Default(AxisGridParameters p)=>new(){Thickness=p.WallThickness,Offset=p.WallOffset,Alignment=p.WallAlignment};
 internal static WallOptions At(AxisGridParameters p,string key)=>p.WallOverrides.TryGetValue(key,out var value)?value:Default(p);
}
internal static class ConstructionDraft
{
 internal static AxisGridParameters Copy(AxisGridParameters p)=>JsonConvert.DeserializeObject<AxisGridParameters>(JsonConvert.SerializeObject(p))!;
 internal static void MigrateDirectedWalls(AxisGridParameters p){if(p.DirectedWalls)return;foreach(var key in p.WallSegments.Where(k=>k.StartsWith("Y:",StringComparison.Ordinal))){var v=WallOptions.At(p,key).Copy();v.Offset=-v.Offset;p.WallOverrides[key]=v;}p.DirectedWalls=true;}
 internal static string[] Perimeter(AxisGridParameters p)=>ConstructionGeometry.Segments(p).Where(k=>{var a=k.Split(':');int i=int.Parse(a[1]),j=int.Parse(a[2]);return a[0]=="X"?j==0||j==p.YBays.Length:i==0||i==p.XBays.Length;}).ToArray();
}
