using AICad.Application;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
using System.Reflection;

if(args.Length==6&&args[0]=="--exclude-outside-hatches")
{
    var liveConfig=new InstallConfig{ContentRoot=Path.GetFullPath(args[1]),DataRoot=Path.GetFullPath(args[2])};
    var repository=new LibraryRepository(liveConfig);var snapshot=repository.LiveCapture(args[3]);
    if(snapshot.Value<string>("readMode")!="SAVED_FILE"||QaUpdateJournal.SharedHash(snapshot.Value<string>("source")!)!=snapshot.Value<string>("snapshotHash"))throw new Exception("Saved source no longer matches the inspected snapshot");
    if(new QaUpdateJournal(liveConfig).Read()!=null)throw new Exception("Pending QA operation; finish it first");
    var state=JObject.FromObject(repository.State());var active=state["items"]!.ToObject<List<LibrarySyncItem>>()!;
    var targets=snapshot["items"]!.ToObject<List<LibrarySyncItem>>()!.Where(i=>i.Kind=="hatches"&&snapshot["presence"]?[i.Id]?.Value<bool?>("inScope")==false&&active.Any(a=>a.Id==i.Id)).ToList();
    if(targets.Count!=10)throw new Exception("Expected the ten reviewed outside-frame hatches; found "+targets.Count);
    repository.CheckQaRemoval(new JObject{["revision"]=state["revision"]!.DeepClone(),["ids"]=JArray.FromObject(targets.Select(i=>i.Id))});
    var review=repository.RegisterLive(args[3]);
    if(args[4]!="--apply"){Console.WriteLine(JArray.FromObject(targets.Select(i=>new{i.Id,i.Name})));return;}
    var cliPrepared=JObject.FromObject(repository.PrepareLiveApproval(new JObject{["reviewId"]=args[3],["savedId"]=args[3]}));
    var cliChoices=new JObject();foreach(var target in targets)cliChoices[target.Id]="exclude";
    string before=QaUpdateJournal.SharedHash(snapshot.Value<string>("source")!);
    var cliResult=JObject.FromObject(await repository.ApproveCad(new JObject{["id"]=cliPrepared["id"]!.DeepClone(),["choices"]=cliChoices},CancellationToken.None));
    var remaining=JObject.FromObject(repository.State())["items"]!.ToObject<List<LibrarySyncItem>>()!;
    if(targets.Any(t=>remaining.Any(i=>i.Id==t.Id))||QaUpdateJournal.SharedHash(snapshot.Value<string>("source")!)!=before)throw new Exception("Exclusion verification failed");
    Json.Atomic(args[5],new{passed=true,removed=targets.Select(i=>new{i.Id,i.Name}),cliResult,sourceUnchanged=true,remainingHatches=remaining.Count(i=>i.Kind=="hatches")});
    Console.WriteLine("EXCLUDED="+targets.Count+" REMAINING="+remaining.Count(i=>i.Kind=="hatches")+" DWG_UNCHANGED=true");return;
}

if(args.Length==5&&args[0]=="--audit-warehouse")
{
    var auditConfig=new InstallConfig{ContentRoot=Path.GetFullPath(args[1]),DataRoot=Path.GetFullPath(args[2])};
    string warehouse=WarehousePaths.Resolve(auditConfig.ContentRoot),catalogFolder=Path.Combine(warehouse,"Catalog");
    if(File.Exists(Path.Combine(catalogFolder,"sync","pending.json")))throw new Exception("Pending transaction: do not audit or recover implicitly");
    var paths=Directory.GetFiles(catalogFolder,"*.json",SearchOption.AllDirectories).Concat(new[]{Path.Combine(warehouse,"Versions","templates","ISO_QUANGANH","active.json"),Path.Combine(warehouse,"Styles","ISO_QUANGANH","active.json"),Path.Combine(warehouse,"Hatches","ISO_QUANGANH","active.json")}).Where(File.Exists).Distinct().OrderBy(p=>p).ToArray();
    string Digest()=>Json.Hash(paths.Select(p=>new{path=Path.GetRelativePath(warehouse,p),hash=Json.FileHash(p)}).ToArray());
    string before=Digest();var repository=new LibraryRepository(auditConfig);var service=new QaLibraryService(auditConfig,repository,new LibraryIdentifierService(auditConfig,repository));
    var capture=repository.LiveCapture(args[3]);var counts=new JArray();
    foreach(string kind in LibrarySyncRules.Kinds)
    {
        var published=JObject.FromObject(service.Read(kind));var auditLive=service.ReadLive(kind,capture);
        counts.Add(new JObject{["kind"]=kind,["published"]=published["items"]!.Count(),["groups"]=published["groups"]!.Count(),["live"]=auditLive["items"]!.Count(),["publishedPreserved"]=published["items"]!.OfType<JObject>().All(p=>auditLive["items"]!.OfType<JObject>().Any(l=>l.Value<string>("resourceId")==p.Value<string>("resourceId")))});
        if(kind=="blocks")
        {
            var lamp=auditLive["items"]!.OfType<JObject>().SingleOrDefault(r=>r.Value<string>("resourceId")=="QA_DenNgu");
            Console.WriteLine("QA_DenNgu status: "+lamp?.Value<string>("liveStatus"));
            if(lamp!=null&&lamp.Value<string>("liveStatus") is not ("DEFINITION_ONLY" or "MISSING"))throw new Exception("Deleted lamp unexpectedly active");
        }
    }
    string after=Digest();if(after!=before||counts.OfType<JObject>().Any(c=>!c.Value<bool>("publishedPreserved")))throw new Exception("Catalog audit changed or lost data");
    Json.Atomic(args[4],new{passed=true,readOnly=true,catalogUnchanged=true,digest=after,kinds=counts});Console.WriteLine(counts);return;
}

if(args.Length>1&&(args[0]=="--verify-snapshot"||args[0]=="--verify-native"))
{
    var signatureType=typeof(QaLibraryService).Assembly.GetType("AICad.Library.LibraryLiveSignature",true)!;
    foreach(string file in args.Skip(1))
    {
        var snapshot=Json.Read<JObject>(File.ReadAllText(file));var native=snapshot["items"]!.ToObject<List<LibrarySyncItem>>()!;
        string method=args[0]=="--verify-native"?"Compute":"Framework150";
        string legacy=(string)signatureType.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{native,snapshot["presence"]!})!;
        bool valid=legacy==snapshot.Value<string>("signature");Console.WriteLine(Path.GetFileName(Path.GetDirectoryName(file))+": "+method+" signature match="+valid);
        if(!valid)throw new Exception("Legacy cross-runtime snapshot mismatch");
    }
    return;
}

void Check(bool value, string message) { if (!value) throw new Exception(message); }
async Task Reject(Func<Task> work, string code) { try { await work(); } catch (CadException e) when (e.Code == code) { return; } throw new Exception("Expected " + code); }
string root = Path.Combine(Environment.GetEnvironmentVariable("QALIB_TEST_PARENT")??Path.GetTempPath(), "qa-library-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var config = new InstallConfig { ContentRoot = root, DataRoot = Path.Combine(root, "data") };
Directory.CreateDirectory(config.DataRoot);
var store = new ContentStore(root);
string source = Path.Combine(root, "CAD", "Originals", CadSourceFile.BaseLibraryName);
Directory.CreateDirectory(Path.GetDirectoryName(source)!); File.WriteAllText(source, "fixture saved DWG bytes");
string sourceHash = Json.FileHash(source); string assets = WarehousePaths.Folder(root, "content-assets"); Directory.CreateDirectory(assets); File.Copy(source, Path.Combine(assets, sourceHash + ".dwg"));
var layer = new LibrarySyncItem { Id = Guid.NewGuid().ToString("N"), Kind = "layers", Name = "QA_TEST", Values = JObject.FromObject(new { color = 7, lineWeight = 25, lineType = "Continuous", plottable = true }) };
var block = new LibrarySyncItem { Id = Guid.NewGuid().ToString("N"), Kind = "blocks", Name = "QA_BLOCK", Values = new() };
var text = new LibrarySyncItem { Id = Guid.NewGuid().ToString("N"), Kind = "texts", Name = "QA_TEXT", Handle = "A", Values = JObject.FromObject(new { font = "tahoma.ttf", height = 0, widthFactor = 1.0 }) };
var dim = new LibrarySyncItem { Id = Guid.NewGuid().ToString("N"), Kind = "dims", Name = "QA_DIM", Handle = "B", Values = JObject.FromObject(new { textStyle = "QA_TEXT", textHeight = 2.5, scale = 1 }) };
var hatch = new LibrarySyncItem { Id = Guid.NewGuid().ToString("N"), Kind = "hatches", Name = "SOLID", Handle = "C", Values = JObject.FromObject(new { pattern = "SOLID", type = "PreDefined", scale = 1, angle = 0, layer = "QA_TEST", color = 256, spacing = 1, doublePattern = false }) };
var template = new TemplateProfile { Id = "ISO_QUANGANH", Version = "1.0.0", Layers = new() { new() { Name = layer.Name, Color = 7, Appearance = new() { LineWeight = 25, LineType = "Continuous", Plottable = true } } }, Blocks = new() { new() { Id = "QA_BLOCK", NativeSource = new() { BlockName = block.Name, FileHash = sourceHash, Min = new(0,0), Max = new(1000,1000) } } } };
store.Publish("templates", template.Id, template.Version, template);
void Catalog(string folder, object content) { string dir = Path.Combine(root, folder, "ISO_QUANGANH", "1.0.0"); Directory.CreateDirectory(dir); Json.Atomic(Path.Combine(dir, "catalog.json"), content); Json.Atomic(Path.Combine(root, folder, "ISO_QUANGANH", "active.json"), new { version = "1.0.0", checksum = Json.FileHash(Path.Combine(dir, "catalog.json")) }); }
JObject Entry(LibrarySyncItem i) { var value = (JObject)i.Values.DeepClone(); value["id"] = i.Kind == "hatches" ? i.Id + new string('a', 32) : Json.Hash(i.Name); value["name"] = i.Name; value["handle"] = i.Handle; return value; }
Catalog("Styles", new { version = "1.0.0", sourceHash, sourceName = Path.GetFileName(source), texts = new[] { Entry(text) }, dims = new[] { Entry(dim) } });
Catalog("Hatches", new { version = "1.0.0", sourceHash, sourceName = Path.GetFileName(source), items = new[] { Entry(hatch) } });
var items = new List<LibrarySyncItem> { layer, block, text, dim, hatch };
var initial = new LibrarySyncState { Items = items.Select(i => i.Copy()).ToList() };
foreach (var i in items) initial.Baselines[i.Id] = new() { App = i.Signature(), Cad = i.Signature(), AppContent = i.ContentSignature(), CadContent = i.ContentSignature(), AppGroup = "", CadGroup = "" };
string stateFile = Path.Combine(root, "Catalog", "sync", "state.json"); Json.Atomic(stateFile, initial);
var sync = new LibrarySyncService(config); var qa = new QaLibraryService(config, sync, new LibraryIdentifierService(config, sync));
var metadata = new LibraryCatalog(root);
foreach (string kind in LibrarySyncRules.Kinds)
{
    var view = JObject.FromObject(qa.Read(kind)); var row = view["items"]!.Single();
    Check(row.Value<string>("id") != "" && row.Value<string>("code") != "", "Missing stable ID/code: " + kind);
    Check(view.Value<string>("source") == source, "Wrong source for " + kind);
    var group = JObject.FromObject(qa.Group(JObject.FromObject(new { kind, name = "Parent", parentId = "" })));
    var child = JObject.FromObject(qa.Group(JObject.FromObject(new { kind, name = "Child", parentId = group.Value<string>("Id") })));
    var request = JObject.FromObject(new { kind, metadataId = row.Value<string>("metadataId"), catalogRevision = view.Value<int>("revision"), revision = 0, name = "Display " + kind, purpose = "Fixture purpose", notes = "Fixture notes", groupId = child.Value<string>("Id"), reviewed = true, action = "update" });
    qa.Manage(request);
    await Reject(() => { qa.Manage(request); return Task.CompletedTask; }, "RESOURCE_CHANGED");
    var updated = JObject.FromObject(qa.Read(kind))["items"]!.Single();
    Check(updated.Value<string>("id") == row.Value<string>("id") && updated.Value<string>("code") == row.Value<string>("code"), "Management rename changed identity");
    Check(updated["management"]!.Value<bool>("Reviewed"), "Review flag lost");
    qa.Group(JObject.FromObject(new { kind, id = group.Value<string>("Id"), action = "remove" }));
    Check(metadata.Groups(kind).Single().ParentId == "", "Deleting parent lost subgroup");
}
// Inspection artifacts are private test fixtures; no Core process or user DWG is touched.
string AppHash() => (string)typeof(LibrarySyncService).GetMethod("AppHash", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(sync, null)!;
string Review(List<LibrarySyncItem> native)
{
    string id = Guid.NewGuid().ToString("N"), job = Path.Combine(config.DataRoot, "library-sync", id); Directory.CreateDirectory(job); File.Copy(source, Path.Combine(job, "source.dwg")); Json.Atomic(Path.Combine(job, "review.json"), new { sourceHash = Json.FileHash(source), appHash = AppHash(), native }); return id;
}
var current = JObject.FromObject(sync.State())["items"]!.ToObject<List<LibrarySyncItem>>()!;
var changed = current.Select(i => i.Copy()).ToList(); changed.Single(i => i.Id == layer.Id).Values["color"] = 3; changed.Single(i => i.Id == text.Id).Name = "QA_TEXT_RENAMED";
string reviewId = Review(changed);
var choices = new JObject { [layer.Id] = "cad" };
var result = JObject.FromObject(await sync.ApproveCad(new() { ["id"] = reviewId, ["choices"] = choices }, CancellationToken.None));
Check(!result.Value<bool>("sourceUpdated") && Json.FileHash(source) == sourceHash, "Approval rewrote source DWG");
Check(store.Template("ISO_QUANGANH").Layers.Single().Color == 3, "Selected CAD edit not published");
Check(new StyleLibraryService(config).Read("texts")["items"]!.Single().Value<string>("name") == "QA_TEXT", "Unselected change leaked into publication");
await Reject(async () => await sync.ApproveCad(new() { ["id"] = reviewId, ["choices"] = choices }, CancellationToken.None), "RESOURCE_CHANGED");
// Stable identity follows a native rename; metadata moves from the old key to new key.
current = JObject.FromObject(sync.State())["items"]!.ToObject<List<LibrarySyncItem>>()!;
var oldCode = JObject.FromObject(qa.Read("texts"))["items"]!.Single().Value<string>("code");
changed = current.Select(i => i.Copy()).ToList(); changed.Single(i => i.Id == text.Id).Name = "QA_TEXT_RENAMED";
reviewId = Review(changed); await sync.ApproveCad(new() { ["id"] = reviewId, ["choices"] = new JObject { [text.Id] = "cad" } }, CancellationToken.None);
var renamed = JObject.FromObject(qa.Read("texts"))["items"]!.Single(); Check(renamed.Value<string>("code") == oldCode, "Native rename changed stable code");
Check(new StyleLibraryService(config).Read("dims")["items"]!.Single().Value<string>("textStyle")=="QA_TEXT_RENAMED","Dim reference did not follow the renamed Text style");
current=JObject.FromObject(sync.State())["items"]!.ToObject<List<LibrarySyncItem>>()!;changed=current.Select(i=>i.Copy()).ToList();changed.Single(i=>i.Id==hatch.Id).Values["scale"]=300;
string templateVersionBefore=store.Template("ISO_QUANGANH").Version;
reviewId=Review(changed);await sync.ApproveCad(new(){["id"]=reviewId,["choices"]=new JObject{[hatch.Id]="cad"}},CancellationToken.None);
Check(new HatchLibraryService(config).Read()["items"]!.Single().Value<double>("scale")==300,"Hatch scale was not published");
Check(store.Template("ISO_QUANGANH").Version==templateVersionBefore,"Hatch-only approval created a duplicate Template version");
// A stale scan cannot publish after another CAD save.
reviewId = Review(changed); File.AppendAllText(source, "saved again");
await Reject(async () => await sync.ApproveCad(new() { ["id"] = reviewId, ["choices"] = choices }, CancellationToken.None), "RESOURCE_CHANGED");
Check(!LibraryEditAuthority.Active(config), "Authority activated merely by reading"); qa.Activate(); Check(LibraryEditAuthority.Active(config), "Authority activation failed");
Check(LibraryEditAuthority.IsLibraryWrite("/api/library-sync/apply", "") && !LibraryEditAuthority.IsLibraryWrite("/api/qa-library/approve", "") && !LibraryEditAuthority.IsLibraryWrite("/api/library-management/update", "skills"), "Authority routing unsafe");
// Live CAD is a projection, never an implicit publication or a source write.
var legacyTemplate=store.Template("ISO_QUANGANH");legacyTemplate.Blocks.Single().Usage=new BlockUsage{Role="native",Reviewed=true};legacyTemplate.Version="1.9.0";store.Publish("templates",legacyTemplate.Id,legacyTemplate.Version,legacyTemplate);
current=JObject.FromObject(sync.State())["items"]!.ToObject<List<LibrarySyncItem>>()!;
JObject Capture(List<LibrarySyncItem> native,JObject presence,string mode="OPEN_DOCUMENT")
{
    string id=Guid.NewGuid().ToString("N"),folder=Path.Combine(config.DataRoot,"library-sync",id);Directory.CreateDirectory(folder);File.Copy(source,Path.Combine(folder,"source.dwg"));File.WriteAllText(Path.Combine(folder,".qa-native-snapshot"),"test fixture");
    string signature=Json.Hash(new{items=native.OrderBy(i=>i.Id).Select(i=>new{i.Id,i.Kind,i.Name,i.Group,i.Purpose,i.Notes,i.Values}),presence=new JObject(presence.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new JProperty(p.Name,p.Value.DeepClone())))});
    var c=new JObject{["id"]=id,["source"]=source,["readMode"]=mode,["items"]=JArray.FromObject(native),["presence"]=presence,["signature"]=signature,["snapshotHash"]=Json.FileHash(source)};Json.Atomic(Path.Combine(folder,"snapshot.json"),c);return c;
}
var presence=new JObject{[block.Id]=new JObject{["samplePresent"]=false,["definitionPresent"]=true}};
changed=current.Select(i=>i.Copy()).ToList();changed.Single(i=>i.Id==layer.Id).Values["color"]=4;
var live=Capture(changed,presence);var registered=sync.RegisterLive(live.Value<string>("id")!);
Check(registered["differences"]!.Single(d=>d.Value<string>("Id")==block.Id).Value<string>("Status")=="CAD_SAMPLE_MISSING","Missing block sample mistaken for an active sample");
var projected=qa.ReadLive("blocks",sync.LiveCapture(live.Value<string>("id")!));
Check(projected["items"]!.Single().Value<string>("liveStatus")=="DEFINITION_ONLY"&&projected["items"]!.Single().Value<bool>("useReviewed"),"Legacy usage review or missing-sample status lost");
// Multiple usage presets may share the same native definition and public ID.
// They remain separate rows; a null Usage record must not abort the entire view.
var presetView=JObject.FromObject(qa.Read("blocks"));var firstPreset=(JObject)presetView["items"]![0]!;firstPreset["profile"]!["Usage"]=JValue.CreateNull();
var secondPreset=(JObject)firstPreset.DeepClone();secondPreset["resourceId"]="SECOND_PRESET";secondPreset["metadataId"]="second-preset";secondPreset["management"]!["GroupId"]="second-group";secondPreset["profile"]!["Usage"]=new JObject{["Reviewed"]=true,["Values"]=new JObject{["Visibility"]="Wardrobe"}};
((JArray)presetView["items"]!).Add(secondPreset);
var projectionType=typeof(QaLibraryService).Assembly.GetType("AICad.Library.LibraryLiveProjection",true)!;
var presetRows=((JObject)projectionType.GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{presetView,live})!)["items"]!.OfType<JObject>().ToList();
Check(presetRows.Count==2&&presetRows.Select(r=>r.Value<string>("resourceId")).Distinct().Count()==2,"Shared-definition presets were lost or merged");
Check(!presetRows[0].Value<bool>("useReviewed")&&presetRows[1].Value<bool>("useReviewed")&&presetRows[1]["management"]!.Value<string>("GroupId")=="second-group","Preset usage, review or group was overwritten");
var projectedLayer=qa.ReadLive("layers",live);Check(projectedLayer["items"]!.Single()["profile"]!.Value<int>("color")==4&&store.Template("ISO_QUANGANH").Layers.Single().Color==3,"Live change was not shown or was published implicitly");
var missing=qa.ReadLive("blocks",Capture(changed.Where(i=>i.Id!=block.Id).ToList(),new()));Check(missing["items"]!.Single().Value<string>("liveStatus")=="MISSING","Absent definition not distinguished");
var fresh=new LibrarySyncItem{Id=Guid.NewGuid().ToString("N"),Kind="blocks",Name="NEW_BLOCK",Values=new()};
var added=qa.ReadLive("blocks",Capture(changed.Append(fresh).ToList(),presence));Check(added["items"]!.Single(i=>i.Value<string>("id")==fresh.Id).Value<bool>("published")==false,"New CAD block falsely marked published");
var outsidePresence=(JObject)presence.DeepClone();outsidePresence[block.Id]!["inScope"]=false;
var outsideCapture=Capture(changed,outsidePresence,"SAVED_FILE");var outsideReview=sync.RegisterLive(outsideCapture.Value<string>("id")!);
Check(outsideReview["differences"]!.Single(d=>d.Value<string>("Id")==block.Id).Value<string>("Status")=="CAD_OUT_OF_SCOPE","Outside scope became missing/removable");
Check(qa.ReadLive("blocks",outsideCapture)["items"]!.Single().Value<string>("liveStatus")=="OUT_OF_SCOPE","Outside scope hidden from live status");
var outsidePrepared=JObject.FromObject(sync.PrepareLiveApproval(new(){["reviewId"]=outsideCapture["id"]!.DeepClone(),["savedId"]=outsideCapture["id"]!.DeepClone()}));
foreach(string choice in new[]{"cad","remove"})await Reject(()=>sync.Apply(new(){["id"]=outsidePrepared["id"]!.DeepClone(),["choices"]=new JObject{[block.Id]=choice}},CancellationToken.None),"LIBRARY_OUT_OF_SCOPE");
Check(JObject.FromObject(sync.State())["items"]!.Any(i=>i.Value<string>("Id")==block.Id),"Outside resource removed from published state");
Console.WriteLine("PASS: outside-frame projection, default retention and publication/removal guards");
var savedLive=Capture(changed,presence,"SAVED_FILE");var prepared=JObject.FromObject(sync.PrepareLiveApproval(new(){["reviewId"]=live["id"]!.DeepClone(),["savedId"]=savedLive["id"]!.DeepClone()}));
Check(prepared.Value<string>("id")==savedLive.Value<string>("id"),"Saved equivalent snapshot rejected");
string? legacyFile=Environment.GetEnvironmentVariable("QA_LEGACY_CAPTURE");
if(!string.IsNullOrEmpty(legacyFile))
{
    // Replay the numeric payload that failed in Framework 1.5.0, only in this
    // disposable fixture. No actual source DWG or warehouse metadata is written.
    var legacy=Json.Read<JObject>(File.ReadAllText(legacyFile));string id=Guid.NewGuid().ToString("N"),folder=Path.Combine(config.DataRoot,"library-sync",id);Directory.CreateDirectory(folder);
    legacy["id"]=id;legacy["source"]=source;legacy["snapshotHash"]=Json.FileHash(source);File.Copy(source,Path.Combine(folder,"source.dwg"));File.WriteAllText(Path.Combine(folder,".qa-native-snapshot"),"Legacy regression fixture");Json.Atomic(Path.Combine(folder,"snapshot.json"),legacy);
    var replay=sync.LiveCapture(id);Check(replay["items"]!.Count()==legacy["items"]!.Count(),"Legacy native snapshot rejected");
    var replayReview=sync.RegisterLive(id);Check(replayReview["differences"] is JArray,"Legacy native live view did not register");
    Check(qa.ReadLive("blocks",replay)["groups"]!.Count()==metadata.Groups("blocks").Count,"Legacy live replay lost catalog groups");
    Console.WriteLine("PASS: original failing Framework 1.5.0 snapshot replayed without a warehouse write");
}
var newer=changed.Select(i=>i.Copy()).ToList();newer.Single(i=>i.Id==layer.Id).Values["color"]=5;var stale=Capture(newer,presence,"SAVED_FILE");
await Reject(()=>{sync.PrepareLiveApproval(new(){["reviewId"]=live["id"]!.DeepClone(),["savedId"]=stale["id"]!.DeepClone()});return Task.CompletedTask;},"RESOURCE_CHANGED");
// Installed consumers block removal; another tool's unselected settings do not.
Json.Atomic(Path.Combine(root,"Tools","catalog.json"),new CadToolIndex{StorageVersion=2,Items=new(){new(){Id="rainmaker-axis",Name="Axis fixture",Kind="dotnet",Enabled=true}}});
string settingsFile=Path.Combine(root,"Catalog","addin-settings","rainmaker-axis.json");Json.Atomic(settingsFile,new{BayDimId=dim.Id});
var noDim=current.Where(i=>i.Id!=dim.Id).ToList();string removal=Review(noDim);
await Reject(async()=>await sync.ApproveCad(new(){["id"]=removal,["choices"]=new JObject{[dim.Id]="remove"}},CancellationToken.None),"DEPENDENCY_IMPACT");
File.Delete(settingsFile);
// A merge preflight may remap registered resource IDs, but deletion cannot bypass consumers.
current=JObject.FromObject(sync.State())["items"]!.ToObject<List<LibrarySyncItem>>()!;
var mergeTarget=current.Single(i=>i.Id==hatch.Id).Copy();mergeTarget.Id=Guid.NewGuid().ToString("N");mergeTarget.Handle="D";mergeTarget.Name="QA_MERGE_TARGET";
var withTarget=current.Concat(new[]{mergeTarget}).ToList();string addTarget=Review(withTarget);
await sync.ApproveCad(new(){["id"]=addTarget,["choices"]=new JObject{[mergeTarget.Id]="cad"}},CancellationToken.None);
Json.Atomic(settingsFile,new{HatchId=hatch.Id,Unrelated="KEEP"});
var mergeState=JObject.FromObject(sync.State());int mergeRevision=mergeState.Value<int>("revision");
await Reject(()=>{sync.CheckQaRemoval(new(){["revision"]=mergeRevision,["ids"]=new JArray(hatch.Id)});return Task.CompletedTask;},"DEPENDENCY_IMPACT");
sync.CheckQaRemoval(new(){["revision"]=mergeRevision,["ids"]=new JArray(hatch.Id),["target"]=mergeTarget.Id});
var remapRequest=new JObject{["operation"]=Guid.NewGuid().ToString("N"),["ids"]=new JArray(hatch.Id),["target"]=mergeTarget.Id};
sync.RemapQaConsumers(remapRequest);sync.RemapQaConsumers(remapRequest);
var remapped=Json.Read<JObject>(File.ReadAllText(settingsFile));Check(remapped.Value<string>("HatchId")==mergeTarget.Id&&remapped.Value<string>("Unrelated")=="KEEP","Consumer remap lost unrelated settings");
File.Delete(settingsFile);string removeTarget=Review(current);
await sync.ApproveCad(new(){["id"]=removeTarget,["choices"]=new JObject{[mergeTarget.Id]="remove"}},CancellationToken.None);
Console.WriteLine("PASS: deletion dependency preflight, merge consumer remapping, idempotent retry and preserved unrelated settings");
// Rename the same layer repeatedly; one active entry and its stable code must survive.
string originalLayerName=JObject.FromObject(sync.State())["items"]!.ToObject<List<LibrarySyncItem>>()!.Single(i=>i.Id==layer.Id).Name;
string layerCode=JObject.FromObject(qa.Read("layers"))["items"]!.Single().Value<string>("code")!;
foreach(string nextName in new[]{"QA_LAYER_RENAME_1","QA_LAYER_RENAME_2",originalLayerName})
{
    var next=JObject.FromObject(sync.State())["items"]!.ToObject<List<LibrarySyncItem>>()!;next.Single(i=>i.Id==layer.Id).Name=nextName;
    string renameReview=Review(next);await sync.ApproveCad(new(){["id"]=renameReview,["choices"]=new JObject{[layer.Id]="cad"}},CancellationToken.None);
    var view=JObject.FromObject(qa.Read("layers"));Check(view["items"]!.Count()==1&&view["items"]![0]!.Value<string>("name")==nextName,"Layer rename left a stale/duplicate catalog entry");
    Check(view["items"]![0]!.Value<string>("code")==layerCode,"Layer rename changed stable code");
}
Console.WriteLine("PASS: repeated layer rename preserves one catalog entry, stable ID/code and dependent references");
// Durable recovery never treats an in-memory clone signature as saved-DWG proof.
var qaJournal=new QaUpdateJournal(config);byte[] sourceBeforeJournal=File.ReadAllBytes(source);
var journalStateHash=Json.FileHash(Path.Combine(root,"Catalog","sync","state.json"));
using(qaJournal.Lock())
{
    await Reject(()=>{using var second=qaJournal.Lock();return Task.CompletedTask;},"SYNC_BUSY");
    var record=qaJournal.Begin(new JObject{["rows"]=new JArray(),["sourceHash"]=QaUpdateJournal.SharedHash(source)});
    Check(!qaJournal.CanContinue(record),"PREPARED without a saved byte hash must not resume");
    File.AppendAllText(source," native CAD saved");record["afterHash"]=QaUpdateJournal.SharedHash(source);record["phase"]="CAD_SAVED";qaJournal.Save(record);
    Check(qaJournal.CanContinue(record),"Saved byte-identical source cannot resume");qaJournal.Complete(record);Check(qaJournal.Read()==null,"Completed journal was not cleared");
    var partial=qaJournal.Begin(new JObject{["rows"]=new JArray()});partial["phase"]="CATALOG_PUBLISHED";partial["afterHash"]=QaUpdateJournal.SharedHash(source);qaJournal.Save(partial);
    File.AppendAllText(source," later CAD edit");Check(!qaJournal.CanContinue(partial),"Changed source was accepted as old saved plan");
    await Reject(()=>{qaJournal.Complete(partial);return Task.CompletedTask;},"SOURCE_CHANGED");
    string currentHash=QaUpdateJournal.SharedHash(source);string archive=qaJournal.AbandonKeepingCad(JObject.FromObject(sync.State()));
    Check(qaJournal.Read()==null&&File.Exists(Path.Combine(archive,"pending-original.json")),"Cancellation lost original journal");
    Check(QaUpdateJournal.SharedHash(source)==currentHash&&QaUpdateJournal.SharedHash(Path.Combine(archive,"preserved-current.dwg"))==currentHash,"Cancellation overwrote current CAD");
    Check(Json.FileHash(Path.Combine(root,"Catalog","sync","state.json"))==journalStateHash,"Cancellation changed published state");
    var invalid=new JObject{["sourceId"]="ISO_QUANGANH",["backupRelative"]="../../foreign/before.dwg"};
    await Reject(()=>{qaJournal.Folder(invalid);return Task.CompletedTask;},"QA_JOURNAL");
}
File.WriteAllBytes(source,sourceBeforeJournal);
Console.WriteLine("PASS: durable saved-byte recovery, stale source refusal, publication checkpoint, preserved CAD/catalog, journal archive, path guard and update lock");
Console.WriteLine("PASS: live CAD projection, legacy usage review, missing sample/definition, new resource, explicit saved publication guard, stale geometry rejection, active add-in dependency guard");
Console.WriteLine("PASS: five kinds, source paths, stable codes, hierarchical groups, metadata review and conflict guards; selected CAD publication without source overwrite; native rename; stale scan rejection; explicit QA authority");
// Group-only commits preserve native resource content and resume after a partial tree write.
foreach(string groupKind in LibrarySyncRules.Kinds)
{
    var view=JObject.FromObject(qa.Read(groupKind));var beforeTree=view["groups"]!.ToObject<List<LibraryGroup>>()!;
    var row=(JObject)view["items"]![0]!;string metadataId=row.Value<string>("metadataId")!;var oldLabel=metadata.Label(groupKind,metadataId);
    string destination=Guid.NewGuid().ToString("N"),childId=Guid.NewGuid().ToString("N");
    var afterTree=beforeTree.Concat(new[]{new LibraryGroup{Id=destination,Name="Group test"},new LibraryGroup{Id=childId,Name="Child test",ParentId=destination}}).ToList();
    var groupPlan=new JObject{["kind"]=groupKind,["beforeGroups"]=JArray.FromObject(beforeTree),["afterGroups"]=JArray.FromObject(afterTree),["labelGroups"]=new JArray(new JObject{["metadataId"]=metadataId,["beforeGroupId"]=oldLabel.GroupId,["groupId"]=destination,["revision"]=oldLabel.Revision})};
    string dwgHash=Json.FileHash(source);var checkOnly=(JObject)groupPlan.DeepClone();checkOnly["checkOnly"]=true;sync.ApplyQaGroupTree(checkOnly);Check(!metadata.Groups(groupKind).Any(g=>g.Id==destination),"Preflight wrote group tree");
    Json.Atomic(Path.Combine(root,"Catalog","management",groupKind+"-groups.json"),afterTree);
    sync.ApplyQaGroupTree(groupPlan);int revision=metadata.Label(groupKind,metadataId).Revision;sync.ApplyQaGroupTree(groupPlan);var label=metadata.Label(groupKind,metadataId);
    Check(label.GroupId==destination&&label.Revision==revision&&label.Name==oldLabel.Name&&label.Purpose==oldLabel.Purpose,"Group resume changed unrelated metadata or repeated revision");
    var removedTree=afterTree.Where(g=>g.Id!=destination).Select(g=>new LibraryGroup{Id=g.Id,Name=g.Name,ParentId=g.ParentId==destination?"":g.ParentId}).ToList();
    sync.ApplyQaGroupTree(new JObject{["kind"]=groupKind,["beforeGroups"]=JArray.FromObject(afterTree),["afterGroups"]=JArray.FromObject(removedTree),["labelGroups"]=new JArray(new JObject{["metadataId"]=metadataId,["beforeGroupId"]=destination,["groupId"]="",["revision"]=revision})});
    Check(!metadata.Groups(groupKind).Any(g=>g.Id==destination)&&metadata.Groups(groupKind).Single(g=>g.Id==childId).ParentId==""&&metadata.Label(groupKind,metadataId).GroupId=="","Group deletion left dangling metadata or child");
    Check(Json.FileHash(source)==dwgHash&&JObject.FromObject(qa.Read(groupKind))["items"]!.Count()==view["items"]!.Count(),"Group-only commit changed source or removed resources");
}
Console.WriteLine("PASS: five-kind group tree commits, metadata retention, partial-write recovery, idempotent retry and child reparenting");
if(!args.Contains("--keep"))
{
    var available=JObject.FromObject(sync.State())["items"]!.ToObject<List<LibrarySyncItem>>()!;var excluded=available.Single(i=>i.Kind=="hatches");
    var exclusionPresence=new JObject{[excluded.Id]=new JObject{["inScope"]=false}};var capture=Capture(available,exclusionPresence,"SAVED_FILE");
    sync.RegisterLive(capture.Value<string>("id")!);var preparedExclusion=JObject.FromObject(sync.PrepareLiveApproval(new JObject{["reviewId"]=capture["id"]!.DeepClone(),["savedId"]=capture["id"]!.DeepClone()}));
    string beforeDwg=Json.FileHash(source);await sync.ApproveCad(new JObject{["id"]=preparedExclusion["id"]!.DeepClone(),["choices"]=new JObject{[excluded.Id]="exclude"}},CancellationToken.None);
    Check(!JObject.FromObject(sync.State())["items"]!.Any(i=>i.Value<string>("Id")==excluded.Id)&&JObject.FromObject(qa.Read("hatches"))["items"]!.Count()==0,"Outside hatch remains available in state or catalog");
    Check(Json.FileHash(source)==beforeDwg,"Catalog exclusion modified CAD");
    Console.WriteLine("PASS: outside hatch exclusion removes consumer choices and preserves DWG");
}
// Remove only this test's uniquely owned sandbox.
if(args.Contains("--keep"))Console.WriteLine("FIXTURE="+root);else Directory.Delete(root, true);
