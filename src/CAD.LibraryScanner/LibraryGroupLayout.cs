using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;

namespace AICad.LibraryScanner;
public sealed partial class LibrarySyncNative
{
    public static void ApplyGroupLayout(Database db,Transaction tx,JObject plan,List<LibrarySyncItem> hints)
    {
        string kind=plan.Value<string>("kind")!;
        var desired=plan["nodes"]!.ToObject<List<LibraryFrameNode>>()!;
        var map=plan["pathMap"]!.ToObject<Dictionary<string,string>>()!;
        var physical=LibraryFrames.Model(db,tx).OfType<BlockReference>().Select(b=>LibraryFrames.Info(b,tx)).Where(n=>n!=null).Cast<LibraryFrameNode>().ToList();
        var scope=new LibraryScanScope(db,tx);
        var native=Read(db,tx,hints);
        var storedGroups=native.ToDictionary(i=>i.Id,i=>i.Group);
        foreach(var framed in ReadFramed(db,tx,hints,out _)){native.RemoveAll(i=>i.Id==framed.Id);native.Add(framed);}
        foreach(var item in native.Where(i=>scope.Includes(i.Kind,i.Handle)))
        {
            var sampleId=scope.Sample(item.Kind,item.Handle);if(sampleId.IsNull)continue;
            var sample=(Entity)tx.GetObject(sampleId,OpenMode.ForRead);
            if(SampleMetadata(sample,tx)==null){AssignGroup(db,tx,item,item.Group,sample);Tag(sample,tx,NativeSampleKey,item);LibraryFrames.AddCell(db,tx,item,sample);}
        }
        var moved=new List<LibrarySyncItem>();
        foreach(var item in native.Where(i=>i.Kind==kind))
        {
            string old=scope.Includes(kind,item.Handle)?scope.Group(kind,item.Handle):item.Group;
            if(storedGroups.TryGetValue(item.Id,out var stored)&&map.TryGetValue(stored,out var destination)&&stored!=destination&&!physical.Any(n=>n.Kind==kind&&n.Path==stored))old=stored;
            if(!map.TryGetValue(old,out var target)){var prefix=map.Where(p=>p.Key!=p.Value&&LibraryFrames.Under(old,p.Key)).OrderByDescending(p=>p.Key.Length).FirstOrDefault();if(prefix.Key==null)continue;target=(prefix.Value+old.Substring(prefix.Key.Length)).Trim(' ','/');}
            if(target==old)continue;
            var sampleId=scope.Sample(kind,item.Handle);
            var samples=LibraryFrames.Model(db,tx).Where(e=>SampleMetadata(e,tx)?.Id==item.Id&&!IsPresentationHelper(e,tx)).ToList();
            if(!sampleId.IsNull&&!samples.Any(e=>e.ObjectId==sampleId))samples.Add((Entity)tx.GetObject(sampleId,OpenMode.ForRead));
            AssignGroup(db,tx,item,target);
            foreach(var sample in samples){MarkSample(sample,tx,item);Tag(sample,tx,NativeSampleKey,item);LibraryFrames.AddCell(db,tx,item,sample);}
            if(samples.Count>0)moved.Add(item);
        }
        LibraryFrames.Clear(db,tx);
        var saved=LibraryFrames.Nodes(db,tx);
        foreach(var node in desired)
        {
            var old=saved.SingleOrDefault(n=>n.Id==node.Id);
            if(old!=null){node.X=old.X;node.Y=old.Y;node.DrawnWidth=old.DrawnWidth;node.DrawnHeight=old.DrawnHeight;node.RequestedWidth=old.RequestedWidth;node.RequestedHeight=old.RequestedHeight;}
        }
        var nodes=physical.Where(n=>n.Kind!=kind).Select(n=>saved.SingleOrDefault(s=>s.Id==n.Id)??n).Concat(desired).ToList();
        LibraryFrames.Save(db,tx,nodes);
        var displayed=native.Where(i=>ModelHasSample(db,tx,i.Id)&&nodes.Any(n=>n.Kind==i.Kind&&n.Path==i.Group)).ToList();
        // Include physical samples not yet published, but never create an insertion
        // from a bare definition or rebuild a deleted unrelated frame.
        foreach(var node in nodes.Where(n=>n.Kind==kind&&n.Path!=""&&n.X==null))
        {var parent=nodes.Single(n=>n.Kind==kind&&n.Path==node.Parent);node.X=(parent.X??100000+Array.IndexOf(LibrarySyncRules.Kinds,kind)*65000)+800;node.Y=(parent.Y??0)-Math.Max(parent.DrawnHeight,6000)-2000;}
        LibraryFrames.Save(db,tx,nodes);LibraryFrames.Build(db,tx,displayed);
        foreach(var group in moved.GroupBy(i=>i.Group))LibraryFrames.AppendToGroup(db,tx,nodes.Single(n=>n.Kind==kind&&n.Path==group.Key),displayed,group.ToList());
        LibraryFrames.Clear(db,tx);LibraryFrames.Build(db,tx,displayed);LibraryFrames.Separate(db,tx,displayed);
    }
}
