using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace AICad.Contracts;
public static class WarehousePaths {
 public static string Resolve(string appRoot){string pointer=Path.Combine(appRoot,"warehouse.local.json");if(!File.Exists(pointer))return Path.GetFullPath(appRoot);string root=Path.GetFullPath(Json.Read<JObject>(File.ReadAllText(pointer)).Value<string>("path")??throw new CadException("WAREHOUSE_UNAVAILABLE","Chưa có đường dẫn kho."));if(!File.Exists(Path.Combine(root,"warehouse.json")))throw new CadException("WAREHOUSE_UNAVAILABLE","Kho không sẵn sàng. Kiểm tra đường dẫn và đồng bộ Drive.");return root;}
 public static string Folder(string appRoot,string kind)=>InRoot(Resolve(appRoot),kind);
 public static string InRoot(string root,string kind){bool shared=File.Exists(Path.Combine(root,"warehouse.json"));string relative=kind switch{"packages"=>shared?"Versions":"packages","skills"=>shared?"Skills":"skills","template-library"=>shared?"Templates":"template-library","content-assets"=>shared?"CAD/Assets":"content-assets",_=>throw new CadException("INVALID_KIND",kind)};return Path.Combine(root,relative);}
 public static bool IsRetired(string appRoot,string kind,string id){string file=Path.Combine(Resolve(appRoot),"Catalog","retired.json");return File.Exists(file)&&(Json.Read<JObject>(File.ReadAllText(file))[kind] as JArray)?.Values<string>().Contains(id)==true;}
 public static void Validate(string root){if(!Path.IsPathRooted(root)||root.StartsWith("http",StringComparison.OrdinalIgnoreCase))throw new CadException("WAREHOUSE_PATH","Chọn đường dẫn thư mục đã đồng bộ trên máy, không dùng link web.");string marker=Path.Combine(root,"warehouse.json");if(!File.Exists(marker)||Json.Read<JObject>(File.ReadAllText(marker)).Value<int>("schemaVersion")!=1)throw new CadException("WAREHOUSE_INVALID","Thư mục chưa có cấu trúc Kho AI CAD hợp lệ.");foreach(string kind in new[]{"packages","skills","template-library","content-assets"})if(!Directory.Exists(InRoot(root,kind)))throw new CadException("WAREHOUSE_INVALID","Kho thiếu thư mục: "+kind);}
 public static void Select(string appRoot,string root){Validate(root);Json.Atomic(Path.Combine(appRoot,"warehouse.local.json"),new{path=Path.GetFullPath(root)});}
}
