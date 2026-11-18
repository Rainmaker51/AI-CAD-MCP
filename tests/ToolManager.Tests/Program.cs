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
File.WriteAllText(source,"updated package");var updated=catalog.Import(source,2,item.Id);Check(updated.Id==item.Id&&updated.Group==".NET / Chung"&&updated.Notes=="Mục đích"&&updated.FileHash!=item.FileHash);Check(File.Exists(catalog.PathFor(item)));
catalog.Update(item.Id,3,updated.Name,updated.Group,updated.Notes,true,updated.Commands,"delete");Reject(()=>catalog.Demand(catalog.Read().Items.Single()),"TOOL_DISABLED");catalog.Update(item.Id,4,updated.Name,updated.Group,updated.Notes,true,updated.Commands,"restore");Check(catalog.Demand(catalog.Read().Items.Single())==catalog.PathFor(updated));
Reject(()=>CadToolCatalog.CommandName("HELLO) (evil"),"TOOL_COMMAND");var malicious=new CadToolEntry{RelativePath="../outside.dll"};Reject(()=>catalog.PathFor(malicious),"TOOL_PATH");
File.AppendAllText(catalog.PathFor(updated),"tampered");Reject(()=>catalog.Demand(updated),"TOOL_CHANGED");
string second=Path.Combine(root,"second");Directory.CreateDirectory(second);File.Copy(source,Path.Combine(second,"sample.dll"));var another=new CadToolCatalog(second);var copy=another.Import(Path.Combine(second,"sample.dll"),0);Check(!Path.IsPathRooted(copy.RelativePath));Check(another.Demand(copy).StartsWith(another.Root));
var config=CadToolMcpConfig.Create(new InstallConfig{ContentRoot=root,HostExecutable=Path.Combine(root,"artifacts/p0-p6/Host/AICad.Host.exe")});Check(config["claudeDesktop"]!["mcpServers"]!["ai_cad"]!["args"]![0]!.Value<string>()!.EndsWith(Path.Combine("MCP","AICad.Mcp.dll")));Check(config.Value<string>("codexToml")!.Contains("[mcp_servers.ai_cad]"));
string? originalDotnetRoot=Environment.GetEnvironmentVariable("DOTNET_ROOT");string systemSdk=Path.Combine(root,"system-sdk");Directory.CreateDirectory(systemSdk);File.WriteAllText(Path.Combine(systemSdk,"dotnet.exe"),"test marker only");
try{Environment.SetEnvironmentVariable("DOTNET_ROOT",systemSdk);var systemConfig=CadToolMcpConfig.Create(new InstallConfig{ContentRoot=root,HostExecutable=Path.Combine(root,"artifacts/p0-p6/Host/AICad.Host.exe")});Check(systemConfig["claudeDesktop"]!["mcpServers"]!["ai_cad"]!.Value<string>("command")==Path.Combine(systemSdk,"dotnet.exe"));}finally{Environment.SetEnvironmentVariable("DOTNET_ROOT",originalDotnetRoot);}
Console.WriteLine("PASS: DLL-only imports; relative shared paths; update preserves metadata/history; optimistic concurrency; trash/restore; code injection/path traversal rejection; package tamper guard; local/system MCP runtime config");
var legacy=catalog.Read();
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
