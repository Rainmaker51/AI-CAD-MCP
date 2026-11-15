using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.Geometry;
using AICad.Application;
using Newtonsoft.Json.Linq;
if(args.Length>0 && args[0]=="probe-utf8") {
 Console.InputEncoding=new System.Text.UTF8Encoding(false);
 Console.OutputEncoding=new System.Text.UTF8Encoding(false);
 Console.Write(Console.In.ReadToEnd());
 return;
}
// A separate test process simulates the CLI; it never reads real credentials.
if(args.Length>0 && (args[0]=="login" || args[0]=="logout")) {
 string fakeHome=Environment.GetEnvironmentVariable("CODEX_HOME")!;
 if(args[0]=="login" && args.Contains("--device-auth")) {
  Console.WriteLine("Test device login pending");
  System.Threading.Thread.Sleep(800);
  File.WriteAllText(Path.Combine(fakeHome,"fake-session"),"test-only");
 } else if(args[0]=="logout") {
  File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"fake-logout-called"),fakeHome);
  File.Delete(Path.Combine(fakeHome,"fake-session"));
 }
 return;
}
string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));string temp=Path.Combine(root,"tests","fixtures","managed-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
var result=new List<object>();int failures=0;
void Test(string name,Action action){try{action();result.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;result.Add(new{name,passed=false,message=e.Message});Console.Error.WriteLine("FAIL "+name+": "+e.Message);}}
void Assert(bool value,string message="Assertion failed"){if(!value)throw new Exception(message);}
void Reject(string code,Action action){try{action();throw new Exception("Expected "+code);}catch(CadException e){Assert(e.Code==code,"Expected "+code+", got "+e.Code);}}
Test("Quang Anh fixed-depth windows ignore wall thickness and retain opening width and visibility",()=>{
 var t=new TemplateProfile{Id="ISO_QUANGANH",Blocks=new(){new(){Id="QA_CUA_SO_1_CANH",
  Usage=new(){Reviewed=true,Values=new(){["DT"]=200}}}}};
 var window=new Operation{Kind="insert_block",AssetId="QA_CUA_SO_1_CANH",OpeningWidth=1180,
  WallThickness=220,DynamicJson="{\"RCUA\":1080,\"Visibility\":\"2 CANH LUA\"}"};
 var other=new Operation{Kind="insert_block",AssetId="QA_CUA_1_CANH",WallThickness=110};
 var fixedWindow=new Operation{Kind="insert_block",AssetId=window.AssetId,WallThickness=110};
 Assert(AiOperationDefaults.UseCatalogWindowDepth(new[]{window,other,fixedWindow},t));
 Assert(window.WallThickness==null&&window.OpeningWidth==1180&&fixedWindow.WallThickness==null&&other.WallThickness==110);
 Assert(Json.Read<JObject>(window.DynamicJson!).Value<string>("Visibility")=="2 CANH LUA");
 Assert(!AiOperationDefaults.UseCatalogWindowDepth(new[]{window,other,fixedWindow},t));
});
TemplateProfile template=Json.Read<TemplateProfile>(File.ReadAllText(Path.Combine(WarehousePaths.Folder(root,"template-library"),"STARTER.json")));
Test("Automatic axes merge near wall lines at the configured threshold and retain documented column lines",()=>{
 var walls=new List<Operation>{new(){Points=new(){new(0,0),new(0,4000)}},
  new(){Points=new(){new(40,0),new(40,4100)}},new(){Points=new(){new(1000,0),new(1000,3000)}},
  new(){Points=new(){new(1050,0),new(1050,3100)}}};
 Assert(ResidentialSourcePlan.AxisPositions(walls,new[]{new Point2(0,0),new Point2(0,4000)},false,50)
  .SequenceEqual(new double[]{0,1050}));
 Assert(ResidentialSourcePlan.AxisPositions(walls,Array.Empty<Point2>(),false,0).Count==4);
});
Test("Measured door frame fits opposite walls without reversing its swing",()=>{
 const string id="DOOR";
 var t=new TemplateProfile{Presets=JObject.Parse("""{"doorFramePlacement":{"DOOR":{"visibilities":["2 CANH"],"zeroFlipFrameSide":-1,"maxCorrectionMm":260,"openingToleranceMm":2}}}"""),
  Blocks=new(){new(){Id=id,Usage=new(){ConversionConfirmed=true,WidthParameter="width",ThicknessParameter="depth",Values=new(){["CUA"]="2 CANH"}},
   NativeSource=new(){DynamicProperties=new(){new(){Name="width",DefaultValue=1400},new(){Name="depth",DefaultValue=220},new(){Name="CUA",DefaultValue="2 CANH"}}}}}};
 Operation Wall(double x,double a,double b)=>new(){Kind="draw_wall",Height=220,Points=new(){new(x,a),new(x,b)}};
 Operation Door(double x,double y,double angle)=>new(){Kind="insert_block",AssetId=id,OpeningWidth=1400,Points=new(){new(x,y)},Rotation=angle};
 var plan=new DrawingPlan{Template=t,Operations=new(){Door(220,1000,Math.PI/2),Door(3780,2400,-Math.PI/2)}};
 SourceDoorPlacement.Fit(plan,new[]{Wall(110,0,1000),Wall(110,2400,4000),Wall(3890,0,1000),Wall(3890,2400,4000)});
 Assert(Math.Abs(plan.Operations[0].Points[0].X)<1e-8&&Math.Abs(plan.Operations[1].Points[0].X-4000)<1e-8);
 Assert(plan.Operations[0].Rotation==Math.PI/2&&plan.Operations[1].Rotation==-Math.PI/2);
});
Test("Source opening evidence ties one door or window to the same wall gap and block",()=>{
 var t=new TemplateProfile{Blocks=new(){new(){Id="WINDOW",Usage=new(){Role="window",ConversionConfirmed=true}}}};
 var ops=new List<Operation>{new(){Kind="draw_wall",Points=new(){new(0,100),new(1000,100)}},
  new(){Kind="draw_wall",Points=new(){new(3500,100),new(5000,100)}},
  new(){Kind="insert_block",AssetId="WINDOW",OpeningWidth=2500,Points=new(){new(1000,200)}}};
 var evidence=JArray.Parse("""[{"operationIndex":2,"mark":"frame_between_jambs","pixelA":{"X":40,"Y":10},"pixelB":{"X":140,"Y":10},"jambA":{"X":1000,"Y":100},"jambB":{"X":3500,"Y":100}}]""");
 SourceOpeningEvidence.Validate(evidence,ops,t);
 Reject("OPENING_EVIDENCE",()=>SourceOpeningEvidence.Validate(new JArray(),ops,t));
 ops[0].Points[1].X=1300;
 Reject("OPENING_EVIDENCE",()=>SourceOpeningEvidence.Validate(evidence,ops,t));
 ops[0].Points[1].X=1000;
 ops[2].Points[0].X=1400;
 Reject("OPENING_EVIDENCE",()=>SourceOpeningEvidence.Validate(evidence,ops,t));
 ops[2].Points[0].X=1000;
 ops.Add(ops[2]);
 var duplicate=(JObject)evidence[0]!.DeepClone();
 duplicate["operationIndex"]=3;
 evidence.Add(duplicate);
 Reject("OPENING_EVIDENCE",()=>SourceOpeningEvidence.Validate(evidence,ops,t));
});
Test("Estimated Quang Anh window width fits the measured RCUA step without widening the opening",()=>{
 var op=new Operation{Kind="insert_block",AssetId="QA_CUA_SO_1_CANH",OpeningWidth=965,Points=new(){new(500,600)},
  DynamicJson="{\"RCUA\":865,\"Visibility\":\"2 CANH LUA\"}"};
 var asset=new BlockAsset{Id=op.AssetId,Usage=new(){Reviewed=true,ConversionConfirmed=true,WidthParameter="RCUA",WidthOffset=-100},
  NativeSource=new(){BlockName="MBCUASO",DynamicProperties=new(){
   new(){Name="RCUA",DefaultValue=800},new(){Name="Visibility",DefaultValue="2 CANH LUA"}}}};
 var plan=new DrawingPlan{Template=new(){Id="ISO_QUANGANH",Blocks=new(){asset}},Operations=new(){op}};
 AiOperationDefaults.FitEstimatedWindowWidths(plan);
 Assert(op.OpeningWidth==960&&op.Points[0].X==500&&op.Points[0].Y==600);
 var values=DraftingRules.Values(op,asset);
 Assert(values.Value<int>("RCUA")==860&&values.Value<string>("Visibility")=="2 CANH LUA");
 AiOperationDefaults.FitEstimatedWindowWidths(plan);
 Assert(plan.Assumptions.Count==1);
});
Test("Quang Anh TV cabinet flips only if its default footprint fits in the opposite direction",()=>{
 var op=new Operation{Kind="insert_block",AssetId="QA_KE_TIVI",Points=new(){new(12170,2000)},DynamicJson="{\"NOI THAT\":\"Ke_Tivi\"}"};
 var plan=new DrawingPlan{
  Template=new(){Id="ISO_QUANGANH",Blocks=new(){new(){Id=op.AssetId,
   NativeSource=new(){Min=new(-900,-2100),Max=new(900,600)},
   Usage=new(){Reviewed=true,Values=new(){["NOI THAT"]="Ke_Tivi"}}}}},
  Scope=Validation.Box(0,0,18000,9000).ToList(),Operations=new(){op}};
 ResidentialSourcePlan.FitCatalogBlocks(plan);
 Assert(Math.Abs(op.Rotation-Math.PI)<1e-8&&op.Points[0].Y==2000&&plan.Assumptions.Count==1);
 ResidentialSourcePlan.FitCatalogBlocks(plan);
 Assert(plan.Assumptions.Count==1);
 op.Points[0].Y=500;
 Reject("OUTSIDE_SCOPE",()=>ResidentialSourcePlan.FitCatalogBlocks(plan));
 op.DynamicJson="{\"CHIEU DAI\":2500}";
 ResidentialSourcePlan.FitCatalogBlocks(plan);
 Assert(plan.Assumptions.Count==1);
});
DrawingPlan Plan()=>new(){SessionId=Guid.NewGuid().ToString("N"),DocumentId=Guid.NewGuid().ToString("N"),DocumentRevision="epoch:1",TaskId="T001",Template=template,TemplateHash=Json.Hash(template),Scope=new(){new(0,0),new(6000,0),new(6000,4200),new(0,4200)},Operations=new(){new(){Kind="insert_block",AssetId="DOOR_900",Layer="A-DOOR",Points=new(){new(2000,1500)}}}};
Test("Wall centerlines generate exact 220 and 110 mm thickness",()=>{foreach(int thickness in new[]{220,110}){var op=new Operation{Kind="draw_wall",Height=thickness,Layer="A-WALL",Points=new(){new(0,0),new(3000,0)}};Validation.Operation(op,new TemplateProfile{Layers=new(){new(){Name="A-WALL"}}},true);var box=DraftingRules.Wall(op);Assert(box.Max(p=>p.Y)-box.Min(p=>p.Y)==thickness);}Reject("INVALID_WALL",()=>Validation.Operation(new(){Kind="draw_wall",Height=137,Points=new(){new(0,0),new(1,0)}},template,false));});
Test("CAD layer names preserve spaces while illegal characters are rejected",()=>{Assert(Json.LayerName("QA_KT_Do Noi That")=="QA_KT_Do Noi That");Reject("INVALID_LAYER",()=>Json.LayerName("bad|layer"));});
Test("Opening size requires confirmed conversion and validates visibility",()=>{var b=new BlockAsset{Id="DOOR",NativeSource=new(){DynamicProperties=new(){new(){Name="WIDTH",DefaultValue=1100},new(){Name="STATE",DefaultValue="ONE",AllowedValues=new(){"ONE","TWO"}}}},Usage=new(){WidthParameter="WIDTH",WidthOffset=-100}};var op=new Operation{OpeningWidth=1000};Reject("WIDTH_BINDING_REQUIRED",()=>DraftingRules.Values(op,b));b.Usage.ConversionConfirmed=true;Assert(DraftingRules.Values(op,b).Value<double>("WIDTH")==900);op.DynamicJson="{\"STATE\":\"INVALID\"}";Reject("INVALID_DYNAMIC_VALUE",()=>DraftingRules.Values(op,b));op.DynamicJson="{\"MISSING\":1}";Reject("INVALID_DYNAMIC_PROPERTY",()=>DraftingRules.Values(op,b));});
Test("Axes require confirmation and do not mutate recipe coordinates",()=>{var r=new DraftingRecipe{IncludeAxes=true,Layers=new(){{"axis","A-AI"},{"axisHead","A-AI"}},Axes=new(){new(){Name="A",Start=new(0,0),End=new(0,5000)}}};Reject("AXES_CONFIRMATION_REQUIRED",()=>DraftingRules.Recipe(r,template));r.AxesConfirmed=true;DraftingRules.Recipe(r,template);var ops=DraftingRules.Axes(r);Assert(ops.Count==5);ops[0].Points[0].X=123;Assert(r.Axes[0].Start.X==0);});
Test("New drafting fields remain absent in legacy plan hashes",()=>{var value=Json.Write(Plan());Assert(!value.Contains("DynamicJson")&&!value.Contains("OpeningWidth")&&!value.Contains("WallThickness")&&!value.Contains("Appearance")&&!value.Contains("Usage"));});
Test("Guided Skill publication pins an independent layer standard",()=>{string app=Path.Combine(temp,"wizard-app");var cfg=new InstallConfig{ContentRoot=app,DataRoot=Path.Combine(temp,"wizard-data"),HostExecutable=Path.Combine(root,"storage/04-builds/artifacts/p0-p6/Host/AICad.Host.exe")};var svc=new WorkspaceService(cfg,new CadClient(cfg));svc.Store.Publish("templates",template.Id,template.Version,template);var draft=new DraftingLibraryService(svc);draft.PublishStandard(new(){["templateId"]=template.Id,["id"]="LAYERS",["name"]="Layer test",["version"]="1.0.0",["roles"]=new JObject{["wall"]="A-AI",["axis"]="A-AI",["axisHead"]="A-AI"}});var skill=draft.Wizard(new(){["templateId"]=template.Id,["standardId"]="LAYERS",["id"]="WIZARD",["name"]="Skill test",["version"]="1.0.0",["builder"]="custom",["recipe"]=JObject.FromObject(new DraftingRecipe())});Assert(skill.Recipe!.StandardVersion=="1.0.0"&&svc.Store.Skill("WIZARD").Recipe!.DoorWidthBasis=="opening");Reject("VERSION_EXISTS",()=>draft.Wizard(new(){["templateId"]=template.Id,["standardId"]="LAYERS",["id"]="WIZARD",["name"]="Skill test",["version"]="1.0.0",["builder"]="custom"}));});
Test("Library names and groups do not mutate immutable versions",()=>{string app=Path.Combine(temp,"managed-catalog");var cfg=new InstallConfig{ContentRoot=app,DataRoot=Path.Combine(app,"data")};var svc=new WorkspaceService(cfg,new CadClient(cfg));svc.Store.Publish("templates",template.Id,template.Version,template);var skill=new SkillDefinition{Id="KEEP",Name="Original",Version="1.0.0",TemplateId=template.Id,Builder="custom"};svc.Store.Publish("skills",skill.Id,skill.Version,skill);string digest=Json.Hash(svc.Store.Skill(skill.Id));var group=svc.Catalog.Group("skills","","Kiến trúc");svc.Catalog.Update("skills",skill.Id,"Tên mới",group.Id);Assert(svc.Catalog.Name("skills",skill.Id,"Original")=="Tên mới");Assert(Json.Hash(svc.Store.Skill(skill.Id))==digest);svc.Catalog.Group("skills",group.Id,"Mặt bằng");Assert(svc.Catalog.Groups("skills").Single().Name=="Mặt bằng");Reject("GROUP_EXISTS",()=>svc.Catalog.Group("skills","","MẶT BẰNG"));svc.Catalog.RemoveGroup("skills",group.Id);Assert(svc.Catalog.Label("skills",skill.Id).GroupId=="");});
Test("Library trash guards dependencies and restores versioned content",()=>{string app=Path.Combine(temp,"trash-catalog");var cfg=new InstallConfig{ContentRoot=app,DataRoot=Path.Combine(app,"data")};var svc=new WorkspaceService(cfg,new CadClient(cfg));svc.Store.Publish("templates",template.Id,template.Version,template);var skill=new SkillDefinition{Id="REMOVE",Name="Original",Version="1.0.0",TemplateId=template.Id,Builder="custom"};svc.Store.Publish("skills",skill.Id,skill.Version,skill);var management=new LibraryManagementService(svc);Reject("DEPENDENCY_IMPACT",()=>management.Delete(new(){["kind"]="templates",["id"]=template.Id},false));management.Delete(new(){["kind"]="skills",["id"]=skill.Id},false);Assert(svc.Skills().Count==0);Assert(svc.Store.Skill(skill.Id).Name=="Original");Reject("RESOURCE_DELETED",()=>svc.SetSkill(skill.Id,true));management.Delete(new(){["kind"]="templates",["id"]=template.Id},false);Assert(svc.Templates().Count==0);Reject("RESOURCE_DELETED",()=>management.Delete(new(){["kind"]="skills",["id"]=skill.Id},true));management.Delete(new(){["kind"]="templates",["id"]=template.Id},true);management.Delete(new(){["kind"]="skills",["id"]=skill.Id},true);Assert(svc.Skills().Count==1&&svc.Templates().Count==1);Reject("INVALID_KIND",()=>svc.Catalog.Groups("../evil"));Reject("INVALID_NAME",()=>svc.Catalog.Update("skills","../evil","x",""));});
Test("Plans awaiting approval prevent trashing their Skill",()=>{string app=Path.Combine(temp,"in-use-catalog");var cfg=new InstallConfig{ContentRoot=app,DataRoot=Path.Combine(app,"data")};var svc=new WorkspaceService(cfg,new CadClient(cfg));svc.Store.Publish("templates",template.Id,template.Version,template);svc.Store.Publish("skills","BUSY","1.0.0",new SkillDefinition{Id="BUSY",Name="Busy",Version="1.0.0",TemplateId=template.Id,Builder="custom"});var p=Plan();p.SkillId="BUSY";svc.Plans.Write(new(){Plan=p,Status="PREVIEW"});Reject("RESOURCE_IN_USE",()=>new LibraryManagementService(svc).Delete(new(){["kind"]="skills",["id"]="BUSY"},false));});
Test("Draft catalog candidates are allowed for review but never silently approved for CAD",()=>{var t=Json.Read<TemplateProfile>(Json.Write(template));var b=new BlockAsset{Id="PENDING",NativeSource=new(){FileHash=new string('a',64),BlockName="PENDING",Min=new(0,0),Max=new(1200,1200),DynamicProperties=new(){new(){Name="WIDTH",DefaultValue=1100}}},Usage=new(){Layer="A-DOOR",Role="door",WidthParameter="WIDTH"}};t.Blocks.Add(b);var op=new Operation{Kind="insert_block",AssetId=b.Id,Layer="A-DOOR",Points=new(){new(1000,1000)}};var r=new DraftingRecipe();Validation.Operation(op,t,true,true);DraftingRules.Enforce(new(){op},r,t,true);Assert(!b.Usage.Reviewed&&!b.Usage.ConversionConfirmed);Reject("CATALOG_REVIEW_REQUIRED",()=>Validation.Operation(op,t,true));Reject("SKILL_ASSET_FORBIDDEN",()=>DraftingRules.Enforce(new(){op},r,t));op.OpeningWidth=900;Reject("WIDTH_BINDING_REQUIRED",()=>Validation.Operation(op,t,true,true));});
Test("AI neutral optional values normalize without dropping meaningful invalid parameters",()=>{var op=new Operation{Kind="draw_line",OpeningWidth=0,WallThickness=0,DynamicJson="{}",Points=new(){new(0,0),new(100,100)}};AiOperationDefaults.Normalize(new[]{op});Assert(op.OpeningWidth==null&&op.WallThickness==null&&op.DynamicJson==null);Validation.Operation(op,template,false);op.WallThickness=220;AiOperationDefaults.Normalize(new[]{op});Reject("INVALID_DYNAMIC_PROPERTY",()=>Validation.Operation(op,template,false));});
Test("AI matching dimension labels become measured dimensions; incorrect labels are rejected",()=>{var op=new Operation{Kind="add_dimension",Text="16000",Points=new(){new(0,0),new(16000,0),new(0,1000)}};AiOperationDefaults.Normalize(new[]{op});Assert(op.Text=="");Validation.Operation(op,template,false);op.Text="15000";AiOperationDefaults.Normalize(new[]{op});Reject("INVALID_DIMENSION",()=>Validation.Operation(op,template,false));});
Test("Preview is permanently ALWAYS and rejects other modes",()=>{Assert(PreviewPreferences.Load(temp).Mode=="ALWAYS");Json.Atomic(Path.Combine(temp,"preview-preferences.json"),new{Mode="OFF"});Assert(PreviewPreferences.Load(temp).Mode=="ALWAYS");PreviewPreferences.Save(temp,"ALWAYS");Reject("INVALID_PREVIEW_POLICY",()=>PreviewPreferences.Save(temp,"OFF"));Reject("INVALID_PREVIEW_POLICY",()=>PreviewPreferences.Save(temp,"SMART"));});
Test("All plans require visible geometry preview",()=>{var p=Plan();Assert(new PreviewPreferences{Mode="OFF"}.DrawGraphics(p));p.Operations.Clear();Assert(new PreviewPreferences().DrawGraphics(p));});
Test("One library accepts 1000 native blocks and refuses 1001",()=>{var t=Json.Read<TemplateProfile>(Json.Write(template));t.Blocks=Enumerable.Range(0,1000).Select(i=>new BlockAsset{Id="B"+i,NativeSource=new(){FileHash=new string('a',64),BlockName="NATIVE"+i,Min=new(0,0),Max=new(10,10)}}).ToList();Validation.Template(t);t.Blocks.Add(new(){Id="EXTRA",Geometry=new(){new(){Kind="draw_line",Points=new(){new(0,0),new(1,1)}}}});Reject("INVALID_TEMPLATE",()=>Validation.Template(t));});
Test("Output anchor translates scope without moving source frame",()=>{var task=new TaskInfo{Scope=new(){new(100,200),new(100,6200),new(-4100,6200),new(-4100,200)},OutputPoint=new(30000,40000)};var scope=TaskPlacement.OutputScope(task);Assert(scope[0].X==30000&&scope[0].Y==40000&&scope[1].Y==46000&&scope[2].X==25800);Assert(task.Scope[0].X==100);});
Test("Legacy plan hashes omit absent output anchor fields",()=>{string value=Json.Write(Plan());Assert(!value.Contains("TaskScope")&&!value.Contains("OutputPoint"));});
Test("Shared warehouse resolves versions outside app and refuses missing warehouse",()=>{string app=Path.Combine(temp,"warehouse-app"),shared=Path.Combine(temp,"warehouse-shared");Directory.CreateDirectory(app);foreach(string sharedFolder in new[]{"Versions","Skills","Templates","CAD/Assets"})Directory.CreateDirectory(Path.Combine(shared,sharedFolder));Json.Atomic(Path.Combine(shared,"warehouse.json"),new{schemaVersion=1});WarehousePaths.Select(app,shared);var store=new ContentStore(app);store.Publish("templates",template.Id,template.Version,template);Assert(File.Exists(Path.Combine(shared,"Versions","templates",template.Id,template.Version,"content.json")));Assert(store.Template(template.Id).Id==template.Id);Json.Atomic(Path.Combine(app,"warehouse.local.json"),new{path=Path.Combine(temp,"missing-warehouse")});Reject("WAREHOUSE_UNAVAILABLE",()=>store.Template(template.Id));});
Test("Shared source archive excludes machine session and AI plan data",()=>{string app=Path.Combine(temp,"source-warehouse-app"),shared=Path.Combine(temp,"source-shared"),data=Path.Combine(temp,"source-local");Directory.CreateDirectory(app);foreach(string sharedFolder in new[]{"Versions","Skills","Templates","CAD/Assets"})Directory.CreateDirectory(Path.Combine(shared,sharedFolder));Json.Atomic(Path.Combine(shared,"warehouse.json"),new{schemaVersion=1});WarehousePaths.Select(app,shared);var config=new InstallConfig{ContentRoot=app,DataRoot=data};var workspace=new WorkspaceService(config,new CadClient(config));var service=new WarehouseService(config,workspace);string id=Guid.NewGuid().ToString("N"),folder=Path.Combine(data,"sources");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,id+".png"),"fixture");string hash=Json.FileHash(Path.Combine(folder,id+".png"));Json.Atomic(Path.Combine(folder,id+".json"),new{id,name="source.png",hash,size=7});Json.Atomic(Path.Combine(folder,id+".analysis.json"),new{calibration=new{confirmed=true,distanceMm=16000},sessionId="PRIVATE_MACHINE",reviewOperations=new[]{"PRIVATE_AI_PLAN"}});service.ArchiveSource(id);string catalog=File.ReadAllText(Path.Combine(shared,"Sources","Catalog",id+".json"));Assert(catalog.Contains("16000")&&!catalog.Contains("PRIVATE_MACHINE")&&!catalog.Contains("PRIVATE_AI_PLAN"));Assert(File.Exists(Path.Combine(folder,id+".png")));});
Test("Versioned libraries preserve old versions and activate typed snapshots",()=>{
 var libraryRoot=Path.Combine(temp,"registry");var store=new ContentStore(libraryRoot);var t=Json.Read<TemplateProfile>(Json.Write(template));t.Id="REGISTRY_TEST";t.Version="1.0.0";store.Publish("templates",t.Id,t.Version,t);
 t.Version="1.1.0";t.ArrowSize=90;store.Publish("templates",t.Id,t.Version,t);
 Assert(store.Template(t.Id).ArrowSize==90);store.Activate("templates",t.Id,"1.0.0");Assert(store.Template(t.Id).ArrowSize==80);
 Assert(store.Versions("templates",t.Id).Count==2);Reject("VERSION_EXISTS",()=>store.Publish("templates",t.Id,"1.0.0",t));
});
Test("Published library content detects file tampering",()=>{
 var libraryRoot=Path.Combine(temp,"tamper-registry");var store=new ContentStore(libraryRoot);var t=Json.Read<TemplateProfile>(Json.Write(template));store.Publish("templates",t.Id,t.Version,t);
 File.AppendAllText(Path.Combine(libraryRoot,"packages","templates",t.Id,t.Version,"content.json")," ");Reject("RESOURCE_CHANGED",()=>store.Template(t.Id));
});
Test("Uncommitted library staging does not appear as an active package",()=>{
 var libraryRoot=Path.Combine(temp,"staging-registry");Directory.CreateDirectory(Path.Combine(libraryRoot,"packages","templates","PENDING","1.0.0-staging-test"));Assert(new ContentStore(libraryRoot).Ids("templates").Count==0);
});
Test("Library paths and kinds are bounded",()=>{
 var store=new ContentStore(Path.Combine(temp,"path-registry"));Reject("INVALID_KIND",()=>store.Active("../bad","A"));Reject("INVALID_NAME",()=>store.Active("templates","../bad"));Reject("INVALID_VERSION",()=>store.Template("A","../bad"));
});
Test("Native block metadata accepts native bounds and rejects source traversal",()=>{
 var t=Json.Read<TemplateProfile>(Json.Write(template));t.Blocks=new(){new(){Id="NATIVE",NativeSource=new(){FileHash=new string('a',64),BlockName="Door with space",Min=new(0,0),Max=new(1000,1000)}}};Validation.Template(t);t.Blocks[0].NativeSource!.FileHash="../source";Reject("INVALID_ASSET",()=>Validation.Template(t));
});
Test("Template updates cannot remove assets used by active Skills",()=>{
 var libraryRoot=Path.Combine(temp,"impact-registry");var store=new ContentStore(libraryRoot);var t=Json.Read<TemplateProfile>(Json.Write(template));store.Publish("templates",t.Id,t.Version,t);
 var skill=new SkillDefinition{Id="DEPENDENT",Name="Dependent workflow",Version="1.0.0",TemplateId=t.Id,Builder="workflow",RequiredTools=new[]{"insert_block"},Workflow=new(){new(){Kind="insert_block",AssetId="DOOR_900",Layer="A-DOOR",Points=new(){new(.5,.5)}}}};store.Publish("skills",skill.Id,skill.Version,skill);
 var svc=new WorkspaceService(new InstallConfig{ContentRoot=libraryRoot,DataRoot=Path.Combine(temp,"impact-data")},cad:null!);t.Blocks.Clear();Reject("DEPENDENCY_IMPACT",()=>svc.DemandTemplateImpact(t));
});
Test("Reviewed source mapping preserves the confirmed 16000 by 5000 scale",()=>{
 string data=Path.Combine(temp,"review-map");var c=new InstallConfig{DataRoot=data,ContentRoot=root};var intelligence=new DrawingIntelligence(c);Directory.CreateDirectory(intelligence.Folder);string id=Guid.NewGuid().ToString("N");
 Json.Atomic(intelligence.AnalysisPath(id),new{bounds=new{minX=-1000,minY=-500,maxX=18000,maxY=7000},reviewOperations=new[]{new Operation{Kind="draw_polyline",Closed=true,Layer="A-WALL",Points=new(){new(0,0),new(16000,0),new(16000,5000),new(0,5000)}}}});
 Reject("NEEDS_INPUT",()=>intelligence.Map(id,new(),out _,out _));
 var ops=intelligence.Map(id,new(){["confirmGeometry"]=true},out var binding,out _);
 Assert(ops[0].Points[1].X-ops[0].Points[0].X==16000&&ops[0].Points[2].Y-ops[0].Points[1].Y==5000);
 Assert(binding.SourceId==id&&binding.Hash==Json.FileHash(intelligence.AnalysisPath(id)));
});
Test("App image rotation normalization is idempotent",()=>{
 var c=new InstallConfig{DataRoot=Path.Combine(temp,"image-units"),ContentRoot=root};var w=new WorkspaceService(c,new CadClient(c));using var accounts=new AiAccounts(c);var image=new ImagePlanService(c,accounts,w);Directory.CreateDirectory(w.Intelligence.Folder);string id=Guid.NewGuid().ToString("N");
 Json.Atomic(w.Intelligence.AnalysisPath(id),new{reviewMetadata=new{},reviewOperations=new[]{new Operation{Kind="insert_block",AssetId="DOOR_900",Layer="A-DOOR",Rotation=90,Points=new(){new(1000,1000)}}}});Json.Atomic(Path.Combine(w.Intelligence.Folder,id+".json"),new{});
 var once=image.Finalize(id,"degrees");double angle=once["reviewOperations"]![0]!.Value<double>("Rotation");Assert(Math.Abs(angle-Math.PI/2)<1e-12);
 string hash=Json.FileHash(w.Intelligence.AnalysisPath(id));image.Finalize(id,"degrees");Assert(hash==Json.FileHash(w.Intelligence.AnalysisPath(id)),"Rotation normalization ran twice");
});
Test("Valid pinned door plan within scope",()=>Validation.Plan(Plan()));
Test("Outside task scope rejected",()=>{var p=Plan();p.Operations[0].Points[0]=new(5900,4100);Reject("OUTSIDE_SCOPE",()=>Validation.Plan(p));});
Test("Crossing line endpoints outside rejected",()=>{var p=Plan();p.Operations=new(){new(){Kind="draw_line",Layer="A-AI",Points=new(){new(-1,100),new(7000,100)}}};Reject("OUTSIDE_SCOPE",()=>Validation.Plan(p));});
Test("Nonfinite coordinate rejected",()=>{var p=Plan();p.Operations[0].Points[0].X=double.NaN;Reject("INVALID_GEOMETRY",()=>Validation.Plan(p));});
Test("Unknown tool rejected",()=>{var p=Plan();p.Operations[0].Kind="execute_lisp";Reject("UNSUPPORTED_TOOL",()=>Validation.Plan(p));});
Test("Template mutation invalidates snapshot",()=>{var p=Plan();p.TemplateHash=new string('0',64);Reject("RESOURCE_CHANGED",()=>Validation.Plan(p));});
Test("Missing block rejected",()=>{var p=Plan();p.Operations[0].AssetId="MISSING";Reject("MISSING_ASSET",()=>Validation.Plan(p));});
Test("Missing layer rejected",()=>{var p=Plan();p.Operations[0].Layer="MISSING";Reject("MISSING_LAYER",()=>Validation.Plan(p));});
Test("Concave boundary rejected",()=>{var p=Plan();p.Scope=new(){new(0,0),new(6000,0),new(2000,1000),new(6000,4200),new(0,4200)};Reject("UNSUPPORTED_SCOPE",()=>Validation.Plan(p));});
Test("Degenerate boundary rejected",()=>Reject("INVALID_SCOPE",()=>Validation.Scope(new List<Point2>{new(0,0),new(1,0),new(2,0)})));
Test("Rotation of scope and placement remains bounded",()=>{var p=Plan();p.Scope=p.Scope.Select(q=>new Point2(-q.Y+5000,q.X)).ToList();p.Operations[0].Points[0]=new(3500,2000);p.Operations[0].Rotation=Math.PI/2;Validation.Plan(p);});
Test("Old plan expires",()=>{var p=Plan();p.CreatedUtc=DateTime.UtcNow.AddDays(-2);Reject("STALE_PLAN",()=>Validation.Plan(p));});
Test("Layout write not silently treated as Model",()=>{var p=Plan();p.Space="Layout1";Reject("UNSUPPORTED_PLAN",()=>Validation.Plan(p));});
Test("Unit mismatch rejected",()=>{var p=Plan();p.Units="m";Reject("UNSUPPORTED_PLAN",()=>Validation.Plan(p));});
Test("JSON duplicate properties rejected",()=>Reject("INVALID_JSON",()=>Json.Read<JObject>("{\"id\":1,\"id\":2}")));
Test("JSON trailing content rejected",()=>Reject("INVALID_JSON",()=>Json.Read<JObject>("{} {}")));
Test("Resource ID path traversal rejected",()=>Reject("INVALID_NAME",()=>Json.Name("../secret")));
Test("Plan ID path traversal rejected",()=>Reject("INVALID_ID",()=>new PlanFiles(temp).Read("../secret")));
Test("Fractional CAD plan digest uses AutoCAD Framework precision",()=>{
 var p=Plan();p.Operations=new(){new(){Kind="draw_polyline",Layer="A-AI",Closed=true,Points=new(){new(2788.3372641367532,5244.9304935034306),new(3288.3372641367532,5744.9304935034306)}}};
 string helper=Path.Combine(root,"src","CAD.HashCompat","bin","Release","net48","AICad.HashCompat.exe");
 string digest=FrameworkPlanHash.Compute(p,helper);
 Assert(digest!=Json.Hash(p),"Fixture must reproduce the original cross-runtime mismatch");
 p.Operations[0].Points[0].X+=1e-9;
 Assert(digest!=FrameworkPlanHash.Compute(p,helper),"A coordinate change must invalidate the digest");
});
Test("Canonical hash independent of property order",()=>Assert(Json.Hash(JObject.Parse("{\"a\":1,\"b\":2}"))==Json.Hash(JObject.Parse("{\"b\":2,\"a\":1}"))));
Test("Plan survives atomic persistence",()=>{var files=new PlanFiles(temp);var p=Plan();using(files.Lock(p.PlanId))files.Write(new(){Plan=p,Hash=Json.Hash(p),Status="READY"});Assert(files.Read(p.PlanId).Hash==Json.Hash(p));});
var config=new InstallConfig{DataRoot=temp,ContentRoot=Path.Combine(temp,"baseline-content")};var client=new CadClient(config);var workspace=new WorkspaceService(config,client);workspace.Store.Publish("templates",template.Id,template.Version,template);
Test("No plugin produces empty sessions, not demo data",()=>Assert(client.Sessions().Count==0));
Test("Starter Skill validates against isolated template fixture",()=>workspace.ValidateSkill(Json.Read<SkillDefinition>(File.ReadAllText(Path.Combine(WarehousePaths.Folder(root,"skills"),"INSERT_DOOR_AXIS_DEMO","manifest.json")))));
Test("Skill missing tool cannot enable",()=>{var s=Json.Read<SkillDefinition>(File.ReadAllText(Path.Combine(WarehousePaths.Folder(root,"skills"),"DRAW_PLAN","manifest.json")));s.RequiredTools=new[]{"draw_spline"};Reject("DEPENDENCY_MISSING",()=>workspace.ValidateSkill(s));});
Test("Dispatched plan cannot cancel",()=>{var p=Plan();workspace.Plans.Write(new(){Plan=p,Hash=Json.Hash(p),Status="DISPATCHED"});Reject("INVALID_STATE",()=>workspace.Cancel(p.PlanId));});
Test("Completed plan cannot cancel or claim undo",()=>{var p=Plan();workspace.Plans.Write(new(){Plan=p,Hash=Json.Hash(p),Status="DONE"});Reject("INVALID_STATE",()=>workspace.Cancel(p.PlanId));});
Test("Recreate refuses completed, dispatched and uncertain plans",()=>{
 foreach(string status in new[]{"DONE","DISPATCHED","UNKNOWN"}){var p=Plan();workspace.Plans.Write(new(){Plan=p,Status=status,Hash=Json.Hash(p)});Reject("RECREATE_UNSAFE",()=>workspace.Recreate(p.PlanId).GetAwaiter().GetResult());}
});
Test("Recreate refuses execution evidence even on failed records",()=>{
 var p=Plan();workspace.Plans.Write(new(){Plan=p,Status="FAILED",ExecutionId=Guid.NewGuid().ToString("N"),Hash=Json.Hash(p)});Reject("RECREATE_UNSAFE",()=>workspace.Recreate(p.PlanId).GetAwaiter().GetResult());
});
Test("Ready plan cancellation persisted",()=>{var p=Plan();workspace.Plans.Write(new(){Plan=p,Hash=Json.Hash(p),Status="READY"});workspace.Cancel(p.PlanId);Assert(workspace.Plans.Read(p.PlanId).Status=="CANCELLED");});
Test("Uncertain drawing execution fences later writes",()=>{var barrier=new ExecutionBarrier(temp,"physical-test");barrier.Dispatch(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));Reject("EXECUTION_UNKNOWN",()=>barrier.DemandAvailable());});
Test("Wrong execution cannot release drawing fence",()=>{var barrier=new ExecutionBarrier(temp,"physical-test-2");barrier.Dispatch(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));Reject("EXECUTION_UNKNOWN",()=>barrier.Finish(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),"DONE"));});
Test("Known failed transaction releases drawing fence",()=>{var barrier=new ExecutionBarrier(temp,"physical-test-3");string plan=Guid.NewGuid().ToString("N"),exec=Guid.NewGuid().ToString("N");barrier.Dispatch(plan,exec);barrier.Finish(plan,exec,"FAILED");barrier.DemandAvailable();});
Test("AI process round-trips Vietnamese prompts as UTF-8",()=>{
 using var accounts=new AiAccounts(new InstallConfig{DataRoot=temp,CodexExecutable=Environment.ProcessPath!});
 using var child=System.Diagnostics.Process.Start(accounts.Start(new AiProfile(),"probe-utf8"))!;
 string text="Vẽ hình chữ nhật 1.200 × 800 mm – tầng ① 中文";
 child.StandardInput.Write(text);child.StandardInput.Close();
 string reply=child.StandardOutput.ReadToEnd();child.WaitForExit();
 Assert(child.ExitCode==0 && reply==text,"Unicode prompt was corrupted at process boundary");
});
Test("AI account profiles use isolated credential directories",()=>{
 using var accounts=new AiAccounts(new InstallConfig{DataRoot=temp,CodexExecutable=Environment.ProcessPath!});
 var first=new AiProfile();var second=new AiProfile();
 Assert(accounts.Home(first)!=accounts.Home(second));
 Assert(accounts.Home(first).StartsWith(Path.Combine(temp,"accounts")));
 Assert(accounts.Start(first,"login","status").Environment["CODEX_HOME"]==accounts.Home(first));
});
Test("Shared AI logout disconnects app without logging out desktop",()=>{
 var accountRoot=Path.Combine(temp,"shared-ai");Directory.CreateDirectory(accountRoot);
 using var accounts=new AiAccounts(new InstallConfig{DataRoot=accountRoot,CodexExecutable=Environment.ProcessPath!});
 accounts.LinkExisting(default).GetAwaiter().GetResult();
 Assert(accounts.Active()?.Shared==true);
 accounts.Logout(default).GetAwaiter().GetResult();
 Assert(accounts.Active()==null);
 Assert(!File.Exists(Path.Combine(accountRoot,"fake-logout-called")),"Shared logout invoked global CLI logout");
});
Test("Private AI login and logout remove its own session",()=>{
 var accountRoot=Path.Combine(temp,"private-ai");Directory.CreateDirectory(accountRoot);
 using var accounts=new AiAccounts(new InstallConfig{DataRoot=accountRoot,CodexExecutable=Environment.ProcessPath!});
 accounts.BeginLogin("Test account",default).GetAwaiter().GetResult();
 Assert(System.Threading.SpinWait.SpinUntil(()=>accounts.Active()!=null,TimeSpan.FromSeconds(8)),"Login did not finish");
 var profile=accounts.Active()!;
 Assert(File.Exists(Path.Combine(accounts.Home(profile),"fake-session")));
 accounts.Logout(default).GetAwaiter().GetResult();
 Assert(accounts.Active()==null);
 Assert(!File.Exists(Path.Combine(accounts.Home(profile),"fake-session")));
});
Test("Cancelling pending AI login never reconnects after logout",()=>{
 var accountRoot=Path.Combine(temp,"cancel-ai");Directory.CreateDirectory(accountRoot);
 using var accounts=new AiAccounts(new InstallConfig{DataRoot=accountRoot,CodexExecutable=Environment.ProcessPath!});
 accounts.BeginLogin("Cancelled account",default).GetAwaiter().GetResult();
 Reject("LOGIN_PENDING",()=>accounts.LinkExisting(default).GetAwaiter().GetResult());
 accounts.Logout(default).GetAwaiter().GetResult();
 System.Threading.Thread.Sleep(1200);
 Assert(accounts.Active()==null,"Cancelled login recreated active account");
});
Directory.CreateDirectory(Path.Combine(root,"tests","results"));Json.Atomic(Path.Combine(root,"tests","results","managed.json"),new{total=result.Count,passed=result.Count-failures,failed=failures,cases=result});Environment.ExitCode=failures==0?0:1;
