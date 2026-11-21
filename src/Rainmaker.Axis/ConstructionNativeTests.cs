using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AICad.Bridge;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Axis;

internal static class ConstructionNativeTests
{
 internal static void Run(string root)
 {
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  var db=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;
  var index=new AxisLibrary(Plugin.Current!.Config.ContentRoot);var settings=index.Load();var dim=index.Options.Single(o=>o.Kind=="dims"&&o.Name=="DIM");settings.BayDimId=settings.TotalDimId=dim.Id;
  string Layer(string name)=>index.Options.Single(o=>o.Kind=="layers"&&o.Name==name).Id;
  settings.ColumnLayerId=Layer("QA_KT_Be Tong");settings.WallLayerId=Layer("QA_KT_Tuong");settings.HatchLayerId=Layer("QA_KT_Hatch");
  var hatch=index.Options.First(o=>o.Kind=="hatches"&&o.Sync.Values.Value<string>("pattern")=="ANSI31");settings.WallHatchId=hatch.Id;
  var p=new AxisGridParameters{XBays=new[]{4000.0,5000},YBays=new[]{3000.0},XLabels=new[]{"1","2","3"},YLabels=new[]{"A","B"},Scale=60,Columns=true,Walls=true,WallHatch=true,ColumnB=300,ColumnH=400};p.ColumnNodes=ConstructionGeometry.Nodes(p);p.WallSegments=ConstructionGeometry.Segments(p);
  var lib=index.Resolve(settings,false);index.ResolveConstruction(lib,p);var origin=new Point3d(200000,0,0);var grid=GridStore.Apply(db,lib,p,origin,null,false,true);
  var differences=GridStore.Changes(db,grid);if(differences.Count>0){using var inspect=db.TransactionManager.StartOpenCloseTransaction();JsonUtil.Atomic(Path.Combine(root,"grid-fingerprint-differences.json"),differences.Select(d=>new{change=d,type=inspect.GetObject(GridStore.HandleId(db,d.Split(':')[0]),OpenMode.ForRead).GetType().Name}).ToArray());}Check(differences.Count==0,"New grid fingerprints stable: "+string.Join(", ",differences));
  using(var tx=db.TransactionManager.StartOpenCloseTransaction()){
   var entities=grid.Members.Keys.Select(h=>tx.GetObject(GridStore.HandleId(db,h),OpenMode.ForRead)).OfType<Entity>().ToArray();var columns=entities.OfType<BlockReference>().Where(b=>b.Layer==lib.ColumnLayer!.Name).ToArray();Check(columns.Length==6&&columns.Select(c=>c.BlockTableRecord).Distinct().Count()==1,"Six columns reuse one sized definition");
   var outlines=entities.OfType<Polyline>().ToArray();Check(outlines.Length>0&&outlines.All(l=>l.Closed&&l.Area>0),"Closed nonzero wall loops");var h=entities.OfType<Hatch>().Single();Check(h.Associative&&h.PatternName==hatch.Sync.Values.Value<string>("pattern")&&Math.Abs(h.PatternAngle-hatch.Sync.Values.Value<double>("angle"))<1e-9&&Math.Abs(h.PatternScale-hatch.Sync.Values.Value<double>("scale"))<1e-9&&h.ColorIndex==hatch.Sync.Values.Value<int>("color"),"Hatch carries selected pattern/scale/angle/color and associative loops");
   Check(h.Area>0,"Wall hatch positive area");
   // Independent expected area: 2 horizontal rows + 3 vertical strips, subtract overlap and six column footprints inside wall union.
   var geometry=ConstructionGeometry.Make(p,new(origin.X,origin.Y));double expected=PreviewArea(geometry);Check(Math.Abs(h.Area-expected)<Math.Max(.1,expected*1e-5),"Hatch area equals wall union minus true column shapes: "+h.Area+" / "+expected);
  }
  var member=GridStore.HandleId(db,grid.Members.Keys.First());var loaded=GridStore.Read(db,member)!;Check(loaded.Id==grid.Id&&loaded.Parameters.Columns&&loaded.Parameters.WallSegments.Length==7,"DWG metadata restores grid/construction selection");
  var culture=System.Threading.Thread.CurrentThread.CurrentCulture;try{System.Threading.Thread.CurrentThread.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("vi-VN");Check(GridStore.Changes(db,loaded).Count==0,"Fingerprints independent of machine number formatting");}finally{System.Threading.Thread.CurrentThread.CurrentCulture=culture;}
  using(var tx=db.TransactionManager.StartTransaction()){var line=grid.Members.Keys.Select(h=>tx.GetObject(GridStore.HandleId(db,h),OpenMode.ForRead)).OfType<Line>().First();line.UpgradeOpen();line.EndPoint+=new Vector3d(1,0,0);tx.Commit();}
  Check(GridStore.Changes(db,loaded).Count==1,"Manual edit detected");
  try{GridStore.Apply(db,lib,p,origin,loaded);throw new Exception("Manual edit overwritten without approval");}catch(CadException e)when(e.Code=="GRID_CHANGED"){}
  p.XBays[0]=4500;p.XLabels=new[]{"1","2a","3"};grid=GridStore.Apply(db,lib,p,origin,loaded,true,true);Check(grid.Revision==2&&grid.Id==loaded.Id&&grid.Members.Count==loaded.Members.Count,"Rebuild preserves grid ID and avoids duplicates");Check(GridStore.Changes(db,grid).Count==0,"Updated fingerprints stable");
  try{GridStore.Apply(db,lib,p,origin);throw new Exception("Duplicate grid accepted");}catch(CadException e)when(e.Code=="GRID_DUPLICATE"){}
  var before=grid.Members.Keys.OrderBy(x=>x).ToArray();var pattern=lib.WallHatch!["pattern"]!.DeepClone();lib.WallHatch["pattern"]="__MISSING_RAINMAKER_PATTERN__";
  bool failed=false;try{GridStore.Apply(db,lib,p,origin,grid,true,true);}catch{failed=true;}Check(failed,"Bad hatch aborts update");lib.WallHatch["pattern"]=pattern;var retained=GridStore.Read(db,GridStore.HandleId(db,before[0]))!;Check(retained.Revision==grid.Revision&&GridStore.Changes(db,retained).Count==0,"Failed operation leaves old geometry/metadata intact");
  // L / T / X unions plus rotated rectangular and circular column subtraction.
  foreach(var shape in new[]{"Rectangle","Circle"}){
   p.ColumnShape=shape;p.ColumnRotation=35;var geometry=ConstructionGeometry.Make(p,new());var loops=ConstructionWriter.WallLoops(geometry);try{Check(loops.Count>0&&loops.All(l=>l.Closed),"L/T/X closed after rotated/circular cut");}finally{foreach(var l in loops)l.Dispose();}
   double ox=shape=="Rectangle"?500000:600000;var cutGrid=GridStore.Apply(db,lib,p,new Point3d(ox,0,0),null,false,true);using(var tx=db.TransactionManager.StartOpenCloseTransaction()){var h=cutGrid.Members.Keys.Select(k=>tx.GetObject(GridStore.HandleId(db,k),OpenMode.ForRead)).OfType<Hatch>().Single();double expected=PreviewArea(geometry);Check(Math.Abs(h.Area-expected)<Math.Max(.1,expected*1e-5),"Hatched wall area matches true "+shape+" cut: "+h.Area+" / "+expected);}
  }
  // Reuse exact DimStyle names, block conflicts, and confirm replacement explicitly.
  using(var tx=db.TransactionManager.StartTransaction()){var table=(DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForRead);Check(table.Has("DIM")&&!table.Cast<ObjectId>().Select(id=>(DimStyleTableRecord)tx.GetObject(id,OpenMode.ForRead)).Any(s=>s.Name.StartsWith("RMAX_")),"No generated RMAX styles");var record=(DimStyleTableRecord)tx.GetObject(table["DIM"],OpenMode.ForWrite);record.Dimtxt=9;tx.Commit();}
  try{RainmakerAxis.WriteConfigured(db,lib,p,new Point3d(250000,0,0));throw new Exception("Dim conflict ignored");}catch(CadException e)when(e.Code=="DIM_CONFLICT"){Check(e.Message.Contains("Dimtxt"),"Conflict shows property differences");}
  var legacy=RainmakerAxis.WriteConfigured(db,lib,p,new Point3d(300000,0,0),null,true);var adopted=GridStore.Adopt(db,legacy.ToArray(),settings);Check(adopted.Adopted&&adopted.Members.Count==legacy.Count,"Explicit legacy selection recognized");var upgraded=GridStore.Apply(db,lib,adopted.Parameters,new Point3d(adopted.X,adopted.Y,0),adopted,true,true);Check(!upgraded.Adopted&&upgraded.Revision==1,"Legacy adopted as persistent grid");
  // Simulate old private-name dimensions; conversion retains dimensions and purges only an unreferenced old style.
  ObjectId oldStyle;using(var tx=db.TransactionManager.StartTransaction()){var table=(DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForWrite);var original=(DimStyleTableRecord)tx.GetObject(table["DIM"],OpenMode.ForRead);var copy=(DimStyleTableRecord)original.Clone();copy.Name="RMAX_"+dim.Code+"_fixture";oldStyle=table.Add(copy);tx.AddNewlyCreatedDBObject(copy,true);var d=upgraded.Members.Keys.Select(h=>tx.GetObject(GridStore.HandleId(db,h),OpenMode.ForRead)).OfType<Dimension>().First();d.UpgradeOpen();d.DimensionStyle=oldStyle;tx.Commit();}
  ObjectId chosen;using(var tx=db.TransactionManager.StartOpenCloseTransaction())chosen=upgraded.Members.Keys.Select(h=>tx.GetObject(GridStore.HandleId(db,h),OpenMode.ForRead)).OfType<Dimension>().First(d=>d.DimensionStyle==oldStyle).ObjectId;
  Check(DimensionLibrary.Migrate(db,lib,index,new[]{chosen},true)==1,"Migrate selected RMAX dimension");using(var tx=db.TransactionManager.StartOpenCloseTransaction()){var d=(Dimension)tx.GetObject(chosen,OpenMode.ForRead);var style=(DimStyleTableRecord)tx.GetObject(d.DimensionStyle,OpenMode.ForRead);Check(style.Name=="DIM"&&oldStyle.IsErased,"RMAX migrated and unused definition removed");}
  p.Columns=false;var ring=GridStore.Apply(db,lib,p,new Point3d(700000,0,0),null,false,true);using(var tx=db.TransactionManager.StartOpenCloseTransaction()){var h=ring.Members.Keys.Select(k=>tx.GetObject(GridStore.HandleId(db,k),OpenMode.ForRead)).OfType<Hatch>().Single();double expected=PreviewArea(ConstructionGeometry.Make(p,new()));Check(Math.Abs(h.Area-expected)<expected*1e-5,"Hatch excludes interior rooms without columns");}p.Columns=true;
  var editor=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;editor.Command("_.UNDO","_BEGIN");var undone=GridStore.Apply(db,lib,p,new Point3d(800000,0,0),null,false,true);editor.Command("_.UNDO","_END");editor.Command("_.U");Check(undone.Members.Keys.All(k=>{var id=GridStore.HandleId(db,k);return id.IsNull||id.IsErased||!id.IsValid;}),"One Undo removes all grid/construction members");
  p.ColumnShape="Rectangle";p.ColumnRotation=0;var window=new AxisGridDialog(60,grid);var rootView=(FrameworkElement)window.Content;rootView.Measure(new Size(1136,760));rootView.Arrange(new Rect(0,0,1136,760));rootView.UpdateLayout();var tabs=(TabControl)typeof(AxisGridDialog).GetField("libraryTabs",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;Check(tabs.Items.Count==2,"Exactly two tabs");var bitmap=new RenderTargetBitmap(1136,760,96,96,PixelFormats.Pbgra32);bitmap.Render(rootView);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(root,"axis133-preview.png")))png.Save(output);window.Close();
  string drawing=Path.Combine(root,"axis133-output.dwg");db.SaveAs(drawing,DwgVersion.AC1032);using(var reopened=new Database(false,true)){reopened.ReadDwgFile(drawing,FileOpenMode.OpenForReadAndAllShare,true,"");var persisted=GridStore.Read(reopened,GridStore.HandleId(reopened,grid.Members.Keys.First()))!;var changed=GridStore.Changes(reopened,persisted);if(changed.Count>0){using var inspect=reopened.TransactionManager.StartOpenCloseTransaction();JsonUtil.Atomic(Path.Combine(root,"grid-reopen-differences.json"),changed.Select(d=>new{change=d,type=inspect.GetObject(GridStore.HandleId(reopened,d.Split(':')[0]),OpenMode.ForRead).GetType().Name}).ToArray());}Check(persisted.Id==grid.Id&&persisted.Parameters.XLabels[1]=="2a"&&changed.Count==0,"Save/reopen managed grid and stable edit detection: "+string.Join(",",changed));}
  JsonUtil.Atomic(Path.Combine(root,"axis133-results.json"),new{passed=true,columns=6,wallSegments=7,closedWalls=true,columnSubtraction=true,rotatedAndCircularCuts=true,interiorHoles=true,oneUndo=true,sharedColumnDefinition=true,libraryHatch=true,manualEditDetection=true,transactionRollback=true,gridEdit=true,noDuplicates=true,legacyAdoption=true,originalDimNames=true,dimConflictGuard=true,rmaxMigration=true,twoTabs=true,saveReopen=true});
 }
 static double PreviewArea(ConstructionGeometry geometry){Geometry Polygon(System.Collections.Generic.IEnumerable<Point2> points){var shape=new StreamGeometry();using(var context=shape.Open()){var vertices=points.Select(p=>new Point(p.X,p.Y)).ToArray();context.BeginFigure(vertices[0],true,true);context.PolyLineTo(vertices.Skip(1).ToArray(),true,false);}return shape;}Geometry wall=Geometry.Empty;foreach(var item in geometry.Walls)wall=new CombinedGeometry(GeometryCombineMode.Union,wall,Polygon(item.Corners));foreach(var c in geometry.Columns)wall=new CombinedGeometry(GeometryCombineMode.Exclude,wall,c.Circle?new EllipseGeometry(new Point(c.Center.X,c.Center.Y),c.B/2,c.B/2):Polygon(c.Corners()));return wall.GetArea(.001,ToleranceType.Absolute);}
}
