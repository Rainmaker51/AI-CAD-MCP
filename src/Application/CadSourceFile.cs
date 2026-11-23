using System;
using System.IO;
using System.Security.Cryptography;
using AICad.Contracts;
namespace AICad.Application;
public static class CadSourceFile {
 public static object Describe(string appRoot,string hash){
  if(!System.Text.RegularExpressions.Regex.IsMatch(hash,"^[a-f0-9]{64}$"))throw new CadException("INVALID_SOURCE","Mã SHA256 nguồn không hợp lệ.");
  string originals=Path.Combine(WarehousePaths.Resolve(appRoot),"CAD","Originals"),asset=Path.Combine(WarehousePaths.Folder(appRoot,"content-assets"),hash+".dwg");
  if(Directory.Exists(originals))foreach(string file in Directory.EnumerateFiles(originals,"*.dwg")){
   try{if(Hash(file)==hash)return new{name=Path.GetFileName(file),path=file,assetPath=asset,sourceHash=hash,originalFound=true};}catch(CadException){}catch(UnauthorizedAccessException){}
  }
  return new{name=Path.GetFileName(asset),path=asset,assetPath=asset,sourceHash=hash,originalFound=false};
 }
 public const string BaseLibraryName="ISO_QuangAnh_ThuVien.dwg";
 public static string BaseLibraryPath(string appRoot){string path=Path.Combine(WarehousePaths.Resolve(appRoot),"CAD","Originals",BaseLibraryName);if(!File.Exists(path))throw new CadException("MISSING_ASSET","Không tìm thấy "+BaseLibraryName+" trong CAD/Originals của kho.");return path;}
 public static string Hash(string path){try{using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}catch(IOException){throw new CadException("SOURCE_BUSY","Không đọc được DWG nguồn; lưu xong bản vẽ rồi thử lại.");}}
 public static void Copy(string source,string destination){try{using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None);input.CopyTo(output);}catch(IOException){throw new CadException("SOURCE_BUSY","Không sao chép được DWG nguồn; lưu xong rồi thử lại.");}}
}
