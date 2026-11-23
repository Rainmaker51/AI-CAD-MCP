using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

#if QA_LIBRARY_CORE
using AICad.Contracts;
namespace AICad.Application;
#else
namespace AICad.Contracts;
#endif

public sealed class LibraryResourceCode
{
 public string SourceId {get;set;}="";
 public string Kind {get;set;}="";
 public string Id {get;set;}="";
 public string Code {get;set;}="";
 public string Name {get;set;}="";
}
public sealed class LibraryResourceCodeIndex
{
 public int SchemaVersion {get;set;}=1;
 public int Revision {get;set;}
 public List<LibraryResourceCode> Items {get;set;}=new();
}
// Human-readable aliases for existing CAD sync identities. Names and file hashes are never identity keys.
public sealed class LibraryResourceCodes(string appRoot)
{
 public static readonly string[] Kinds={"blocks","layers","dims","texts","hatches"};
 static string Prefix(string kind)=>kind switch{"blocks"=>"BLK","layers"=>"LAY","dims"=>"DIM","texts"=>"TXT","hatches"=>"HAT",_=>throw new CadException("RESOURCE_KIND","Loại thư viện không hợp lệ.")};
 string Root=>Path.Combine(WarehousePaths.Resolve(appRoot),"Catalog");
 public string FilePath=>Path.Combine(Root,"resource-codes.json");
 static string Key(LibraryResourceCode item)=>item.SourceId+"/"+item.Kind+"/"+item.Id;
 public LibraryResourceCodeIndex Read()
 {
  var index=File.Exists(FilePath)?Json.Read<LibraryResourceCodeIndex>(File.ReadAllText(FilePath)):new();
  if(index.SchemaVersion!=1||index.Items==null||index.Revision<0)throw new CadException("RESOURCE_CODES","Danh mục mã tài nguyên không hợp lệ.");
  foreach(var item in index.Items){Json.Name(item.SourceId);Prefix(item.Kind);if(!Guid.TryParseExact(item.Id,"N",out _)||!System.Text.RegularExpressions.Regex.IsMatch(item.Code,"^"+Prefix(item.Kind)+"-[0-9]{4,9}$"))throw new CadException("RESOURCE_CODES","Mã tài nguyên hoặc ID không hợp lệ.");}
  if(index.Items.Select(Key).Distinct().Count()!=index.Items.Count||index.Items.Select(i=>i.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=index.Items.Count)throw new CadException("RESOURCE_CODES","Danh mục bị trùng mã/ID; không tự cấp lại mã.");
  return index;
 }
 public LibraryResourceCodeIndex Ensure(IEnumerable<LibraryResourceCode> resources)
 {
  Directory.CreateDirectory(Root);using var mutex=new Mutex(false,"Local\\AICadResourceCodes_"+Json.Hash(Root));bool held=false;
  try{
   try{held=mutex.WaitOne(2000);}catch(AbandonedMutexException){held=true;}if(!held)throw new CadException("RESOURCE_CODES_BUSY","Đang cập nhật mã tài nguyên.");
   using var lease=new FileStream(Path.Combine(Root,"resource-codes.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
   var seeds=resources.ToList();foreach(var seed in seeds){Json.Name(seed.SourceId);Prefix(seed.Kind);if(!Guid.TryParseExact(seed.Id,"N",out _)||seed.Name==null||seed.Name.Length>500)throw new CadException("RESOURCE_ID","Chỉ cấp mã cho tài nguyên có ID đồng bộ hợp lệ.");}
   if(seeds.Select(Key).Distinct().Count()!=seeds.Count)throw new CadException("RESOURCE_ID","Danh sách có ID tài nguyên trùng nhau.");
   var index=Read();bool changed=false;
   foreach(var seed in seeds.OrderBy(s=>s.Kind,StringComparer.Ordinal).ThenBy(s=>s.Id,StringComparer.Ordinal)){
    var item=index.Items.SingleOrDefault(i=>Key(i)==Key(seed));
    if(item==null){int next=index.Items.Where(i=>i.Kind==seed.Kind).Select(i=>int.Parse(i.Code.Substring(4))).DefaultIfEmpty(0).Max()+1;if(next>999999999)throw new CadException("RESOURCE_CODES_LIMIT","Đã hết phạm vi mã tài nguyên.");
     item=new(){SourceId=seed.SourceId,Kind=seed.Kind,Id=seed.Id,Code=Prefix(seed.Kind)+"-"+next.ToString("D4"),Name=seed.Name};index.Items.Add(item);changed=true;
    }else if(item.Name!=seed.Name){item.Name=seed.Name;changed=true;}
   }
   // Removed identities remain reserved. A new object, even with the same name, gets a new code.
   if(changed){index.Revision++;Json.Atomic(FilePath,index);}return index;
  }finally{if(held)mutex.ReleaseMutex();}
 }
}
