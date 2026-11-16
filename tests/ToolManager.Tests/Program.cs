using System;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
void Check(bool value){if(!value)throw new Exception("Assertion failed");}
void Reject(Action work,string code){try{work();}catch(CadException e)when(e.Code==code){return;}throw new Exception("Expected "+code);}
string root=Path.Combine(Path.GetTempPath(),"aicad-tools-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var catalog=new CadToolCatalog(root);string input=Path.Combine(root,"source");Directory.CreateDirectory(input);string source=Path.Combine(input,"hello.lsp");
File.WriteAllText(source,"; (defun c:COMMENT () )\n;| (defun c:BLOCK () ) ;| nested |; |;\n(setq text \"(defun c:STRING () )\")\n(defun c:HELLO () (princ))\n(defun c:hello () (princ))\n(defun c:SECOND () (princ))");
var item=catalog.Import(source,0);Check(item.Commands.SequenceEqual(new[]{"HELLO","SECOND"}));Check(!Path.IsPathRooted(item.RelativePath));Check(catalog.Demand(item).StartsWith(catalog.Root));
catalog.Update(item.Id,1,"Hello","LISP / Chung","Mục đích",true,item.Commands);Reject(()=>catalog.Update(item.Id,1,"Stale","","",true,item.Commands),"STALE_EDIT");
File.AppendAllText(source,"\n(defun c:NEW () (princ))");var updated=catalog.Import(source,2,item.Id);Check(updated.Id==item.Id&&updated.Group=="LISP / Chung"&&updated.Notes=="Mục đích"&&updated.FileHash!=item.FileHash&&updated.Commands.Contains("NEW"));Check(File.Exists(catalog.PathFor(item)));
catalog.Update(item.Id,3,updated.Name,updated.Group,updated.Notes,true,updated.Commands,"delete");Reject(()=>catalog.Demand(catalog.Read().Items.Single()),"TOOL_DISABLED");catalog.Update(item.Id,4,updated.Name,updated.Group,updated.Notes,true,updated.Commands,"restore");Check(catalog.Demand(catalog.Read().Items.Single())==catalog.PathFor(updated));
Reject(()=>CadToolCatalog.CommandName("HELLO) (evil"),"TOOL_COMMAND");var malicious=new CadToolEntry{RelativePath="../outside.dll"};Reject(()=>catalog.PathFor(malicious),"TOOL_PATH");
File.AppendAllText(catalog.PathFor(updated),"tampered");Reject(()=>catalog.Demand(updated),"TOOL_CHANGED");
string second=Path.Combine(root,"second");Directory.CreateDirectory(second);File.Copy(source,Path.Combine(second,"hello.lsp"));var another=new CadToolCatalog(second);var copy=another.Import(Path.Combine(second,"hello.lsp"),0);Check(!Path.IsPathRooted(copy.RelativePath));Check(another.Demand(copy).StartsWith(another.Root));
var config=CadToolMcpConfig.Create(new InstallConfig{ContentRoot=root,HostExecutable=Path.Combine(root,"artifacts/p0-p6/Host/AICad.Host.exe")});Check(config["claudeDesktop"]!["mcpServers"]!["ai_cad"]!["args"]![0]!.Value<string>()!.EndsWith(Path.Combine("MCP","AICad.Mcp.dll")));Check(config.Value<string>("codexToml")!.Contains("[mcp_servers.ai_cad]"));
string? originalDotnetRoot=Environment.GetEnvironmentVariable("DOTNET_ROOT");string systemSdk=Path.Combine(root,"system-sdk");Directory.CreateDirectory(systemSdk);File.WriteAllText(Path.Combine(systemSdk,"dotnet.exe"),"test marker only");
try{Environment.SetEnvironmentVariable("DOTNET_ROOT",systemSdk);var systemConfig=CadToolMcpConfig.Create(new InstallConfig{ContentRoot=root,HostExecutable=Path.Combine(root,"artifacts/p0-p6/Host/AICad.Host.exe")});Check(systemConfig["claudeDesktop"]!["mcpServers"]!["ai_cad"]!.Value<string>("command")==Path.Combine(systemSdk,"dotnet.exe"));}finally{Environment.SetEnvironmentVariable("DOTNET_ROOT",originalDotnetRoot);}
Console.WriteLine("PASS: Lisp parser excludes comments/strings; relative shared paths; update preserves metadata/history; optimistic concurrency; trash/restore; code injection/path traversal rejection; package tamper guard; local/system MCP runtime config");
