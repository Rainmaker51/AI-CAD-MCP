using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

internal static class AxisNativeTests
{
 internal static void Run(string root)
 {
  void Check(bool v,string message){if(!v)throw new Exception(message);}
  var library=new AxisLibrary(Plugin.Current!.Config.ContentRoot);var settings=library.Load();
  Check(settings.BayDimId==""&&settings.TotalDimId=="","Must not guess initial Dim styles");
  var dims=library.Options.Where(o=>o.Kind=="dims"&&o.Name!="Standard").OrderBy(o=>o.Code).Take(2).ToArray();Check(dims.Length==2,"Need two distinct test Dim styles");
  settings.BayDimId=dims[0].Id;settings.TotalDimId=dims[1].Id;settings.DimOffset=1300;settings.DimRowGap=700;
  var selected=library.Resolve(settings,false);library.Save(settings);Check(library.Load().BayDimId==settings.BayDimId,"Settings persist stable ID");
  var stale=library.Load();library.Save(settings);try{library.Save(stale);throw new Exception("Stale save accepted");}catch(CadException e)when(e.Code=="STALE_EDIT"){}
  selected=library.Resolve(settings,false);
  var p=new AxisGridParameters{Scale=60,XBays=new double[]{4000,5000},YBays=new double[]{3000},XLabels=new[]{"1","2","3"},YLabels=new[]{"A","B"},Library=selected};
  var plan=RainmakerAxis.ConfiguredPlan(selected,p,Point3d.Origin);
  Check(library.Load().DimOffset==1300&&library.Load().DimRowGap==700,"Spacing persists");
  Check(plan.Operations.Where(o=>o.Kind=="add_dimension"&&o.Points[0].Y==o.Points[1].Y).All(o=>Math.Abs(Math.Abs(o.Points[2].Y-o.Points[0].Y)-(o.Style=="bay"?1300:2000))<1e-6),"Custom spacing used by shared plan");
  Check(plan.Operations.Count(o=>o.Kind=="draw_line")==5&&plan.Operations.Count(o=>o.Kind=="insert_block")==10&&plan.Operations.Count(o=>o.Kind=="add_dimension")==10,"Shared geometry model counts");
  settings.Top=false;settings.Right=false;settings.HeadEnds="Start";
  var reduced=RainmakerAxis.ConfiguredPlan(selected,p,Point3d.Origin);Check(reduced.Operations.Count(o=>o.Kind=="insert_block")==5&&reduced.Operations.Count(o=>o.Kind=="add_dimension")==5,"Placement switches affect shared plan");
  settings.Top=true;settings.Right=true;settings.HeadEnds="Both";
  var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
  RainmakerAxis.WriteConfigured(doc.Database,selected,p,Point3d.Origin);
  using(var source=new Database(false,true)){
   source.ReadDwgFile(selected.DimFile,FileOpenMode.OpenForReadAndAllShare,true,"");source.CloseInput(true);
   using var read=source.TransactionManager.StartOpenCloseTransaction();using var tx=doc.Database.TransactionManager.StartTransaction();
   var bt=(BlockTable)tx.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
   var entities=model.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).ToList();
   var written=entities.OfType<Dimension>().ToArray();Check(written.Length==10&&written.Select(d=>d.DimensionStyle).Distinct().Count()==2,"Two actual chosen Dim styles applied");
   var sourceTable=(DimStyleTable)read.GetObject(source.DimStyleTableId,OpenMode.ForRead);
   foreach(var d in written){
    var actual=(DimStyleTableRecord)tx.GetObject(d.DimensionStyle,OpenMode.ForRead);var choice=actual.Name.Contains(selected.BayDim.Code)?selected.BayDim:selected.TotalDim;
    var expected=(DimStyleTableRecord)read.GetObject(sourceTable[choice.Name],OpenMode.ForRead);
    Check(actual.Dimtxt==expected.Dimtxt&&actual.Dimasz==expected.Dimasz&&actual.Dimscale==expected.Dimscale&&actual.Dimlfac==expected.Dimlfac&&actual.Dimdec==expected.Dimdec&&actual.Dimexo==expected.Dimexo&&actual.Dimexe==expected.Dimexe&&actual.Dimgap==expected.Dimgap&&actual.Annotative==expected.Annotative,"Real source Dim properties preserved without formula overrides");
    var text=(TextStyleTableRecord)tx.GetObject(actual.Dimtxsty,OpenMode.ForRead);var sourceText=(TextStyleTableRecord)read.GetObject(expected.Dimtxsty,OpenMode.ForRead);Check(text.FileName==sourceText.FileName&&text.BigFontFileName==sourceText.BigFontFileName,"Text style dependencies cloned");
   }
   Check(entities.OfType<Line>().Count(l=>l.Layer==selected.AxisLayer.Name)==5,"Selected axis layer");
   Check(entities.OfType<BlockReference>().Count(b=>b.Layer==selected.HeadLayer.Name)==10,"Selected head layer");
  }
  var window=new AxisGridDialog(60);
  object Field(string name)=>typeof(AxisGridDialog).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
  foreach(var pair in new[]{("bayDim",settings.BayDimId),("totalDim",settings.TotalDimId)}){
   var combo=(ComboBox)Field(pair.Item1);combo.SelectedItem=combo.Items.OfType<AxisLibraryOption>().Single(o=>o.Id==pair.Item2);
  }
  var view=(FrameworkElement)window.Content;view.Measure(new Size(1076,700));view.Arrange(new Rect(0,0,1076,700));view.UpdateLayout();
  ((TabControl)Field("libraryTabs")).SelectedIndex=1;view.Measure(new Size(1076,700));view.Arrange(new Rect(0,0,1076,700));view.UpdateLayout();
  foreach(var name in new[]{"axisLayer","headBlock","headLayer","bayDim","totalDim","dimLayer"}){
   var combo=(ComboBox)Field(name);combo.ApplyTemplate();var editor=combo.Template.FindName("PART_EditableTextBox",combo) as TextBox;
   Check(editor!=null&&editor.Visibility==System.Windows.Visibility.Visible&&editor.Text==((AxisLibraryOption)combo.SelectedItem).Label&&!string.IsNullOrWhiteSpace(editor.Text),"Selected code/name visibly restored: "+name+" editor="+(editor?.Text??"<missing>")+" selected="+combo.SelectedItem+" text="+combo.Text);
  }
  typeof(AxisGridDialog).GetMethod("UpdateState",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
  Check(((Button)Field("create")).IsEnabled,"Configured dialog enables creation");
  var bitmap=new RenderTargetBitmap(1076,700,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(root,"axis-upgrade-preview.png")))encoder.Save(output);
  ((TextBox)Field("dimOffset")).Text="0";typeof(AxisGridDialog).GetMethod("UpdateState",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);Check(!((Button)Field("create")).IsEnabled,"Zero spacing blocks drawing");((TextBox)Field("dimOffset")).Text="1300";
  ((TextBox)Field("xBays")).Text="bad";typeof(AxisGridDialog).GetMethod("UpdateState",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);Check(!((Button)Field("create")).IsEnabled,"Invalid input blocks drawing and clears preview");window.Close();
  string drawing=Path.Combine(root,"axis-upgrade-output.dwg");doc.Database.SaveAs(drawing,DwgVersion.AC1032);
  using(var reopened=new Database(false,true)){reopened.ReadDwgFile(drawing,FileOpenMode.OpenForReadAndAllShare,true,"");using var tx=reopened.TransactionManager.StartOpenCloseTransaction();var bt=(BlockTable)tx.GetObject(reopened.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);Check(model.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Dimension>().Count()==10,"Dimensions survive save/reopen");}
  string annoFile=Path.Combine(root,"annotative-source.dwg");
  using(var source=new Database(false,true)){
   source.ReadDwgFile(selected.DimFile,FileOpenMode.OpenForReadAndAllShare,true,"");source.CloseInput(true);using(var tx=source.TransactionManager.StartTransaction()){var table=(DimStyleTable)tx.GetObject(source.DimStyleTableId,OpenMode.ForRead);var dim=(DimStyleTableRecord)tx.GetObject(table[selected.BayDim.Name],OpenMode.ForWrite);dim.Annotative=AnnotativeStates.True;dim.Dimscale=0;tx.Commit();}source.SaveAs(annoFile,DwgVersion.AC1032);
  }
  string originalFile=selected.DimFile,originalHash=selected.DimHash;selected.DimFile=annoFile;selected.DimHash=JsonUtil.FileHash(annoFile);string beforeScale=doc.Database.Cannoscale.Name;
  RainmakerAxis.WriteConfigured(doc.Database,selected,p,new Point3d(30000,0,0));
  using(var tx=doc.Database.TransactionManager.StartTransaction()){
   var bt=(BlockTable)tx.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);var annotated=model.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<AlignedDimension>().Where(d=>d.XLine1Point.X>=30000&&((DimStyleTableRecord)tx.GetObject(d.DimensionStyle,OpenMode.ForRead)).Name.Contains(selected.BayDim.Code)).ToList();
   JsonUtil.Atomic(Path.Combine(root,"annotative-check.json"),new{count=annotated.Count,dimensions=annotated.Select(d=>new{annotative=d.Annotative.ToString(),hasContext=d.HasContext(EntityFactory.GetScale(doc.Database,60)),d.Dimscale}).ToList()});
   Check(annotated.Count==6&&annotated.All(d=>d.Annotative==AnnotativeStates.True&&d.HasContext(EntityFactory.GetScale(doc.Database,60))&&d.Dimscale==0),"Annotative bay dimensions carry chosen annotation context without double scaling");Check(doc.Database.Cannoscale.Name==beforeScale,"Do not change drawing current annotation scale");
  }
  selected.DimFile=originalFile;selected.DimHash=originalHash;
  JsonUtil.Atomic(Path.Combine(root,"axis-upgrade-results.json"),new{passed=true,axisLines=5,axisHeads=10,dimensions=10,sourceDimProperties=true,textDependencies=true,persistedStableIds=true,staleSaveRejected=true,placementOptions=true,dialogAndPreviewRendered=true,invalidInputBlocksDrawing=true,saveReopen=true,annotativeContext=true});
 }
}
