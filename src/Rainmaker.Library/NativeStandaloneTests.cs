using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AICad.Application;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Library;
public sealed partial class NativeTests
{
    static void VerifyStandalone(string evidence)
    {
        string root=Environment.GetEnvironmentVariable("QALIB_STANDALONE_FIXTURE")??"";
        if(!Path.IsPathRooted(root)||!Path.GetFileName(root).StartsWith("qa-library-tests-",StringComparison.Ordinal))throw new InvalidOperationException("Owned standalone fixture required");
        var config=new InstallConfig{ContentRoot=root,DataRoot=Path.Combine(root,"native-data"),HostExecutable=Path.Combine(root,"intentionally-missing-host.exe")};
        Check(!File.Exists(config.HostExecutable),"The standalone test must have no Host executable");
        var ct=CancellationToken.None;
        using var client=new QaClient(config);
        JObject Call(string action,JObject? body=null)=>client.Call("api/qa-library/"+action,body,ct).GetAwaiter().GetResult();
        string source="";
        foreach(string kind in LibrarySyncRules.Kinds)
        {
            var view=Call(kind);Check(view["items"]!.Count()==1,"Offline catalog lost "+kind);source=view.Value<string>("source")!;
            var row=(JObject)view["items"]![0]!;
            var group=Call("group",new(){["kind"]=kind,["name"]="Native standalone",["parentId"]=""});
            var manage=new JObject{["kind"]=kind,["metadataId"]=row["metadataId"]!.DeepClone(),["catalogRevision"]=view["revision"]!.DeepClone(),["revision"]=row["management"]!["Revision"]!.DeepClone(),["name"]="Native "+kind,["purpose"]="No service required",["notes"]="Preserved native fixture",["groupId"]=group["Id"]!.DeepClone(),["reviewed"]=true,["action"]="update"};
            Call("manage",manage);
            var updated=Call(kind);Check(updated["items"]![0]!["management"]!.Value<string>("Purpose")=="No service required","Offline metadata write lost "+kind);
        }
        var blocks=Call("blocks");Check(QaBrowserWindow.UseReviewed((JObject)blocks["items"]![0]!),"Offline read lost legacy block review");
        Call("block-usage",new(){["resourceId"]="QA_BLOCK",["version"]=blocks["version"]!.DeepClone(),["usage"]=JObject.FromObject(new BlockUsage{Role="native",Reviewed=true,Layer="QA_TEST"})});
        Check(Call("blocks")["items"]![0]!["profile"]!["Usage"]!.Value<bool>("Reviewed"),"Offline block configuration failed");
        string sourceHash=CadSourceFile.Hash(source);
        var repository=new LibraryRepository(config);
        var current=JObject.FromObject(repository.State())["items"]!.ToObject<System.Collections.Generic.List<LibrarySyncItem>>()!;
        var native=current.Select(i=>i.Copy()).ToList();var hatch=native.Single(i=>i.Kind=="hatches");hatch.Values["scale"]=333.0;
        var presence=new JObject{[native.Single(i=>i.Kind=="blocks").Id]=new JObject{["samplePresent"]=true,["definitionPresent"]=true}};
        JObject Snapshot(string mode)
        {
            string id=Guid.NewGuid().ToString("N"),folder=Path.Combine(config.DataRoot,"library-sync",id);Directory.CreateDirectory(folder);File.Copy(source,Path.Combine(folder,"source.dwg"));File.WriteAllText(Path.Combine(folder,".qa-native-snapshot"),"Standalone repository fixture");
            var data=new JObject{["id"]=id,["source"]=source,["readMode"]=mode,["items"]=JArray.FromObject(native),["presence"]=presence.DeepClone(),["signatureFormat"]=2,["signature"]=LibraryRepository.SnapshotSignature(native,presence),["snapshotHash"]=sourceHash};JsonUtil.Atomic(Path.Combine(folder,"snapshot.json"),data);return data;
        }
        var before=Snapshot("OPEN_DOCUMENT");Call("live/register",new(){["id"]=before["id"]!.DeepClone()});
        Check(Call("live/"+before.Value<string>("id")+"/blocks")["groups"]!.Count()>0,"Offline live read lost groups");
        var saved=Snapshot("SAVED_FILE");var prepared=Call("live/prepare",new(){["reviewId"]=before["id"]!.DeepClone(),["savedId"]=saved["id"]!.DeepClone()});
        Call("approve",new(){["id"]=prepared["id"]!.DeepClone(),["choices"]=new JObject{[hatch.Id]="cad"}});
        Check(Call("hatches")["items"]![0]!["profile"]!.Value<double>("scale")==333,"Offline selected publication failed");
        Check(CadSourceFile.Hash(source)==sourceHash,"Offline publication overwrote source");
        using(var reopened=new QaClient(config))Check(reopened.Call("api/qa-library/hatches",null,ct).GetAwaiter().GetResult()["items"]![0]!["profile"]!.Value<double>("scale")==333,"Offline catalog lost on reopen");
        // The fixture deliberately contains invalid DWG bytes. A real failed native
        // read must show the catalog and its groups, with editing blocked.
        var window=new QaBrowserWindow(test:true);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            typeof(QaBrowserWindow).GetField("client",flags)!.SetValue(window,client);
            bool failed=false;
            try{((Task)typeof(QaBrowserWindow).GetMethod("RefreshLive",flags)!.Invoke(window,null)!).GetAwaiter().GetResult();}
            catch(InvalidOperationException){failed=true;}
            var list=(ListBox)typeof(QaBrowserWindow).GetField("list",flags)!.GetValue(window)!;
            var tree=(TreeView)typeof(QaBrowserWindow).GetField("tree",flags)!.GetValue(window)!;
            Check(failed&&list.Items.Count==1&&tree.Items.Count>=3,"Native read failure blanked the offline catalog");
        }
        finally{window.Close();}
        Check(!File.Exists(Path.Combine(config.DataRoot,"host.json")),"QA created a Host descriptor");
        JsonUtil.Atomic(Path.Combine(evidence,"standalone.json"),new{passed=true,hostExecutableMissing=true,noHostDescriptor=true,kinds=5,metadata=true,groups=true,legacyReview=true,blockConfiguration=true,selectedPublication=true,sourcePreserved=true,reopened=true,failedReadKeepsCatalog=true});
        VerifyRealCatalogReadOnly(evidence);
    }
    static void VerifyRealCatalogReadOnly(string evidence)
    {
        string app=Environment.GetEnvironmentVariable("QA_REAL_APP_ROOT")??"",data=Environment.GetEnvironmentVariable("QA_REAL_DATA_ROOT")??"",snapshot=Environment.GetEnvironmentVariable("QA_REAL_SNAPSHOT")??"";
        if(app=="")return;
        string warehouse=WarehousePaths.Resolve(app),catalog=Path.Combine(warehouse,"Catalog");
        Check(!File.Exists(Path.Combine(catalog,"sync","pending.json")),"Do not implicitly recover a real catalog during audit");
        var paths=Directory.GetFiles(catalog,"*.json",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        string Digest()=>JsonUtil.Hash(paths.Select(p=>new{path=p.Substring(warehouse.Length),hash=JsonUtil.FileHash(p)}).ToArray());
        string before=Digest();
        using var client=new QaClient(new InstallConfig{ContentRoot=app,DataRoot=data,HostExecutable=Path.Combine(app,"intentionally-missing-host.exe")});
        var counts=new JArray();
        foreach(string kind in LibrarySyncRules.Kinds)
        {
            var published=client.Call("api/qa-library/"+kind,null,CancellationToken.None).GetAwaiter().GetResult();
            var live=client.Call("api/qa-library/live/"+snapshot+"/"+kind,null,CancellationToken.None).GetAwaiter().GetResult();
            Check(published["items"]!.OfType<JObject>().All(p=>live["items"]!.OfType<JObject>().Any(l=>l.Value<string>("resourceId")==p.Value<string>("resourceId"))),"Actual catalog profile lost: "+kind);
            counts.Add(new JObject{["kind"]=kind,["published"]=published["items"]!.Count(),["groups"]=published["groups"]!.Count(),["live"]=live["items"]!.Count()});
            if(kind=="blocks")
            {
                var lamp=live["items"]!.OfType<JObject>().SingleOrDefault(r=>r.Value<string>("resourceId")=="QA_DenNgu");
                Check(lamp==null||lamp.Value<string>("liveStatus") is "DEFINITION_ONLY" or "MISSING","Deleted lamp became active");
            }
        }
        Check(before==Digest(),"Read-only native audit modified the real catalog");
        JsonUtil.Atomic(Path.Combine(evidence,"real-catalog.json"),new{passed=true,readOnly=true,actualManagerContracts=true,catalogUnchanged=true,kinds=counts});
    }
}
