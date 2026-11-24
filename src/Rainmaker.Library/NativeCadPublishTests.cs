using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AICad.Application;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;
public sealed partial class NativeTests
{
    static void VerifyCadPublication(string evidence)
    {
        string donor=Environment.GetEnvironmentVariable("QALIB_STANDALONE_FIXTURE")??"";
        Check(Path.GetFileName(donor).StartsWith("qa-library-tests-",StringComparison.Ordinal),"Owned business fixture required");
        string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";
        Check(File.Exists(Path.Combine(root,".aicad-tools-test")),"Owned native runtime required");
        foreach(string file in Directory.GetFiles(donor,"*",SearchOption.AllDirectories))
        {string relative=file.Substring(donor.Length+1);if(!new[]{"Versions","Styles","Hatches","Catalog","content-assets","template-library","skills","packages"}.Any(d=>relative.StartsWith(d+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))continue;string to=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(to)!);File.Copy(file,to,true);}
        var config=new InstallConfig{ContentRoot=root,DataRoot=Path.Combine(root,"native-data")};string source=CadSourceFile.BaseLibraryPath(root);
        var doc=AcApp.DocumentManager.MdiActiveDocument;Check(QaCadSnapshot.Same(doc.Name,source),"Integration fixture must already be the source document");
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        using var client=new QaClient(config);var window=new QaBrowserWindow(test:true);
        try
        {
            window.PublicationProgress=step=>File.AppendAllText(Path.Combine(evidence,"publication-steps.log"),step+"\n");
            window.PublicationProgress("FIXTURE_WINDOW");
            typeof(QaBrowserWindow).GetField("client",flags)!.SetValue(window,client);
            var sync=client.Call("api/library-sync",null,CancellationToken.None).GetAwaiter().GetResult();var hints=sync["items"]!.ToObject<System.Collections.Generic.List<LibrarySyncItem>>()!;
            typeof(QaBrowserWindow).GetField("hints",flags)!.SetValue(window,hints);
            using(var locked=doc.LockDocument())
            {
                using(var tx=doc.Database.TransactionManager.StartTransaction())
                {var table=(LayerTable)tx.GetObject(doc.Database.LayerTableId,OpenMode.ForWrite);var layer=new LayerTableRecord{Name="CAD_PUBLISH_END_TO_END",LineWeight=LineWeight.LineWeight035};table.Add(layer);tx.AddNewlyCreatedDBObject(layer,true);tx.Commit();}
                doc.Database.SaveAs(source,true,DwgVersion.Current,doc.Database.SecurityParameters);
            }
            window.PublicationProgress("FIXTURE_SOURCE_SAVED");
            var view=client.Call("api/qa-library/layers",null,CancellationToken.None).GetAwaiter().GetResult();window.Bind(view);
            window.PublicationProgress("FIXTURE_BOUND");
            var saved=QaCadSnapshot.CaptureSaved(config,source,hints);
            JObject Review()=>client.Call("api/qa-library/live/register",new(){["id"]=saved["id"]!.DeepClone()},CancellationToken.None).GetAwaiter().GetResult();
            var review=Review();var item=saved["items"]!.ToObject<System.Collections.Generic.List<LibrarySyncItem>>()!.Single(i=>i.Name=="CAD_PUBLISH_END_TO_END");
            window.PublicationProgress("FIXTURE_REVIEWED");
            var plan=new JObject{["sourceHash"]=saved["snapshotHash"]!.DeepClone(),["appHash"]=review["appHash"]!.DeepClone(),["labels"]=new JObject(),["deferred"]=5,["rows"]=new JArray(new JObject{["id"]=item.Id,["kind"]="layers",["name"]=item.Name,["action"]="cad",["new"]=true,["bindId"]="",["mergeId"]="",["group"]="CAD / Native",["native"]=JObject.FromObject(item),["app"]=null})};
            ((Task)typeof(QaBrowserWindow).GetMethod("ApplyCadPlan",flags)!.Invoke(window,new object[]{plan})!).GetAwaiter().GetResult();
            var published=client.Call("api/qa-library/layers",null,CancellationToken.None).GetAwaiter().GetResult();
            Check(published["items"]!.OfType<JObject>().Any(r=>r.Value<string>("name")==item.Name&&r.Value<string>("id")==item.Id),"Confirmed CAD plan did not publish selected layer");
            Check(new QaUpdateJournal(config).Read()==null,"Successful CAD plan left a pending journal");
            Check(client.Call("api/qa-library/blocks",null,CancellationToken.None).GetAwaiter().GetResult()["items"]!.Count()>0,"Deferred missing resources were removed");
            using(var tx=doc.Database.TransactionManager.StartOpenCloseTransaction())
            {var table=(LayerTable)tx.GetObject(doc.Database.LayerTableId,OpenMode.ForRead);Check(((LayerTableRecord)tx.GetObject(table[item.Name],OpenMode.ForRead)).LineWeight==LineWeight.LineWeight035,"Publication overwrote native layer style");Check(LibraryFrames.Model(doc.Database,tx).OfType<MText>().Any(t=>LibrarySyncNative.SampleMetadata(t,tx)?.Id==item.Id),"Confirmed CAD plan did not create presentation");}
            // Reopen repository + replay the durable saved plan: no duplicate resources.
            using(var locked=new QaUpdateJournal(config).Lock())
            {
                var journal=new QaUpdateJournal(config);plan["sourceHash"]=QaUpdateJournal.SharedHash(source);var record=journal.Begin(plan);record["afterHash"]=QaUpdateJournal.SharedHash(source);journal.Save(record);
                ((Task)typeof(QaBrowserWindow).GetMethod("FinishCadPlan",flags)!.Invoke(window,new object[]{record})!).GetAwaiter().GetResult();
            }
            Check(new QaUpdateJournal(config).Read()==null,"Idempotent completion did not clear journal");
            QaCadSnapshot.Discard(config,saved);
            JsonUtil.Atomic(Path.Combine(evidence,"cad-publication.json"),new{passed=true,actualWindowPlan=true,nativeLayerPreserved=true,selectedOnly=true,presentationCreated=true,savedSource=true,completionReplay=true,deferredPreserved=true});
        }
        finally
        {
            window.Close();
        }
    }
}
