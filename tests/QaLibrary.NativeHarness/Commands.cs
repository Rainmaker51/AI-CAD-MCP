using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using AICad.Contracts;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

[assembly:CommandClass(typeof(QaLibrary.NativeHarness.Commands))]
namespace QaLibrary.NativeHarness;
public sealed class Commands
{
    const BindingFlags Internal=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    [CommandMethod("QANATIVEVERIFY")]
    public void Run()
    {
        string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";
        if(!Path.IsPathRooted(root)||!File.Exists(Path.Combine(root,".aicad-tools-test")))return;
        IDisposable? runtime=null;
        try
        {
            var bridge=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="CAD.Bridge.AutoCAD");
            var config=JsonUtil.Read<InstallConfig>(File.ReadAllText(Path.Combine(root,"install.json")));
            var catalog=new CadToolCatalog(root);var tool=catalog.Import(Path.Combine(root,"library","Rainmaker.Library.dll"),0);
            string dispatched="";Action<string,string> dispatch=(id,command)=>dispatched=id+":"+command;
            var type=bridge.GetType("AICad.Bridge.CadToolRuntime",true)!;
            runtime=(IDisposable)Activator.CreateInstance(type,Internal,null,new object[]{config,dispatch},null)!;
            var loaded=type.GetMethod("Load",Internal)!.Invoke(runtime,new object[]{tool,false})!;
            var assembly=(Assembly)loaded.GetType().GetField("Assembly",Internal)!.GetValue(loaded)!;
            if(Environment.GetEnvironmentVariable("QA_CAPTURE_ONLY")=="1")
            {
                var captureConfig=JsonUtil.Read<InstallConfig>(File.ReadAllText(Path.Combine(root,"capture-request.json")));
                string warehouse=WarehousePaths.Resolve(captureConfig.ContentRoot);
                if(File.Exists(Path.Combine(warehouse,"Catalog","sync","pending.json"))||File.Exists(Path.Combine(warehouse,"Catalog","qa-transactions","ISO_QUANGANH.pending.json")))throw new InvalidOperationException("Finish pending operations before reading maintenance snapshot");
                var clientType=assembly.GetType("Rainmaker.Library.QaClient",true)!;using var client=(IDisposable)Activator.CreateInstance(clientType,Internal,null,new object[]{captureConfig},null)!;
                var task=(System.Threading.Tasks.Task<JObject>)clientType.GetMethod("Call",Internal)!.Invoke(client,new object?[]{"api/library-sync",null,System.Threading.CancellationToken.None})!;
                var state=task.GetAwaiter().GetResult();var hints=state["items"]!.ToObject<System.Collections.Generic.List<LibrarySyncItem>>()!;
                var snapshotType=assembly.GetType("Rainmaker.Library.QaCadSnapshot",true)!;
                var snapshot=(JObject)snapshotType.GetMethod("CaptureSaved",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{captureConfig,state.Value<string>("source")!,hints})!;
                JsonUtil.Atomic(Path.Combine(root,"capture-result.json"),snapshot);return;
            }
            var managed=((IEnumerable)loaded.GetType().GetField("Commands",Internal)!.GetValue(loaded)!).Cast<object>().Select(c=>(string)c.GetType().GetField("Name",Internal)!.GetValue(c)!).ToArray();
            var plugin=assembly.GetType("Rainmaker.Library.LibraryPlugin",true)!;
            var current=plugin.GetField("Current",BindingFlags.Static|BindingFlags.NonPublic)!;
            var pins=((ICadManagedAddin)current.GetValue(null)!).ManagedCommands;
            if(managed.Length!=pins.Count||managed.Length!=18||!managed.Contains("QALIBINSERT")||managed.Any(n=>n.EndsWith("TEST")||n=="QALIBUIVERIFY"))throw new InvalidOperationException("Managed command descriptors do not match runtime");
            foreach(string command in new[]{"QALIBPACK","QALIBINSERT"})
            {
                plugin.GetMethod("Send",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{command});
                if(dispatched!=tool.Id+":"+command)throw new InvalidOperationException("Manager dispatch was bypassed");
            }
            string data=Path.Combine(root,"library-data");Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,".library-manager-test"),"Owned native fixture");
            Environment.SetEnvironmentVariable("QALIB_TEST_ROOT",data);
            var tests=assembly.GetType("Rainmaker.Library.NativeTests",true)!;tests.GetMethod("Run")!.Invoke(Activator.CreateInstance(tests),null);
            var result=JsonUtil.Read<JObject>(File.ReadAllText(Path.Combine(data,"result.json")));if(!result.Value<bool>("passed"))throw new InvalidOperationException(result["error"]?.ToString());
            catalog.Update(tool.Id,1,tool.Name,"QA","",true,managed);
            catalog.PinCommands(tool.Id,2,new[]{pins.Single(p=>p.Command=="QALIBRARY")});
            if(catalog.Read().Items.Single().RibbonPins.Count!=1)throw new InvalidOperationException("Expected one Ribbon entry");
            type.GetMethod("Disable",Internal)!.Invoke(runtime,new object[]{tool.Id});if(current.GetValue(null)!=null)throw new InvalidOperationException("Disable failed lifecycle cleanup");
            JsonUtil.Atomic(Path.Combine(root,"ribbon-manager-results.json"),new{passed=true,actualManager=bridge.GetName().Version!.ToString(),managedCommands=managed.Length,singleRibbonEntry=true,managerDispatch=true,disableTerminates=true});
        }
        catch(System.Exception error){JsonUtil.Atomic(Path.Combine(root,"ribbon-manager-results.json"),new{passed=false,error=error.ToString()});}
        finally{runtime?.Dispose();}
    }
}
