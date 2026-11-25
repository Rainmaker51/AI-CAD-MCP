using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;

namespace AICad.LibraryScanner;

/// <summary>Only native samples inside their typed Model-space frame enter QA.</summary>
public sealed class LibraryScanScope
{
    readonly Dictionary<string,LibraryFrameNode> accepted=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,ObjectId> samples=new(StringComparer.OrdinalIgnoreCase);
    public JObject Diagnostics {get;}=new();
    static string Key(string kind,string handle)=>kind+"|"+handle;
    public bool Includes(string kind,string handle)=>accepted.ContainsKey(Key(kind,handle));
    public string Group(string kind,string handle)=>accepted[Key(kind,handle)].Path;
    public ObjectId Sample(string kind,string handle)=>samples.TryGetValue(Key(kind,handle),out var id)?id:ObjectId.Null;

    public LibraryScanScope(Database db,Transaction tx)
    {
        var model=LibraryFrames.Model(db,tx).ToArray();
        var frames=model.OfType<BlockReference>().Select(b=>new{Block=b,Node=LibraryFrames.Info(b,tx)}).Where(f=>f.Node!=null&&LibrarySyncRules.Kinds.Contains(f.Node.Kind)).ToArray();
        if(LibrarySyncRules.Kinds.Any(k=>!frames.Any(f=>f.Node!.Kind==k&&f.Node.Path=="")))
            throw new CadException("LIBRARY_SCAN_FRAMES","Thiếu khung gốc Block, Layer, Hatch, Text hoặc Dim. Mở DWG thư viện và khởi tạo/khôi phục bố trí; QA không quét toàn bản vẽ thay thế.");
        var candidates=new Dictionary<string,List<Tuple<LibraryFrameNode,ObjectId>>>();var issues=new JArray();
        foreach(var e in model)
        {
            if(LibraryFrames.Info(e,tx)!=null)continue;
            var tag=LibrarySyncNative.SampleMetadata(e,tx);
            // QA description and cell borders are not native library samples.
            if(LibrarySyncNative.IsPresentationHelper(e,tx))continue;
            string kind;ObjectId resource;
            if(e is BlockReference b){kind="blocks";resource=b.IsDynamicBlock?b.DynamicBlockTableRecord:b.BlockTableRecord;}
            else if(e is Hatch){kind="hatches";resource=e.ObjectId;}
            else if(e is Dimension d){kind="dims";resource=d.DimensionStyle;}
            else if(e is DBText t&&e is not AttributeDefinition){kind="texts";resource=t.TextStyleId;}
            else if(e is MText m){kind="texts";resource=m.TextStyleId;}
            else if(e is Line||e is Polyline||e is Arc||e is Circle){kind="layers";resource=e.LayerId;}
            else continue;
            var containing=new List<LibraryFrameNode>();bool crossing=false;
            foreach(var f in frames.Where(f=>f.Node!.Kind==kind))
            {
                // Frame border is the transformed unit rectangle, not its ATT text extents.
                try
                {
                    var ext=e.GeometricExtents;var inverse=f.Block.BlockTransform.Inverse();
                    var points=new[]{new Point3d(ext.MinPoint.X,ext.MinPoint.Y,0),new Point3d(ext.MinPoint.X,ext.MaxPoint.Y,0),new Point3d(ext.MaxPoint.X,ext.MinPoint.Y,0),new Point3d(ext.MaxPoint.X,ext.MaxPoint.Y,0)}.Select(p=>p.TransformBy(inverse)).ToArray();
                    const double tolerance=1e-8;
                    if(points.All(p=>p.X>=-tolerance&&p.X<=1+tolerance&&p.Y>=-1-tolerance&&p.Y<=tolerance))containing.Add(f.Node!);
                    else if(points.Max(p=>p.X)>=0&&points.Min(p=>p.X)<=1&&points.Max(p=>p.Y)>=-1&&points.Min(p=>p.Y)<=0)crossing=true;
                }
                catch(Autodesk.AutoCAD.Runtime.Exception){ }
            }
            var leaves=containing.Any(n=>n.Path=="")?containing.Where(n=>!containing.Any(other=>other.Id!=n.Id&&other.Path!=n.Path&&LibraryFrames.Under(other.Path,n.Path))).ToArray():Array.Empty<LibraryFrameNode>();
            if(leaves.Length!=1)
            {
                if(leaves.Length>1||crossing)issues.Add(new JObject{["handle"]=e.Handle.ToString(),["kind"]=kind,["reason"]=leaves.Length>1?"OVERLAPPING_FRAMES":"CROSSES_FRAME"});
                continue;
            }
            string key=Key(kind,resource.Handle.ToString());if(!candidates.TryGetValue(key,out var groups))candidates[key]=groups=new();groups.Add(Tuple.Create(leaves[0],e.ObjectId));
        }
        foreach(var pair in candidates)
        {
            var groups=pair.Value.Select(p=>p.Item1).GroupBy(n=>n.Id).Select(g=>g.First()).ToArray();
            if(groups.Length==1){accepted[pair.Key]=groups[0];samples[pair.Key]=pair.Value[0].Item2;}
            else issues.Add(new JObject{["resource"]=pair.Key,["reason"]="MULTIPLE_GROUPS"});
        }
        Diagnostics["mode"]="TYPED_FRAMES";Diagnostics["issues"]=issues;
    }
}
