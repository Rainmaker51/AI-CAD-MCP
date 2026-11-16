using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace AICad.Contracts;
public sealed class CadToolEntry {
 public string Id{get;set;}="";public string Name{get;set;}="";public string Kind{get;set;}="";
 public string RelativePath{get;set;}="";public string FileHash{get;set;}="";public string PackageHash{get;set;}="";
 public string Group{get;set;}="";public string Notes{get;set;}="";public bool Enabled{get;set;}=true;
 public DateTime? DeletedUtc{get;set;}public List<string> Commands{get;set;}=new();public DateTime UpdatedUtc{get;set;}
}
public sealed class CadToolIndex {public int Revision{get;set;}public List<CadToolEntry> Items{get;set;}=new();}
public sealed class CadToolCatalog {
 readonly string root;public string Root=>root;string Index=>Path.Combine(root,"catalog.json");
 public CadToolCatalog(string appRoot){root=Path.Combine(WarehousePaths.Resolve(appRoot),"Tools");}
 public CadToolIndex Read()=>File.Exists(Index)?Json.Read<CadToolIndex>(File.ReadAllText(Index)):new();
 public string PathFor(CadToolEntry item){string full=Path.GetFullPath(Path.Combine(root,item.RelativePath));if(!full.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.IsPathRooted(item.RelativePath))throw new CadException("TOOL_PATH","Đường dẫn công cụ không nằm trong kho.");return full;}
 public string Demand(CadToolEntry item){if(!item.Enabled||item.DeletedUtc.HasValue)throw new CadException("TOOL_DISABLED","Công cụ đang tắt hoặc nằm trong thùng rác.");string file=PathFor(item);if(!File.Exists(file)||Json.FileHash(file)!=item.FileHash)throw new CadException("TOOL_CHANGED","File công cụ đã thay đổi. Nhập cập nhật rồi nạp lại.");string folder=Path.GetDirectoryName(file)!;if(PackageHash(Directory.GetFiles(folder))!=item.PackageHash)throw new CadException("TOOL_CHANGED","Gói công cụ hoặc dependency đã thay đổi. Nhập cập nhật rồi nạp lại.");return file;}
 static string PackageHash(IEnumerable<string> files)=>Json.Hash(files.OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Select(p=>new{name=Path.GetFileName(p),hash=Json.FileHash(p)}).ToList());
 public CadToolEntry Import(string source,int revision,string? replaceId=null){
  source=Path.GetFullPath(source);string ext=Path.GetExtension(source).ToLowerInvariant();string kind=ext==".dll"?"dotnet":new[]{".lsp",".fas",".vlx"}.Contains(ext)?"lisp":throw new CadException("TOOL_TYPE","Chọn LSP, FAS, VLX hoặc DLL .NET Framework 4.8.");
  if(!File.Exists(source))throw new CadException("TOOL_MISSING","Không tìm thấy file công cụ.");
  string stem=Path.GetFileNameWithoutExtension(source);if(kind=="dotnet"&&Reserved(stem))throw new CadException("TOOL_RESERVED","Không nhập DLL của AutoCAD hoặc của AI CAD làm công cụ.");
  var files=kind=="dotnet"?Directory.GetFiles(Path.GetDirectoryName(source)!).Where(p=>new[]{".dll",".pdb"}.Contains(Path.GetExtension(p).ToLowerInvariant())&&!Reserved(Path.GetFileNameWithoutExtension(p))).ToArray():new[]{source};
  if(files.Length>200||files.Sum(p=>new FileInfo(p).Length)>100L*1024*1024)throw new CadException("TOOL_SIZE","Gói công cụ tối đa 100 MB / 200 file. Đặt DLL và dependency trong thư mục riêng.");
  CadToolEntry? imported=null;Change(revision,index=>{
   var old=replaceId==null?null:index.Items.SingleOrDefault(t=>t.Id==replaceId)??throw new CadException("TOOL_MISSING","Công cụ không còn trong danh mục.");
   if(old!=null&&old.Kind!=kind)throw new CadException("TOOL_TYPE","Giữ đúng loại công cụ khi cập nhật.");
   string id=old?.Id??Guid.NewGuid().ToString("N"),package=PackageHash(files);string relative=Path.Combine(kind,id,package,Path.GetFileName(source)),destination=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
   foreach(string file in files){string target=Path.Combine(Path.GetDirectoryName(destination)!,Path.GetFileName(file));if(!File.Exists(target))File.Copy(file,target,false);if(Json.FileHash(file)!=Json.FileHash(target))throw new CadException("TOOL_CHANGED","File thay đổi trong lúc nhập.");}
   imported=new CadToolEntry{Id=id,Name=old?.Name??stem,Kind=kind,RelativePath=relative,FileHash=Json.FileHash(destination),PackageHash=package,Group=old?.Group??"",Notes=old?.Notes??"",Enabled=old?.Enabled??true,Commands=ext==".lsp"?LispCommands(File.ReadAllText(destination)):old?.Commands??new(),UpdatedUtc=DateTime.UtcNow};
   if(old!=null)index.Items.Remove(old);index.Items.Add(imported);
  });return imported!;
 }
 static bool Reserved(string name)=>new[]{"acmgd","acdbmgd","accoremgd","CAD.Bridge.AutoCAD","CAD.Contracts","CAD.Geometry"}.Contains(name,StringComparer.OrdinalIgnoreCase)||name.StartsWith("System.",StringComparison.OrdinalIgnoreCase);
 public void Update(string id,int revision,string name,string group,string notes,bool enabled,IEnumerable<string> commands,string action="save")=>Change(revision,index=>{
  var item=index.Items.SingleOrDefault(t=>t.Id==id)??throw new CadException("TOOL_MISSING","Công cụ không còn trong danh mục.");
  if(action=="delete")item.DeletedUtc=DateTime.UtcNow;else if(action=="restore")item.DeletedUtc=null;else{
   if(string.IsNullOrWhiteSpace(name)||name.Length>120||group.Length>120||notes.Length>4000)throw new CadException("TOOL_METADATA","Tên tối đa 120 ký tự; ghi chú tối đa 4.000 ký tự.");
   var values=commands.Where(c=>!string.IsNullOrWhiteSpace(c)).Select(c=>CommandName(c.Trim())).Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(values.Count>200)throw new CadException("TOOL_COMMAND","Tối đa 200 lệnh mỗi công cụ.");item.Name=name.Trim();item.Group=group.Trim();item.Notes=notes;item.Enabled=enabled;item.Commands=values;
  }item.UpdatedUtc=DateTime.UtcNow;
 });
 void Change(int revision,Action<CadToolIndex> action){Directory.CreateDirectory(root);using var mutex=new Mutex(false,"Local\\AICadTools_"+Json.Hash(root));bool held=false;try{try{held=mutex.WaitOne(2000);}catch(AbandonedMutexException){held=true;}if(!held)throw new CadException("TOOL_BUSY","Danh mục công cụ đang được cập nhật.");var index=Read();if(index.Revision!=revision)throw new CadException("STALE_EDIT","Danh mục đã đổi. Làm mới rồi thử lại.");action(index);index.Revision++;Json.Atomic(Index,index);}finally{if(held)mutex.ReleaseMutex();}}
 public static string CommandName(string value){if(!Regex.IsMatch(value,@"^[\p{L}\p{N}_$.-]{1,100}$"))throw new CadException("TOOL_COMMAND","Tên lệnh chỉ gồm chữ, số, _, $, ., -; không nhập biểu thức hoặc macro.");return value;}
 public static List<string> LispCommands(string source){
  // Preserve token boundaries while stripping strings, line comments and ;| ... |; comments.
  var clean=new StringBuilder();bool quote=false,line=false;int block=0;
  for(int i=0;i<source.Length;i++){char c=source[i],n=i+1<source.Length?source[i+1]:'\0';
   if(line){if(c=='\n'){line=false;clean.Append('\n');}else clean.Append(' ');continue;}
   if(block>0){if(c==';'&&n=='|'){block++;i++;}else if(c=='|'&&n==';'){block--;i++;}clean.Append(' ');continue;}
   if(quote){if(c=='\\'){i++;clean.Append("  ");}else{if(c=='"')quote=false;clean.Append(' ');}continue;}
   if(c=='"'){quote=true;clean.Append(' ');}else if(c==';'&&n=='|'){block=1;i++;clean.Append("  ");}else if(c==';'){line=true;clean.Append(' ');}else clean.Append(c);
  }
  return Regex.Matches(clean.ToString(),@"\(\s*defun\s+c:([^\s()]+)",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>m.Groups[1].Value).Where(v=>Regex.IsMatch(v,@"^[\p{L}\p{N}_$.-]{1,100}$")).Distinct(StringComparer.OrdinalIgnoreCase).Take(200).ToList();
 }
}
