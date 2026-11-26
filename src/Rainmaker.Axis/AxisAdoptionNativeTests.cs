using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Axis;
internal static class AxisAdoptionNativeTests
{
 internal static void Run(string root,AxisResolvedLibrary library)
 {
  void Check(bool condition,string message){if(!condition)throw new Exception(message);}
  var db=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;
  var selection=new List<ObjectId>();const double x=2500000,y=2500000;
  using(var tx=db.TransactionManager.StartTransaction()){
   var model=(BlockTableRecord)tx.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
   void Add(Entity e){model.AppendEntity(e);tx.AddNewlyCreatedDBObject(e,true);selection.Add(e.ObjectId);}
   var poly=new Polyline();poly.AddVertexAt(0,new(x,y),0,0,0);poly.AddVertexAt(1,new(x,y+3000),0,0,0);poly.AddVertexAt(2,new(x+4000,y+3000),0,0,0);Add(poly);
   Add(new Line(new(x+4000,y,0),new(x+4000,y+1500,0)));Add(new Line(new(x+4000,y+3000,0),new(x+4000,y+1500,0)));
   Add(new Line(new(x,y,0),new(x+4000,y,0)));Add(new Circle(new(x+1000,y+1000,0),Vector3d.ZAxis,200));tx.Commit();
  }
  var adopted=GridStore.Adopt(db,selection.ToArray(),library.Settings);
  Check(adopted.Members.Count==4&&adopted.IgnoredSelectionCount==1,"Adoption must not own the selected unrelated circle");
  Check(adopted.X==x&&adopted.Y==y&&adopted.Parameters.XBays.SequenceEqual(new[]{4000.0})&&adopted.Parameters.YBays.SequenceEqual(new[]{3000.0}),"Mixed Polyline/fragment Line topology lost coordinates");
  Check(adopted.Parameters.DimExtensionLength==500&&!adopted.Parameters.Columns&&!adopted.Parameters.Walls,"Default 500 offset, columns and walls remain optional");
  Check(GridStore.Changes(db,adopted).Count==0&&!selection.Any(id=>id.IsErased),"Recognition must not write or erase source objects");
  adopted.Parameters.FixedDimExtensions=true;
  var window=new AxisGridDialog(60,adopted);
  try{
   var view=(System.Windows.FrameworkElement)window.Content;view.Measure(new System.Windows.Size(1640,910));view.Arrange(new System.Windows.Rect(0,0,1640,910));view.UpdateLayout();
   var image=new System.Windows.Media.Imaging.RenderTargetBitmap(1640,910,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(view);
   var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using var output=File.Create(Path.Combine(root,"axis-adoption-preview.png"));encoder.Save(output);
  }finally{window.Close();}
  var readings=new List<object>();GridDefinition? last=null;
  foreach(int denominator in new[]{60,100}){
   var p=adopted.Parameters;p.Scale=denominator;p.FixedDimExtensions=true;p.DimExtensionLength=500;
   var applied=GridStore.Apply(db,library,p,new(x+(denominator==60?0:20000),y,0),denominator==60?adopted:null,false,true);
   using var tx=db.TransactionManager.StartOpenCloseTransaction();
   var dims=applied.Members.Keys.Select(k=>tx.GetObject(GridStore.HandleId(db,k),OpenMode.ForRead)).OfType<AlignedDimension>().ToArray();
   Check(dims.Length==8&&dims.All(d=>!d.DimfxlenOn&&Math.Abs(d.Dimexo*denominator-500)<1e-7),"Both bay and total dim must use 500 from axes at every scale");
   foreach(var d in dims){
    var block=(BlockTableRecord)tx.GetObject(d.DimBlockId,OpenMode.ForRead);
    bool horizontal=Math.Abs(d.XLine1Point.Y-d.XLine2Point.Y)<1e-6;
    var lines=block.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Line>().ToArray();
    foreach(var axisPoint in new[]{d.XLine1Point,d.XLine2Point}){
     bool actual=lines.Any(l=>horizontal?
      Math.Abs(l.StartPoint.X-axisPoint.X)<.01&&Math.Abs(l.EndPoint.X-axisPoint.X)<.01&&Math.Abs(Math.Min(Math.Abs(l.StartPoint.Y-axisPoint.Y),Math.Abs(l.EndPoint.Y-axisPoint.Y))-500)<.01:
      Math.Abs(l.StartPoint.Y-axisPoint.Y)<.01&&Math.Abs(l.EndPoint.Y-axisPoint.Y)<.01&&Math.Abs(Math.Min(Math.Abs(l.StartPoint.X-axisPoint.X),Math.Abs(l.EndPoint.X-axisPoint.X))-500)<.01);
     Check(actual,"Rendered dimension extension foot is not 500 mm from axis");
    }
   }
   readings.Add(new{scale=denominator,dimensions=dims.Length,dimexo=dims[0].Dimexo});
   Check(GridStore.Changes(db,applied).Count==0,"New dimension fingerprint fails its own baseline");
   last=applied;
  }
  Check(selection.Take(4).All(id=>id.IsErased)&&!selection.Last().IsErased,"Apply replaced anything outside recognized axis members");
  string drawing=Path.Combine(root,"axis-adoption-output.dwg");db.SaveAs(drawing,DwgVersion.AC1032);
  using(var reopened=new Database(false,true)){
   reopened.ReadDwgFile(drawing,FileOpenMode.OpenForReadAndAllShare,true,"");reopened.CloseInput(true);
   var restored=GridStore.Read(reopened,GridStore.HandleId(reopened,last!.Members.Keys.First()))!;
   Check(restored.Parameters.DimExtensionLength==500&&restored.MemberHashVersion==3&&GridStore.Changes(reopened,restored).Count==0,"Save/reopen lost axis-foot offset or new fingerprint");
  }
  JsonUtil.Atomic(Path.Combine(root,"axis-adoption-results.json"),new{passed=true,polylineAndFragments=true,unrelatedSelectionPreserved=true,recognitionReadOnly=true,defaultAxisFootGap=500,bothRows=true,renderedFeetVerified=true,saveReopen=true,readings});
 }
}
