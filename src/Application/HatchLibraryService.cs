using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
namespace AICad.Application;
public sealed class HatchLibraryService(InstallConfig config){
 readonly SemaphoreSlim gate=new(1,1);
 string Root=>Path.Combine(WarehousePaths.Resolve(config.ContentRoot),"Hatches","ISO_QUANGANH");
 public JObject Read(){var catalog=Raw();var management=new LibraryCatalog(config.ContentRoot);catalog["groups"]=JArray.FromObject(management.Groups("hatches"));foreach(var item in catalog["items"]!.OfType<JObject>()){var m=management.Label("hatches",item.Value<string>("id")!);item["management"]=JObject.FromObject(m);}return catalog;}
 public LibraryLabel Update(JObject p){string id=p.Value<string>("id")??"";if(!Raw()["items"]!.OfType<JObject>().Any(i=>i.Value<string>("id")==id))throw new CadException("NOT_FOUND","Mẫu hatch không thuộc thư viện hiện tại.");return new LibraryCatalog(config.ContentRoot).UpdateHatch(id,p.Value<int?>("revision")??-1,p.Value<string>("name")??"",p.Value<string>("purpose")??"",p.Value<string>("notes")??"",p.Value<string>("groupId")??"",p.Value<bool?>("reviewed")??false,p.Value<string>("action")??"update");}
 public JArray SkillContext(){return new JArray(Read()["items"]!.OfType<JObject>().Where(i=>i["management"]!.Value<bool>("Reviewed")&&i["management"]!["DeletedUtc"]?.Type==JTokenType.Null).Select(i=>new JObject{["id"]=i["id"]!.DeepClone(),["name"]=i["management"]!["Name"]!.DeepClone(),["purpose"]=i["management"]!["Purpose"]!.DeepClone(),["notes"]=i["management"]!["Notes"]!.DeepClone(),["pattern"]=i["pattern"]?.DeepClone(),["scale"]=i["scale"]?.DeepClone(),["angle"]=i["angle"]?.DeepClone(),["layer"]=i["layer"]?.DeepClone(),["engineSupported"]=i["engineSupported"]?.DeepClone()}));}
 JObject Raw(){string active=Path.Combine(Root,"active.json");if(!File.Exists(active))return new JObject{["items"]=new JArray(),["message"]="Chưa nhập hatch từ DWG nguồn."};var pointer=Json.Read<JObject>(File.ReadAllText(active));string version=ContentStore.VersionName(pointer.Value<string>("version")!);string file=Path.Combine(Root,version,"catalog.json");if(Json.FileHash(file)!=pointer.Value<string>("checksum"))throw new CadException("RESOURCE_CHANGED","Danh mục hatch đã thay đổi ngoài ứng dụng.");return Json.Read<JObject>(File.ReadAllText(file));}
 public string Image(string id){var catalog=Read();var entry=catalog["items"]!.OfType<JObject>().SingleOrDefault(x=>x.Value<string>("id")==id)??throw new CadException("NOT_FOUND","Mẫu hatch không còn trong thư viện.");string version=catalog.Value<string>("version")!;string file=Path.Combine(Root,ContentStore.VersionName(version),Json.Hash(id)+".png");if(entry["imageFile"]==null||!File.Exists(file))throw new FileNotFoundException();if(Json.FileHash(file)!=entry.Value<string>("imageHash"))throw new CadException("RESOURCE_CHANGED","Ảnh mẫu hatch đã thay đổi.");return file;}
 public async Task<JObject> Import(CancellationToken ct){
  if(!await gate.WaitAsync(0,ct))throw new CadException("LIBRARY_BUSY","Đang đọc hatch, chờ hoàn tất rồi thử lại.");
  try{
   string source=CadSourceFile.BaseLibraryPath(config.ContentRoot);
   string hash=CadSourceFile.Hash(source),job=Path.Combine(config.DataRoot,"hatch-jobs",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);CadSourceFile.Copy(source,Path.Combine(job,"source.dwg"));if(Json.FileHash(Path.Combine(job,"source.dwg"))!=hash)throw new CadException("SOURCE_CHANGED","DWG đổi trong lúc sao chép; thử lại sau khi lưu xong.");
   File.WriteAllText(Path.Combine(job,".aicad-library-job"),"AI CAD hatch job");
   string scanner=Path.Combine(Path.GetDirectoryName(config.HostExecutable)!,"LibraryScanner","CAD.LibraryScanner.dll"),script=Path.Combine(job,"scan.scr");
   File.WriteAllText(script,"_NETLOAD\n\""+scanner.Replace('\\','/')+"\"\nAICADHATCHSCAN\n_QUIT\n_Y\n",Encoding.ASCII);
   var start=new ProcessStartInfo(@"C:\Program Files\Autodesk\AutoCAD 2021\accoreconsole.exe"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
   foreach(string a in new[]{"/i",Path.Combine(config.ContentRoot,"content","launch","blank-mm.dwg"),"/s",script,"/isolate","AICadHatches",Path.Combine(job,"profile")})start.ArgumentList.Add(a);
   start.Environment["AICAD_LIBRARY_JOB"]=job;using var process=Process.Start(start)??throw new CadException("SCANNER_MISSING","Không khởi chạy được bộ đọc hatch.");var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
   using var limit=CancellationTokenSource.CreateLinkedTokenSource(ct);limit.CancelAfter(TimeSpan.FromMinutes(3));try{await process.WaitForExitAsync(limit.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);ct.ThrowIfCancellationRequested();throw new CadException("HATCH_TIMEOUT","Đọc hatch quá thời gian; thử lại khi CAD sẵn sàng.");}await Task.WhenAll(stdout,stderr);
   string output=Path.Combine(job,"hatches.json");if(!File.Exists(output))throw new CadException("SCAN_FAILED","AutoCAD chưa trả kết quả quét hatch.");var catalog=Json.Read<JObject>(File.ReadAllText(output));if(catalog.Value<string>("error") is string error)throw new CadException("SCAN_FAILED",error);
   if(catalog.Value<string>("sourceHash")!=hash||CadSourceFile.Hash(source)!=hash)throw new CadException("SOURCE_CHANGED","DWG nguồn đã đổi trong lúc đọc hatch.");
   if(catalog["items"] is not JArray items||items.Count==0)throw new CadException("NO_HATCH","Không tìm thấy hatch trong DWG; thư viện cũ được giữ nguyên.");
   string assets=WarehousePaths.Folder(config.ContentRoot,"content-assets");Directory.CreateDirectory(assets);string asset=Path.Combine(assets,hash+".dwg");if(!File.Exists(asset))File.Copy(Path.Combine(job,"source.dwg"),asset);if(Json.FileHash(asset)!=hash)throw new CadException("SOURCE_CHANGED","DWG trong Assets không khớp nguồn hatch.");
   using var mutex=new Mutex(false,"Local\\AICadHatches_"+Json.Hash(Root));bool held=false;
   try{try{held=mutex.WaitOne(2000);}catch(AbandonedMutexException){held=true;}if(!held)throw new CadException("LIBRARY_BUSY","Máy khác đang xuất bản hatch.");
    string previous=Read().Value<string>("version")??"1.0.0";var v=new Version(previous);string version=File.Exists(Path.Combine(Root,"active.json"))?$"{v.Major}.{v.Minor}.{v.Build+1}":"1.0.0";
    string stage=Path.Combine(Root,"staging-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
    foreach(var item in items.OfType<JObject>())if(item.Value<string>("imageFile") is string image){string id=item.Value<string>("id")!;if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-f0-9]{64}$")||image!=id+".png")throw new CadException("INVALID_ID","Mã mẫu hatch không hợp lệ.");string target=Path.Combine(stage,Json.Hash(id)+".png");File.Copy(Path.Combine(job,image),target);item["imageHash"]=Json.FileHash(target);}
    catalog["version"]=version;catalog["name"]="Hatch Quang Anh";catalog["sourceName"]=Path.GetFileName(source);catalog["importedUtc"]=DateTime.UtcNow;
    Json.Atomic(Path.Combine(stage,"catalog.json"),catalog);string checksum=Json.FileHash(Path.Combine(stage,"catalog.json"));Directory.Move(stage,Path.Combine(Root,version));Json.Atomic(Path.Combine(Root,"active.json"),new{version,checksum});
   }finally{if(held)mutex.ReleaseMutex();}
   return Read();
  }finally{gate.Release();}
 }
}
