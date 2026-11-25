using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;

namespace AICad.LibraryScanner;
public sealed partial class LibrarySyncNative
{
    public static void BindCadIdentity(Database db,Transaction tx,LibrarySyncItem native,string stableId)
    {var copy=native.Copy();copy.Id=stableId;Tag(Resolve(db,tx,native),tx,ResourceKey,copy);foreach(var e in LibraryFrames.Model(db,tx).Where(e=>SampleMetadata(e,tx)?.Id==native.Id).ToArray())MarkSample(e,tx,copy);}

    // Identity-only consolidation. Actual hatch parameters must already be equal in CAD.
    public static void ConsolidateCadHatchIdentity(Database db,Transaction tx,LibrarySyncItem source,LibrarySyncItem target)
    {
        if(source.Kind!="hatches"||target.Kind!="hatches"||!HatchMatch(source.Values,target.Values))throw new CadException("HATCH_NOT_EQUAL","Hatch còn khác pattern/scale/góc/màu/layer. Sửa trên CAD trước khi gộp danh mục.");
        foreach(var hatch in MutationEntities(db,tx).OfType<Hatch>().Where(h=>Metadata(h,tx,ResourceKey)?.Id==source.Id||h.Handle.ToString()==source.Handle).ToArray())
        {Tag(hatch,tx,ResourceKey,target);if(SampleMetadata(hatch,tx)!=null)MarkSample(hatch,tx,target);}
        foreach(var e in LibraryFrames.Model(db,tx).Where(e=>SampleMetadata(e,tx)?.Id==source.Id).ToArray())
        {if(IsPresentationHelper(e,tx))tx.GetObject(e.ObjectId,OpenMode.ForWrite).Erase();else MarkSample(e,tx,target);}
    }
    public static void PresentFromCad(Database db,Transaction tx,List<LibrarySyncItem> selected,HashSet<string> newIds,List<LibrarySyncItem> hints)
    {
        var additions=new List<LibrarySyncItem>();
        foreach(var requested in selected)
        {
            var actual=(LibraryFrames.Initialized(db,tx)?ReadFramed(db,tx,hints,out _):Read(db,tx,hints)).SingleOrDefault(i=>i.Id==requested.Id);
            if(actual==null)continue;
            actual.Group=requested.Group;actual.GroupEditedInCad=true;actual.Purpose=requested.Purpose;actual.Notes=requested.Notes;
            if(requested.Values["qaDisplayName"]!=null)actual.Values["qaDisplayName"]=requested.Values["qaDisplayName"]!.DeepClone();
            // Only management metadata is written back; never call Write/EditResource.
            Tag(Resolve(db,tx,actual),tx,ResourceKey,actual);
            bool hasSample=ModelHasSample(db,tx,actual.Id);
            if(!hasSample&&newIds.Contains(actual.Id))
            {
                var model=LibraryFrames.Model(db,tx).ToArray();Entity? sample=null;var resource=Resolve(db,tx,actual);
                var framedSample=LibraryFrames.Initialized(db,tx)?new LibraryScanScope(db,tx).Sample(actual.Kind,actual.Handle):ObjectId.Null;
                if(!framedSample.IsNull)sample=(Entity)tx.GetObject(framedSample,OpenMode.ForRead);
                else if(resource is BlockTableRecord block)
                {
                    sample=model.OfType<BlockReference>().FirstOrDefault(b=>(b.IsDynamicBlock?b.DynamicBlockTableRecord:b.BlockTableRecord)==block.ObjectId);
                    if(sample==null)
                    {
                        var existing=MutationEntities(db,tx).OfType<BlockReference>().FirstOrDefault(b=>(b.IsDynamicBlock?b.DynamicBlockTableRecord:b.BlockTableRecord)==block.ObjectId);
                        if(existing!=null)sample=(Entity)existing.Clone();
                    }
                    // A bare definition is not permission to generate a block insertion.
                    if(sample==null)continue;
                }
                else if(resource is LayerTableRecord layer)sample=new Line(Point3d.Origin,new Point3d(5200,0,0)){LayerId=layer.ObjectId};
                else if(resource is TextStyleTableRecord style)sample=new DBText{TextString="Aa Bb 0123456789",TextStyleId=style.ObjectId,Height=style.TextSize>0?style.TextSize:200};
                else if(resource is DimStyleTableRecord dim)sample=new RotatedDimension(0,Point3d.Origin,new Point3d(3000,0,0),new Point3d(0,800,0),"",dim.ObjectId);
                else if(resource is Hatch hatch)
                {
                    var owner=(BlockTableRecord)tx.GetObject(hatch.OwnerId,OpenMode.ForRead);
                    if(owner.Name==BlockTableRecord.ModelSpace)sample=hatch;
                    else sample=CopyHatchPreview(db,tx,hatch);
                }
                if(sample!=null)
                {
                    if(sample.ObjectId.IsNull){var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);ms.AppendEntity(sample);tx.AddNewlyCreatedDBObject(sample,true);}
                    if(sample is Dimension dimSample)dimSample.RecomputeDimensionBlock(true);
                    MarkSample(sample,tx,actual);Tag(sample,tx,NativeSampleKey,actual);LibraryFrames.AddCell(db,tx,actual,sample);hasSample=true;additions.Add(actual);
                }
            }
            if(hasSample)
            {
                foreach(var sample in LibraryFrames.Model(db,tx).Where(e=>SampleMetadata(e,tx)?.Id==actual.Id).ToArray())
                {
                    // Geometry and variant choices belong to CAD. Refresh only the
                    // generated display of a style that AutoCAD already owns.
                    if(sample is Dimension dimension){tx.GetObject(sample.ObjectId,OpenMode.ForWrite);dimension.RecomputeDimensionBlock(true);}
                    if(sample is DBText text&&resourceStyle(db,tx,actual) is TextStyleTableRecord textStyle)
                    {tx.GetObject(text.ObjectId,OpenMode.ForWrite);if(textStyle.TextSize>0)text.Height=textStyle.TextSize;text.WidthFactor=textStyle.XScale;text.Oblique=textStyle.ObliquingAngle;}
                    if(sample is Hatch preview&&resourceStyle(db,tx,actual) is Hatch source&&preview.ObjectId!=source.ObjectId)
                    {
                        if(!HatchMatch(HatchValues(preview),HatchValues(source)))
                        {var position=preview.GeometricExtents.MinPoint;var replacement=CopyHatchPreview(db,tx,source);replacement.TransformBy(Matrix3d.Displacement(position-replacement.GeometricExtents.MinPoint));MarkSample(replacement,tx,actual);tx.GetObject(preview.ObjectId,OpenMode.ForWrite).Erase();}
                    }
                }
                RefreshResourcePresentation(db,tx,actual);
            }
            else if(!newIds.Contains(actual.Id))RemoveOrphanPresentation(db,tx,new[]{actual.Id});
        }
        if(additions.Count>0)
        {
            var nodes=LibraryFrames.Nodes(db,tx);var all=Read(db,tx,hints);
            foreach(var grouping in additions.GroupBy(i=>i.Kind+"\n"+i.Group))
            {var first=grouping.First();var node=LibraryFrames.Ensure(nodes,first.Kind,first.Group);LibraryFrames.AppendToGroup(db,tx,node,all,grouping.ToList());}
            LibraryFrames.Save(db,tx,nodes);LibraryFrames.Clear(db,tx);LibraryFrames.Build(db,tx,all.Where(i=>ModelHasSample(db,tx,i.Id)).ToList());
        }
    }
    static Hatch CopyHatchPreview(Database db,Transaction tx,Hatch source)
    {
        var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var mapping=new IdMapping();
        db.DeepCloneObjects(new ObjectIdCollection(new[]{source.ObjectId}),bt[BlockTableRecord.ModelSpace],mapping,false);
        var preview=(Hatch)tx.GetObject(mapping[source.ObjectId].Value,OpenMode.ForWrite);if(preview.Associative)preview.Associative=false;
        if(!preview.ExtensionDictionary.IsNull){var dictionary=(DBDictionary)tx.GetObject(preview.ExtensionDictionary,OpenMode.ForWrite);if(dictionary.Contains(ResourceKey))dictionary.Remove(ResourceKey);}
        return preview;
    }
    static DBObject? resourceStyle(Database db,Transaction tx,LibrarySyncItem item)=>item.Kind=="texts"||item.Kind=="hatches"?Resolve(db,tx,item):null;
    public static void RemoveOrphanPresentation(Database db,Transaction tx,IEnumerable<string> ids)
    {
        var selected=new HashSet<string>(ids);
        foreach(var e in LibraryFrames.Model(db,tx).Where(e=>IsPresentationHelper(e,tx)&&SampleMetadata(e,tx) is LibrarySyncItem tag&&selected.Contains(tag.Id)).ToArray())
            tx.GetObject(e.ObjectId,OpenMode.ForWrite).Erase();
    }
}
