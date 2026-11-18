using AICad.Application;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
using System.Reflection;

void Check(bool value,string message){if(!value)throw new Exception(message);}
void Reject(Action work,string code){try{work();}catch(CadException e)when(e.Code==code){return;}throw new Exception("Expected "+code);}
var template=new TemplateProfile{Id="ISO_QUANGANH",Version="1.0.0",Layers=new(){new(){Name="QA_DOOR"}}};
BlockAsset Block(string id,string name,string parameter)=>new(){Id=id,NativeSource=new(){BlockName=name,FileHash=new string('a',64),DynamicProperties=new(){new(){Name=parameter,DefaultValue=900.0}}},Usage=new(){Role="door",Layer="QA_DOOR",Reviewed=true,WidthParameter=parameter,ConversionConfirmed=true}};
template.Blocks.Add(Block("OLD","MBCUASO","Width"));template.Blocks.Add(Block("NEW","NEW_WINDOW","W"));
var skill=new SkillDefinition{Id="SOURCE_ISO_QUANGANH",Name="Sample",TemplateId=template.Id,Version="1.0.0",Recipe=new(){AllowedBlocks=new(){"OLD","NEW"}},Workflow=new(){new(){Kind="insert_block",AssetId="OLD",Layer="QA_DOOR",Points=new(){new(100,200)},Rotation=Math.PI/2,DynamicJson="{\"Width\":810}",OpeningWidth=930}}};
var map=new Dictionary<string,string>{{"Width","W"}};
var built=BlockReplacement.Build(template,new[]{skill},"MBCUASO","NEW",map,10,0,90);
Check(built.Errors.Count==0,string.Join(";",built.Errors));
Check(skill.Workflow[0].AssetId=="OLD"&&template.Blocks.Count==2,"Preview mutated input");
Check(built.Template.Blocks.Count==1&&built.Skills[0].Recipe!.AllowedBlocks.SequenceEqual(new[]{"NEW"}),"Old links remain");
var op=built.Skills[0].Workflow[0];
Check(op.AssetId=="NEW"&&JObject.Parse(op.DynamicJson!).Value<int>("W")==810&&op.OpeningWidth==930,"Mapping changed values or opening width");
Check(Math.Abs(op.Points[0].X-100)<.001&&Math.Abs(op.Points[0].Y-210)<.001&&Math.Abs(op.Rotation-Math.PI)<.001,"Local offset/rotation wrong");
Check(BlockReplacement.Build(template,new[]{skill},"MBCUASO","NEW",new()).Errors.Count>0,"Missing dynamic mapping accepted");
var unsafeTemplate=Json.Read<TemplateProfile>(Json.Write(template));unsafeTemplate.Blocks[1].Usage!.Layer="OTHER";
Check(BlockReplacement.Build(unsafeTemplate,new[]{skill},"MBCUASO","NEW",map).Errors.Count>0,"Layer mismatch accepted");
unsafeTemplate=Json.Read<TemplateProfile>(Json.Write(template));unsafeTemplate.Blocks[1].NativeSource!.DynamicProperties![0].AllowedValues=new(){750};
Check(BlockReplacement.Build(unsafeTemplate,new[]{skill},"MBCUASO","NEW",map).Errors.Count>0,"Disallowed dynamic value accepted");
Console.WriteLine("PASS: pure preview, recipe/workflow references, local placement, dynamic mapping and compatibility checks");

string root=Path.Combine(Path.GetTempPath(),"aicad-replacement-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var config=new InstallConfig{ContentRoot=root,DataRoot=Path.Combine(root,"data")};Directory.CreateDirectory(config.DataRoot);
var store=new ContentStore(root);
string assetFolder=WarehousePaths.Folder(root,"content-assets");Directory.CreateDirectory(assetFolder);string donor=Path.Combine(root,"donor.dwg");File.WriteAllText(donor,"fixture DWG bytes");string hash=Json.FileHash(donor);File.Copy(donor,Path.Combine(assetFolder,hash+".dwg"));
foreach(var block in template.Blocks)block.NativeSource!.FileHash=hash;
store.Publish("templates",template.Id,template.Version,template);store.Publish("skills",skill.Id,skill.Version,skill);
string source=Path.Combine(root,"CAD","Originals",CadSourceFile.BaseLibraryName);Directory.CreateDirectory(Path.GetDirectoryName(source)!);File.WriteAllText(source,"source remains unchanged");string sourceHash=Json.FileHash(source);
var old=new LibrarySyncItem{Id=Guid.NewGuid().ToString("N"),Kind="blocks",Name="MBCUASO"};
var replacement=new LibrarySyncItem{Id=Guid.NewGuid().ToString("N"),Kind="blocks",Name="NEW_WINDOW"};
string statePath=Path.Combine(root,"Catalog","sync","state.json");Json.Atomic(statePath,new LibrarySyncState{Items=new(){old,replacement}});
string reviewId=Guid.NewGuid().ToString("N"),reviewFile=Path.Combine(config.DataRoot,"library-sync",reviewId,"review.json");
var service=new LibrarySyncService(config);
string AppHash()=>(string)typeof(LibrarySyncService).GetMethod("AppHash",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(service,null)!;
void Review()=>Json.Atomic(reviewFile,new{sourceHash,appHash=AppHash(),revision=0,native=new[]{replacement}});
Review();
var request=JObject.FromObject(new{id=reviewId,sourceId=old.Id,targetId="NEW",parameterMap=map,offsetX=0,offsetY=0,rotation=0});
var preview=JObject.FromObject(service.PreviewReplacement(request));Check(!preview["errors"]!.Any(),"Preview blocked compatible fixture");
Check(store.Active("skills",skill.Id)=="1.0.0"&&File.Exists(source),"Preview published changes");
Reject(()=>service.ApplyReplacement(JObject.FromObject(new{request,fingerprint=preview.Value<string>("fingerprint"),confirmed=false})),"REPLACEMENT_APPROVAL");
var changed=(JObject)request.DeepClone();changed["offsetX"]=12;
Reject(()=>service.ApplyReplacement(JObject.FromObject(new{request=changed,fingerprint=preview.Value<string>("fingerprint"),confirmed=true})),"RESOURCE_CHANGED");
string skillPointer=Path.Combine(root,"packages","skills",skill.Id,"active.json");
using(var locked=new FileStream(skillPointer,FileMode.Open,FileAccess.Read,FileShare.Read))
{
    bool failed=false;
    try{service.ApplyReplacement(JObject.FromObject(new{request,fingerprint=preview.Value<string>("fingerprint"),confirmed=true}));}
    catch(Exception e)when(e is IOException or UnauthorizedAccessException){failed=true;}
    Check(failed,"Locked publication was accepted");
}
Check(store.Active("templates",template.Id)=="1.0.0"&&store.Active("skills",skill.Id)=="1.0.0","Failed migration did not roll back pointers");
Check(!File.Exists(Path.Combine(root,"Catalog","block-replacements.json"))&&Json.FileHash(source)==sourceHash,"Failed migration left a retirement policy or changed source");
Review();preview=JObject.FromObject(service.PreviewReplacement(request));
var result=JObject.FromObject(service.ApplyReplacement(JObject.FromObject(new{request,fingerprint=preview.Value<string>("fingerprint"),confirmed=true})));
Check(result.Value<string>("status")=="REPLACED"&&Json.FileHash(source)==sourceHash,"Replacement mutated DWG");
Check(store.Active("skills",skill.Id)!="1.0.0"&&store.Active("templates",template.Id)!="1.0.0","Versions not published");
Check(store.Skill(skill.Id).Workflow[0].AssetId=="NEW"&&store.Skill(skill.Id,"1.0.0").Workflow[0].AssetId=="OLD","History or active links wrong");
Check(Json.Read<LibrarySyncState>(File.ReadAllText(statePath)).Items.All(i=>i.Id!=old.Id),"Old block remains in sync state");
var oldPlan=new DrawingPlan{Template=template,Operations=skill.Workflow};
Reject(()=>BlockReplacementPolicy.Demand(root,oldPlan),"RESOURCE_REPLACED");
BlockReplacementPolicy.Demand(root,new(){Template=store.Template(template.Id),Operations=store.Skill(skill.Id).Workflow});
Reject(()=>service.ApplyReplacement(JObject.FromObject(new{request,fingerprint=preview.Value<string>("fingerprint"),confirmed=true})),"RESOURCE_CHANGED");
Console.WriteLine("PASS: confirmation, exact preview approval, rollback on locked publication, immutable versions, removal, preserved source/history, old-plan guard and replay rejection");
