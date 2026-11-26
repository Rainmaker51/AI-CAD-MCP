using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AICad.Bridge;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Axis;
internal static class AxisInteraction160Tests
{
 internal static void Run(string root)
 {
  void Check(bool value,string text){if(!value)throw new Exception(text);}
  var db=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;
  var library=new AxisLibrary(Plugin.Current!.Config.ContentRoot);var settings=library.Load();var window=new AxisGridDialog(60);
  object Field(string name)=>typeof(AxisGridDialog).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
  object? Call(string name,params object[] args)=>typeof(AxisGridDialog).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,args);
  AxisGridParameters Read()=>(AxisGridParameters)Call("Read")!;void Update()=>Call("UpdateState");
  ((TextBox)Field("xBays")).Text="5000 3000+2000 1000-500 2000";((TextBox)Field("yBays")).Text="4000 4000 3000";Update();Check(Read().XBays.SequenceEqual(new double[]{5000,5000,500,2000})&&((TextBlock)Field("bayResults")).Text.Contains("5000 · 5000 · 500 · 2000"),"Expressions display evaluated results");
  ((CheckBox)Field("columnsOn")).IsChecked=true;((CheckBox)Field("wallsOn")).IsChecked=true;Update();var p=Read();var geometry=ConstructionGeometry.Make(p,new());var corner=geometry.Columns.Single(c=>c.Key=="0:0");Check(corner.Center.X==40&&corner.Center.Y==40&&corner.Corners().Min(c=>c.X)==-110&&corner.Corners().Min(c=>c.Y)==-110,"300 column and 220 wall share exterior faces, not an inward 110 center shift");
  var rotated=ConstructionDraft.Copy(p);rotated.ColumnRotation=30;geometry=ConstructionGeometry.Make(rotated,new());corner=geometry.Columns.Single(c=>c.Key=="0:0");Check(Math.Abs(corner.Corners().Min(c=>c.X)+110)<1e-7&&Math.Abs(corner.Corners().Min(c=>c.Y)+110)<1e-7,"Rotated column uses projected half-width to retain exterior face alignment");
  var view=(FrameworkElement)window.Content;var preview=(AxisPreview)Field("preview");void Render(string? name=null){view.Measure(new Size(1640,910));view.Arrange(new Rect(0,0,1640,910));view.UpdateLayout();view.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.DataBind,new Action(()=>{}));var image=new RenderTargetBitmap(1640,910,96,96,PixelFormats.Pbgra32);image.Render(view);if(name!=null){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(root,name));png.Save(file);}}
  Render();Call("SelectPreview","Columns",new[]{"1:1","2:1"});Update();Call("ResetHistory");Update();((TextBox)Field("columnB")).Text="450";((TextBox)Field("columnRotation")).Text="30";Update();Check(Read().ColumnOverrides["1:1"].B==450,"Multi-selection edit recorded");Call("UndoDraft");Check(ColumnOptions.At(Read(),"1:1").B==300&&ColumnOptions.At(Read(),"2:1").Rotation==0,"One undo restores all columns in multi-selection edit");Call("RedoDraft");Check(Read().ColumnOverrides["1:1"].B==450&&Read().ColumnOverrides["2:1"].Rotation==30,"Redo restores multi-selection edit");
  Call("ClearPicked");Update();int count=((System.Collections.ICollection)Field("undoDraft")).Count;preview.Fit();Render();Call("SelectPreview","Columns",new[]{"1:1"});Update();Check(((System.Collections.ICollection)Field("undoDraft")).Count==count,"Selection and pan/zoom do not enter history");
  string key="Y:2:1";Call("SelectPreview","Walls",new[]{key});Update();Check(Read().WallSegments.Contains(key),"Ghost wall adds draft segment");Call("UndoDraft");Check(!Read().WallSegments.Contains(key),"Undo removes newly added wall");Call("RedoDraft");Check(Read().WallSegments.Contains(key),"Redo restores added wall");Call("RemovePicked","Walls");Update();Check(!Read().WallSegments.Contains(key),"Explicit wall removal");Call("UndoDraft");Check(Read().WallSegments.Contains(key),"Undo restores removed wall");
  ((CheckBox)Field("fixedDim")).IsChecked=true;((TextBox)Field("dimExtension")).Text="300";((TextBox)Field("dimOffset")).Text="1000";Update();p=Read();Check(p.FixedDimExtensions&&p.DimExtensionLength==300,"Dim extension UI reads model-space length");
  var lib=library.Resolve((AxisLibrarySettings)Call("ReadSettings")!,false);library.ResolveConstruction(lib,p);var dimReadings=new System.Collections.Generic.List<object>();
  foreach(int denominator in new[]{60,100}){p.Scale=denominator;var grid=GridStore.Apply(db,lib,p,new Point3d(1700000+denominator*1000,0,0),null,false,true);using var tx=db.TransactionManager.StartOpenCloseTransaction();var dims=grid.Members.Keys.Select(k=>tx.GetObject(GridStore.HandleId(db,k),OpenMode.ForRead)).OfType<AlignedDimension>().ToArray();Check(dims.Length>0&&dims.All(d=>!d.DimfxlenOn&&Math.Abs(d.Dimexo*denominator-300)<1e-7),"Dimension offset from axis respects input at 1:"+denominator);foreach(var d in dims){var style=(DimStyleTableRecord)tx.GetObject(d.DimensionStyle,OpenMode.ForRead);Check(style.Name=="DIM"&&Math.Abs(style.Dimexo-d.Dimexo)>1e-9,"Axis offset overrides entity, not warehouse style");var block=(BlockTableRecord)tx.GetObject(d.DimBlockId,OpenMode.ForRead);bool horizontal=Math.Abs(d.XLine1Point.Y-d.XLine2Point.Y)<1e-6;var lines=block.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Line>().Where(l=>horizontal?Math.Abs(l.StartPoint.X-l.EndPoint.X)<1e-5:Math.Abs(l.StartPoint.Y-l.EndPoint.Y)<1e-5).ToArray();Check(lines.Any(l=>Math.Abs(l.Length-(horizontal?Math.Abs(d.DimLinePoint.Y-d.XLine1Point.Y):Math.Abs(d.DimLinePoint.X-d.XLine1Point.X))+300-d.Dimexe*denominator)<.01),"Actual dim graphics start 300 from each axis for both dimension rows");}dimReadings.Add(new{scale=denominator,dimfxlen=dims[0].Dimfxlen});}
  p.Scale=60;var managed=GridStore.Apply(db,lib,p,new Point3d(2000000,0,0),null,false,true);Call("LoadDefinition",managed);Render();Check(((System.Collections.ICollection)Field("undoDraft")).Count==0,"Switching/reloading grid clears draft history");
  // A loaded grid has an additional immutable CAD scene; edits must not rebuild it.
  var rows=(ListBox)Field("columnRows");var rowsSource=rows.ItemsSource;int existingBuilds=preview.ExistingBuilds;Call("SelectPreview","Columns",new[]{"1:1"});Update();var watch=Stopwatch.StartNew();for(int i=0;i<6;i++){((TextBox)Field("columnRotation")).Text=(i*5).ToString();Update();Render();}double columnMs=watch.Elapsed.TotalMilliseconds;Check(ReferenceEquals(rowsSource,rows.ItemsSource)&&preview.ExistingBuilds==existingBuilds,"Loaded-grid property edits reuse row controls and immutable CAD overlay");
  Call("SelectPreview","Walls",new[]{key});Update();watch.Restart();for(int i=0;i<4;i++){Call("RemovePicked","Walls");Update();Render();Call("RestorePicked","Walls");Update();Render();}double wallMs=watch.Elapsed.TotalMilliseconds;Check(columnMs<6000&&wallMs<6000,"Loaded-grid column/wall edits remain responsive in fixture");Render("axis160-preview.png");
  string drawing=Path.Combine(root,"axis160-output.dwg");db.SaveAs(drawing,DwgVersion.AC1032);using(var reopened=new Database(false,true)){reopened.ReadDwgFile(drawing,FileOpenMode.OpenForReadAndAllShare,true,"");var restored=GridStore.Read(reopened,GridStore.HandleId(reopened,managed.Members.Keys.First()))!;Check(restored.Parameters.FixedDimExtensions&&restored.Parameters.ColumnFaceAnchors&&GridStore.Changes(reopened,restored).Count==0,"Save/reopen preserves face anchoring, fixed dim and new fingerprint version");using var tx=reopened.TransactionManager.StartTransaction();var d=restored.Members.Keys.Select(k=>tx.GetObject(GridStore.HandleId(reopened,k),OpenMode.ForRead)).OfType<Dimension>().First();d.UpgradeOpen();d.Dimexo+=1;tx.Commit();Check(GridStore.Changes(reopened,restored).Count==1,"Manual fixed extension override change is detected");}
  window.Close();JsonUtil.Atomic(Path.Combine(root,"axis160-results.json"),new{passed=true,expressions=true,faceAnchor40=true,rotatedFaceAnchor=true,undoRedoBatch=true,undoRedoWalls=true,selectionExcluded=true,historyReset=true,fixedDim300=true,sourceDimUnchanged=true,saveReopen=true,loadedColumnEditsMilliseconds=columnMs,loadedWallToggleMilliseconds=wallMs,overlayBuilds=preview.ExistingBuilds,dimReadings});
 }
}
