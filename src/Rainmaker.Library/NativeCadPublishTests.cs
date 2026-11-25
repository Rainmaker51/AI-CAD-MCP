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
                {var table=(LayerTable)tx.GetObject(doc.Database.LayerTableId,OpenMode.ForWrite);var layer=new LayerTableRecord{Name="CAD_PUBLISH_END_TO_END",LineWeight=LineWeight.LineWeight035};table.Add(layer);tx.AddNewlyCreatedDBObject(layer,true);
                    var frameItem=LibrarySyncNative.ReadDrawing(doc.Database,tx).Single(i=>i.Name==layer.Name);
                    var bt=(BlockTable)tx.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                    var sample=new Line(Autodesk.AutoCAD.Geometry.Point3d.Origin,new Autodesk.AutoCAD.Geometry.Point3d(5000,0,0)){LayerId=layer.ObjectId};model.AppendEntity(sample);tx.AddNewlyCreatedDBObject(sample,true);
                    LibrarySyncNative.AssignGroup(doc.Database,tx,frameItem,"CAD / Native",sample);LibraryFrames.AddCell(doc.Database,tx,frameItem,sample);LibraryFrames.Build(doc.Database,tx,new(){frameItem});LibraryFrames.Separate(doc.Database,tx,new(){frameItem});tx.Commit();}
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
            typeof(QaBrowserWindow).GetField("kind",flags)!.SetValue(window,"layers");
            ((Task)typeof(QaBrowserWindow).GetMethod("RefreshLive",flags)!.Invoke(window,null)!).GetAwaiter().GetResult();
            string groupId=Guid.NewGuid().ToString("N");
            void SyncGroup(JObject request)=>((Task)typeof(QaBrowserWindow).GetMethod("SyncGroup",flags)!.Invoke(window,new object[]{request})!).GetAwaiter().GetResult();
            SyncGroup(new JObject{["id"]=groupId,["action"]="create",["name"]="Window group",["parentId"]=""});
            var layerGroups=client.Call("api/qa-library/layers",null,CancellationToken.None).GetAwaiter().GetResult()["groups"]!.ToObject<System.Collections.Generic.List<LibraryGroup>>()!;
            string originalGroup=layerGroups.Single(g=>QaBrowserWindow.TreePath(layerGroups,g.Id)=="CAD / Native").Id;
            using(var locked=doc.LockDocument())
            {using(var tx=doc.Database.TransactionManager.StartTransaction()){var frame=LibraryFrames.Model(doc.Database,tx).OfType<BlockReference>().Single(b=>LibraryFrames.Info(b,tx)?.CatalogGroupId==originalGroup);frame.UpgradeOpen();frame.Erase();tx.Commit();}doc.Database.SaveAs(source,true,DwgVersion.Current,doc.Database.SecurityParameters);}
            ((Task)typeof(QaBrowserWindow).GetMethod("RefreshLive",flags)!.Invoke(window,null)!).GetAwaiter().GetResult();
            SyncGroup(new JObject{["id"]=originalGroup,["action"]="remove",["targetId"]=groupId,["childMode"]="lift"});
            var grouped=client.Call("api/qa-library/layers",null,CancellationToken.None).GetAwaiter().GetResult();
            Check(!grouped["groups"]!.Any(g=>g.Value<string>("Id")==originalGroup)&&grouped["items"]!.OfType<JObject>().Single(r=>r.Value<string>("id")==item.Id)["management"]!.Value<string>("GroupId")==groupId,"Actual window group operation left removed group or stale metadata");
            Check(new QaUpdateJournal(config).Read()==null,"Group operation left a pending journal");
            var completedGroup=Directory.GetFiles(Path.Combine(root,"Versions","qa-mutations"),"result.json",SearchOption.AllDirectories).Select(f=>JObject.Parse(File.ReadAllText(f))).Single(j=>j["groupAction"]?["request"]?.Value<string>("id")==originalGroup);
            completedGroup["phase"]="CAD_SAVED";JsonUtil.Atomic(new QaUpdateJournal(config).PendingPath,completedGroup);
            ((Task)typeof(QaBrowserWindow).GetMethod("CompletePendingSync",flags)!.Invoke(window,null)!).GetAwaiter().GetResult();
            Check(new QaUpdateJournal(config).Read()==null,"Saved group retry failed while source drawing remained open");
            JsonUtil.Atomic(Path.Combine(evidence,"group-publication.json"),new{passed=true,actualWindowGroup=true,emptyGroupFrame=true,deletedCadFrameReconciled=true,metadataAndCadTogether=true,journalCompleted=true,retryWithOpenDrawing=true});
            QaCadSnapshot.Discard(config,saved);
            JsonUtil.Atomic(Path.Combine(evidence,"cad-publication.json"),new{passed=true,actualWindowPlan=true,nativeLayerPreserved=true,selectedOnly=true,presentationCreated=true,savedSource=true,completionReplay=true,deferredPreserved=true});
        }
        finally
        {
            window.Close();
        }
    }
}
