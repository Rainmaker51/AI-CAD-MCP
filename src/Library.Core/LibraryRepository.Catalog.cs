using System;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.Geometry;
using Newtonsoft.Json.Linq;

namespace AICad.Application;
public partial class LibraryRepository
{
    protected static string RelativePath(string root,string path)
    {
        root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        path=Path.GetFullPath(path);
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new CadException("LIBRARY_PATH","Đường dẫn nằm ngoài thư mục quản lý.");
        return Uri.UnescapeDataString(new Uri(root).MakeRelativeUri(new Uri(path)).ToString()).Replace('/',Path.DirectorySeparatorChar);
    }
    public JObject CatalogView(string kind)
    {
        if(kind is not ("hatches" or "texts" or "dims"))throw new CadException("INVALID_KIND",kind);
        var raw=Raw(kind=="hatches"?"Hatches":"Styles");
        var items=(JArray)(raw[kind=="hatches"?"items":kind]??new JArray()).DeepClone();
        foreach(var item in items.OfType<JObject>())item["management"]=JObject.FromObject(Catalog.Label(kind,item.Value<string>("id")!));
        return new JObject{["version"]=raw["version"]?.DeepClone(),["sourceHash"]=raw["sourceHash"]?.DeepClone(),["sourceName"]=raw["sourceName"]?.DeepClone(),["items"]=items,["groups"]=JArray.FromObject(Catalog.Groups(kind))};
    }
    void PublishConfiguredTemplate(TemplateProfile template)
    {
        Catalog.Available("templates",template.Id);Validation.Template(template);
        foreach(string id in Store.Ids("skills").Where(id=>!WarehousePaths.IsRetired(config.ContentRoot,"skills",id)&&!Catalog.Deleted("skills",id)))
        {
            var skill=Store.Skill(id);if(skill.TemplateId!=template.Id)continue;
            try{if(skill.Recipe!=null)DraftingRules.Recipe(skill.Recipe,template);foreach(var operation in skill.Workflow)Validation.Operation(operation,template,true);}
            catch(CadException error){throw new CadException("DEPENDENCY_IMPACT","Skill "+id+": "+error.Message);}
        }
        foreach(string hash in template.Blocks.Where(b=>b.NativeSource!=null).Select(b=>b.NativeSource!.FileHash).Concat(template.Layers.Where(l=>!string.IsNullOrEmpty(l.Appearance?.SourceHash)).Select(l=>l.Appearance!.SourceHash)).Distinct())
        {
            string asset=Path.Combine(WarehousePaths.Folder(config.ContentRoot,"content-assets"),hash+".dwg");
            if(!File.Exists(asset)||Json.FileHash(asset)!=hash)throw new CadException("MISSING_ASSET","Thiếu hoặc thay đổi DWG nguồn thư viện.");
        }
        Store.Publish("templates",template.Id,template.Version,template);
    }
}
