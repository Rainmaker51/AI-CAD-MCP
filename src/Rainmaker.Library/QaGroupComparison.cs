using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using AICad.LibraryScanner;
using Newtonsoft.Json.Linq;

namespace Rainmaker.Library;
internal static class QaGroupComparison
{
    internal static JArray Compare(Dictionary<string,JObject> views,JObject snapshot)
    {
        var output=new JArray();var physical=(snapshot["presence"]?["groups"]?["frames"] as JArray??new()).ToObject<List<LibraryFrameNode>>()!;
        var registered=(snapshot["presence"]?["groups"]?["registered"] as JArray??new()).ToObject<List<LibraryFrameNode>>()!;
        foreach(var pair in views)
        {
            string kind=pair.Key;var groups=pair.Value["groups"]!.ToObject<List<LibraryGroup>>()!;var used=new HashSet<string>();
            JObject Item(string id,string path)=>new(){["Id"]=id,["Kind"]=kind,["Name"]=path,["Group"]=path};
            void Add(string key,string status,JObject? before,JObject? after,JObject request)=>output.Add(new JObject{["Id"]="group-"+kind+"-"+key,["EntityType"]="group",["Status"]=status,["App"]=before,["Cad"]=after,["GroupRequest"]=request});
            foreach(var g in groups)
            {
                string path=QaBrowserWindow.TreePath(groups,g.Id);
                var frame=physical.SingleOrDefault(n=>n.Kind==kind&&n.CatalogGroupId==g.Id)??physical.SingleOrDefault(n=>n.Kind==kind&&n.Path==path)??physical.SingleOrDefault(n=>n.Kind==kind&&registered.Any(r=>r.Id==n.Id&&r.Kind==kind&&r.Path==path));
                if(frame==null){Add(g.Id,"GROUP_MISSING",Item(g.Id,path),null,new JObject{["kind"]=kind,["id"]=g.Id,["action"]="remove"});continue;}
                used.Add(frame.Id);
                Add(g.Id,frame.Path==path?"GROUP_MATCH":"GROUP_CHANGED",Item(g.Id,path),Item(frame.Id,frame.Path),new JObject{["kind"]=kind,["id"]=g.Id,["name"]=frame.Path.Split('/').Last().Trim(),["parentPath"]=frame.Parent,["frameId"]=frame.Id});
            }
            foreach(var f in physical.Where(n=>n.Kind==kind&&n.Path!=""&&!used.Contains(n.Id)))
                Add(f.Id,"GROUP_NEW",null,Item(f.Id,f.Path),new JObject{["kind"]=kind,["id"]=f.Id,["frameId"]=f.Id,["action"]="create",["name"]=f.Path.Split('/').Last().Trim(),["parentPath"]=f.Parent});
        }
        return output;
    }
}
