using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
namespace AICad.Contracts;

public sealed class CadSharedComponentFile {public string Name{get;set;}="";public string Hash{get;set;}="";}
public sealed class CadSharedComponent {
 public int SchemaVersion{get;set;}=1;public int Revision{get;set;}
 public string Name{get;set;}="Rainmaker.Library.Reader";public string Version{get;set;}="";
 public string PackageHash{get;set;}="";public List<CadSharedComponentFile> Files{get;set;}=new();
}
// Local dependency installation, deliberately separate from the shared tool catalog.
public sealed class CadSharedComponentStore {
 public const string ReaderName="Rainmaker.Library.Reader";
 public string Root{get;}
 string Pointer=>Path.Combine(Root,"current.json");
 public CadSharedComponentStore(string dataRoot){Root=Path.GetFullPath(Path.Combine(dataRoot,"tool-manager","shared-components",ReaderName));CheckPath(Root);}
 static void CheckPath(string path){for(var d=new DirectoryInfo(Path.GetDirectoryName(path)!);d!=null;d=d.Parent)if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new CadException("COMPONENT_PATH","Không dùng thư mục liên kết cho thành phần dùng chung.");if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new CadException("COMPONENT_PATH","Không dùng file liên kết.");}
 static bool Digest(string hash)=>hash.Length==64&&hash.All(c=>"0123456789abcdef".Contains(c));
 public CadSharedComponent? Read(){CheckPath(Pointer);if(!File.Exists(Pointer))return null;var value=Json.Read<CadSharedComponent>(File.ReadAllText(Pointer));Validate(value);return value;}
 static void Validate(CadSharedComponent value){
  if(value.SchemaVersion!=1||value.Name!=ReaderName||value.Revision<1||!System.Version.TryParse(value.Version,out var v)||v.ToString()!=value.Version||!Digest(value.PackageHash)||value.Files.Count<1||value.Files.Count>2||value.Files.Select(f=>f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=value.Files.Count||value.Files.Any(f=>!new[]{ReaderName+".dll",ReaderName+".pdb"}.Contains(f.Name)||!Digest(f.Hash))||!value.Files.Any(f=>f.Name==ReaderName+".dll")||Json.Hash(value.Files)!=value.PackageHash)throw new CadException("COMPONENT_INVALID","Cấu hình Reader không hợp lệ; cài lại gói Reader đã bàn giao.");
 }
 public string Demand(CadSharedComponent value){
  Validate(value);string folder=Path.Combine(Root,"packages",value.Version+"-"+value.PackageHash.Substring(0,12));
  foreach(var file in value.Files){string path=Path.Combine(folder,file.Name);CheckPath(path);if(!File.Exists(path)||Json.FileHash(path)!=file.Hash)throw new CadException("COMPONENT_CHANGED","Reader thiếu hoặc sai hash. Cài lại từ gói đã bàn giao.");}
  return Path.Combine(folder,ReaderName+".dll");
 }
 public CadSharedComponent Install(string source,int revision){
  source=Path.GetFullPath(source);if(Path.GetFileName(source)!=ReaderName+".dll"||!File.Exists(source))throw new CadException("COMPONENT_TYPE","Chọn Rainmaker.Library.Reader.dll trong gói Reader riêng.");CheckPath(source);
  var identity=AssemblyName.GetAssemblyName(source);if(identity.Name!=ReaderName||identity.Version!=new Version(1,0,0,0))throw new CadException("LIBRARY_PROTOCOL","Chỉ hỗ trợ Reader giao thức V1.");
  string version=FileVersionInfo.GetVersionInfo(source).FileVersion;if(!Version.TryParse(version,out var parsed)||parsed.Major!=1)throw new CadException("LIBRARY_PROTOCOL","FileVersion Reader không tương thích.");
  var files=new[]{source,Path.ChangeExtension(source,".pdb")}.Where(File.Exists).OrderBy(Path.GetFileName,StringComparer.Ordinal).Select(p=>new CadSharedComponentFile{Name=Path.GetFileName(p),Hash=Json.FileHash(p)}).ToList();
  CheckPath(Pointer);Directory.CreateDirectory(Root);using var lease=new FileStream(Path.Combine(Root,".install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  var old=Read();if((old?.Revision??0)!=revision)throw new CadException("STALE_EDIT","Reader đã được cập nhật. Làm mới rồi thử lại.");
  var next=new CadSharedComponent{Revision=revision+1,Version=version,Files=files,PackageHash=Json.Hash(files)};
  string package=Path.Combine(Root,"packages",version+"-"+next.PackageHash.Substring(0,12));
  if(!Directory.Exists(package)){
   string staging=Path.Combine(Root,".staging-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
   try{foreach(var file in files){string path=Path.Combine(staging,file.Name);File.Copy(Path.Combine(Path.GetDirectoryName(source)!,file.Name),path);if(Json.FileHash(path)!=file.Hash)throw new CadException("COMPONENT_CHANGED","Gói nguồn thay đổi trong lúc cài; thử lại sau khi đồng bộ đủ.");}Directory.CreateDirectory(Path.GetDirectoryName(package)!);Directory.Move(staging,package);}
   finally{if(Directory.Exists(staging))foreach(string file in Directory.GetFiles(staging))File.Delete(file);if(Directory.Exists(staging))Directory.Delete(staging);}
  }
  Demand(next);if(old!=null)Json.Atomic(Path.Combine(Root,"history",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".json"),old);
  Json.Atomic(Pointer,next);return next;
 }
}
