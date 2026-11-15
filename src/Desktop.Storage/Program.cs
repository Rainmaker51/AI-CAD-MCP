using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;

Console.OutputEncoding=System.Text.Encoding.UTF8;
string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));
var jsonOptions=new JsonSerializerOptions{WriteIndented=true};
var pairs=new[]{("tests/fixtures","storage/01-test-data/fixtures"),("tests/results","storage/01-test-data/results"),("data-p0-p6","storage/02-app-data/data-p0-p6"),(".tools/python-packages","storage/03-python-ocr/python-packages"),("artifacts","storage/04-builds/artifacts")};
string Checked(string baseRoot,string relative){string path=Path.GetFullPath(Path.Combine(baseRoot,relative));if(!path.StartsWith(Path.GetFullPath(baseRoot)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Path leaves workspace: "+path);return path;}
bool Link(string path)=>(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;
string Target(string path)=>new DirectoryInfo(path).ResolveLinkTarget(true)?.FullName??path;
(long files,long bytes) Inventory(string path){long n=0,b=0;foreach(var f in Directory.EnumerateFiles(path,"*",SearchOption.AllDirectories)){n++;b+=new FileInfo(f).Length;}return(n,b);}
void Junction(string source,string target){
 string Quote(string s)=>"'"+s.Replace("'","''")+"'";
 var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell/v1.0/powershell.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,RedirectStandardOutput=true};
 start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-Command");start.ArgumentList.Add("$ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path "+Quote(source)+" -Target "+Quote(target)+" | Out-Null");
 using var p=Process.Start(start)!;var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();p.WaitForExit();if(p.ExitCode!=0)throw new IOException(error.Result);_=output.Result;
}
object MoveBuilds(string baseRoot){string source=Checked(baseRoot,"artifacts"),target=Checked(baseRoot,"storage/04-builds/artifacts");Directory.CreateDirectory(target);string marker=Checked(baseRoot,"storage/build-copy-complete.json");if(File.Exists(marker))return new{source="artifacts",target="storage/04-builds/artifacts",status="ALREADY_COPIED_ORIGINAL_RETAINED"};long count=0,bytes=0;foreach(string file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){string next=Checked(baseRoot,"storage/04-builds/artifacts/"+Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(next)!);if(!File.Exists(next))File.Copy(file,next);else if(Path.GetFileName(file)!="install.json"){using var a=File.OpenRead(file);using var b=File.OpenRead(next);if(!System.Security.Cryptography.SHA256.HashData(a).SequenceEqual(System.Security.Cryptography.SHA256.HashData(b)))throw new IOException("Existing build copy differs: "+file);}count++;bytes+=new FileInfo(file).Length;}Save(marker,new{createdUtc=DateTime.UtcNow,files=count,bytes});return new{source="artifacts",target="storage/04-builds/artifacts",status="COPIED_ORIGINAL_RETAINED",files=count,bytes};}
object MoveOne(string baseRoot,(string old,string next) pair){
 string source=Checked(baseRoot,pair.old),target=Checked(baseRoot,pair.next);
 if(pair.old=="artifacts"&&Directory.Exists(source)&&!Link(source))return MoveBuilds(baseRoot);
 if(Directory.Exists(source)&&Link(source)){if(!string.Equals(Target(source),target,StringComparison.OrdinalIgnoreCase))throw new IOException("Existing link has an unexpected target: "+source);return new{source=pair.old,target=pair.next,status="ALREADY_ORGANIZED"};}
 if(Directory.Exists(target))throw new IOException("Destination already exists; refusing to merge: "+target);
 if(!Directory.Exists(source))throw new DirectoryNotFoundException(source);
 var before=Inventory(source);Directory.CreateDirectory(Path.GetDirectoryName(target)!);Directory.Move(source,target);
 try{Junction(source,target);}catch{if(!Directory.Exists(source))Directory.Move(target,source);throw;}
 var after=Inventory(target);if(before!=after)throw new IOException("Inventory changed during relocation: "+source);
 return new{source=pair.old,target=pair.next,status="MOVED",files=after.files,bytes=after.bytes};
}
void Save(string path,object value){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(value,jsonOptions));}
void StopHost(){var buildLocations=new[]{Checked(root,"artifacts")+Path.DirectorySeparatorChar,Checked(root,"storage/04-builds/artifacts")+Path.DirectorySeparatorChar};foreach(var p in Process.GetProcessesByName("AICad.Host"))using(p){string? path;try{path=p.MainModule?.FileName;}catch(System.ComponentModel.Win32Exception){continue;}if(path==null||!buildLocations.Any(location=>Path.GetFullPath(path).StartsWith(location,StringComparison.OrdinalIgnoreCase))||Path.GetFileName(path)!="AICad.Host.exe")continue;p.Kill();if(!p.WaitForExit(10000))throw new IOException("An app Host did not stop.");}}
void Configs(){
 string builds=Checked(root,"storage/04-builds/artifacts");foreach(string file in Directory.EnumerateFiles(builds,"install.json",SearchOption.AllDirectories)){
  var c=JsonNode.Parse(File.ReadAllText(file))!.AsObject();string? data=c["DataRoot"]?.GetValue<string>();
  if(data!=null&&string.Equals(Path.GetFullPath(data),Checked(root,"data-p0-p6"),StringComparison.OrdinalIgnoreCase)){
   string backup=file+".before-storage-layout";if(!File.Exists(backup))File.Copy(file,backup);
   c["DataRoot"]=Checked(root,"storage/02-app-data/data-p0-p6");
  }
  string? host=c["HostExecutable"]?.GetValue<string>();string oldBuilds=Checked(root,"artifacts")+Path.DirectorySeparatorChar;if(host!=null&&Path.GetFullPath(host).StartsWith(oldBuilds,StringComparison.OrdinalIgnoreCase))c["HostExecutable"]=Path.Combine(builds,Path.GetRelativePath(Checked(root,"artifacts"),host));File.WriteAllText(file,c.ToJsonString(jsonOptions));
 }
}
try{
 if(args.SequenceEqual(new[]{"--self-test"})){
  string temp=Path.Combine(Path.GetTempPath(),"aicad-storage-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  string source=Checked(temp,"tests/fixtures");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"keep.txt"),"preserve original content");
  MoveOne(temp,pairs[0]);if(File.ReadAllText(Path.Combine(source,"keep.txt"))!="preserve original content")throw new Exception("Alias did not preserve content");MoveOne(temp,pairs[0]);
  bool rejected=false;try{Checked(temp,"../outside");}catch(IOException){rejected=true;}if(!rejected)throw new Exception("Containment guard failed");
  string collision=Checked(temp,"storage/02-app-data/data-p0-p6");Directory.CreateDirectory(collision);rejected=false;try{MoveOne(temp,pairs[2]);}catch(IOException){rejected=true;}if(!rejected)throw new Exception("Existing destination was accepted");
  Console.WriteLine("PASS relocation preserves content, alias access, idempotence, containment, collision refusal. Test root: "+temp);return;
 }
 if(!File.Exists(Checked(root,"AI-CAD-MCP.slnx")))throw new IOException("Run the published organizer from tools/storage-organizer inside the project.");
 string mode=args.SingleOrDefault()??"--plan";if(!new[]{"--plan","--apply","--verify"}.Contains(mode))throw new ArgumentException("Use --plan, --apply or --verify.");
 if(mode=="--plan"){foreach(var p in pairs)Console.WriteLine(p.Item1+" -> "+p.Item2);Console.WriteLine("No files changed. --apply requires all AutoCAD windows closed.");return;}
 if(mode=="--apply"){
  var cad=Process.GetProcessesByName("acad");if(cad.Length>0){foreach(var p in cad)p.Dispose();throw new IOException("Save drawings and close all AutoCAD windows before applying.");}
  StopHost();var report=new List<object>();foreach(var p in pairs){var entry=MoveOne(root,p);report.Add(entry);Save(Checked(root,"storage/layout.json"),new{version=1,root,updatedUtc=DateTime.UtcNow,entries=report});}
  Configs();Console.WriteLine("Storage organized. Compatibility paths preserved; original build folders retained as backup. Start Mo AI CAD.cmd to reopen the app.");
 }
 foreach(var p in pairs){string alias=Checked(root,p.Item1),target=Checked(root,p.Item2);bool valid=p.Item1=="artifacts"&&!Link(alias)?Directory.GetDirectories(alias).All(c=>Directory.Exists(Path.Combine(target,Path.GetFileName(c)))):Link(alias)&&string.Equals(Target(alias),target,StringComparison.OrdinalIgnoreCase);if(!Directory.Exists(target)||!Directory.Exists(alias)||!valid)throw new IOException("Invalid compatibility path: "+alias);var size=Inventory(target);Console.WriteLine(p.Item2+": "+size.files+" files, "+Math.Round(size.bytes/1048576d,1)+" MB; path OK");}
 var config=JsonNode.Parse(File.ReadAllText(Checked(root,"storage/04-builds/artifacts/p0-p6/install.json")))!;string dataRoot=config["DataRoot"]!.GetValue<string>();if(Link(dataRoot))throw new IOException("DataRoot must point to its physical directory, not its compatibility link.");
 Console.WriteLine("PASS: all storage aliases and physical DataRoot verified.");
}catch(Exception e){Console.Error.WriteLine("Storage organization stopped: "+e.Message);Console.Error.WriteLine("Original data is retained. Check storage/layout.json before retrying a partial migration.");Environment.ExitCode=1;}
