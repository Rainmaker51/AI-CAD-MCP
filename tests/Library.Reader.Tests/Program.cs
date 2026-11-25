using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rainmaker.Library.Reader;
using ReaderCompatibilityClient;
void Check(bool value,string message){if(!value)throw new Exception(message);}
void Reject(Action action,string code){try{action();}catch(LibraryReadException error)when(error.Code==code){return;}throw new Exception("Expected "+code);}
string Sha(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
if(args.Length==2&&args[0]=="--audit")
{
    var auditReader=LibraryReader.Open(args[1]);var auditSnapshot=auditReader.Read();Console.WriteLine("Protocol="+auditReader.ProtocolVersion);
    foreach(var group in auditSnapshot.ListResources().GroupBy(r=>r.Key.Kind))Console.WriteLine(group.Key+"="+group.Count());
    auditSnapshot.ResolveResources(auditSnapshot.ListResources().Select(r=>r.Key));Console.WriteLine("PASS: current warehouse read and all published assets verified");return;
}
string repo=Directory.GetCurrentDirectory(),run=Path.Combine(repo,".tools","test-runs","reader-v1-"+Guid.NewGuid().ToString("N")),root=Path.Combine(run,"warehouse");Directory.CreateDirectory(root);
File.WriteAllText(Path.Combine(run,"test-run.json"),JsonConvert.SerializeObject(new{component="Rainmaker.Library.Reader",purpose="V1 storage migration and read-only protocol tests",createdUtc=DateTime.UtcNow}));
File.WriteAllText(Path.Combine(repo,".tools","reader-test-run.txt"),run);
void Write(string path,object value){string file=Path.Combine(root,path);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file,JsonConvert.SerializeObject(value,Formatting.Indented),new UTF8Encoding(false));}
string LabelId(string id)=>Sha(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new[]{"ISO_QUANGANH",id},Formatting.None)));
string[] kinds={"blocks","layers","hatches","texts","dims"};var ids=kinds.Select((k,i)=>(k,id:(i+1).ToString("x").PadLeft(32,'0'))).ToDictionary(x=>x.k,x=>x.id);
byte[] dwg=Encoding.UTF8.GetBytes("owned hash fixture, not a real drawing");string hash=Sha(dwg);
Directory.CreateDirectory(Path.Combine(root,"CAD","Assets"));File.WriteAllBytes(Path.Combine(root,"CAD","Assets",hash+".dwg"),dwg);Write("warehouse.json",new{schemaVersion=1});
var layer=new JObject{["Name"]="LAYER",["Color"]=7,["Appearance"]=new JObject{["LineType"]="Continuous",["LineWeight"]=25,["Rgb"]=null,["Plottable"]=true,["SourceHash"]=hash}};
var dim=new JObject{["id"]=ids["dims"],["name"]="DIM",["textStyle"]="TEXT",["textHeight"]=2.5,["arrowSize"]=1,["scale"]=1,["annotative"]=false};
var text=new JObject{["id"]=ids["texts"],["name"]="TEXT",["font"]="tahoma.ttf",["height"]=0,["widthFactor"]=1};
var hatch=new JObject{["id"]=ids["hatches"],["name"]="HATCH",["handle"]="AB",["pattern"]="SOLID",["type"]="PreDefined",["scale"]=1,["angle"]=0,["layer"]="LAYER",["color"]=7,["spacing"]=1,["doublePattern"]=false};
var block=new JObject{["Id"]="BLOCK",["Geometry"]=new JArray(),["NativeSource"]=new JObject{["FileHash"]=hash,["BlockName"]="BLOCK",["Min"]=new JObject{["X"]=0,["Y"]=0},["Max"]=new JObject{["X"]=100,["Y"]=100}}};
var template=new JObject{["Id"]="ISO_QUANGANH",["Name"]="Fixture",["Version"]="1.0.0",["Units"]="mm",["Layers"]=new JArray(layer),["Blocks"]=new JArray(block)};
void Publish(string directory,string filename,JObject value,bool templateVersion=false){Write(directory+"/1.0.0/"+filename,value);string digest=Sha(File.ReadAllBytes(Path.Combine(root,directory,"1.0.0",filename)));Write(directory+"/active.json",new{version="1.0.0",checksum=digest});if(templateVersion)File.WriteAllText(Path.Combine(root,directory,"1.0.0","file-sha256.txt"),digest);}
Publish("Versions/templates/ISO_QUANGANH","content.json",template,true);Publish("Styles/ISO_QUANGANH","catalog.json",new JObject{["version"]="1.0.0",["sourceHash"]=hash,["texts"]=new JArray(text),["dims"]=new JArray(dim)});Publish("Hatches/ISO_QUANGANH","catalog.json",new JObject{["version"]="1.0.0",["sourceHash"]=hash,["items"]=new JArray(hatch)});
var codes=new JArray();var items=new JArray();foreach(string kind in kinds)
{
    string name=kind=="blocks"?"BLOCK":kind=="layers"?"LAYER":kind=="hatches"?"HATCH":kind=="texts"?"TEXT":"DIM";
    codes.Add(new JObject{["SourceId"]="ISO_QUANGANH",["Kind"]=kind,["Id"]=ids[kind],["Code"]=kind+"-001",["Name"]=name});
    var values=kind=="blocks"?new JObject{["attributes"]=new JArray()}:kind=="layers"?new JObject{["color"]=7,["lineType"]="Continuous",["lineWeight"]=25,["plottable"]=true}:kind=="dims"?dim:kind=="texts"?text:hatch;
    items.Add(new JObject{["Id"]=ids[kind],["Kind"]=kind,["Name"]=name,["Handle"]=kind=="hatches"?"AB":"",["Values"]=values.DeepClone()});
    Write("Catalog/management/"+kind+"/"+(kind is "blocks" or "layers"?LabelId(name):ids[kind])+".json",new{Name="Display "+kind,Purpose="Purpose "+kind,Notes="Keep",Reviewed=true,GroupId="g",Revision=1});Write("Catalog/management/"+kind+"-groups.json",new[]{new{Id="g",Name="Group",ParentId=""}});
}
string retired="f".PadLeft(32,'0');codes.Add(new JObject{["SourceId"]="ISO_QUANGANH",["Kind"]="hatches",["Id"]=retired,["Code"]="OLD-1"});Write("Catalog/resource-codes.json",new{SchemaVersion=1,Items=codes});Write("Catalog/sync/state.json",new{Revision=1,Items=items});
var reader=LibraryReader.Open(root);var snapshot=reader.Read();Check(snapshot.ListResources().Count==5,"Five resource kinds not available");Check(snapshot.FindByCode("blocks-001").DisplayName=="Display blocks"&&snapshot.FindByCode("blocks-001").GroupPath=="Group","Metadata joined incorrectly");
Check(!snapshot.FindByCode("OLD-1").Available&&snapshot.FindByCode("OLD-1").Availability=="RETIRED","Retired code became selectable");Reject(()=>snapshot.ResolveResources(new[]{new ResourceKeyV1("ISO_QUANGANH","hatches",retired)}),"RESOURCE_UNAVAILABLE");snapshot.ResolveResources(snapshot.ListResources().Select(r=>r.Key));
var sameBinary=Client.List(reader);string clientHash=Sha(File.ReadAllBytes(typeof(Client).Assembly.Location));
var legacyDim=(JObject)dim.DeepClone();legacyDim.Remove("annotative");Publish("Styles/ISO_QUANGANH","catalog.json",new JObject{["version"]="1.0.0",["sourceHash"]=hash,["texts"]=new JArray(text),["dims"]=new JArray(legacyDim)});
Check(reader.Read().ListResources("dims").Count==1,"Legacy published dim without annotative was hidden");
snapshot=reader.Read();
items.OfType<JObject>().Single(i=>i.Value<string>("Kind")=="hatches")["Draft"]=true;Write("Catalog/sync/state.json",new{Revision=2,Items=items});
Check(reader.Read().ListResources("hatches").Count==0,"Draft became selectable");items.OfType<JObject>().Single(i=>i.Value<string>("Kind")=="hatches").Remove("Draft");
items.OfType<JObject>().Single(i=>i.Value<string>("Kind")=="hatches")["Values"]!["scale"]=25;Write("Catalog/sync/state.json",new{Revision=3,Items=items});
Check(reader.Read().ListResources("hatches").Count==0,"Unpublished parameter mismatch became selectable");items.OfType<JObject>().Single(i=>i.Value<string>("Kind")=="hatches")["Values"]!["scale"]=1;Write("Catalog/sync/state.json",new{Revision=4,Items=items});snapshot=reader.Read();
Write("Catalog/management/hatches/"+ids["hatches"]+".json",new{DeletedUtc=DateTime.UtcNow});Reject(()=>snapshot.ResolveResources(new[]{snapshot.ListResources()[0].Key}),"CATALOG_CHANGED");Check(reader.Read().ListResources("hatches").Count==0,"Trash still selectable");
Write("Catalog/management/hatches/"+ids["hatches"]+".json",new{Name="Display hatches",Purpose="Purpose hatches",Notes="Keep",Reviewed=true,GroupId="g",Revision=1});
Write("Catalog/qa-transactions/ISO_QUANGANH.pending.json",new{phase="PREPARED"});Reject(()=>reader.Read(),"LIBRARY_PENDING");File.Delete(Path.Combine(root,"Catalog/qa-transactions/ISO_QUANGANH.pending.json"));
File.AppendAllText(Path.Combine(root,"Styles/ISO_QUANGANH/1.0.0/catalog.json")," ");Reject(()=>reader.Read(),"INTEGRITY_MISMATCH");Publish("Styles/ISO_QUANGANH","catalog.json",new JObject{["version"]="1.0.0",["sourceHash"]=hash,["texts"]=new JArray(text),["dims"]=new JArray(dim)});
Directory.CreateDirectory(Path.Combine(root,"Migrated"));foreach(string dir in new[]{"Versions","Styles","Hatches","CAD"})Directory.Move(Path.Combine(root,dir),Path.Combine(root,"Migrated",dir));Directory.Move(Path.Combine(root,"Catalog","sync"),Path.Combine(root,"Migrated","sync"));Directory.Move(Path.Combine(root,"Catalog","management"),Path.Combine(root,"Migrated","management"));File.Move(Path.Combine(root,"Catalog","resource-codes.json"),Path.Combine(root,"Migrated","codes.json"));
Write("Catalog/library-reader.json",new{schemaVersion=1,format="json-v1",paths=new{state="Migrated/sync/state.json",codes="Migrated/codes.json",templates="Migrated/Versions/templates",styles="Migrated/Styles",hatches="Migrated/Hatches",management="Migrated/management",assets="Migrated/CAD/Assets"}});
Check(Client.List(reader).SequenceEqual(sameBinary)&&Sha(File.ReadAllBytes(typeof(Client).Assembly.Location))==clientHash,"Storage migration required a consumer change");Check(Client.Resolve(reader,"dims-001")==hash&&LibraryReader.ResolveAssetFile(root,hash).Contains("Migrated"),"Asset resolution did not follow layout migration");
Write("Catalog/library-reader.json",new{schemaVersion=1,format="json-v1",paths=new{state="../../outside.json"}});Reject(()=>reader.Read(),"CATALOG_PATH");
File.WriteAllText(Path.Combine(run,"results.json"),JsonConvert.SerializeObject(new{passed=true,fiveKinds=true,metadata=true,retiredAndTrashExcluded=true,pendingGuard=true,hashValidation=true,snapshotGuard=true,unchangedClientBinary=true,storageMigration=true,assetMigration=true,pathGuard=true,clientHash},Formatting.Indented));
Console.WriteLine("PASS: Reader V1 five kinds, metadata, retirement, integrity, pending/stale snapshot, migrated layout with unchanged consumer binary");Console.WriteLine("EVIDENCE="+run);
