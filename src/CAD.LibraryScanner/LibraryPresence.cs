using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;

namespace AICad.LibraryScanner;

public static class LibraryPresence
{
    public static void RebindSnapshotHandles(Database db,Transaction tx)
    {
        var objects=new List<DBObject>();
        foreach(ObjectId tableId in new[]{db.LayerTableId,db.TextStyleTableId,db.DimStyleTableId,db.BlockTableId})
            foreach(ObjectId id in (SymbolTable)tx.GetObject(tableId,OpenMode.ForRead))
            {
                var item=tx.GetObject(id,OpenMode.ForRead);objects.Add(item);
                if(item is BlockTableRecord block)foreach(ObjectId child in block)if(!child.IsErased)objects.Add(tx.GetObject(child,OpenMode.ForRead));
            }
        foreach(var obj in objects)LibrarySyncNative.RebindSnapshotHandle(obj,tx);
    }
    public static JObject Read(Database db,Transaction tx,List<LibrarySyncItem> items)
    {
        var result=new JObject();var byName=items.Where(i=>i.Kind=="blocks").ToDictionary(i=>i.Name,StringComparer.OrdinalIgnoreCase);
        foreach(var item in byName.Values)result[item.Id]=new JObject{["definitionPresent"]=true,["samplePresent"]=false,["modelReferences"]=0,["layoutReferences"]=0,["nestedReferences"]=0};
        foreach(ObjectId id in (BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead))
        {
            var owner=(BlockTableRecord)tx.GetObject(id,OpenMode.ForRead);if(owner.IsFromExternalReference)continue;
            foreach(ObjectId child in owner)
            {
                if(child.IsErased||tx.GetObject(child,OpenMode.ForRead) is not BlockReference block)continue;
                var definition=(BlockTableRecord)tx.GetObject(block.IsDynamicBlock?block.DynamicBlockTableRecord:block.BlockTableRecord,OpenMode.ForRead);
                if(!byName.TryGetValue(definition.Name,out var item))continue;
                var presence=(JObject)result[item.Id]!;
                string key=owner.Name==BlockTableRecord.ModelSpace?"modelReferences":owner.IsLayout?"layoutReferences":"nestedReferences";
                presence[key]=presence.Value<int>(key)+1;if(owner.IsLayout)presence["samplePresent"]=true;
            }
        }
        return result;
    }
}
