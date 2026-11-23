using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;

internal sealed class QaInsertRequest
{
    internal Document Target=null!;
    internal string Snapshot="",Hash="",Name="";
    internal bool Replace;
    internal double Scale=1,Degrees;
    internal JObject Dynamic=new(),Attributes=new();
}
internal static class QaBlockInsert
{
    internal static QaInsertRequest? Pending;
    internal static void Execute()
    {
        var request=Pending;Pending=null;Commands.PendingLayout=null;
        try
        {
            if(request==null)throw new InvalidOperationException("Chọn block trong QA rồi nhấn Chèn block vào bản vẽ hiện tại.");
            if(AcApp.DocumentManager.MdiActiveDocument!=request.Target)throw new InvalidOperationException("Bản vẽ đích đã thay đổi. Chọn lại block để chèn.");
            if(AICad.Contracts.Json.FileHash(request.Snapshot)!=request.Hash)throw new InvalidOperationException("Bản đọc thư viện đã thay đổi. Làm mới CAD rồi chèn lại.");
            var point=request.Target.Editor.GetPoint("\nChọn điểm chèn block "+request.Name+": ");if(point.Status!=PromptStatus.OK)return;
            var world=point.Value.TransformBy(request.Target.Editor.CurrentUserCoordinateSystem);
            using var locked=request.Target.LockDocument();
            Insert(request.Target.Database,request,world);
            request.Target.Editor.Regen();LibraryPlugin.Current?.Message("Đã chèn "+request.Name+" vào "+Path.GetFileName(request.Target.Name)+".");
        }
        catch(Exception error){AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nThư viện QA: "+error.Message);LibraryPlugin.Current?.Message(error.Message);}
        finally{LibraryPlugin.Current?.CompleteLayout();}
    }
    internal static Dictionary<string,string> AttributeDefaults(string file,string name)
    {
        using var db=Read(file);using var tx=db.TransactionManager.StartOpenCloseTransaction();var table=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);
        return ((BlockTableRecord)tx.GetObject(table[name],OpenMode.ForRead)).Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<AttributeDefinition>().Where(a=>!a.Constant).GroupBy(a=>a.Tag,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First().TextString,StringComparer.OrdinalIgnoreCase);
    }
    internal static JObject DescribeExisting(Database target,string name)
    {
        // Clone just this definition and dependencies; never probe a resident project DB.
        using var scratch=new Database(true,true){Insunits=target.Insunits};
        using(var read=target.TransactionManager.StartOpenCloseTransaction())
        {
            var table=(BlockTable)read.GetObject(target.BlockTableId,OpenMode.ForRead);
            target.WblockCloneObjects(new ObjectIdCollection(new[]{table[name]}),scratch.BlockTableId,new IdMapping(),DuplicateRecordCloning.Ignore,false);
        }
        var previous=HostApplicationServices.WorkingDatabase;
        try{HostApplicationServices.WorkingDatabase=scratch;using var tx=scratch.TransactionManager.StartTransaction();return LibrarySyncNative.ReadDrawing(scratch,tx).Single(i=>i.Kind=="blocks"&&i.Name==name).Values;}
        finally{HostApplicationServices.WorkingDatabase=previous;}
    }
    static Database Read(string file){var db=new Database(false,true);try{db.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);return db;}catch{db.Dispose();throw;}}
    internal static ObjectId Insert(Database target,QaInsertRequest request,Point3d point)
    {
        if(!request.Scale.IsFiniteCompat()||request.Scale<=0||!request.Degrees.IsFiniteCompat())throw new InvalidOperationException("Tỷ lệ phải lớn hơn 0 và góc xoay phải hữu hạn.");
        using var donor=Read(request.Snapshot);using var sourceTx=donor.TransactionManager.StartTransaction();
        var sourceTable=(BlockTable)sourceTx.GetObject(donor.BlockTableId,OpenMode.ForRead);
        if(!sourceTable.Has(request.Name))throw new InvalidOperationException("Không còn block trong nguồn: "+request.Name);
        var sourceBlock=(BlockTableRecord)sourceTx.GetObject(sourceTable[request.Name],OpenMode.ForRead);
        UnitsValue sourceUnits=sourceBlock.Units==UnitsValue.Undefined?donor.Insunits:sourceBlock.Units;
        if(sourceUnits==UnitsValue.Undefined||target.Insunits==UnitsValue.Undefined)throw new InvalidOperationException("Đặt đơn vị bản vẽ nguồn và đích (UNITS) trước khi chèn block.");
        using var tx=target.TransactionManager.StartTransaction();var table=(BlockTable)tx.GetObject(target.BlockTableId,OpenMode.ForRead);
        ObjectId definition=table.Has(request.Name)?table[request.Name]:ObjectId.Null;
        if(!definition.IsNull&&!request.Replace){var keep=(BlockTableRecord)tx.GetObject(definition,OpenMode.ForRead);sourceUnits=keep.Units==UnitsValue.Undefined?target.Insunits:keep.Units;}
        double units=UnitsConverter.GetConversionFactor(sourceUnits,target.Insunits);
        bool import=definition.IsNull||request.Replace;
        if(import)
        {
            sourceBlock.UpgradeOpen();sourceBlock.Name="QA_IMPORT_"+Guid.NewGuid().ToString("N");
            var map=new IdMapping();donor.WblockCloneObjects(new ObjectIdCollection(new[]{sourceBlock.ObjectId}),target.BlockTableId,map,DuplicateRecordCloning.Ignore,false);
            var imported=(BlockTableRecord)tx.GetObject(map[sourceBlock.ObjectId].Value,OpenMode.ForWrite);
            if(!definition.IsNull)
            {
                var old=(BlockTableRecord)tx.GetObject(definition,OpenMode.ForWrite);old.Name="QA_PREVIOUS_"+Guid.NewGuid().ToString("N");
                foreach(ObjectId ownerId in table)
                {
                    var owner=(BlockTableRecord)tx.GetObject(ownerId,OpenMode.ForRead);if(owner.IsFromExternalReference)continue;
                    foreach(ObjectId id in owner.Cast<ObjectId>().ToArray())
                    {
                        if(tx.GetObject(id,OpenMode.ForRead) is not BlockReference existing||existing.BlockTableRecord!=definition&&(!existing.IsDynamicBlock||existing.DynamicBlockTableRecord!=definition))continue;
                        var values=existing.IsDynamicBlock?existing.DynamicBlockReferencePropertyCollection.Cast<DynamicBlockReferenceProperty>().Where(p=>!p.ReadOnly).ToDictionary(p=>p.PropertyName,p=>p.Value):new Dictionary<string,object>();
                        existing.UpgradeOpen();existing.BlockTableRecord=imported.ObjectId;
                        if(existing.IsDynamicBlock)foreach(DynamicBlockReferenceProperty property in existing.DynamicBlockReferencePropertyCollection)if(!property.ReadOnly&&values.TryGetValue(property.PropertyName,out object value)&&value.GetType()==property.Value.GetType())property.Value=value;
                    }
                }
                var unused=new ObjectIdCollection(new[]{definition});target.Purge(unused);foreach(ObjectId id in unused)tx.GetObject(id,OpenMode.ForWrite).Erase();
            }
            imported.Name=request.Name;definition=imported.ObjectId;
            // Strip QA ownership from newly cloned data; the destination is a project drawing.
            foreach(IdPair pair in map)if(pair.IsCloned&&!pair.Value.IsNull)
            {
                var obj=tx.GetObject(pair.Value,OpenMode.ForRead);if(obj.ExtensionDictionary.IsNull)continue;
                var dictionary=(DBDictionary)tx.GetObject(obj.ExtensionDictionary,OpenMode.ForWrite);
                foreach(string key in new[]{"AICAD_SYNC_RESOURCE","AICAD_SYNC_SAMPLE","AICAD_LIB_FRAME","AICAD_LIB_CELL"})if(dictionary.Contains(key)){var entry=tx.GetObject(dictionary.GetAt(key),OpenMode.ForWrite);dictionary.Remove(key);entry.Erase();}
            }
        }
        var model=(BlockTableRecord)tx.GetObject(target.CurrentSpaceId,OpenMode.ForWrite);
        var reference=new BlockReference(point,definition){Rotation=request.Degrees*Math.PI/180,ScaleFactors=new Scale3d(request.Scale*units)};
        reference.SetDatabaseDefaults(target);model.AppendEntity(reference);tx.AddNewlyCreatedDBObject(reference,true);
        if(reference.IsDynamicBlock)foreach(DynamicBlockReferenceProperty property in reference.DynamicBlockReferencePropertyCollection)
        {
            if(property.ReadOnly||request.Dynamic[property.PropertyName] is not JValue value)continue;
            object converted=Convert.ChangeType(value.Value!,property.Value.GetType(),CultureInfo.InvariantCulture);
            var allowed=property.GetAllowedValues();if(allowed.Length>0&&!allowed.Contains(converted))throw new InvalidOperationException("Giá trị không hợp lệ cho tham số động "+property.PropertyName);
            property.Value=converted;
        }
        var actual=(BlockTableRecord)tx.GetObject(reference.BlockTableRecord,OpenMode.ForRead);
        foreach(ObjectId id in actual)
        {
            if(tx.GetObject(id,OpenMode.ForRead) is not AttributeDefinition att||att.Constant)continue;
            using var attribute=new AttributeReference();attribute.SetAttributeFromBlock(att,reference.BlockTransform);attribute.TextString=request.Attributes.Value<string>(att.Tag)??att.TextString;
            reference.AttributeCollection.AppendAttribute(attribute);tx.AddNewlyCreatedDBObject(attribute,true);attribute.AdjustAlignment(target);
        }
        tx.Commit();return reference.ObjectId;
    }
}
internal static class QaNumber
{
    internal static bool IsFiniteCompat(this double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
}

internal sealed partial class QaBrowserWindow
{
    System.Threading.Tasks.Task InsertSelected()
    {
        var row=Selected;if(row==null||capture==null)throw new InvalidOperationException("Làm mới CAD và chọn block cần chèn.");
        var target=Commands.Document;if(QaCadSnapshot.Same(target.Name,Source))throw new InvalidOperationException("Chuyển sang bản vẽ đích khác DWG thư viện rồi nhấn Chèn block.");
        string file=Path.Combine(client!.Config.DataRoot,"library-sync",capture.Value<string>("id")!,"source.dwg");
        var request=new QaInsertRequest{Target=target,Snapshot=file,Hash=capture.Value<string>("snapshotHash")!,Name=row.Value<string>("name")!};
        bool keepExisting=false;
        using(var tx=target.Database.TransactionManager.StartOpenCloseTransaction())if(((BlockTable)tx.GetObject(target.Database.BlockTableId,OpenMode.ForRead)).Has(request.Name))
        {
            var collision=new QaChoice("Bản vẽ đích đã có block cùng tên",new[]{("keep","Giữ định nghĩa hiện có"),("update","Cập nhật từ thư viện (đổi các block cùng tên đã chèn)")},"keep");
            if(AcApp.ShowModalWindow(collision)!=true)return System.Threading.Tasks.Task.CompletedTask;request.Replace=collision.Choice=="update";keepExisting=!request.Replace;
        }
        var data=new JObject{["scale"]=1.0,["rotationDegrees"]=0.0};
        var form=new QaForm("Chèn "+request.Name,data,new[]{"scale","rotationDegrees"});if(AcApp.ShowModalWindow(form)!=true)return System.Threading.Tasks.Task.CompletedTask;
        request.Scale=form.Value.Value<double>("scale");request.Degrees=form.Value.Value<double>("rotationDegrees");
        if(!request.Scale.IsFiniteCompat()||request.Scale<=0||!request.Degrees.IsFiniteCompat())throw new InvalidOperationException("Tỷ lệ phải lớn hơn 0, góc xoay phải hữu hạn.");
        var values=keepExisting?QaBlockInsert.DescribeExisting(target.Database,request.Name):row["item"]?["Values"] as JObject??new();
        // Native insertion starts from DWG defaults, never from retired Skill usage profiles.
        var usage=new JObject();
        var dynamicValues=new JObject();var options=new Dictionary<string,string[]>();
        foreach(var p in (values["properties"] as JArray??new()).OfType<JObject>().Where(p=>p.Value<bool?>("readOnly")!=true))
        {
            string name=p.Value<string>("name")!;if((usage[name]??p["value"]) is JValue v)dynamicValues[name]=v.DeepClone();
            if(p["allowed"] is JArray allowed&&allowed.Count>0)options[name]=allowed.Select(v=>v.ToString()).ToArray();
        }
        if(dynamicValues.HasValues){var dynamicForm=new QaForm("Tham số động của block",dynamicValues,dynamicValues.Properties().Select(p=>p.Name).ToArray(),options);if(AcApp.ShowModalWindow(dynamicForm)!=true)return System.Threading.Tasks.Task.CompletedTask;request.Dynamic=dynamicForm.Value;}
        var defaults=new JObject();foreach(var att in (values["attributes"] as JArray??new()).OfType<JObject>().Where(a=>a.Value<bool?>("constant")!=true))defaults[att.Value<string>("tag")!]=att["value"]?.DeepClone()??"";
        if(defaults.HasValues){var attributes=new QaForm("Nội dung thuộc tính ATT",defaults,defaults.Properties().Select(p=>p.Name).ToArray());if(AcApp.ShowModalWindow(attributes)!=true)return System.Threading.Tasks.Task.CompletedTask;request.Attributes=attributes.Value;}
        QaBlockInsert.Pending=request;
        try{LibraryPlugin.Current!.BeginLayout("QALIBINSERT",new LayoutCommandContext(target.Name,"blocks","",false));}catch{QaBlockInsert.Pending=null;throw;}
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
