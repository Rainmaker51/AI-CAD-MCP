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
