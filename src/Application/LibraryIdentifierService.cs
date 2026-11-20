using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

public sealed class LibraryIdentifierItem
{
 public string Kind {get;set;}="";
 public string SourceId {get;set;}="ISO_QUANGANH";
 public string ResourceId {get;set;}="";
 public string Id {get;set;}="";
 public string Code {get;set;}="";
 public string Name {get;set;}="";
 public string Status {get;set;}="PENDING_SYNC";
}
public sealed class LibraryIdentifierService(InstallConfig config,LibrarySyncService sync)
{
 public object Ensure()
 {
  var state=JObject.FromObject(sync.State())["items"]!.ToObject<List<LibrarySyncItem>>()!;
  // All saved sync identities remain reserved, including entries later removed from the active catalog.
  var registry=new LibraryResourceCodes(config.ContentRoot);
  var index=registry.Ensure(state.Where(i=>!i.Draft).Select(i=>new LibraryResourceCode{SourceId="ISO_QUANGANH",Kind=i.Kind,Id=i.Id,Name=i.Name}));
  var items=new List<LibraryIdentifierItem>();var store=new ContentStore(config.ContentRoot);var template=store.Template("ISO_QUANGANH");
  foreach(var block in template.Blocks)Add("blocks",block.Id,block.NativeSource?.BlockName??block.Id,"");
  foreach(var layer in template.Layers)Add("layers",layer.Name,layer.Name,"");
  var styles=new StyleLibraryService(config);
  foreach(string kind in new[]{"texts","dims"})foreach(var resource in styles.Read(kind)["items"]!.OfType<JObject>())Add(kind,resource.Value<string>("id")!,resource.Value<string>("name")!,"");
  foreach(var hatch in new HatchLibraryService(config).Read()["items"]!.OfType<JObject>())Add("hatches",hatch.Value<string>("id")!,hatch.Value<string>("pattern")??"",hatch.Value<string>("handle")??"");
  return new {revision=index.Revision,sourceId="ISO_QUANGANH",items};

  void Add(string kind,string resourceId,string name,string handle)
  {
   var matches=state.Where(i=>!i.Draft&&i.Kind==kind&&(kind=="hatches"?resourceId.StartsWith(i.Id,StringComparison.OrdinalIgnoreCase)||handle!=""&&i.Handle.Equals(handle,StringComparison.OrdinalIgnoreCase):i.Name==name)).ToList();
   var item=new LibraryIdentifierItem{Kind=kind,ResourceId=resourceId,Name=name};
   if(matches.Count==1){var entry=index.Items.Single(i=>i.Kind==kind&&i.Id==matches[0].Id&&i.SourceId=="ISO_QUANGANH");item.Id=entry.Id;item.Code=entry.Code;item.Status="IDENTIFIED";}
   else if(matches.Count>1)item.Status="AMBIGUOUS";
   items.Add(item);
  }
 }
}
