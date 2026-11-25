using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Rainmaker.Library.Reader;

public static class LibraryReader
{
    public static ILibraryReaderV1 Open(string applicationRoot,string sourceId="ISO_QUANGANH")=>new JsonWarehouseReader(applicationRoot,sourceId);
    public static string ResolveAssetFile(string applicationRoot,string sha256)=>JsonWarehouseReader.ResolveAssetFile(applicationRoot,sha256);
}
internal sealed class JsonWarehouseReader:ILibraryReaderV1
{
    readonly string appRoot,source;
    internal JsonWarehouseReader(string appRoot,string source){this.appRoot=Path.GetFullPath(appRoot);this.source=SafeName(source);}
    public int ProtocolVersion=>1;
    public string GetCatalogRevision()=>Read().Revision;
    static string SafeName(string value)=>Regex.IsMatch(value??"","^[a-zA-Z0-9_-]{1,100}$")?value!:throw new LibraryReadException("INVALID_ID","Tên nguồn/ID không hợp lệ.");
    static string Hash(byte[] data){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
    static byte[] Bytes(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var memory=new MemoryStream();stream.CopyTo(memory);return memory.ToArray();}
    internal static string ResolveAssetFile(string applicationRoot,string sha256)
    {
        if(!Regex.IsMatch(sha256??"","^[a-f0-9]{64}$"))throw new LibraryReadException("ASSET_HASH","Hash DWG không hợp lệ.");
        string root=Path.GetFullPath(applicationRoot),pointer=Path.Combine(root,"warehouse.local.json");if(File.Exists(pointer))root=Path.GetFullPath(Parse(Bytes(pointer)).Value<string>("path")??throw new LibraryReadException("WAREHOUSE_UNAVAILABLE","Chưa chọn kho."));
        string descriptor=Within(root,"Catalog/library-reader.json");var layout=File.Exists(descriptor)?Parse(Bytes(descriptor)):new JObject();
        if(layout.HasValues&&(layout.Value<int>("schemaVersion")!=1||layout.Value<string>("format")!="json-v1"))throw new LibraryReadException("STORAGE_UNSUPPORTED","Cập nhật bộ đọc kho.");
        string assets=layout["paths"]?.Value<string>("assets")??(File.Exists(Path.Combine(root,"warehouse.json"))?"CAD/Assets":"content-assets");string file=Within(root,assets+"/"+sha256+".dwg");
        if(!File.Exists(file)||Hash(Bytes(file))!=sha256)throw new LibraryReadException("ASSET_CHANGED","DWG đã xuất thiếu hoặc thay đổi.");return file;
    }
    static JObject Parse(byte[] bytes){if(bytes.Length>16*1024*1024)throw new LibraryReadException("CATALOG_TOO_LARGE","Danh mục vượt giới hạn 16 MB.");using var text=new StringReader(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));using var reader=new JsonTextReader(text){MaxDepth=64,DateParseHandling=DateParseHandling.None};return JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});}
    static string Within(string root,string relative)
    {
        if(Path.IsPathRooted(relative))throw new LibraryReadException("CATALOG_PATH","Đường dẫn kho phải tương đối.");string full=Path.GetFullPath(Path.Combine(root,relative));
        if(!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new LibraryReadException("CATALOG_PATH","Đường dẫn nằm ngoài kho.");
        for(var dir=new DirectoryInfo(Path.GetDirectoryName(full)!);dir!=null&&dir.FullName.StartsWith(root,StringComparison.OrdinalIgnoreCase);dir=dir.Parent)if(dir.Exists&&(dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new LibraryReadException("CATALOG_PATH","Không đọc tài nguyên qua liên kết thư mục.");
        return full;
    }
    static bool Equal(JToken? a,JToken? b)
    {
        if(a==null||a.Type==JTokenType.Null)return b==null||b.Type==JTokenType.Null;
        if(b==null||b.Type==JTokenType.Null)return false;
        if((a.Type==JTokenType.Float||a.Type==JTokenType.Integer)&&(b.Type==JTokenType.Float||b.Type==JTokenType.Integer))return a.Value<double>()==b.Value<double>();
        return JToken.DeepEquals(a,b);
    }
    static JObject Fields(JObject value,params string[] names)=>new JObject(names.Where(n=>value[n]!=null).Select(n=>new JProperty(n,value[n]!.DeepClone())));
    public LibrarySnapshotV1 Read()
    {
        try{return ReadChecked();}
        catch(LibraryReadException){throw;}
        catch(Exception error)when(error is IOException||error is UnauthorizedAccessException||error is JsonException||error is InvalidOperationException||error is FormatException)
        {throw new LibraryReadException("CATALOG_INVALID","Không đọc được phiên bản kho hợp lệ: "+error.Message);}
    }
    LibrarySnapshotV1 ReadChecked()
    {
        var tracked=new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase);
        byte[]? ReadBytes(string file,bool optional=false){if(!File.Exists(file)){if(tracked.TryGetValue(file,out var existed)&&existed!=null)throw new LibraryReadException("CATALOG_CHANGED","Kho đang thay đổi.");tracked[file]=null;if(optional)return null;throw new LibraryReadException("CATALOG_MISSING","Thiếu dữ liệu kho: "+file);}var data=Bytes(file);string hash=Hash(data);if(tracked.TryGetValue(file,out var old)&&old!=hash)throw new LibraryReadException("CATALOG_CHANGED","Kho đang thay đổi.");tracked[file]=hash;return data;}
        JObject Object(string file,bool optional=false){var data=ReadBytes(file,optional);return data==null?new JObject():Parse(data);}
        string root=appRoot;var pointer=Object(Path.Combine(appRoot,"warehouse.local.json"),true);
        if(pointer["path"]!=null){root=Path.GetFullPath(pointer.Value<string>("path")!);if(!File.Exists(Path.Combine(root,"warehouse.json")))throw new LibraryReadException("WAREHOUSE_UNAVAILABLE","Kho không sẵn sàng. Kiểm tra đường dẫn và đồng bộ.");}
        bool shared=File.Exists(Path.Combine(root,"warehouse.json"));
        void Pending(){foreach(string file in new[]{"Catalog/sync/pending.json","Catalog/qa-transactions/ISO_QUANGANH.pending.json"})if(File.Exists(Within(root,file)))throw new LibraryReadException("LIBRARY_PENDING","QA đang có cập nhật chưa hoàn tất. Xử lý cập nhật rồi nạp lại thư viện.");}
        Pending();
        var layout=Object(Within(root,"Catalog/library-reader.json"),true);
        if(layout.HasValues&&(layout.Value<int>("schemaVersion")!=1||layout.Value<string>("format")!="json-v1"))throw new LibraryReadException("STORAGE_UNSUPPORTED","Cập nhật DLL đọc kho để hỗ trợ cấu trúc lưu trữ này.");
        string PathFor(string key,string fallback)=>Within(root,layout["paths"]?.Value<string>(key)??fallback);
        string stateFile=PathFor("state","Catalog/sync/state.json"),codesFile=PathFor("codes","Catalog/resource-codes.json"),management=PathFor("management","Catalog/management"),assets=PathFor("assets",shared?"CAD/Assets":"content-assets");
        var state=Object(stateFile);var codes=Object(codesFile);if(codes.Value<int>("SchemaVersion")!=1)throw new LibraryReadException("STORAGE_UNSUPPORTED","Phiên bản sổ mã không được hỗ trợ.");
        var items=(state["Items"] as JArray??throw new LibraryReadException("CATALOG_INVALID","Thiếu danh mục tài nguyên.")).OfType<JObject>().ToList();
        if(items.GroupBy(i=>i.Value<string>("Id")).Any(g=>g.Count()>1))throw new LibraryReadException("DUPLICATE_ID","Trùng ID trong trạng thái kho.");
        JObject Versioned(string folder,string filename,bool template=false)
        {
            var active=Object(Path.Combine(folder,"active.json"));string version=active.Value<string>("version")??"";
            if(!Version.TryParse(version,out var parsed)||parsed.ToString()!=version)throw new LibraryReadException("CATALOG_VERSION","Phiên bản không hợp lệ.");
            string file=Within(folder,version+"/"+filename);var data=ReadBytes(file)!;string expected=template?null!:active.Value<string>("checksum")!;
            if(template){var digest=ReadBytes(Path.Combine(Path.GetDirectoryName(file)!,"file-sha256.txt"))!;expected=Encoding.UTF8.GetString(digest).Trim();}
            if(!Regex.IsMatch(expected??"","^[a-f0-9]{64}$")||Hash(data)!=expected)throw new LibraryReadException("INTEGRITY_MISMATCH","Hash danh mục không khớp: "+file);
            return Parse(data);
        }
        var template=Versioned(Path.Combine(PathFor("templates",shared?"Versions/templates":"packages/templates"),source),"content.json",true);
        var styles=Versioned(Path.Combine(PathFor("styles","Styles"),source),"catalog.json");
        var hatches=Versioned(Path.Combine(PathFor("hatches","Hatches"),source),"catalog.json");
        var retired=Object(Within(root,"Catalog/retired.json"),true);
        if((retired["templates"] as JArray)?.Values<string>().Contains(source)==true)throw new LibraryReadException("SOURCE_RETIRED","Nguồn thư viện đã bị loại khỏi sử dụng.");
        JObject Label(string kind,string id)=>Object(Path.Combine(management,kind,SafeName(id)+".json"),true);
        if(Label("templates",source)["DeletedUtc"]?.Type is JTokenType.Date or JTokenType.String)throw new LibraryReadException("SOURCE_RETIRED","Template đã ở thùng rác.");
        var activeBlocks=new List<JObject>();var activeLayers=new List<JObject>();var resources=new List<ResourceV1>();
        var kinds=new[]{"blocks","layers","hatches","texts","dims"};
        foreach(var entry in (codes["Items"] as JArray??new()).OfType<JObject>().Where(e=>e.Value<string>("SourceId")==source&&kinds.Contains(e.Value<string>("Kind")!)))
        {
            string kind=entry.Value<string>("Kind")!,id=SafeName(entry.Value<string>("Id")!),code=entry.Value<string>("Code")??"";
            var resource=new ResourceV1{Key=new(source,kind,id),Code=code,Name=entry.Value<string>("Name")??""};resources.Add(resource);
            var item=items.SingleOrDefault(i=>i.Value<string>("Id")==id&&i.Value<string>("Kind")==kind);if(item==null)continue;
            resource.Name=item.Value<string>("Name")??"";resource.DisplayName=resource.Name;if(item.Value<bool?>("Draft")==true){resource.Availability="DRAFT";continue;}
            var matches=new List<JObject>();
            if(kind=="blocks")matches=(template["Blocks"] as JArray??new()).OfType<JObject>().Where(b=>b["NativeSource"]?.Value<string>("BlockName")==resource.Name).ToList();
            else if(kind=="layers")matches=(template["Layers"] as JArray??new()).OfType<JObject>().Where(l=>l.Value<string>("Name")==resource.Name).ToList();
            else matches=((kind=="hatches"?hatches["items"]:styles[kind]) as JArray??new()).OfType<JObject>().Where(v=>kind=="hatches"?(v.Value<string>("id")??"").StartsWith(id,StringComparison.Ordinal)||v.Value<string>("id")==entry.Value<string>("ResourceId")||v.Value<string>("handle")==item.Value<string>("Handle"):v.Value<string>("name")==resource.Name).ToList();
            if(matches.Count==0){resource.Availability="UNPUBLISHED";continue;}
            var published=new List<Tuple<JObject,JObject>>();
            foreach(var profile in matches)
            {
                string resourceId=kind=="blocks"?profile.Value<string>("Id")!:kind=="layers"?resource.Name:profile.Value<string>("id")!;
                string labelId=kind=="blocks"||kind=="layers"?Hash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new[]{source,resourceId},Formatting.None))):resourceId;
                var label=Label(kind,labelId);if(label["DeletedUtc"]!=null&&label["DeletedUtc"]!.Type!=JTokenType.Null)continue;
                published.Add(Tuple.Create(profile,label));
            }
            if(published.Count==0){resource.Availability="TRASH";continue;}
            var profileItem=published.OrderByDescending(p=>kind=="blocks"&&p.Item1.Value<string>("Id")==resource.Name).First();var profileData=profileItem.Item1;var metadata=profileItem.Item2;
            var values=kind=="layers"?new JObject{["color"]=profileData["Color"]?.DeepClone(),["rgb"]=profileData["Appearance"]?["Rgb"]?.DeepClone(),["lineWeight"]=profileData["Appearance"]?["LineWeight"]?.DeepClone(),["lineType"]=profileData["Appearance"]?["LineType"]?.DeepClone(),["plottable"]=profileData["Appearance"]?["Plottable"]?.DeepClone()}:kind=="blocks"?(JObject)(item["Values"]?.DeepClone()??new JObject()):(JObject)profileData.DeepClone();
            // Older style exports omit annotative; the accepted sync state carries it.
            // The native renderer still clones the actual style from the published DWG.
            if(kind=="dims"&&values["annotative"]==null&&item["Values"]?["annotative"]!=null)values["annotative"]=item["Values"]!["annotative"]!.DeepClone();
            string[] parameterKeys=kind=="hatches"?new[]{"pattern","type","scale","angle","layer","color","rgb","spacing","doublePattern"}:kind=="dims"?new[]{"textStyle","textHeight","arrowSize","scale","linearFactor","precision","extensionOffset","extensionBeyond","baselineSpacing","textGap","rounding","unitFormat","angularPrecision","zeroSuppression","annotative"}:kind=="texts"?new[]{"font","bigFont","height","widthFactor","obliqueAngle","vertical"}:kind=="layers"?new[]{"color","rgb","lineWeight","lineType","plottable"}:Array.Empty<string>();
            if(parameterKeys.Any(k=>item["Values"]?[k]!=null&&!Equal(item["Values"]![k],values[k]))){resource.Availability="UNPUBLISHED";continue;}
            resource.Availability="AVAILABLE";resource.ProfileJsonV1=profileData.ToString(Formatting.None);resource.ParametersJsonV1=(kind=="blocks"?values:Fields(values,parameterKeys)).ToString(Formatting.None);
            resource.DisplayName=metadata.Value<string>("Name") is string alias&&alias!=""?alias:resource.Name;resource.Purpose=metadata.Value<string>("Purpose")??"";resource.Notes=metadata.Value<string>("Notes")??"";resource.Reviewed=metadata.Value<bool?>("Reviewed")??false;
            var groupBytes=ReadBytes(Path.Combine(management,kind+"-groups.json"),true);var groups=groupBytes==null?new JArray():JArray.Parse(Encoding.UTF8.GetString(groupBytes).TrimStart('\uFEFF'));var seen=new HashSet<string>();var names=new List<string>();string group=metadata.Value<string>("GroupId")??"";
            while(group!=""){if(!seen.Add(group)||seen.Count>15)throw new LibraryReadException("GROUP_CYCLE","Cây nhóm không hợp lệ.");var g=groups.OfType<JObject>().SingleOrDefault(n=>n.Value<string>("Id")==group)??throw new LibraryReadException("GROUP_MISSING","Nhóm không tồn tại.");names.Insert(0,g.Value<string>("Name")!);group=g.Value<string>("ParentId")??"";}resource.GroupPath=string.Join(" / ",names);
            resource.Version=(kind=="blocks"||kind=="layers"?template:kind=="hatches"?hatches:styles).Value<string>(kind=="blocks"||kind=="layers"?"Version":"version")??"";
            resource.SourceHash=kind=="blocks"?profileData["NativeSource"]?.Value<string>("FileHash")??"":kind=="layers"?profileData["Appearance"]?.Value<string>("SourceHash")??"":(kind=="hatches"?hatches:styles).Value<string>("sourceHash")??"";
            if(resource.SourceHash!=""){if(!Regex.IsMatch(resource.SourceHash,"^[a-f0-9]{64}$"))throw new LibraryReadException("ASSET_HASH","Hash DWG không hợp lệ.");resource.SourceFile=Path.Combine(assets,resource.SourceHash+".dwg");}
            if(kind=="blocks")activeBlocks.AddRange(published.Select(p=>(JObject)p.Item1.DeepClone()));if(kind=="layers")activeLayers.Add((JObject)profileData.DeepClone());
        }
        if(resources.GroupBy(r=>r.Key.Kind+"|"+r.Key.Id).Any(g=>g.Count()>1)||resources.Where(r=>r.Code!="").GroupBy(r=>r.Code,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new LibraryReadException("DUPLICATE_ID","Trùng ID hoặc mã trong sổ mã.");
        void Verify(){Pending();foreach(var file in tracked)if(File.Exists(file.Key)?file.Value==null||Hash(Bytes(file.Key))!=file.Value:file.Value!=null)throw new LibraryReadException("CATALOG_CHANGED","Kho đã thay đổi. Nạp lại thư viện trước khi triển khai.");}
        Verify();
        var render=Fields(template,"Id","Name","Version","Units","Font","TextHeight","ArrowSize","Presets");render["Blocks"]=new JArray(activeBlocks);render["Layers"]=new JArray(activeLayers);
        return new LibrarySnapshotV1(resources,Verify,r=>{if((r.Key.Kind=="blocks"||r.Key.Kind=="dims")&&r.SourceHash=="")throw new LibraryReadException("ASSET_MISSING","Chưa có DWG đã xuất: "+r.Code);if(r.SourceFile!=""&&(!File.Exists(r.SourceFile)||Hash(Bytes(r.SourceFile))!=r.SourceHash))throw new LibraryReadException("ASSET_CHANGED","DWG đã xuất thiếu hoặc thay đổi: "+r.Code);}){SourceId=source,TemplateJsonV1=render.ToString(Formatting.None),Revision=Hash(Encoding.UTF8.GetBytes(string.Join("\n",tracked.OrderBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).Select(p=>p.Key+"|"+p.Value))))};
    }
}
