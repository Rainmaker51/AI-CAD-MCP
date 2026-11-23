using System;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
void Check(bool value){if(!value)throw new Exception("Assertion failed");}
void Reject(Action work,string code){try{work();}catch(CadException e)when(e.Code==code){return;}throw new Exception("Expected "+code);}
string root=Path.Combine(Path.GetTempPath(),"aicad-tools-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var catalog=new CadToolCatalog(root);string input=Path.Combine(root,"source");Directory.CreateDirectory(input);string source=Path.Combine(input,"sample.dll");
File.WriteAllText(source,"first package");
var item=catalog.Import(source,0);Check(item.Kind=="dotnet");Check(!Path.IsPathRooted(item.RelativePath));Check(catalog.Demand(item).StartsWith(catalog.Root));
Reject(()=>catalog.Import(Path.Combine(input,"legacy.lsp"),1),"TOOL_TYPE");
catalog.Update(item.Id,1,"Hello",".NET / Chung","Mục đích",true,item.Commands);Reject(()=>catalog.Update(item.Id,1,"Stale","","",true,item.Commands),"STALE_EDIT");
File.WriteAllText(source,"updated package");var updated=catalog.Import(source,2,item.Id);Check(updated.Id==item.Id&&updated.Group==".NET / Chung"&&updated.Notes=="Mục đích"&&updated.FileHash!=item.FileHash);Check(!File.Exists(catalog.PathFor(item)));Check(Directory.GetFiles(Path.Combine(catalog.Root,"dotnet","_Development"),"sample.dll",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p)=="first package"));
catalog.Update(item.Id,3,updated.Name,updated.Group,updated.Notes,true,updated.Commands,"delete");Reject(()=>catalog.Demand(catalog.Read().Items.Single()),"TOOL_DISABLED");catalog.Update(item.Id,4,updated.Name,updated.Group,updated.Notes,true,updated.Commands,"restore");Check(catalog.Demand(catalog.Read().Items.Single())==catalog.PathFor(updated));
Reject(()=>CadToolCatalog.CommandName("HELLO) (evil"),"TOOL_COMMAND");var malicious=new CadToolEntry{RelativePath="../outside.dll"};Reject(()=>catalog.PathFor(malicious),"TOOL_PATH");
File.AppendAllText(catalog.PathFor(updated),"tampered");Reject(()=>catalog.Demand(updated),"TOOL_CHANGED");
string second=Path.Combine(root,"second");Directory.CreateDirectory(second);File.Copy(source,Path.Combine(second,"sample.dll"));var another=new CadToolCatalog(second);var copy=another.Import(Path.Combine(second,"sample.dll"),0);Check(!Path.IsPathRooted(copy.RelativePath));Check(another.Demand(copy).StartsWith(another.Root));
var config=CadToolMcpConfig.Create(new InstallConfig{ContentRoot=root,HostExecutable=Path.Combine(root,"artifacts/p0-p6/Host/AICad.Host.exe")});Check(config["claudeDesktop"]!["mcpServers"]!["ai_cad"]!["args"]![0]!.Value<string>()!.EndsWith(Path.Combine("MCP","AICad.Mcp.dll")));Check(config.Value<string>("codexToml")!.Contains("[mcp_servers.ai_cad]"));
string? originalDotnetRoot=Environment.GetEnvironmentVariable("DOTNET_ROOT");string systemSdk=Path.Combine(root,"system-sdk");Directory.CreateDirectory(systemSdk);File.WriteAllText(Path.Combine(systemSdk,"dotnet.exe"),"test marker only");
try{Environment.SetEnvironmentVariable("DOTNET_ROOT",systemSdk);var systemConfig=CadToolMcpConfig.Create(new InstallConfig{ContentRoot=root,HostExecutable=Path.Combine(root,"artifacts/p0-p6/Host/AICad.Host.exe")});Check(systemConfig["claudeDesktop"]!["mcpServers"]!["ai_cad"]!.Value<string>("command")==Path.Combine(systemSdk,"dotnet.exe"));}finally{Environment.SetEnvironmentVariable("DOTNET_ROOT",originalDotnetRoot);}
Console.WriteLine("PASS: DLL-only imports; relative shared paths; update preserves metadata/history; optimistic concurrency; trash/restore; code injection/path traversal rejection; package tamper guard; local/system MCP runtime config");
var legacy=catalog.Read();
legacy.Items.Single().Enabled=false; // Prior tamper test must not poison the separate Axis fixture.
legacy.Items.Add(new CadToolEntry{Id=CadToolCatalog.AxisToolId,Kind="builtin",Name="Trục dự án",
 Group="Bản vẽ",Notes="Ghi chú đã lưu",Enabled=false,Commands=new(){CadToolCatalog.AxisCommand}});
Json.Atomic(Path.Combine(catalog.Root,"catalog.json"),legacy);
string axisSource=Path.Combine(input,"Rainmaker.Axis.dll");
File.WriteAllText(axisSource,"axis package");
var axis=catalog.Import(axisSource,legacy.Revision);
Check(axis.Id==CadToolCatalog.AxisToolId&&axis.Kind=="dotnet"&&axis.Name=="Trục dự án"&&axis.Group=="Bản vẽ");
Check(axis.Notes=="Ghi chú đã lưu"&&!axis.Enabled&&axis.RibbonPins.Count==0&&axis.Commands.Single()==CadToolCatalog.AxisCommand);
catalog.SetEnabled(axis.Id,legacy.Revision+1,true);
var enabledAxis=catalog.Read().Items.Single(t=>t.Id==axis.Id);
Check(File.Exists(catalog.Demand(enabledAxis))&&catalog.PathFor(enabledAxis).EndsWith("Rainmaker.Axis.dll"));
catalog.Update(axis.Id,legacy.Revision+2,axis.Name,axis.Group,axis.Notes,true,axis.Commands,"delete");
Check(catalog.Read().Items.Single(t=>t.Id==axis.Id).DeletedUtc.HasValue);
Console.WriteLine("PASS: the Axis DLL replaces the legacy entry, preserves its settings and supports trash");

string ribbonRoot=Path.Combine(root,"ribbon");Directory.CreateDirectory(ribbonRoot);var ribbons=new CadToolCatalog(ribbonRoot);
Directory.CreateDirectory(ribbons.Root);Json.Atomic(Path.Combine(ribbons.Root,"catalog.json"),new JObject{["Revision"]=0,["Items"]=new JArray()});
Check(ribbons.Read().Tabs.Single().Id==CadToolCatalog.DefaultTabId);
for(int i=0;i<4;i++){var persisted=ribbons.Read();Json.Atomic(Path.Combine(ribbons.Root,"catalog.json"),persisted);Check(ribbons.Read().Tabs.Count==1);}
var ribbonTool=ribbons.Import(source,0);ribbons.Update(ribbonTool.Id,1,"Thư viện","Mẫu","",true,new[]{"QATEST","QATEST2"});
string libraryTab=ribbons.SaveTab(2,"","Thư viện",10);string architectureTab=ribbons.SaveTab(3,"","Kiến trúc",20);
Reject(()=>ribbons.SaveTab(4,"","THƯ VIỆN",30),"RIBBON_TAB_EXISTS");
ribbons.Update(ribbonTool.Id,4,"Thư viện","Mẫu","",true,new[]{"QATEST","QATEST2"},tabId:libraryTab);
ribbons.PinCommands(ribbonTool.Id,5,new[]{new CadRibbonPin{Command="QATEST",Label="Nhận vào nhóm"},new CadRibbonPin{Command="QATEST2",Label="Xếp gọn"}});
ribbons.SetRibbonPin(ribbonTool.Id,6,new(){Command="QATEST2",Label="Xếp gọn",TabId=architectureTab},true);
ribbons.SaveTab(7,libraryTab,"Quản lý thư viện",15,false);Check(ribbons.Read().Tabs.Single(t=>t.Id==libraryTab).Name=="Quản lý thư viện");
Check(!ribbons.Read().Tabs.Single(t=>t.Id==libraryTab).Enabled&&ribbons.Read().Items.Single().TabId==libraryTab);
Reject(()=>ribbons.Update(ribbonTool.Id,8,"Thư viện","","",true,new[]{"QATEST"},tabId:"missing"),"RIBBON_TAB_MISSING");Check(ribbons.Read().Revision==8&&ribbons.Read().Items.Single().Commands.Count==2);
var ribbonUpdate=ribbons.Import(source,8,ribbonTool.Id);Check(ribbonUpdate.TabId==libraryTab&&ribbonUpdate.RibbonPins.Count==2);
ribbons.DeleteTab(9,architectureTab);Check(ribbons.Read().Items.Single().RibbonPins.Single(p=>p.Command=="QATEST2").TabId=="");
ribbons.DeleteTab(10,libraryTab);Check(ribbons.Read().Items.Single().TabId==CadToolCatalog.DefaultTabId&&ribbons.Read().Items.Single().RibbonPins.Count==2&&File.Exists(ribbons.PathFor(ribbonUpdate)));
Reject(()=>ribbons.DeleteTab(11,CadToolCatalog.DefaultTabId),"RIBBON_DEFAULT_TAB");Reject(()=>ribbons.DeleteTab(11,"ACAD.ID_TabHome"),"RIBBON_TAB_MISSING");
Reject(()=>ribbons.SaveTab(10,"","Stale",0),"STALE_EDIT");
Console.WriteLine("PASS: legacy tab migration, create/rename/hide, whole-addin routing, per-command override, preserved assignments on update, safe tab deletion and protected native/default tabs");
// Inherited Ribbon groups follow the saved tool group; explicit overrides remain separate.
var groupedTool=ribbons.Read().Items.Single();
var inheritedPin=groupedTool.RibbonPins.Single(p=>p.Command=="QATEST");
Check(CadToolCatalog.RibbonGroup(groupedTool,inheritedPin)=="Mẫu");
ribbons.Update(groupedTool.Id,11,groupedTool.Name,"Thư viện",groupedTool.Notes,true,groupedTool.Commands);
groupedTool=ribbons.Read().Items.Single();
Check(CadToolCatalog.RibbonGroup(groupedTool,groupedTool.RibbonPins.Single(p=>p.Command=="QATEST"))=="Thư viện");
ribbons.SetRibbonPin(groupedTool.Id,12,new(){Command="QATEST2",Label="Riêng",Group="Nhóm riêng"},true);
groupedTool=ribbons.Read().Items.Single();
Check(CadToolCatalog.RibbonGroup(groupedTool,groupedTool.RibbonPins.Single(p=>p.Command=="QATEST2"))=="Nhóm riêng");
ribbons.SetRibbonPin(groupedTool.Id,13,new(){Command="QATEST2",Label="Theo công cụ",Group=""},true);
groupedTool=ribbons.Read().Items.Single();
Check(CadToolCatalog.RibbonGroup(groupedTool,groupedTool.RibbonPins.Single(p=>p.Command=="QATEST2"))=="Thư viện");
Console.WriteLine("PASS: inherited Ribbon groups follow profile changes and explicit groups can return to inheritance");

// A saved command list can be pinned without loading the DLL; tab and pins commit together.
string savedTab=ribbons.SaveTab(14,"","Saved commands",30);
Reject(()=>ribbons.PinCommands(groupedTool.Id,15,new[]{new CadRibbonPin{Command="UNKNOWN"}},savedTab),"TOOL_COMMAND");
Check(ribbons.Read().Revision==15&&ribbons.Read().Items.Single().TabId==CadToolCatalog.DefaultTabId);
ribbons.SetRibbonPin(groupedTool.Id,15,new(){Command="QATEST2"},false);
ribbons.PinCommands(groupedTool.Id,16,new[]{new CadRibbonPin{Command="QATEST",Label="Do not replace"},new CadRibbonPin{Command="QATEST2",Label="New saved pin",Group=""}},savedTab);
var savedPins=ribbons.Read().Items.Single();
Check(ribbons.Read().Revision==17&&savedPins.TabId==savedTab&&savedPins.RibbonPins.Count==2);
Check(savedPins.RibbonPins.Single(p=>p.Command=="QATEST").Label=="Nhận vào nhóm");
Check(CadToolCatalog.RibbonGroup(savedPins,savedPins.RibbonPins.Single(p=>p.Command=="QATEST2"))=="Thư viện");
Console.WriteLine("PASS: saved commands pin without loading, atomic tab/pins rollback, custom labels preserved, new pins inherit group");

// New installations are single-version; history survives and shared replicas cannot load partial packages.
var storage=new CadToolCatalog(Path.Combine(root,"storage"));
var installed=storage.Import(source,0);Check(installed.RelativePath.Contains(Path.Combine("dotnet","_Installed","sample")));
Reject(()=>storage.Import(source,1),"TOOL_DUPLICATE");Check(storage.Read().Revision==1);
storage.Update(installed.Id,1,"Configured name","Saved group","Saved notes",true,new[]{"QATEST"});
storage.SetRibbonPin(installed.Id,2,new(){Command="QATEST",Label="Saved pin"},true);
File.WriteAllText(source,"third build");var next=storage.Import(source,3,installed.Id);
Check(next.Id==installed.Id&&next.Name=="Configured name"&&next.RibbonPins.Single().Label=="Saved pin");
Check(Directory.GetDirectories(Path.Combine(storage.Root,"dotnet","_Installed","sample")).Length==1&&!File.Exists(storage.PathFor(installed)));
Check(Directory.GetFiles(Path.Combine(storage.Root,"dotnet","_Development","sample","versions"),"sample.dll",SearchOption.AllDirectories).Length==2);
Reject(()=>storage.Demand(installed),"TOOL_CHANGED");
// Reverse installation is a normal update from the immutable archive.
string previous=Directory.GetFiles(Path.Combine(storage.Root,"dotnet","_Development","sample","versions"),"sample.dll",SearchOption.AllDirectories).Single(f=>File.ReadAllText(f)!="third build");
var restored=storage.Import(previous,4,next.Id);Check(restored.FileHash==installed.FileHash&&restored.RibbonPins.Count==1);
Check(Directory.GetDirectories(Path.Combine(storage.Root,"dotnet","_Installed","sample")).Length==1);
using(var held=new FileStream(Path.Combine(storage.Root,".catalog-write.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
    Reject(()=>storage.SetEnabled(restored.Id,5,false),"TOOL_BUSY");
Check(storage.Read().Revision==5);
// A second root receives catalog before DLL files: it must refuse loading until the copy is complete.
var replica=new CadToolCatalog(Path.Combine(root,"replica"));Directory.CreateDirectory(replica.Root);
File.Copy(Path.Combine(storage.Root,"catalog.json"),Path.Combine(replica.Root,"catalog.json"));
Reject(()=>replica.Demand(replica.Read().Items.Single()),"TOOL_CHANGED");
string replicaFile=replica.PathFor(restored);Directory.CreateDirectory(Path.GetDirectoryName(replicaFile)!);
foreach(string file in Directory.GetFiles(Path.GetDirectoryName(storage.PathFor(restored))!))File.Copy(file,Path.Combine(Path.GetDirectoryName(replicaFile)!,Path.GetFileName(file)));
Check(replica.Demand(replica.Read().Items.Single())==replicaFile);
// Migration preserves the actual selected binary and every tool setting, then removes the old folder.
var migrate=new CadToolCatalog(Path.Combine(root,"migration"));var created=migrate.Import(source,0);
var oldIndex=migrate.Read();var selected=oldIndex.Items.Single();string newPath=migrate.PathFor(selected);
string legacyFolder=Path.Combine(migrate.Root,"dotnet",selected.Id,selected.PackageHash);Directory.CreateDirectory(legacyFolder);
foreach(string file in Directory.GetFiles(Path.GetDirectoryName(newPath)!))File.Copy(file,Path.Combine(legacyFolder,Path.GetFileName(file)));
selected.RelativePath=Path.Combine("dotnet",selected.Id,selected.PackageHash,"sample.dll");oldIndex.StorageVersion=0;Json.Atomic(Path.Combine(migrate.Root,"catalog.json"),oldIndex);
File.SetAttributes(Path.Combine(legacyFolder,"sample.dll"),FileAttributes.ReadOnly);
File.SetAttributes(legacyFolder,File.GetAttributes(legacyFolder)|FileAttributes.ReadOnly);
File.SetAttributes(Path.GetDirectoryName(legacyFolder)!,File.GetAttributes(Path.GetDirectoryName(legacyFolder)!)|FileAttributes.ReadOnly);
Directory.Delete(Path.Combine(migrate.Root,"dotnet","_Installed"),true);
migrate.MigrateInstalled(1);var moved=migrate.Read().Items.Single();
Check(migrate.Read().StorageVersion==2&&moved.Id==created.Id&&moved.FileHash==created.FileHash&&moved.PackageHash==created.PackageHash);
Check(!Directory.Exists(legacyFolder)&&File.Exists(migrate.Demand(moved)));
Check(Directory.GetDirectories(Path.Combine(migrate.Root,"dotnet","_Installed","sample")).Length==1);
Console.WriteLine("PASS: one installed version, immutable history, rollback, duplicate rejection, warehouse write lock, partial-sync refusal and legacy migration");
// An interrupted removal can leave an empty read-only legacy folder. Retrying must finish the cleanup.
Directory.CreateDirectory(legacyFolder);File.SetAttributes(legacyFolder,File.GetAttributes(legacyFolder)|FileAttributes.ReadOnly);
migrate.MigrateInstalled(migrate.Read().Revision);Check(!Directory.Exists(Path.GetDirectoryName(legacyFolder)!));
Console.WriteLine("PASS: read-only sync folders and interrupted empty-folder cleanup retries");

// Ribbon edits are atomic and undo only restores the same unchanged package.
var editor=new CadToolCatalog(Path.Combine(root,"ribbon-editing"));var editTool=editor.Import(source,0);
editor.Update(editTool.Id,1,"Library","Original group","Notes",true,new[]{"QALIBRARY","QALIBPACK"});
string aTab=editor.SaveTab(2,"","A",1),bTab=editor.SaveTab(3,"","B",2),hiddenTab=editor.SaveTab(4,"","Hidden",3,false);
editor.SetRibbonLayout(editTool.Id,5,aTab,new[]{new CadRibbonPin{Command="QALIBRARY",Label="Open custom",Order=17},new CadRibbonPin{Command="QALIBPACK",Label="Pack custom",TabId=bTab,Group="Custom",Order=23}},"FIXTURE");
string beforeLayout=CadToolCatalog.RibbonLayoutHash(editor.Read().Items.Single());int beforeRevision=editor.Read().Revision;
editor.MoveRibbon(editTool.Id,beforeRevision,bTab);var movedLayout=editor.Read();
Check(movedLayout.Revision==beforeRevision+1&&movedLayout.Items.Single().RibbonPins.All(p=>p.TabId==""));
Check(movedLayout.Items.Single().RibbonPins.All(p=>CadToolCatalog.RibbonTarget(movedLayout,movedLayout.Items.Single(),p)==bTab));
Check(movedLayout.Items.Single().RibbonPins[0].Label=="Open custom"&&movedLayout.Items.Single().RibbonPins[1].Order==23);
Check(new CadToolCatalog(Path.Combine(root,"ribbon-editing")).CanUndoRibbon(editTool.Id));
editor.UndoRibbon(editTool.Id,movedLayout.Revision);Check(CadToolCatalog.RibbonLayoutHash(editor.Read().Items.Single())==beforeLayout&&!editor.CanUndoRibbon(editTool.Id));
int unchanged=editor.Read().Revision;Reject(()=>editor.MoveRibbon(editTool.Id,unchanged,hiddenTab),"RIBBON_TAB_HIDDEN");Check(editor.Read().Revision==unchanged);
Reject(()=>editor.SetRibbonLayout(editTool.Id,unchanged,aTab,new[]{new CadRibbonPin{Command="UNKNOWN"}},"INVALID"),"TOOL_COMMAND");Check(editor.Read().Revision==unchanged);
editor.SetRibbonLayout(editTool.Id,unchanged,aTab,Array.Empty<CadRibbonPin>(),"UNPIN_ALL");Check(editor.Read().Items.Single().RibbonPins.Count==0);
editor.UndoRibbon(editTool.Id,editor.Read().Revision);Check(CadToolCatalog.RibbonLayoutHash(editor.Read().Items.Single())==beforeLayout);
editor.SetRibbonLayout(editTool.Id,editor.Read().Revision,bTab,new[]{new CadRibbonPin{Command="QALIBRARY",Label="Main"}},"MAIN_ONLY");
var saved=editor.Read().Items.Single();Check(saved.Commands.Count==2&&saved.RibbonPins.Count==1&&saved.Name=="Library"&&saved.Group=="Original group"&&saved.Notes=="Notes");
editor.SetRibbonPin(editTool.Id,editor.Read().Revision,new(){Command="QALIBPACK",Label="Other session"},true);
Check(!editor.CanUndoRibbon(editTool.Id));Reject(()=>editor.UndoRibbon(editTool.Id,editor.Read().Revision),"RIBBON_UNDO_CHANGED");
Console.WriteLine("PASS: mixed tab overrides move atomically, reopen/undo, unpin all, main-only, hidden/invalid rejection and cross-session undo guard");
