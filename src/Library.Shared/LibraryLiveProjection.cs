using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AICad.Library;

// Linked into Host and QA. Does not add an API to Manager's shared Contracts assembly.
internal static class LibraryLiveProjection
{
    internal static JObject Build(JObject published,JObject capture)
    {
        var output=(JObject)published.DeepClone();string kind=published.Value<string>("kind")!;
        var old=(published["items"] as JArray??new()).OfType<JObject>().ToList();
        var native=(capture["items"] as JArray??new()).OfType<JObject>().Where(i=>i.Value<string>("Kind")==kind).ToList();
        var rows=new JArray();var seen=new HashSet<string>();
        foreach(var item in native)
        {
            string id=item.Value<string>("Id")!,name=item.Value<string>("Name")!;
            if(name.StartsWith("AICAD_LIB_",StringComparison.OrdinalIgnoreCase))continue;
            var matches=old.Where(r=>r.Value<string>("id")==id).ToList();
            if(matches.Count==0)matches=old.Where(r=>r.Value<string>("name")==name).ToList();
            // Several approved usage profiles can legitimately share one native
            // block definition. Keep every profile, its group, review and parameters.
            foreach(var previous in matches.Count==0?new List<JObject?>{null}:matches.Cast<JObject?>())
            {
            if(previous!=null)seen.Add(previous.Value<string>("resourceId")!);
            var row=previous==null?new JObject{["resourceId"]=name,["metadataId"]="",["code"]="",["management"]=new JObject{["Name"]="",["Purpose"]=item["Purpose"]?.DeepClone()??"",["Notes"]=item["Notes"]?.DeepClone()??"",["GroupId"]="",["Reviewed"]=false,["Revision"]=0,["DeletedUtc"]=null}}:(JObject)previous.DeepClone();
            row["id"]=id;row["name"]=name;row["displayName"]=row["management"]!.Value<string>("Name") is string alias&&alias!=""?alias:name;
            if(matches.Count>1&&string.IsNullOrEmpty(row["management"]!.Value<string>("Name")))row["displayName"]=name+" · "+row.Value<string>("resourceId");
            row["item"]=item.DeepClone();row["published"]=previous!=null;
            var values=(JObject)item["Values"]!;
            if(kind=="blocks")
            {
                var profile=row["profile"] as JObject??new();var source=profile["NativeSource"] as JObject??new();
                source["BlockName"]=name;source["Min"]=new JObject{["X"]=values["minX"]?.DeepClone(),["Y"]=values["minY"]?.DeepClone()};source["Max"]=new JObject{["X"]=values["maxX"]?.DeepClone(),["Y"]=values["maxY"]?.DeepClone()};
                source["DynamicProperties"]=new JArray((values["properties"] as JArray??new()).OfType<JObject>().Select(p=>new JObject{["Name"]=p["name"]?.DeepClone(),["DefaultValue"]=p["value"]?.DeepClone(),["ReadOnly"]=p["readOnly"]?.DeepClone(),["AllowedValues"]=p["allowed"]?.DeepClone()??new JArray(),["Units"]=p["units"]?.DeepClone()}));
                profile["NativeSource"]=source;profile["Hiện trạng CAD"]=values.DeepClone();row["profile"]=profile;
            }
            else row["profile"]=values.DeepClone();
            var presence=capture["presence"]?[id] as JObject;
            bool missingSample=kind=="blocks"&&presence?.Value<bool?>("samplePresent")==false;
            row["presence"]=presence?.DeepClone();
            bool changed=previous!=null&&previous["item"]?["Values"]!=null&&!JToken.DeepEquals(previous["item"]!["Values"],values);
            row["liveStatus"]=presence?.Value<bool?>("inScope")==false?"OUT_OF_SCOPE":missingSample?"DEFINITION_ONLY":previous==null?"NEW":changed?"CHANGED":"PRESENT";
            row["useReviewed"]=kind=="blocks"?(row["profile"]?["Usage"] as JObject)?.Value<bool?>("Reviewed")??false:row["management"]!.Value<bool?>("Reviewed")??false;
            if(changed||previous==null)row["image"]="";
            rows.Add(row);
            }
        }
        foreach(var previous in old.Where(r=>!seen.Contains(r.Value<string>("resourceId")!)))
        {
            var row=(JObject)previous.DeepClone();row["published"]=true;row["liveStatus"]="MISSING";
            row["useReviewed"]=kind=="blocks"?(row["profile"]?["Usage"] as JObject)?.Value<bool?>("Reviewed")??false:row["management"]!.Value<bool?>("Reviewed")??false;
            rows.Add(row);
        }
        output["items"]=rows;output["live"]=true;output["snapshotId"]=capture["id"]?.DeepClone();output["readMode"]=capture["readMode"]?.DeepClone();return output;
    }
}
