using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;

[assembly:CommandClass(typeof(AICad.Bridge.CadRibbonEditingTests))]
namespace AICad.Bridge;
public sealed class CadRibbonEditingTests
{
 static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 static IEnumerable<DependencyObject> Logical(DependencyObject root)
 {
  yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var nested in Logical(child))yield return nested;
 }
 [CommandMethod("AICADRIBBONEDITTEST")]
 public void Run()
 {
  string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";
  if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".manager-ribbon-test")))return;
  try
  {
   void Step(string value)=>File.WriteAllText(Path.Combine(root,"step.txt"),value);
   Step("start");
   var config=new InstallConfig{ContentRoot=root,DataRoot=Path.Combine(root,"data"),HostExecutable=Plugin.Current!.Config.HostExecutable};
   var catalog=new CadToolCatalog(root);string donor=Path.Combine(root,"donor","UiFixture.dll");Directory.CreateDirectory(Path.GetDirectoryName(donor)!);File.WriteAllText(donor,"Inert test fixture; never loaded");
   var tool=catalog.Import(donor,0);catalog.Update(tool.Id,1,"QA Library","Library group","Keep notes",true,new[]{"QALIBRARY","QALIBBLOCK","QALIBPACK"});
   string a=catalog.SaveTab(2,"","A library",10),b=catalog.SaveTab(3,"","B architecture",20),hidden=catalog.SaveTab(4,"","Hidden tab",30,false);
   catalog.SetRibbonLayout(tool.Id,5,a,new[]{new CadRibbonPin{Command="QALIBRARY",Label="Open QA",Order=17},new CadRibbonPin{Command="QALIBPACK",Label="Pack custom",TabId=b,Group="Preserve group",Order=23}},"FIXTURE");
   var control=new RibbonControl();var external=new RibbonTab{Id="EXTERNAL_TEST_TAB",Title="Home"};control.Tabs.Add(external);
   var fake=new Plugin{Config=config};var renderer=new RainmakerRibbon(fake);fake.Ribbon=renderer;
   renderer.RenderForTest(control,catalog.Read());
   Step("renderer");
   bool Has(string tab,string label)=>control.Tabs.Single(t=>t.Id==RainmakerRibbon.NativeTabId(tab)).Panels.SelectMany(p=>p.Source.Items).OfType<RibbonButton>().Any(button=>button.Text==label);
   Check(Has(a,"Open QA")&&Has(b,"Pack custom"),"Mixed default and explicit tab setup failed");
   using(var palette=new CadToolPalette(fake,test:true))
   {
    Step("palette");
    var page=typeof(CadToolPalette).GetField("addins",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(palette)!;
    var fields=BindingFlags.Instance|BindingFlags.NonPublic;
    var toolList=(DataGrid)page.GetType().GetField("list",fields)!.GetValue(page)!;
    Check(toolList.Columns.Any(c=>c.Header?.ToString()=="Tab Ribbon"),"Missing Ribbon tab column");
    var selected=catalog.Read().Items.Single();Check(CadToolRibbonSetup.Location(catalog.Read(),selected).Contains("A library")&&CadToolRibbonSetup.Location(catalog.Read(),selected).Contains("B architecture"),"Actual location omitted per-command overrides");
    var target=(ComboBox)page.GetType().GetField("toolTab",fields)!.GetValue(page)!;
    target.SelectedItem=target.Items.Cast<CadRibbonTab>().Single(t=>t.Id==b);
    void Click(string text){Step("click: "+text);var button=Logical(palette.RootForTest).OfType<System.Windows.Controls.Button>().Single(x=>x.Content as string==text);Check(button.IsEnabled,"Button unexpectedly disabled: "+text);button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Step("clicked: "+text);}
    string original=CadToolCatalog.RibbonLayoutHash(selected);
    Click("Chuyển toàn bộ nút");renderer.RenderForTest(control,catalog.Read());
    Check(!Has(a,"Open QA")&&Has(b,"Open QA")&&Has(b,"Pack custom"),"Moved buttons remain on old tab or vanished");
    Check(catalog.Read().Items.Single().RibbonPins.All(p=>p.TabId==""),"Move retained conflicting overrides");
    Click("Hoàn tác Ribbon gần nhất");renderer.RenderForTest(control,catalog.Read());Check(CadToolCatalog.RibbonLayoutHash(catalog.Read().Items.Single())==original&&Has(a,"Open QA"),"Move undo failed");
    Click("Bỏ ghim tất cả");renderer.RenderForTest(control,catalog.Read());Check(!Has(a,"Open QA")&&!Has(b,"Pack custom")&&catalog.Read().Items.Single().Commands.Count==3,"Unpin all removed commands or left buttons");
    Click("Hoàn tác Ribbon gần nhất");Check(CadToolCatalog.RibbonLayoutHash(catalog.Read().Items.Single())==original,"Unpin all undo failed");
    var main=(ComboBox)page.GetType().GetField("mainCommand",fields)!.GetValue(page)!;main.SelectedItem="QALIBRARY";
    Click("Chỉ ghim lệnh mở bảng");Check(catalog.Read().Items.Single().RibbonPins.Single().Command=="QALIBRARY","Main-only failed");
    string mainLayout=CadToolCatalog.RibbonLayoutHash(catalog.Read().Items.Single());
    Click("Ghim tất cả lệnh lên Ribbon");Check(catalog.Read().Items.Single().RibbonPins.Count==3,"Pin all did not add missing commands");
    Click("Hoàn tác Ribbon gần nhất");Check(CadToolCatalog.RibbonLayoutHash(catalog.Read().Items.Single())==mainLayout,"Accidental pin-all recovery failed");
    var pins=(DataGrid)page.GetType().GetField("ribbonList",fields)!.GetValue(page)!;
    var pack=pins.Items.Cast<object>().Single(row=>(string)row.GetType().GetProperty("Command")!.GetValue(row)! == "QALIBPACK");
    pins.SelectedItems.Add(pack);Click("Ghim mục chọn");Click("Lưu các nút");Check(catalog.Read().Items.Single().RibbonPins.Count==2,"Bulk selected pin/save failed");
    var keep=catalog.Read().Items.Single();Check(keep.Notes=="Keep notes"&&keep.Group=="Library group"&&keep.RibbonPins.Single(p=>p.Command=="QALIBRARY").Label=="Open QA","Ribbon edit changed profile or custom labels");
    var view=palette.RootForTest;view.Measure(new Size(620,900));view.Arrange(new Rect(0,0,620,900));view.UpdateLayout();
    var bitmap=new RenderTargetBitmap(620,900,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(root,"manager.png")))png.Save(file);Step("rendered");
   }
   Step("disposed");renderer.RenderForTest(control,catalog.Read());Step("rerendered");Check(control.Tabs.Contains(external),"Manager removed a foreign tab");renderer.Remove();Step("removed");Check(control.Tabs.Count==1&&control.Tabs[0]==external,"Manager cleanup removed foreign UI");
   JsonUtil.Atomic(Path.Combine(root,"ribbon-edit-results.json"),new{passed=true,tabColumn=true,mixedTabMove=true,noOldButtons=true,undoMove=true,unpinAll=true,undoUnpinAll=true,mainOnly=true,pinAllRecovery=true,bulkSelected=true,profilePreserved=true,externalTabsPreserved=true,wpfRendered=true});
  }
  catch(System.Exception error){JsonUtil.Atomic(Path.Combine(root,"ribbon-edit-results.json"),new{passed=false,error=error.ToString()});}
 }
}
