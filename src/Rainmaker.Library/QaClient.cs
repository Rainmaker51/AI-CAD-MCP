using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AICad.Application;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Library;

// In-process adapter. QA does not use HTTP, Host, tokens or the .NET 10 runtime.
internal sealed class QaClient:IDisposable
{
    internal readonly InstallConfig Config;
    readonly LibraryRepository repository;
    readonly QaLibraryService library;
    bool disposed;
    readonly ResolveEventHandler geometryResolve;
    static InstallConfig Configuration()
    {
        var bridge=AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a=>a.GetName().Name=="CAD.Bridge.AutoCAD")??throw new InvalidOperationException("Mở QA qua CAD Tool Manager để nhận cấu hình kho trên máy này.");
        string file=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(bridge.Location)!,"..","install.json"));
        return JsonUtil.Read<InstallConfig>(File.ReadAllText(file));
    }
    internal QaClient():this(Configuration()){}
    internal QaClient(InstallConfig config)
    {
        Config=config;_ = WarehousePaths.Resolve(config.ContentRoot);
        // Geometry is a Manager-owned reserved dependency. Reuse that exact file;
        // Manager correctly excludes it from add-in packages.
        var bridge=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="CAD.Bridge.AutoCAD");
        string geometryFile=Path.Combine(Path.GetDirectoryName(bridge.Location)!,"CAD.Geometry.dll");
        if(!File.Exists(geometryFile))throw new FileNotFoundException("Bản cài Manager thiếu CAD.Geometry.dll. Cập nhật Manager trước khi mở QA.",geometryFile);
        var geometry=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="CAD.Geometry")??Assembly.LoadFrom(geometryFile);
        geometryResolve=(_,args)=>args.RequestingAssembly?.GetName().Name=="Rainmaker.Library.Core"&&args.Name==geometry.FullName?geometry:null;
        AppDomain.CurrentDomain.AssemblyResolve+=geometryResolve;
        repository=new LibraryRepository(config);
        library=new QaLibraryService(config,repository,new LibraryIdentifierService(config,repository));
    }
    internal async Task<JObject> Call(string path,JObject? body,CancellationToken ct)
    {
        if(disposed)throw new ObjectDisposedException(nameof(QaClient));ct.ThrowIfCancellationRequested();path=path.Trim('/');
        object result;
        if(path=="api/library-sync")result=repository.State();
        else
        {
            const string prefix="api/qa-library/";
            if(!path.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidOperationException("Thao tác thư viện không được hỗ trợ: "+path);
            string action=path.Substring(prefix.Length);var p=body??new JObject();
            if(action=="authority")result=library.Authority();
            else if(action=="activate")result=library.Activate();
            else if(action=="manage")result=library.Manage(p);
            else if(action=="group")result=library.Group(p);
            else if(action=="block-usage")result=repository.ConfigureQaBlock(p);
            else if(action=="live/register")result=repository.RegisterLive(p.Value<string>("id")??"");
            else if(action=="live/prepare")result=repository.PrepareLiveApproval(p);
            else if(action=="approve")result=await repository.ApproveCad(p,ct);
            else if(action.StartsWith("live/",StringComparison.Ordinal))
            {
                var parts=action.Split('/');if(parts.Length!=3)throw new CadException("QA_SNAPSHOT","Địa chỉ bản đọc không hợp lệ.");
                result=library.ReadLive(parts[2],repository.LiveCapture(parts[1]));
            }
            else if(LibrarySyncRules.Kinds.Contains(action))result=library.Read(action);
            else throw new InvalidOperationException("Thao tác thư viện không được hỗ trợ: "+action);
        }
        return result as JObject??JObject.FromObject(result);
    }
    internal Task<byte[]?> Image(string path,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(disposed)throw new ObjectDisposedException(nameof(QaClient));
        var parts=path.Trim('/').Split('/');string? file=null;
        if(parts.Length==4&&parts[0]=="api"&&parts[1]=="hatch-library"&&parts[3]=="image")
        {
            string id=Uri.UnescapeDataString(parts[2]);var catalog=repository.CatalogView("hatches");
            var item=catalog["items"]!.OfType<JObject>().SingleOrDefault(i=>i.Value<string>("id")==id);
            if(item?["imageFile"]!=null)
            {
                file=Path.Combine(WarehousePaths.Resolve(Config.ContentRoot),"Hatches","ISO_QUANGANH",ContentStore.VersionName(catalog.Value<string>("version")!),JsonUtil.Hash(id)+".png");
                if(File.Exists(file)&&JsonUtil.FileHash(file)!=item.Value<string>("imageHash"))throw new CadException("RESOURCE_CHANGED","Ảnh hatch đã thay đổi.");
            }
        }
        else if(parts.Length==5&&parts[0]=="api"&&parts[1]=="block-previews"&&parts[2]=="ISO_QUANGANH"&&parts[4]=="image")
        {
            string id=Uri.UnescapeDataString(parts[3]);var template=new ContentStore(Config.ContentRoot).Template("ISO_QUANGANH");
            if(template.Blocks.Any(b=>b.Id==id))file=Path.Combine(Config.DataRoot,"block-previews","png-v1-"+JsonUtil.Hash(template),JsonUtil.Hash(id)+".png");
        }
        return Task.FromResult(file!=null&&File.Exists(file)?File.ReadAllBytes(file):null);
    }
    public void Dispose(){if(disposed)return;disposed=true;AppDomain.CurrentDomain.AssemblyResolve-=geometryResolve;}
}
