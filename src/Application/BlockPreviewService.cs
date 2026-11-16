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
public sealed class BlockPreviewService(InstallConfig config,WorkspaceService workspace){
 readonly SemaphoreSlim gate=new(1,1);
 string DirectoryFor(TemplateProfile t)=>Path.Combine(config.DataRoot,"block-previews","png-v1-"+Json.Hash(t));
 string Cache(TemplateProfile t)=>Path.Combine(DirectoryFor(t),"manifest.json");
 public string Image(string id,string blockId){var t=workspace.Template(id);if(!t.Blocks.Any(b=>b.Id==blockId))throw new CadException("MISSING_ASSET","Block không thuộc phiên bản thư viện hiện tại.");string file=Path.Combine(DirectoryFor(t),Json.Hash(blockId)+".png");if(!File.Exists(file))throw new FileNotFoundException();return file;}
 public JObject Read(string id){var t=workspace.Template(id);return File.Exists(Cache(t))?Json.Read<JObject>(File.ReadAllText(Cache(t))):new JObject();}
 public async Task<JObject> Generate(string id,CancellationToken ct){
  if(!await gate.WaitAsync(0,ct))throw new CadException("PREVIEW_BUSY","Đang tạo ảnh block; chờ hoàn tất rồi thử lại.");
  try{
   var template=workspace.Template(id);var result=new JObject();
   foreach(var group in template.Blocks.Where(b=>b.NativeSource!=null).GroupBy(b=>b.NativeSource!.FileHash)){
    string source=Path.Combine(WarehousePaths.Folder(config.ContentRoot,"content-assets"),group.Key+".dwg");
    if(!File.Exists(source)||Json.FileHash(source)!=group.Key)throw new CadException("SOURCE_CHANGED","DWG nguồn thiếu hoặc đã thay đổi.");
    string job=Path.Combine(config.DataRoot,"block-preview-jobs",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);
    File.Copy(source,Path.Combine(job,"source.dwg"));File.WriteAllText(Path.Combine(job,".aicad-library-job"),"AI CAD preview job");Json.Atomic(Path.Combine(job,"blocks.json"),group.ToList());
    string scanner=Path.Combine(Path.GetDirectoryName(config.HostExecutable)!,"LibraryScanner","CAD.LibraryScanner.dll"),script=Path.Combine(job,"preview.scr");
    File.WriteAllText(script,"_NETLOAD\n\""+scanner.Replace('\\','/')+"\"\nAICADBLOCKPREVIEWS\n_QUIT\n_Y\n",Encoding.ASCII);
    var start=new ProcessStartInfo(@"C:\Program Files\Autodesk\AutoCAD 2021\accoreconsole.exe"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(string a in new[]{"/i",Path.Combine(config.ContentRoot,"content","launch","blank-mm.dwg"),"/s",script,"/isolate","AICadPreview",Path.Combine(job,"profile")})start.ArgumentList.Add(a);
    start.Environment["AICAD_LIBRARY_JOB"]=job;
    using var process=Process.Start(start)??throw new CadException("PREVIEW_FAILED","Không khởi động được bộ đọc CAD.");
    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
    using var limit=CancellationTokenSource.CreateLinkedTokenSource(ct);limit.CancelAfter(TimeSpan.FromMinutes(3));
    try{await process.WaitForExitAsync(limit.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);ct.ThrowIfCancellationRequested();throw new CadException("PREVIEW_TIMEOUT","Đọc ảnh block quá thời gian. Có thể thử lại.");}
    await Task.WhenAll(stdout,stderr);
    string output=Path.Combine(job,"previews.json");if(!File.Exists(output))throw new CadException("PREVIEW_FAILED","Chưa xuất được ảnh block từ CAD.");
    var previews=Json.Read<JObject>(File.ReadAllText(output));if(previews.Value<string>("error") is string error)throw new CadException("PREVIEW_FAILED",error);
    Directory.CreateDirectory(DirectoryFor(template));
    foreach(var block in group){var preview=previews[block.Id] as JObject;if(preview==null)continue;
     if(preview.Value<string>("imageFile") is string filename){string expected=Json.Hash(block.Id)+".png";if(filename!=expected)throw new CadException("PREVIEW_FAILED","Tên ảnh block không hợp lệ.");File.Copy(Path.Combine(job,expected),Path.Combine(DirectoryFor(template),expected),true);}
     result[block.Id]=preview.DeepClone();}
   }
   if(Json.Hash(workspace.Template(id))!=Json.Hash(template))throw new CadException("RESOURCE_CHANGED","Thư viện đổi trong lúc tạo ảnh; tải lại để xem phiên bản mới.");
   Json.Atomic(Cache(template),result);return result;
  }finally{gate.Release();}
 }
}
