using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AICad.Contracts;

namespace Rainmaker.Axis;

internal sealed class AxisPreview:FrameworkElement
{
 DrawingPlan? plan;internal double Zoom=1;
 internal void Update(DrawingPlan? value){plan=value;InvalidateVisual();}
 protected override void OnRender(DrawingContext dc)
 {
  base.OnRender(dc);dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(12,19,28)),null,new Rect(RenderSize));
  if(plan==null||ActualWidth<20||ActualHeight<20)return;
  dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
  var points=plan.Operations.SelectMany(o=>o.Points).ToArray();if(points.Length==0){dc.Pop();return;}
  double minX=points.Min(p=>p.X),maxX=points.Max(p=>p.X),minY=points.Min(p=>p.Y),maxY=points.Max(p=>p.Y);
  double factor=Math.Min((ActualWidth-72)/Math.Max(1,maxX-minX),(ActualHeight-88)/Math.Max(1,maxY-minY))*Zoom;
  Point P(Point2 p)=>new(ActualWidth/2+(p.X-(minX+maxX)/2)*factor,ActualHeight/2-(p.Y-(minY+maxY)/2)*factor);
  var axis=new Pen(new SolidColorBrush(Color.FromRgb(160,185,221)),1){DashStyle=DashStyles.DashDot};
  var dims=new Pen(new SolidColorBrush(Color.FromRgb(107,157,206)),.8);
  void Text(string text,Point point,Brush color,bool vertical=false){var f=new FormattedText(text,CultureInfo.GetCultureInfo("vi-VN"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,color,1);if(vertical)dc.PushTransform(new RotateTransform(-90,point.X,point.Y));dc.DrawText(f,new Point(point.X-f.Width/2,point.Y-f.Height/2));if(vertical)dc.Pop();}
  foreach(var op in plan.Operations){
   if(op.Kind=="draw_line")dc.DrawLine(axis,P(op.Points[0]),P(op.Points[1]));
   else if(op.Kind=="insert_block"){var p=P(op.Points[0]);dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(18,29,43)),new Pen(Brushes.LightSteelBlue,1),p,13,13);Text(op.AttributeText??"",p,Brushes.White);}
   else if(op.Kind=="add_dimension"){
    var a=P(op.Points[0]);var b=P(op.Points[1]);var l=P(op.Points[2]);bool horizontal=Math.Abs(a.Y-b.Y)<1e-6;
    var x=horizontal?new Point(a.X,l.Y):new Point(l.X,a.Y);var y=horizontal?new Point(b.X,l.Y):new Point(l.X,b.Y);
    dc.DrawLine(dims,a,x);dc.DrawLine(dims,b,y);dc.DrawLine(dims,x,y);
    foreach(var mark in new[]{x,y})dc.DrawLine(dims,new Point(mark.X-3,mark.Y+3),new Point(mark.X+3,mark.Y-3));
    double distance=Math.Sqrt(Math.Pow(op.Points[1].X-op.Points[0].X,2)+Math.Pow(op.Points[1].Y-op.Points[0].Y,2));
    Text(distance.ToString("0.##",CultureInfo.InvariantCulture),new Point((x.X+y.X)/2+(horizontal?0:-8),(x.Y+y.Y)/2+(horizontal?-10:0)),op.Style=="total"?Brushes.LightSkyBlue:Brushes.LightGray,!horizontal);
   }
  }
  dc.Pop();
 }
}
