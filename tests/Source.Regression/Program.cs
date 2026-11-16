using System;
using System.Collections.Generic;
using AICad.Application;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

var template=new TemplateProfile{Blocks=new(){new(){Id="WINDOW",Usage=new(){Role="window",ConversionConfirmed=true}}}};
var ops=new List<Operation>{new(){Kind="draw_wall",Points=new(){new(0,100),new(1000,100)}},
 new(){Kind="draw_wall",Points=new(){new(3500,100),new(5000,100)}},
 new(){Kind="insert_block",AssetId="WINDOW",OpeningWidth=2500,Points=new(){new(1000,200)}}};
var evidence=JArray.Parse("""[{"operationIndex":1,"mark":"frame_between_jambs","pixelA":{"X":40,"Y":10},"pixelB":{"X":140,"Y":10},"jambA":{"X":1000,"Y":100},"jambB":{"X":3500,"Y":100}}]""");
void Assert(bool condition){if(!condition)throw new Exception("Assertion failed");}
void Reject(Action action){try{action();}catch(CadException e) when(e.Code=="OPENING_EVIDENCE"){return;}throw new Exception("Expected rejection");}
var repaired=SourceOpeningEvidence.NormalizeReferences(evidence,ops,template);
Assert(repaired[0]!.Value<int>("operationIndex")==2);
Assert(evidence[0]!.Value<int>("operationIndex")==1); // Raw response remains unchanged.
Assert(JToken.DeepEquals(repaired,SourceOpeningEvidence.NormalizeReferences(repaired,ops,template)));
ops.Add(ops[2]);
Reject(()=>SourceOpeningEvidence.NormalizeReferences(evidence,ops,template)); // Ambiguous blocks.
ops.RemoveAt(3);
ops[0].Points[1].X=1300;
Reject(()=>SourceOpeningEvidence.NormalizeReferences(evidence,ops,template)); // Wall crosses opening.
ops[0].Points[1].X=1000;
Reject(()=>SourceOpeningEvidence.NormalizeReferences(new JArray(),ops,template)); // Missing evidence.
var duplicate=(JArray)evidence.DeepClone();duplicate.Add(evidence[0]!.DeepClone());
Reject(()=>SourceOpeningEvidence.NormalizeReferences(duplicate,ops,template));
Console.WriteLine("PASS: index repair, immutable input, idempotence, ambiguity, crossing wall, missing and duplicate evidence");
string temp=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"aicad-tree-"+Guid.NewGuid().ToString("N"));
var catalog=new LibraryCatalog(temp);
catalog.EnsureTemplateGroups();catalog.EnsureTemplateGroups();
Assert(catalog.Groups("templates").Count==4);
var architecture=catalog.Groups("templates").Find(g=>g.Name=="Kiến trúc")!;
var doors=catalog.Group("templates","","Cửa",architecture.Id);
var windows=catalog.Group("templates","","Cửa sổ",doors.Id);
Assert(catalog.Groups("templates").Find(g=>g.Id==windows.Id)!.ParentId==doors.Id);
try{catalog.Group("templates",architecture.Id,"Kiến trúc",windows.Id);throw new Exception("Cycle accepted");}
catch(CadException e) when(e.Code=="INVALID_GROUP_TREE"){}
var common=catalog.Groups("templates").Find(g=>g.Name=="Thư viện chung")!;
catalog.Group("templates",doors.Id,"Cửa",common.Id);
Assert(catalog.Groups("templates").Find(g=>g.Id==doors.Id)!.ParentId==common.Id);
catalog.RemoveGroup("templates",doors.Id);
Assert(catalog.Groups("templates").Find(g=>g.Id==windows.Id)!.ParentId==common.Id);
Console.WriteLine("PASS: group defaults, nested groups, cycle rejection, branch moves, child reparenting");
catalog.EnsureLayerGroups();catalog.EnsureLayerGroups();
Assert(catalog.Groups("layers").Count==4);
var layerArch=catalog.Groups("layers").Find(g=>g.Name=="Kiến trúc")!;
Assert(layerArch.Id!=architecture.Id);
var layerWalls=catalog.Group("layers","","Tường",layerArch.Id);
string layerKey=Json.Hash(new[]{"TEMPLATE_A","QA_KT_Tuong"});
catalog.SetGroups("layers",new(){layerKey},layerWalls.Id);
Assert(catalog.Label("layers",layerKey).GroupId==layerWalls.Id);
Assert(catalog.Label("templates",layerKey).GroupId=="");
try{catalog.SetGroups("layers",new(){layerKey},architecture.Id);throw new Exception("Accepted block group for layer");}
catch(CadException e) when(e.Code=="GROUP_NOT_FOUND"){}
catalog.RemoveGroup("layers",layerWalls.Id);
Assert(catalog.Label("layers",layerKey).GroupId=="");
Console.WriteLine("PASS: independent layer tree, isolated assignments, foreign group rejection and ungroup on removal");
var config=new InstallConfig{ContentRoot=temp,DataRoot=System.IO.Path.Combine(temp,"data")};
var workspace=new WorkspaceService(config,new CadClient(config));
var sourceTemplate=new TemplateProfile{Id="TEST_LAYERS",Version="1.0.0",Layers=new(){new(){Name="QA_KT_Tuong"},new(){Name="QA_KT_Dim"}}};
workspace.Store.Publish("templates",sourceTemplate.Id,sourceTemplate.Version,sourceTemplate);
var service=new LibraryManagementService(workspace);string beforeHash=Json.Hash(workspace.Template(sourceTemplate.Id));
var assignment=JObject.FromObject(new{templateId=sourceTemplate.Id,version="1.0.0",names=new[]{"QA_KT_Tuong","QA_KT_Dim"},groupId=layerArch.Id});
service.AssignLayers(assignment);
var layerState=JObject.FromObject(service.LayerState(sourceTemplate.Id));
Assert(layerState["items"]![0]!.Value<string>("GroupId")==layerArch.Id);
Assert(Json.Hash(workspace.Template(sourceTemplate.Id))==beforeHash);
assignment["version"]="0.0.1";
try{service.AssignLayers(assignment);throw new Exception("Accepted stale template");}catch(CadException e) when(e.Code=="RESOURCE_CHANGED"){}
assignment["version"]="1.0.0";assignment["names"]=new JArray("QA_KT_Tuong","MISSING");
try{service.AssignLayers(assignment);throw new Exception("Accepted missing layer");}catch(CadException e) when(e.Code=="INVALID_LAYER"){}
Console.WriteLine("PASS: bulk layer assignment, template unchanged, stale-version and missing-layer rejection");
catalog.EnsureBlockGroups();catalog.EnsureBlockGroups();
Assert(catalog.Groups("blocks").Count==4);
var blockArch=catalog.Groups("blocks").Find(g=>g.Name=="Kiến trúc")!;
Assert(blockArch.Id!=layerArch.Id);
var blockDoors=catalog.Group("blocks","","Cửa",blockArch.Id);
catalog.Group("blocks",blockDoors.Id,"Cửa đi",blockArch.Id);
sourceTemplate.Version="1.0.1";sourceTemplate.Blocks=new(){new(){Id="DOOR"},new(){Id="WINDOW"}};
workspace.Store.Publish("templates",sourceTemplate.Id,sourceTemplate.Version,sourceTemplate);
beforeHash=Json.Hash(workspace.Template(sourceTemplate.Id));
assignment=JObject.FromObject(new{templateId=sourceTemplate.Id,version="1.0.1",names=new[]{"DOOR","WINDOW"},groupId=blockDoors.Id});
service.AssignBlocks(assignment);
Assert(JObject.FromObject(service.BlockState(sourceTemplate.Id))["items"]![0]!.Value<string>("GroupId")==blockDoors.Id);
Assert(Json.Hash(workspace.Template(sourceTemplate.Id))==beforeHash);
assignment["groupId"]=layerArch.Id;
try{service.AssignBlocks(assignment);throw new Exception("Accepted layer group for block");}catch(CadException e) when(e.Code=="GROUP_NOT_FOUND"){}
catalog.RemoveGroup("blocks",blockDoors.Id);
Assert(JObject.FromObject(service.BlockState(sourceTemplate.Id))["items"]![0]!.Value<string>("GroupId")=="");
Console.WriteLine("PASS: independent block groups, rename, bulk assignment, foreign group rejection, safe removal");
var hatches=new HatchLibraryService(config);
Assert(((JArray)hatches.Read()["items"]!).Count==0);
string hatchRoot=System.IO.Path.Combine(temp,"Hatches","ISO_QUANGANH"),hatchVersion=System.IO.Path.Combine(hatchRoot,"1.0.0");
System.IO.Directory.CreateDirectory(hatchVersion);string hatchId=new string('a',64),imagePath=System.IO.Path.Combine(hatchVersion,Json.Hash(hatchId)+".png");
System.IO.File.WriteAllBytes(imagePath,new byte[]{1,2,3});
string catalogFile=System.IO.Path.Combine(hatchVersion,"catalog.json");
Json.Atomic(catalogFile,new{version="1.0.0",items=new[]{new{id=hatchId,imageFile=hatchId+".png",imageHash=Json.FileHash(imagePath)}}});
Json.Atomic(System.IO.Path.Combine(hatchRoot,"active.json"),new{version="1.0.0",checksum=Json.FileHash(catalogFile)});
Assert(hatches.Image(hatchId)==imagePath);
string hatchChecksum=Json.FileHash(catalogFile);
var editHatch=JObject.FromObject(new{id=hatchId,revision=0,name="Bê tông mặt cắt",purpose="Ký hiệu bê tông trong mặt cắt",notes="Kiểm tra tỷ lệ trước khi dùng",reviewed=true,action="update"});
hatches.Update(editHatch);Assert(hatches.SkillContext().Count==1);
Assert(hatches.Read()["items"]![0]!["management"]!.Value<string>("Purpose")=="Ký hiệu bê tông trong mặt cắt");
Assert(Json.FileHash(catalogFile)==hatchChecksum);
try{hatches.Update(editHatch);throw new Exception("Accepted stale hatch edit");}catch(CadException e) when(e.Code=="RESOURCE_CHANGED"){}
hatches.Update(JObject.FromObject(new{id=hatchId,revision=1,action="delete"}));Assert(hatches.SkillContext().Count==0);
hatches.Update(JObject.FromObject(new{id=hatchId,revision=2,action="restore"}));Assert(hatches.SkillContext().Count==1);
editHatch["revision"]=3;editHatch["purpose"]="";
try{hatches.Update(editHatch);throw new Exception("Reviewed hatch without purpose");}catch(CadException e) when(e.Code=="PURPOSE_REQUIRED"){}
var hg=catalog.Group("hatches","","Bê tông");editHatch["purpose"]="Concrete";editHatch["groupId"]=hg.Id;hatches.Update(editHatch);
catalog.RemoveGroup("hatches",hg.Id);
Assert(hatches.Read()["items"]![0]!["management"]!.Value<string>("Purpose")=="Concrete");
Assert(hatches.Read()["items"]![0]!["management"]!.Value<string>("GroupId")=="");
Console.WriteLine("PASS: hatch notes, review gate, delete/restore, stale edits, source unchanged, group removal preserves notes");
System.IO.File.AppendAllText(imagePath,"changed");
try{hatches.Image(hatchId);throw new Exception("Accepted changed hatch image");}catch(CadException e) when(e.Code=="RESOURCE_CHANGED"){}
try{hatches.Image("../../outside");throw new Exception("Accepted unknown hatch image");}catch(CadException e) when(e.Code=="NOT_FOUND"){}
System.IO.File.AppendAllText(catalogFile," ");
try{hatches.Read();throw new Exception("Accepted changed hatch catalog");}catch(CadException e) when(e.Code=="RESOURCE_CHANGED"){}
Console.WriteLine("PASS: empty hatch library, validated image path, unknown ID and integrity checks");
Assert(SourceAnnotationText.Decode("Bª t«ng cèt thÐp","QA_VnArial")=="Bê tông cốt thép");
Assert(SourceAnnotationText.Decode("§¸ tù nhiªn ( mÆt ®øng )","QA_VnArial")=="Đá tự nhiên ( mặt đứng )");
Assert(SourceAnnotationText.Decode("CỬA ĐI","Arial")=="CỬA ĐI");
var styles=new StyleLibraryService(config);Assert(((JArray)styles.Read("texts")["items"]!).Count==0);
string styleRoot=System.IO.Path.Combine(temp,"Styles","ISO_QUANGANH"),styleVersion=System.IO.Path.Combine(styleRoot,"1.0.0");System.IO.Directory.CreateDirectory(styleVersion);
string styleFile=System.IO.Path.Combine(styleVersion,"catalog.json");var styleId=Json.Hash("Standard");
Json.Atomic(styleFile,new{version="1.0.0",texts=new[]{new{id=styleId,name="Standard",font="arial.ttf"}},dims=new[]{new{id=styleId,name="Standard",scale=1}}});Json.Atomic(System.IO.Path.Combine(styleRoot,"active.json"),new{version="1.0.0",checksum=Json.FileHash(styleFile)});
var styleEdit=JObject.FromObject(new{id=styleId,revision=0,action="update",name="Text ghi chú",purpose="Ghi chú bản vẽ",reviewed=true});styles.Update("texts",styleEdit);
Assert(styles.SkillContext("texts").Count==1&&styles.SkillContext("dims").Count==0);
styles.Update("texts",JObject.FromObject(new{id=styleId,revision=1,action="delete"}));Assert(styles.SkillContext("texts").Count==0);
styles.Update("texts",JObject.FromObject(new{id=styleId,revision=2,action="restore"}));Assert(styles.SkillContext("texts").Count==1);
Console.WriteLine("PASS: TCVN3 conversion, separate Text/Dim metadata, review and delete/restore");
string openSource=System.IO.Path.Combine(temp,"open-source.dwg"),copiedSource=System.IO.Path.Combine(temp,"copied-source.dwg");System.IO.File.WriteAllText(openSource,"source snapshot");
using(var open=new System.IO.FileStream(openSource,System.IO.FileMode.Open,System.IO.FileAccess.ReadWrite,System.IO.FileShare.ReadWrite)){
 string sharedHash=CadSourceFile.Hash(openSource);CadSourceFile.Copy(openSource,copiedSource);Assert(Json.FileHash(copiedSource)==sharedHash);
}
Console.WriteLine("PASS: shared read of open DWG source with verified snapshot");
string originals=System.IO.Path.Combine(temp,"CAD","Originals");System.IO.Directory.CreateDirectory(originals);
string baseDwg=System.IO.Path.Combine(originals,CadSourceFile.BaseLibraryName),otherDwg=System.IO.Path.Combine(originals,"NoiThat.dwg");
System.IO.File.WriteAllText(baseDwg,"base source");System.IO.File.WriteAllText(otherDwg,"different library");
var sourceDetails=JObject.FromObject(CadSourceFile.Describe(temp,Json.FileHash(otherDwg)));
Assert(sourceDetails.Value<string>("name")=="NoiThat.dwg"&&sourceDetails.Value<string>("path")==otherDwg&&sourceDetails.Value<bool>("originalFound"));
string importedHash=Json.FileHash(baseDwg);System.IO.File.AppendAllText(baseDwg," edited after import");
var historicalDetails=JObject.FromObject(CadSourceFile.Describe(temp,importedHash));
Assert(!historicalDetails.Value<bool>("originalFound")&&historicalDetails.Value<string>("path")==System.IO.Path.Combine(temp,"content-assets",importedHash+".dwg"));
try{CadSourceFile.Describe(temp,"../outside");throw new Exception("Accepted invalid source hash");}catch(CadException e)when(e.Code=="INVALID_SOURCE"){}
Console.WriteLine("PASS: source paths match each DWG by hash, historical fallback and path traversal rejection");
