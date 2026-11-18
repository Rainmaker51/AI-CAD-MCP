using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

void Check(bool condition,string message){if(!condition)throw new Exception(message);}
var item=new LibrarySyncItem{Id=Guid.NewGuid().ToString("N"),Kind="layers",Name="QA_TEST",Values=JObject.FromObject(new{color=7,lineWeight=9,lineType="Continuous",plottable=true})};
LibrarySyncRules.Validate(item);
var state=new LibrarySyncState{Items=new(){item.Copy()}};
string Status(LibrarySyncItem? native)=>LibrarySyncRules.Compare(state,native==null?Array.Empty<LibrarySyncItem>():new[]{native}).Single().Status;
Check(Status(item.Copy())=="MATCH","Equal resources must match");
state.Baselines[item.Id]=new LibrarySyncBaseline{App=item.Signature(),Cad=item.Signature()};
var cad=item.Copy();cad.Values["color"]=3;
Check(Status(cad)=="CAD_CHANGED","CAD-only edit must be offered to app");
state.Items[0].Notes="Chỉnh trong app";
Check(Status(item.Copy())=="APP_CHANGED","App-only edit must be offered to CAD");
Check(Status(cad)=="CONFLICT","Concurrent edits must require a choice");
cad.Name="QA_RENAMED";
Check(Status(cad)=="CONFLICT","Stable ID must match through rename");
Check(Status(null)=="CAD_MISSING","Missing resource must not silently delete app data");
LibrarySyncRules.ValidateRemoval(new LibrarySyncDifference{App=item});
foreach(var invalidRemoval in new[]{new LibrarySyncDifference{App=item,Cad=cad},new LibrarySyncDifference{Cad=cad},new LibrarySyncDifference()})
{
 try{LibrarySyncRules.ValidateRemoval(invalidRemoval);throw new Exception("Removal accepted without an app-only resource");}catch(CadException e)when(e.Code=="SYNC_REMOVE"){}
}
var fresh=new LibrarySyncState();Check(LibrarySyncRules.Compare(fresh,new[]{item}).Single().Status=="CAD_NEW","New CAD resource detection failed");
fresh.Items.Add(item);Check(LibrarySyncRules.Compare(fresh,Array.Empty<LibrarySyncItem>()).Single().Status=="APP_NEW","New app resource detection failed");
foreach(int weight in new[]{10,12,14,-99})
{
 var invalid=item.Copy();invalid.Values["lineWeight"]=weight;
 try{LibrarySyncRules.Validate(invalid);throw new Exception("Invalid AutoCAD weight accepted");}catch(CadException e)when(e.Code=="SYNC_LAYER"){}
}
var invalidId=item.Copy();invalidId.Id="../source";
try{LibrarySyncRules.Validate(invalidId);throw new Exception("Path traversal ID accepted");}catch(CadException){}
var copy=item.Copy();copy.Values["color"]=1;Check(item.Values.Value<int>("color")==7,"Draft copy mutates published settings");
var json=Json.Write(new LibrarySyncState{Items=new(){item},Baselines=new Dictionary<string,LibrarySyncBaseline>{{item.Id,new(){App=item.Signature(),Cad=item.Signature()}}}});
Check(LibrarySyncRules.Compare(Json.Read<LibrarySyncState>(json),new[]{item}).Single().Status=="MATCH","Baseline persistence failed");
Console.WriteLine("PASS: two-way changes, conflicts, stable identity, missing resources, valid AutoCAD weights and persisted baselines");
var classified=item.Copy();classified.Group="Kiến trúc / Nội thất";
var groupingState=new LibrarySyncState{Items=new(){classified},Baselines=new(){{item.Id,new(){App=item.Signature(),Cad=item.Signature(),AppContent=item.ContentSignature(),CadContent=item.ContentSignature()}}}};
var grouped=LibrarySyncRules.Compare(groupingState,new[]{item.Copy()}).Single();
Check(grouped.Status=="MATCH"&&grouped.GroupDifferent,"Classification alone must not require CAD synchronization");
var modifiedCad=item.Copy();modifiedCad.Values["color"]=5;
Check(LibrarySyncRules.Compare(groupingState,new[]{modifiedCad}).Single().Status=="CAD_CHANGED","App classification must not conflict with a CAD data edit");
classified.Values["lineWeight"]=25;
Check(LibrarySyncRules.Compare(groupingState,new[]{item.Copy()}).Single().Status=="APP_CHANGED","App data edits must still require synchronization");
Check(LibrarySyncRules.Compare(groupingState,new[]{modifiedCad}).Single().Status=="CONFLICT","Concurrent data edits must still conflict");
var legacyGrouping=new LibrarySyncState{Items=new(){item.Copy()},Baselines=new(){{item.Id,new(){App=item.Signature(),Cad=item.Signature()}}}};
legacyGrouping.Items[0].Group="Nhóm mới";
Check(LibrarySyncRules.Compare(legacyGrouping,new[]{item.Copy()}).Single().Status=="MATCH","Old baselines must also tolerate classification-only changes");
Console.WriteLine("PASS: app classification stays matched; CAD/app data edits and conflicts remain detectable");
var cadClassification=item.Copy();cadClassification.Group="Đổi trực tiếp trong CAD";cadClassification.GroupEditedInCad=true;
var cadClassificationState=new LibrarySyncState{Items=new(){item.Copy()},Baselines=new(){{item.Id,new(){App=item.Signature(),Cad=item.Signature(),AppContent=item.ContentSignature(),CadContent=item.ContentSignature(),CadGroup=item.Group,AppGroup=item.Group}}}};
var cadClassificationDiff=LibrarySyncRules.Compare(cadClassificationState,new[]{cadClassification}).Single();
Check(cadClassificationDiff.Status=="CAD_GROUP_CHANGED"&&cadClassificationDiff.CadGroupChanged&&cadClassificationDiff.Suggested=="cad","CAD group edits must be importable");
cadClassificationState.Items[0].Group="Đồng thời đổi trong app";
Check(LibrarySyncRules.Compare(cadClassificationState,new[]{cadClassification}).Single().Status=="CONFLICT","Concurrent group edits must require a choice");
cadClassificationState.Baselines[item.Id].CadGroup=cadClassification.Group;
Check(LibrarySyncRules.Compare(cadClassificationState,new[]{cadClassification}).Single().Status=="MATCH","Old CAD flag must not override a newer app-only classification");
Console.WriteLine("PASS: CAD group adoption, concurrent group conflict and app-only regrouping after CAD import");
