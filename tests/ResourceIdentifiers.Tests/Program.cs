using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AICad.Application;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
void Check(bool condition,string message){if(!condition)throw new Exception(message);}
string root=Path.Combine(Path.GetTempPath(),"resource-codes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
Json.Atomic(Path.Combine(root,"warehouse.json"),new{schemaVersion=1});Directory.CreateDirectory(Path.Combine(root,"Skills"));Directory.CreateDirectory(Path.Combine(root,"Templates"));
Directory.CreateDirectory(Path.Combine(root,"CAD","Originals"));File.WriteAllText(Path.Combine(root,"CAD","Originals",CadSourceFile.BaseLibraryName),"Not opened: identifier fixture");
var registry=new LibraryResourceCodes(root);
var seeds=LibraryResourceCodes.Kinds.Select((kind,i)=>new LibraryResourceCode{Kind=kind,SourceId="ISO_QUANGANH",Id=(i+1).ToString("x32"),Name="Before rename"}).ToList();
var first=registry.Ensure(seeds);var original=first.Items.ToDictionary(i=>i.Id,i=>i.Code);
Check(first.Items.Count==5&&first.Items.Select(i=>i.Code).Distinct().Count()==5,"Unique codes for all five kinds");
string before=Json.FileHash(registry.FilePath);registry.Ensure(seeds.AsEnumerable().Reverse());Check(Json.FileHash(registry.FilePath)==before,"Stable repeat/order, no rewrite");
foreach(var seed in seeds)seed.Name="After rename";
var renamed=registry.Ensure(seeds);Check(renamed.Items.All(i=>original[i.Id]==i.Code),"Renaming must preserve stable ID/code");
var added=new LibraryResourceCode{Kind="blocks",SourceId="ISO_QUANGANH",Id="f".PadLeft(32,'0'),Name="Before rename"};
var next=registry.Ensure(new[]{added});Check(next.Items.Count==6&&next.Items.Single(i=>i.Id==added.Id).Code!="BLK-0001","Removed entries stay reserved; new identity cannot reuse code");
try{registry.Ensure(new[]{added,added});throw new Exception("Duplicate accepted");}catch(CadException){}
try{registry.Ensure(new[]{new LibraryResourceCode{Kind="blocks",SourceId="../outside",Id=Guid.NewGuid().ToString("N")}});throw new Exception("Traversal accepted");}catch(CadException){}
var saved=registry.Read();saved.Items[0].Code=saved.Items[1].Code;Json.Atomic(registry.FilePath,saved);try{registry.Read();throw new Exception("Corrupt code list accepted");}catch(CadException){}Json.Atomic(registry.FilePath,next);
// Map old metadata IDs and names to the same source object after a CAD rename and sync.
var store=new ContentStore(root);var template=new TemplateProfile{Id="ISO_QUANGANH",Version="1.0.0",Name="Test",Layers=new(){new(){Name="Before rename"}},Blocks=new(){new(){Id="ASSET",NativeSource=new(){BlockName="Before rename"}}}};
store.Publish("templates",template.Id,template.Version,template);
var state=new LibrarySyncState{Items=seeds.Select(s=>new LibrarySyncItem{Kind=s.Kind,Id=s.Id,Name="Before rename",Handle=s.Kind=="hatches"?"ABC":""}).ToList()};
void SaveState()=>Json.Atomic(Path.Combine(root,"Catalog","sync","state.json"),state);
SaveState();
void PublishRaw(string folder,object value){string f=Path.Combine(root,folder,"ISO_QUANGANH","1.0.0","catalog.json");Json.Atomic(f,value);Json.Atomic(Path.Combine(root,folder,"ISO_QUANGANH","active.json"),new{version="1.0.0",checksum=Json.FileHash(f)});}
PublishRaw("Styles",new{version="1.0.0",texts=new[]{new{id="TEXT_METADATA",name="Before rename"}},dims=new[]{new{id="DIM_METADATA",name="Before rename"}}});
PublishRaw("Hatches",new{version="1.0.0",items=new[]{new{id=seeds.Single(s=>s.Kind=="hatches").Id+new string('a',32),pattern="ANSI31",handle="ABC"}}});
var config=new InstallConfig{ContentRoot=root,DataRoot=Path.Combine(root,"data")};var service=new LibraryIdentifierService(config,new LibrarySyncService(config));
JArray Items()=>JObject.FromObject(service.Ensure())["items"]!.ToObject<JArray>()!;
var items=Items();Check(items.Count==5&&items.All(i=>i.Value<string>("Status")=="IDENTIFIED"),"Map all five catalog types through sync IDs");
var blockCode=items.Single(i=>i.Value<string>("Kind")=="blocks").Value<string>("Code");
state.Items.Single(i=>i.Kind=="blocks").Name="Renamed in CAD";SaveState();template.Version="1.0.1";template.Blocks[0].Id="RENAMED_ASSET";template.Blocks[0].NativeSource!.BlockName="Renamed in CAD";store.Publish("templates",template.Id,template.Version,template);
var after=Items().Single(i=>i.Value<string>("Kind")=="blocks");Check(after.Value<string>("Code")==blockCode&&after.Value<string>("Id")==seeds.Single(s=>s.Kind=="blocks").Id,"CAD rename preserves code while metadata ID changes");
state.Items.RemoveAll(i=>i.Kind=="blocks");SaveState();Check(Items().Single(i=>i.Value<string>("Kind")=="blocks").Value<string>("Status")=="PENDING_SYNC","No name-based invented identity for unsynced resource");
Console.WriteLine("PASS: five kind prefixes; persisted identity/code; rename/group-independent aliases; repeat/no rewrite; no code reuse; duplicate/path/corruption guards; legacy metadata mapping; CAD rename with changed asset ID; unsynced resources not guessed.");
