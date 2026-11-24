using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;

namespace AICad.LibraryScanner;

public sealed partial class LibrarySyncNative
{
    // Enumerate definitions once, including layout and nested contents; never modify xrefs.
    static IEnumerable<Entity> MutationEntities(Database db,Transaction tx)
    {
        foreach(ObjectId id in (BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead))
        {
            var block=(BlockTableRecord)tx.GetObject(id,OpenMode.ForRead);
            if(block.IsFromExternalReference||block.IsDependent)continue;
            foreach(ObjectId child in block.Cast<ObjectId>().ToArray())
                if(!child.IsErased&&tx.GetObject(child,OpenMode.ForRead) is Entity entity)yield return entity;
        }
    }
    public static bool MatchesHatch(Hatch hatch,LibrarySyncItem item,Transaction tx)=>
        Metadata(hatch,tx,ResourceKey)?.Id==item.Id||HatchMatch(HatchValues(hatch),item.Values);

    public static List<string> HatchLocations(Database db,Transaction tx,IEnumerable<LibrarySyncItem> items)
    {
        var selected=items.ToArray();
        return MutationEntities(db,tx).OfType<Hatch>().Where(h=>selected.Any(i=>MatchesHatch(h,i,tx)))
            .Select(h=>((BlockTableRecord)tx.GetObject(h.OwnerId,OpenMode.ForRead)).Name+" / "+h.Handle).ToList();
    }
    public static int MergeHatches(Database db,Transaction tx,List<LibrarySyncItem> sources,LibrarySyncItem target)
    {
        if(target.Kind!="hatches"||sources.Any(i=>i.Kind!="hatches"||i.Id==target.Id))throw new InvalidOperationException("Chọn hatch nguồn khác hatch đích.");
        var matches=MutationEntities(db,tx).OfType<Hatch>().Where(h=>sources.Any(i=>MatchesHatch(h,i,tx))).ToArray();
        var v=target.Values;var type=(HatchPatternType)Enum.Parse(typeof(HatchPatternType),v.Value<string>("type")??"PreDefined");
        foreach(var hatch in matches)
        {
            tx.GetObject(hatch.ObjectId,OpenMode.ForWrite);hatch.Layer=v.Value<string>("layer")??"0";hatch.Color=ColorOf(v);
            hatch.SetHatchPattern(type,v.Value<string>("pattern"));hatch.PatternScale=v.Value<double>("scale");hatch.PatternAngle=v.Value<double>("angle");
            if(type==HatchPatternType.UserDefined){hatch.PatternSpace=v.Value<double>("spacing");hatch.PatternDouble=v.Value<bool>("doublePattern");}
            // Preserve loops, associativity, elevation, normal and origin.
            hatch.EvaluateHatch(true);Tag(hatch,tx,ResourceKey,target);
            if(SampleMetadata(hatch,tx)!=null)MarkSample(hatch,tx,target);
        }
        RemoveAnnotations(db,tx,new HashSet<string>(sources.Select(i=>i.Id)));
        return matches.Length;
    }
    static void RemoveAnnotations(Database db,Transaction tx,HashSet<string> ids)
    {
        foreach(var entity in MutationEntities(db,tx).ToArray())
            if(entity is MText||entity is Polyline)
                if(SampleMetadata(entity,tx) is LibrarySyncItem tag&&ids.Contains(tag.Id)){tx.GetObject(entity.ObjectId,OpenMode.ForWrite).Erase();}
    }
    public static List<string> DeletionLocations(Database db,Transaction tx,List<LibrarySyncItem> items)
    {
        return MutationEntities(db,tx).Where(e=>items.Any(i=>Uses(e,i,db,tx)))
            .Select(e=>((BlockTableRecord)tx.GetObject(e.OwnerId,OpenMode.ForRead)).Name+" / "+e.Handle).ToList();
    }
    static bool Uses(Entity entity,LibrarySyncItem item,Database db,Transaction tx)
    {
        if(SampleMetadata(entity,tx)?.Id==item.Id)return true;
        switch(item.Kind)
        {
            case "blocks":return entity is BlockReference b&&string.Equals(((BlockTableRecord)tx.GetObject(b.IsDynamicBlock?b.DynamicBlockTableRecord:b.BlockTableRecord,OpenMode.ForRead)).Name,item.Name,StringComparison.OrdinalIgnoreCase);
            case "layers":return string.Equals(entity.Layer,item.Name,StringComparison.OrdinalIgnoreCase);
            case "texts":return entity is DBText text&&((TextStyleTableRecord)tx.GetObject(text.TextStyleId,OpenMode.ForRead)).Name.Equals(item.Name,StringComparison.OrdinalIgnoreCase)||entity is MText mt&&((TextStyleTableRecord)tx.GetObject(mt.TextStyleId,OpenMode.ForRead)).Name.Equals(item.Name,StringComparison.OrdinalIgnoreCase);
            case "dims":return entity is Dimension dim&&((DimStyleTableRecord)tx.GetObject(dim.DimensionStyle,OpenMode.ForRead)).Name.Equals(item.Name,StringComparison.OrdinalIgnoreCase);
            case "hatches":return entity is Hatch hatch&&MatchesHatch(hatch,item,tx);
            default:throw new InvalidOperationException("Loại thư viện không hợp lệ.");
        }
    }
    public static int DeleteResources(Database db,Transaction tx,List<LibrarySyncItem> items)
    {
        foreach(var item in items)
            if(item.Kind=="layers"&&(item.Name.Equals("0",StringComparison.OrdinalIgnoreCase)||item.Name.Equals("Defpoints",StringComparison.OrdinalIgnoreCase))||
                (item.Kind=="texts"||item.Kind=="dims")&&item.Name.Equals("Standard",StringComparison.OrdinalIgnoreCase)||item.Kind=="blocks"&&item.Name.StartsWith("*",StringComparison.Ordinal))
                throw new InvalidOperationException("Không xóa tài nguyên hệ thống: "+item.Name);
        var selectedIds=new HashSet<string>(items.Select(i=>i.Id));
        var entities=MutationEntities(db,tx).Where(e=>items.Any(i=>Uses(e,i,db,tx))||
            (e is MText||e is Polyline)&&SampleMetadata(e,tx) is LibrarySyncItem tag&&selectedIds.Contains(tag.Id)).ToArray();
        var erased=new List<ObjectId>();
        try
        {
        foreach(var entity in entities){erased.Add(entity.ObjectId);tx.GetObject(entity.ObjectId,OpenMode.ForWrite).Erase();}
        foreach(var item in items.Where(i=>i.Kind!="hatches"))
        {
            ObjectId tableId=item.Kind=="blocks"?db.BlockTableId:item.Kind=="layers"?db.LayerTableId:item.Kind=="texts"?db.TextStyleTableId:db.DimStyleTableId;
            var table=(SymbolTable)tx.GetObject(tableId,OpenMode.ForRead);if(!table.Has(item.Name))continue;
            var id=table[item.Name];var purge=new ObjectIdCollection(new[]{id});db.Purge(purge);
            if(purge.Count!=1)throw new InvalidOperationException("Không xóa được định nghĩa "+item.Name+": còn phụ thuộc hoặc là mục hệ thống. Toàn bộ lần xóa đã hủy.");
            erased.Add(id);tx.GetObject(id,OpenMode.ForWrite).Erase();
        }
        return entities.Length;
        }
        catch
        {
            // Side databases can have undo recording disabled. Do not rely only on
            // Transaction.Abort to undo erasures when a later definition cannot purge.
            foreach(var id in erased.AsEnumerable().Reverse())if(id.IsErased)tx.GetObject(id,OpenMode.ForWrite,true).Erase(false);
            throw;
        }
    }
    public static void SetResourceMetadata(Database db,Transaction tx,LibrarySyncItem item)
    {
        var hint=item.Copy();hint.Handle=""; // Browser snapshot handles belong to a clone, not this database.
        var current=Read(db,tx,new(){hint}).SingleOrDefault(i=>i.Id==item.Id);
        if(current==null)throw new InvalidOperationException("Tài nguyên không còn trong CAD: "+item.Name);
        item.Handle=current.Handle;
        Tag(Resolve(db,tx,item),tx,ResourceKey,item);
        AssignGroup(db,tx,item,item.Group);
        foreach(var label in MutationEntities(db,tx).OfType<MText>().Where(e=>SampleMetadata(e,tx)?.Id==item.Id).ToArray())
        {
            var lines=label.Text.Replace("\r","").Split('\n').ToList();
            foreach(var field in new[]{new{Key="Công dụng | ",Value=item.Purpose},new{Key="Ghi chú | ",Value=item.Notes}})
            {int index=lines.FindIndex(l=>l.StartsWith(field.Key,StringComparison.Ordinal));if(index>=0)lines[index]=field.Key+field.Value;else lines.Add(field.Key+field.Value);}
            label.UpgradeOpen();label.Contents=string.Join("\\P",lines.Select(Safe));
        }
    }
}
