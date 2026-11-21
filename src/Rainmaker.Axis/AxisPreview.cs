using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AICad.Contracts;

namespace Rainmaker.Axis;

internal sealed class PreviewHit
{
 internal string Kind="",Key="";internal Point A,B;internal Rect Bounds;internal Geometry Shape=Geometry.Empty;
}
internal sealed class AxisPreview:FrameworkElement
{
 DrawingPlan? plan;ConstructionGeometry? construction;AxisGridParameters? parameters;
 internal double Zoom=1;internal Vector Pan;
 internal string PickMode="Columns";
 internal HashSet<string> PickedColumns=new(),PickedWalls=new();
 internal Func<string,string[],bool>? SelectionRequested;
 internal Action<string>? DeleteRequested;
 CadScene? existing;Geometry cachedExisting=Geometry.Empty;
 internal int ExistingBuilds;
 internal CadScene? Existing{get=>existing;set{if(ReferenceEquals(existing,value))return;existing=value;var path=new StreamGeometry();using(var ctx=path.Open())if(value!=null)foreach(var points in value.Paths){if(points.Count==0)continue;ctx.BeginFigure(new Point(points[0].X,points[0].Y),false,false);ctx.PolyLineTo(points.Skip(1).Select(p=>new Point(p.X,p.Y)).ToArray(),true,false);}path.Freeze();cachedExisting=path;ExistingBuilds++;}}
 internal bool ShowExisting=true,ShowColumnAxes=true;
 Geometry cachedWalls=Geometry.Empty,cachedWallUnion=Geometry.Empty;string wallKey="";ConstructionGeometry? candidates;string geometryKey="";
 internal int BooleanBuilds;
 internal readonly List<(string Key,Point Origin,Point X,Point Y)> ColumnAxisMarks=new();
 internal readonly List<PreviewHit> Hits=new();
 Func<Point2,Point> project=p=>new Point(p.X,p.Y);
 Point? down,panStart;Rect? marquee;bool crossing;MouseButton? panButton;
 internal AxisPreview(){Focusable=true;Cursor=Cursors.Cross;System.Windows.Automation.AutomationProperties.SetName(this,"Preview tương tác: chọn cột hoặc đoạn tường");}
 internal void Update(DrawingPlan? value,ConstructionGeometry? geometry=null,AxisGridParameters? p=null){plan=value;construction=geometry;parameters=p;
  if(p==null||geometry==null){candidates=null;cachedWalls=Geometry.Empty;geometryKey="";}
  else{string key=Newtonsoft.Json.JsonConvert.SerializeObject(new{p.Envelope,p.Columns,p.Walls,p.ColumnNodes,p.WallSegments,p.ColumnShape,p.ColumnB,p.ColumnH,p.ColumnRotation,p.ColumnAnchor,p.ColumnOffsetX,p.ColumnOffsetY,p.ColumnWallInset,p.ColumnFaceAnchors,p.ColumnOverrides,p.WallThickness,p.WallOffset,p.WallAlignment,p.WallOverrides,p.DirectedWalls,p.CleanWallJunctions,p.XBays,p.YBays});if(key!=geometryKey){
   string newWallKey=Newtonsoft.Json.JsonConvert.SerializeObject(geometry.Walls.Select(w=>new{w.Key,w.Corners}));if(newWallKey!=wallKey){var wallGroup=new GeometryGroup{FillRule=FillRule.Nonzero};foreach(var w in geometry.Walls)wallGroup.Children.Add(WorldPolygon(w.Corners));cachedWallUnion=Geometry.Combine(wallGroup,Geometry.Empty,GeometryCombineMode.Union,null,.01,ToleranceType.Absolute);cachedWallUnion.Freeze();wallKey=newWallKey;}
   var cuts=new GeometryGroup{FillRule=FillRule.Nonzero};foreach(var c in geometry.Columns)cuts.Children.Add(WorldColumn(c));
   // Materialize a flat path once. A nested CombinedGeometry tree was recursively
   // evaluated for every repaint, including pan/marquee, causing long UI stalls.
   cachedWalls=Geometry.Combine(cachedWallUnion,cuts,GeometryCombineMode.Exclude,null,.01,ToleranceType.Absolute);cachedWalls.Freeze();BooleanBuilds++;
   var all=ConstructionDraft.Copy(p);if(all.Columns)all.ColumnNodes=ConstructionGeometry.Nodes(all);if(all.Walls)all.WallSegments=ConstructionGeometry.Segments(all);candidates=ConstructionGeometry.Make(all,new());geometryKey=key;
  }}InvalidateVisual();}
 internal static Geometry WorldPolygon(IEnumerable<Point2> vertices){var shape=new StreamGeometry();using(var ctx=shape.Open()){var v=vertices.Select(p=>new Point(p.X,p.Y)).ToArray();ctx.BeginFigure(v[0],true,true);ctx.PolyLineTo(v.Skip(1).ToArray(),true,false);}shape.Freeze();return shape;}
 internal static Geometry WorldColumn(ColumnFootprint c)=>c.Circle?new EllipseGeometry(new Point(c.Center.X,c.Center.Y),c.B/2,c.B/2):WorldPolygon(c.Corners());
 internal Point Project(Point2 p)=>project(p);
 internal void Fit(){Zoom=1;Pan=new();InvalidateVisual();}
 internal void ZoomAt(double factor,Point anchor){double old=Zoom;Zoom=Math.Max(.2,Math.Min(8,Zoom*factor));var offset=anchor-new Point(ActualWidth/2,ActualHeight/2);Pan=offset-(offset-Pan)*(Zoom/old);InvalidateVisual();}
 void SelectKeys(string[] keys,bool additive,bool toggle=false){var old=PickMode=="Columns"?PickedColumns:PickedWalls;var set=additive?new HashSet<string>(old):new HashSet<string>();foreach(var key in keys){if(toggle&&!set.Add(key))set.Remove(key);else set.Add(key);}if(SelectionRequested?.Invoke(PickMode,set.ToArray())!=false){if(PickMode=="Columns")PickedColumns=set;else PickedWalls=set;InvalidateVisual();}}
 static double Distance(Point point,Point a,Point b){var v=b-a;double t=v.LengthSquared<1e-12?0:Math.Max(0,Math.Min(1,Vector.Multiply(point-a,v)/v.LengthSquared));return (point-(a+t*v)).Length;}
 internal void SelectAt(Point point,bool additive=false){var hit=Hits.Where(h=>h.Kind==PickMode).Select(h=>new{Hit=h,Distance=h.Kind=="Columns"?(point-h.A).Length:Distance(point,h.A,h.B)}).Where(h=>h.Distance<=10||h.Hit.Kind=="Columns"&&h.Hit.Bounds.Contains(point)).OrderBy(h=>h.Distance).FirstOrDefault();SelectKeys(hit==null?Array.Empty<string>():new[]{hit.Hit.Key},additive,additive);}
 internal void SelectRectangle(Rect rectangle,bool additive=false,bool crossing=true){var area=new RectangleGeometry(rectangle);SelectKeys(Hits.Where(h=>h.Kind==PickMode&&(crossing?rectangle.IntersectsWith(h.Bounds)&&h.Shape.FillContainsWithDetail(area)!=IntersectionDetail.Empty:rectangle.Contains(h.Bounds))).Select(h=>h.Key).ToArray(),additive);}
 internal void BeginPan(Point point){panStart=point;}
 internal void MovePan(Point point){if(!panStart.HasValue)return;Pan+=point-panStart.Value;panStart=point;InvalidateVisual();}
 internal void EndPan(){panStart=null;}
 protected override void OnMouseDown(MouseButtonEventArgs e){base.OnMouseDown(e);Focus();if(e.ChangedButton==MouseButton.Left)down=e.GetPosition(this);else if(e.ChangedButton==MouseButton.Middle||e.ChangedButton==MouseButton.Right){panButton=e.ChangedButton;BeginPan(e.GetPosition(this));}else return;CaptureMouse();e.Handled=true;}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);var point=e.GetPosition(this);if(panStart.HasValue){MovePan(point);}else if(down.HasValue&&(point-down.Value).Length>5){marquee=new Rect(down.Value,point);crossing=point.X<down.Value.X;InvalidateVisual();}}
 protected override void OnMouseUp(MouseButtonEventArgs e){base.OnMouseUp(e);if(e.ChangedButton==MouseButton.Left&&down.HasValue){bool ctrl=(Keyboard.Modifiers&ModifierKeys.Control)!=0;if(marquee.HasValue)SelectRectangle(marquee.Value,ctrl,crossing);else SelectAt(e.GetPosition(this),ctrl);}if(panButton.HasValue&&e.ChangedButton!=panButton.Value)return;down=panStart=null;panButton=null;marquee=null;ReleaseMouseCapture();InvalidateVisual();e.Handled=true;}
 protected override void OnMouseWheel(MouseWheelEventArgs e){ZoomAt(e.Delta>0?1.15:1/1.15,e.GetPosition(this));e.Handled=true;}
 protected override void OnLostMouseCapture(MouseEventArgs e){base.OnLostMouseCapture(e);down=panStart=null;panButton=null;marquee=null;InvalidateVisual();}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.Key==Key.A&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){SelectKeys(Hits.Where(h=>h.Kind==PickMode).Select(h=>h.Key).ToArray(),false);e.Handled=true;}else if(e.Key==Key.Escape&&(PickMode=="Columns"?PickedColumns.Count:PickedWalls.Count)>0){SelectKeys(Array.Empty<string>(),false);e.Handled=true;}else if(e.Key==Key.Delete){DeleteRequested?.Invoke(PickMode);e.Handled=true;}}
 protected override void OnRender(DrawingContext dc)
 {
  base.OnRender(dc);Hits.Clear();ColumnAxisMarks.Clear();dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(12,19,28)),null,new Rect(RenderSize));if(plan==null||ActualWidth<30||ActualHeight<30)return;dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
  var points=plan.Operations.SelectMany(o=>o.Points).ToList();if(construction!=null){points.AddRange(construction.Columns.SelectMany(c=>c.Circle?new[]{new Point2(c.Center.X-c.B/2,c.Center.Y-c.B/2),new Point2(c.Center.X+c.B/2,c.Center.Y+c.B/2)}:c.Corners().ToArray()));points.AddRange(construction.Walls.SelectMany(w=>w.Corners));}if(ShowExisting&&Existing!=null&&!cachedExisting.Bounds.IsEmpty){var bounds=cachedExisting.Bounds;points.Add(new(bounds.Left,bounds.Top));points.Add(new(bounds.Right,bounds.Bottom));}if(parameters?.Envelope is AxisEnvelope envelope){points.Add(new(-envelope.Left,-envelope.Bottom));points.Add(new(envelope.Width-envelope.Left,envelope.Height-envelope.Bottom));}if(points.Count==0){dc.Pop();return;}
  double minX=points.Min(p=>p.X),maxX=points.Max(p=>p.X),minY=points.Min(p=>p.Y),maxY=points.Max(p=>p.Y);double factor=Math.Max(.000001,Math.Min((ActualWidth-56)/Math.Max(1,maxX-minX),(ActualHeight-68)/Math.Max(1,maxY-minY)))*Zoom;
  Point P(Point2 p)=>new(ActualWidth/2+(p.X-(minX+maxX)/2)*factor+Pan.X,ActualHeight/2-(p.Y-(minY+maxY)/2)*factor+Pan.Y);project=P;
  Geometry Screen(Geometry world){var shape=world.Clone();shape.Transform=new MatrixTransform(factor,0,0,-factor,ActualWidth/2-(minX+maxX)/2*factor+Pan.X,ActualHeight/2+(minY+maxY)/2*factor+Pan.Y);return shape;}
  if(parameters?.Envelope is AxisEnvelope frame){var a=P(new(-frame.Left,-frame.Bottom));var b=P(new(frame.Width-frame.Left,frame.Height-frame.Bottom));dc.DrawRectangle(null,new Pen(Brushes.MediumPurple,1.4){DashStyle=DashStyles.Dash},new Rect(a,b));var caption=new FormattedText($"Phủ bì {frame.Width:0.##} × {frame.Height:0.##} mm",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,Brushes.Plum,1);dc.DrawText(caption,new Point(Math.Min(a.X,b.X),Math.Max(a.Y,b.Y)+3));}
  var ghost=new Pen(new SolidColorBrush(Color.FromArgb(80,145,170,190)),1){DashStyle=DashStyles.Dash};
  if(construction!=null&&parameters!=null&&candidates!=null){if(parameters.ShowWalls)dc.DrawGeometry(parameters.ShowHatch&&parameters.WallHatch?new SolidColorBrush(Color.FromArgb(65,116,179,194)):null,new Pen(Brushes.Teal,1),Screen(cachedWalls));
   if(parameters.ShowColumns)foreach(var c in construction.Columns)dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(100,123,153)),new Pen(Brushes.LightSteelBlue,1),Screen(WorldColumn(c)));
   var activeColumns=new HashSet<string>(parameters.ColumnNodes);var activeWalls=construction.Walls.ToDictionary(w=>w.Key);var actualColumns=construction.Columns.ToDictionary(c=>c.Key);
   if(parameters.Columns&&parameters.ShowColumns)foreach(var candidate in candidates.Columns){var c=actualColumns.TryGetValue(candidate.Key,out var actual)?actual:candidate;var shape=Screen(WorldColumn(c));Hits.Add(new(){Kind="Columns",Key=c.Key,A=P(c.Center),B=P(c.Center),Bounds=shape.Bounds,Shape=shape});if(!activeColumns.Contains(c.Key))dc.DrawGeometry(null,ghost,shape);if(PickedColumns.Contains(c.Key)){dc.DrawGeometry(null,new Pen(Brushes.Gold,2.5),shape);dc.DrawEllipse(null,new Pen(Brushes.Gold,1),P(c.Center),6,6);
     if(ShowColumnAxes){var origin=P(c.Center);var x=origin+new Vector(Math.Cos(c.Angle),-Math.Sin(c.Angle))*28;var y=origin+new Vector(-Math.Sin(c.Angle),-Math.Cos(c.Angle))*28;ColumnAxisMarks.Add((c.Key,origin,x,y));
      void Arrow(Point end,Brush color,string label){var pen=new Pen(color,1.5);dc.DrawLine(pen,origin,end);var v=end-origin;v.Normalize();var n=new Vector(-v.Y,v.X);dc.DrawLine(pen,end,end-v*6+n*3);dc.DrawLine(pen,end,end-v*6-n*3);var t=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,color,1);dc.DrawText(t,end+v*4);}
      Arrow(x,Brushes.Salmon,"Xc");Arrow(y,Brushes.LightGreen,"Yc");
     }
    }}
   if(parameters.Walls&&parameters.ShowWalls)foreach(var candidate in candidates.Walls){var w=activeWalls.TryGetValue(candidate.Key,out var actual)?actual:candidate;var a=P(w.A);var b=P(w.B);bool active=activeWalls.ContainsKey(w.Key);var shape=active?Screen(WorldPolygon(w.Corners)):new LineGeometry(a,b).GetWidenedPathGeometry(new Pen(Brushes.White,3));Hits.Add(new(){Kind="Walls",Key=w.Key,A=a,B=b,Bounds=shape.Bounds,Shape=shape});if(!active)dc.DrawLine(ghost,a,b);if(PickedWalls.Contains(w.Key)){dc.DrawGeometry(null,new Pen(Brushes.Gold,2),shape);var v=b-a;if(v.Length>1){v.Normalize();var mid=a+(b-a)*.5;dc.DrawLine(new Pen(Brushes.Gold,2),mid,mid-v*8+new Vector(-v.Y,v.X)*4);dc.DrawLine(new Pen(Brushes.Gold,2),mid,mid-v*8-new Vector(-v.Y,v.X)*4);}}}
  }
  if(ShowExisting&&Existing!=null){var pen=new Pen(new SolidColorBrush(Color.FromArgb(170,255,167,76)),1.2){DashStyle=DashStyles.Dot};dc.DrawGeometry(null,pen,Screen(cachedExisting));}
  var axis=new Pen(new SolidColorBrush(Color.FromRgb(160,185,221)),1){DashStyle=DashStyles.DashDot};var dims=new Pen(new SolidColorBrush(Color.FromRgb(107,157,206)),.8);var occupied=new List<Rect>();
  void Text(string text,Point point,Brush color,bool vertical=false,bool avoid=false){var f=new FormattedText(text,CultureInfo.GetCultureInfo("vi-VN"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,color,1);var rect=new Rect(point.X-f.Width/2,point.Y-f.Height/2,f.Width,f.Height);if(avoid&&!vertical){for(int i=0;i<6&&occupied.Any(r=>r.IntersectsWith(rect));i++)rect.Offset(i%2==0?12:-24,0);}occupied.Add(rect);if(vertical)dc.PushTransform(new RotateTransform(-90,point.X,point.Y));dc.DrawText(f,rect.TopLeft);if(vertical)dc.Pop();}
  foreach(var op in plan.Operations.Where(o=>o.Kind=="insert_block")){var p=P(op.Points[0]);occupied.Add(new Rect(p.X-12,p.Y-12,24,24));}
  foreach(var op in plan.Operations){if(op.Kind=="draw_line")dc.DrawLine(axis,P(op.Points[0]),P(op.Points[1]));else if(op.Kind=="insert_block"){var p=P(op.Points[0]);dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(18,29,43)),new Pen(Brushes.LightSteelBlue,1),p,11,11);Text(op.AttributeText??"",p,Brushes.White);}else if(op.Kind=="add_dimension"){var a=P(op.Points[0]);var b=P(op.Points[1]);var l=P(op.Points[2]);bool horizontal=Math.Abs(a.Y-b.Y)<1e-6;var x=horizontal?new Point(a.X,l.Y):new Point(l.X,a.Y);var y=horizontal?new Point(b.X,l.Y):new Point(l.X,b.Y);Point Foot(Point origin,Point end){var v=origin-end;if(parameters?.FixedDimExtensions!=true||v.Length<.00001)return origin;double length=Math.Min(v.Length,parameters.DimExtensionLength*factor);v.Normalize();return end+v*length;}dc.DrawLine(dims,Foot(a,x),x);dc.DrawLine(dims,Foot(b,y),y);dc.DrawLine(dims,x,y);foreach(var mark in new[]{x,y})dc.DrawLine(dims,new Point(mark.X-3,mark.Y+3),new Point(mark.X+3,mark.Y-3));if((y-x).Length>24){double length=Math.Sqrt(Math.Pow(op.Points[1].X-op.Points[0].X,2)+Math.Pow(op.Points[1].Y-op.Points[0].Y,2));Text(length.ToString("0.##",CultureInfo.InvariantCulture),new Point((x.X+y.X)/2+(horizontal?0:-7),(x.Y+y.Y)/2+(horizontal?-9:0)),op.Style=="total"?Brushes.LightSkyBlue:Brushes.LightGray,!horizontal,true);}}}
  if(marquee.HasValue){var pen=new Pen(crossing?Brushes.LightGreen:Brushes.LightSkyBlue,1);if(crossing)pen.DashStyle=DashStyles.Dash;dc.DrawRectangle(new SolidColorBrush(crossing?Color.FromArgb(30,110,220,130):Color.FromArgb(30,148,185,255)),pen,marquee.Value);}dc.Pop();
 }
}
