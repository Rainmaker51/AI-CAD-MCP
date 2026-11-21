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
internal static class AxisInteraction170Tests
{
 internal static void Run(string root)
 {
  void Check(bool value,string text){if(!value)throw new Exception(text);}
  var db=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;
  var library=new AxisLibrary(Plugin.Current!.Config.ContentRoot);var settings=library.Load();var window=new AxisGridDialog(60);
  object Field(string name)=>typeof(AxisGridDialog).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
  object? Call(string name,params object[] args)=>typeof(AxisGridDialog).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,args);
  AxisGridParameters Read()=>(AxisGridParameters)Call("Read")!;void Update()=>Call("UpdateState");
  ((TextBox)Field("xStart")).Text="1";((TextBox)Field("yStart")).Text="A";((TextBox)Field("xBays")).Text="2000";((TextBox)Field("yBays")).Text="5000 5000";((CheckBox)Field("envelopeOn")).IsChecked=true;Update();
  Check(Read().Envelope!.SpanX==5780&&Read().Envelope!.SpanY==14780,"6000/15000 envelope subtracts two 110 ends once");
  Check(((TextBlock)Field("remainingY")).Text.Contains("4780")&&!((Button)Field("create")).IsEnabled,"Partial grid shows final remaining 4780 and blocks CAD write");
  Call("FillEnvelope",false);Check(Read().YBays.SequenceEqual(new double[]{5000,5000,4780}),"Fill final bay preserves prior spans");Call("UndoDraft");Check(Read().YBays.SequenceEqual(new double[]{5000,5000}),"Undo fill final bay");Call("RedoDraft");Check(Read().YBays.Last()==4780,"Redo fill final bay");
  Call("FillEnvelope",true);Check(Read().XBays.SequenceEqual(new double[]{2000,3780})&&((Button)Field("create")).IsEnabled,"Both dimensions match and enable CAD write");
  var matched=Read();Call("FillEnvelope",false);Check(Read().YBays.SequenceEqual(matched.YBays),"Already matched bay is not reduced or appended again");
  ((TextBox)Field("yBays")).Text="5000 5000 5000";Update();Check(((TextBlock)Field("remainingY")).Text.Contains("Vượt 220")&&!((Button)Field("create")).IsEnabled,"Overflow is reported without changing prior bays");
  Call("UndoDraft");((TextBox)Field("divideXCount")).Text="3";Call("DivideEnvelope",true);var p=Read();Check(p.XBays.Length==3&&p.Envelope!.RemainingX(p.XBays)==0&&p.YBays.Last()==4780,"Divide X only, three equal spans without rounding residual");
  ((TextBox)Field("divideYCount")).Text="3";Call("DivideEnvelope",false);p=Read();Check(p.YBays.Length==3&&p.Envelope!.RemainingY(p.YBays)==0&&Math.Abs(p.YBays[0]-14780.0/3)<1e-9,"Three Y bays use the net span, not 15000 gross");
  ((TextBox)Field("divideYCount")).Text="0";Call("DivideEnvelope",false);Check(Read().YBays.SequenceEqual(p.YBays),"Invalid equal-division count does not mutate bays");((TextBox)Field("divideYCount")).Text="3";
  ((TextBox)Field("envelopeWidth")).Text="200";Update();Check(!((Button)Field("create")).IsEnabled,"Invalid gross size blocks placement");((TextBox)Field("envelopeWidth")).Text="6000";Update();
  ((CheckBox)Field("columnsOn")).IsChecked=true;((CheckBox)Field("wallsOn")).IsChecked=true;Update();p=Read();var lib=library.Resolve(settings,false);library.ResolveConstruction(lib,p);
  var grossOrigin=new Point3d(2300000,40000,0);var axisOrigin=RainmakerAxis.PlacementOrigin(p,grossOrigin);Check(axisOrigin==new Point3d(2300110,40110,0),"Choosing exterior corner offsets first axis exactly once");
  var grid=GridStore.Apply(db,lib,p,axisOrigin,null,false,true);var g=ConstructionGeometry.Make(p,new(axisOrigin.X,axisOrigin.Y));Check(Math.Abs(g.Columns.Min(c=>c.Corners().Min(v=>v.X))-grossOrigin.X)<1e-6&&Math.Abs(g.Columns.Max(c=>c.Corners().Max(v=>v.X))-(grossOrigin.X+6000))<1e-6,"Automatic boundary columns align to gross sides");
  using(var tx=db.TransactionManager.StartOpenCloseTransaction()){var outlines=grid.Members.Keys.Select(k=>tx.GetObject(GridStore.HandleId(db,k),OpenMode.ForRead)).OfType<Polyline>().ToArray();Check(outlines.All(l=>l.Closed)&&Math.Abs(outlines.Min(l=>l.GeometricExtents.MinPoint.X)-grossOrigin.X)<1e-6&&Math.Abs(outlines.Max(l=>l.GeometricExtents.MaxPoint.Y)-(grossOrigin.Y+15000))<1e-6,"Closed wall output matches exterior width/height");}
  var changed=ConstructionDraft.Copy(p);changed.Envelope!.Left=150;changed.Envelope.Right=70;Check(RainmakerAxis.EditingOrigin(grid,changed)==new Point3d(axisOrigin.X+40,axisOrigin.Y,0),"Editing margins preserves exterior corner without a second placement offset");var other=ConstructionGeometry.Make(changed,new());Check(Math.Abs(other.Columns.Min(c=>c.Corners().Min(v=>v.X))+150)<1e-6,"Asymmetric envelope controls automatic column exterior face");
  var partial=ConstructionDraft.Copy(p);partial.YBays[0]-=100;try{GridStore.Apply(db,lib,partial,new Point3d(2400000,0,0),null,false,true);throw new Exception("Incomplete envelope accepted");}catch(ArgumentException){}
  Call("LoadDefinition",grid);Check(Read().Envelope!=null&&Read().Envelope!.Width==6000&&((Button)Field("create")).IsEnabled,"Reload restores envelope and matched state");
  var view=(FrameworkElement)window.Content;view.Measure(new Size(1640,980));view.Arrange(new Rect(0,0,1640,980));view.UpdateLayout();view.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.DataBind,new Action(()=>{}));var image=new RenderTargetBitmap(1640,980,96,96,PixelFormats.Pbgra32);image.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(root,"axis170-preview.png")))png.Save(file);
  string drawing=Path.Combine(root,"axis170-output.dwg");db.SaveAs(drawing,DwgVersion.AC1032);using(var reopened=new Database(false,true)){reopened.ReadDwgFile(drawing,FileOpenMode.OpenForReadAndAllShare,true,"");var saved=GridStore.Read(reopened,GridStore.HandleId(reopened,grid.Members.Keys.First()))!;Check(saved.Parameters.Envelope!.Width==6000&&saved.Parameters.Envelope.RemainingY(saved.Parameters.YBays)==0&&GridStore.Changes(reopened,saved).Count==0,"Save/reopen retains exact envelope/division and edit detection");}
  ((CheckBox)Field("envelopeOn")).IsChecked=false;Update();Check(Read().Envelope==null,"Disabling envelope preserves ordinary bay-entry mode");window.Close();
  JsonUtil.Atomic(Path.Combine(root,"axis170-results.json"),new{passed=true,gross6000x15000=true,net5780x14780=true,remaining4780=true,noDoubleDeduction=true,manualBaysPreserved=true,divideSeparateAxes=true,exactDivision=true,undoRedo=true,overflowBlocked=true,invalidProfilesBlocked=true,placementOffsetOnce=true,exteriorWallsAndColumns=true,asymmetricMargins=true,editOriginPreserved=true,saveReopen=true,oldModePreserved=true});
 }
}
