using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;
namespace Rainmaker.Library;
public sealed partial class NativeTests
{
    static void VerifyGroupLifecycle(Database db,List<LibrarySyncItem> hints,string evidence)
    {
        foreach(string kind in LibrarySyncRules.Kinds)using(var tx=db.TransactionManager.StartTransaction())
        {
            LibraryFrames.Separate(db,tx,hints);
            var frames=LibraryFrames.Model(db,tx).OfType<BlockReference>().Select(b=>LibraryFrames.Info(b,tx)).Where(n=>n!=null&&n.Kind==kind&&n.Path!="").Cast<LibraryFrameNode>().ToList();
            var groups=frames.Select(n=>new LibraryGroup{Id=n.Id,Name=n.Path.Split('/').Last().Trim(),ParentId=n.Parent==""?"":frames.Single(p=>p.Path==n.Parent).Id}).ToList();
            var resource=hints.Single(i=>i.Kind==kind);string originalId=resource.Id;var leaf=groups.Single(g=>QaBrowserWindow.TreePath(groups,g.Id)==resource.Group);
            var view=new JObject{["kind"]=kind,["groups"]=JArray.FromObject(groups),["items"]=new JArray(new JObject{["id"]=resource.Id,["metadataId"]=resource.Id,["management"]=new JObject{["GroupId"]=leaf.Id,["Revision"]=0}})};
            JObject Snapshot()=>new(){["presence"]=new JObject{["groups"]=LibraryFrames.GroupSnapshot(db,tx)}};
            string added=Guid.NewGuid().ToString("N"),child=Guid.NewGuid().ToString("N");
            var plan=QaBrowserWindow.PrepareGroupPlan(view,Snapshot(),new JObject{["id"]=added,["action"]="create",["name"]="Temporary",["parentId"]=""});
            LibrarySyncNative.ApplyGroupLayout(db,tx,plan,hints);view["groups"]=plan["afterGroups"]!.DeepClone();
            Check(LibraryFrames.Model(db,tx).OfType<BlockReference>().Any(b=>LibraryFrames.Info(b,tx)?.CatalogGroupId==added),"Empty group has no physical frame: "+kind);
            plan=QaBrowserWindow.PrepareGroupPlan(view,Snapshot(),new JObject{["id"]=child,["action"]="create",["name"]="Child",["parentId"]=added});
            LibrarySyncNative.ApplyGroupLayout(db,tx,plan,hints);view["groups"]=plan["afterGroups"]!.DeepClone();
            var erased=LibraryFrames.Model(db,tx).OfType<BlockReference>().Single(b=>LibraryFrames.Info(b,tx)?.Id==leaf.Id);erased.UpgradeOpen();erased.Erase();
            var differences=QaGroupComparison.Compare(new Dictionary<string,JObject>{{kind,view}},Snapshot());Check(differences.Any(d=>d.Value<string>("Status")=="GROUP_MISSING"),"Deleted CAD frame not reported");
            plan=QaBrowserWindow.PrepareGroupPlan(view,Snapshot(),new JObject{["id"]=leaf.Id,["action"]="remove",["targetId"]=added,["childMode"]="lift"});
            LibrarySyncNative.ApplyGroupLayout(db,tx,plan,hints);view["groups"]=plan["afterGroups"]!.DeepClone();
            var read=LibrarySyncNative.ReadFramed(db,tx,hints,out var presence);Check(read.Single(i=>i.Id==originalId).Group=="Temporary"&&presence[originalId]!.Value<bool>("inScope"),"Missing-frame deletion did not move existing sample: "+kind);
            Check(!LibraryFrames.Model(db,tx).OfType<BlockReference>().Any(b=>LibraryFrames.Info(b,tx)?.Id==leaf.Id),"Deleted group frame was restored");
            ((JObject)view["items"]![0]!["management"]!)["GroupId"]=added;
            plan=QaBrowserWindow.PrepareGroupPlan(view,Snapshot(),new JObject{["id"]=added,["action"]="remove",["targetId"]="",["childMode"]="branch"});
            LibrarySyncNative.ApplyGroupLayout(db,tx,plan,hints);
            Check(plan["afterGroups"]!.OfType<JObject>().Single(g=>g.Value<string>("Id")==child).Value<string>("ParentId")=="","Child group did not move up");
            read=LibrarySyncNative.ReadFramed(db,tx,hints,out presence);Check(read.Any(i=>i.Id==originalId)&&presence[originalId]!.Value<bool>("inScope"),"Group deletion erased resource or left it outside root");
            tx.Abort();
        }
        AICad.Contracts.Json.Atomic(Path.Combine(evidence,"groups.json"),new{passed=true,fiveKinds=true,emptyFrameCreated=true,missingFrameDetected=true,deleteMissingFrame=true,samplesMoved=true,childrenMoved=true,resourceIdsPreserved=true});
    }
}
