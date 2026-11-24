using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Library;
public sealed partial class NativeTests
{
    static void VerifyMutations(string evidence)
    {
        using var db=new Database(true,true);var previous=HostApplicationServices.WorkingDatabase;HostApplicationServices.WorkingDatabase=db;
        try
        {
            ObjectId unusedId,nestedId,sourceId,targetId;LibrarySyncItem source,target;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);
                var unused=new BlockTableRecord{Name="QA_DELETE_UNUSED"};bt.Add(unused);tx.AddNewlyCreatedDBObject(unused,true);unusedId=unused.ObjectId;
                var leaf=new BlockTableRecord{Name="QA_DELETE_NESTED"};bt.Add(leaf);tx.AddNewlyCreatedDBObject(leaf,true);nestedId=leaf.ObjectId;
                var geometry=new Line(Point3d.Origin,new Point3d(100,100,0));leaf.AppendEntity(geometry);tx.AddNewlyCreatedDBObject(geometry,true);
                var container=new BlockTableRecord{Name="QA_CONTAINER"};bt.Add(container);tx.AddNewlyCreatedDBObject(container,true);
                var reference=new BlockReference(Point3d.Origin,nestedId);container.AppendEntity(reference);tx.AddNewlyCreatedDBObject(reference,true);
                Hatch Add(BlockTableRecord owner,string pattern,double scale)
                {
                    var hatch=new Hatch();owner.AppendEntity(hatch);tx.AddNewlyCreatedDBObject(hatch,true);hatch.SetHatchPattern(HatchPatternType.PreDefined,pattern);hatch.PatternScale=scale;
                    var loop=new HatchLoop(HatchLoopTypes.External|HatchLoopTypes.Polyline);foreach(var p in new[]{new Point2d(0,0),new Point2d(1000,0),new Point2d(1000,800),new Point2d(0,800)})loop.Polyline.Add(new BulgeVertex(p,0));hatch.AppendLoop(loop);hatch.EvaluateHatch(true);return hatch;
                }
                sourceId=Add(container,"ANSI31",20).ObjectId;targetId=Add((BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite),"SOLID",1).ObjectId;
                var items=LibrarySyncNative.ReadDrawing(db,tx);source=items.Single(i=>i.Kind=="hatches"&&i.Values.Value<string>("pattern")=="ANSI31");target=items.Single(i=>i.Kind=="hatches"&&i.Values.Value<string>("pattern")=="SOLID");tx.Commit();
            }
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var hatch=(Hatch)tx.GetObject(sourceId,OpenMode.ForRead);int loops=hatch.NumberOfLoops;double area=hatch.Area;
                Check(LibrarySyncNative.MergeHatches(db,tx,new(){source},target)==1,"Merge must find nested hatch");
                Check(hatch.PatternName=="SOLID"&&hatch.NumberOfLoops==loops&&Math.Abs(hatch.Area-area)<1e-5,"Merge changed boundary");
                var read=LibrarySyncNative.ReadDrawing(db,tx,new(){source,target});Check(read.All(i=>i.Id!=source.Id)&&read.Count(i=>i.Id==target.Id)==1,"Merge left duplicate resource or lost target ID");tx.Commit();
            }
            using(var tx=db.TransactionManager.StartTransaction())
            {
                LibrarySyncNative.DeleteResources(db,tx,new(){new(){Id=Guid.NewGuid().ToString("N"),Kind="blocks",Name="QA_DELETE_UNUSED"},new(){Id=Guid.NewGuid().ToString("N"),Kind="blocks",Name="QA_DELETE_NESTED"}});tx.Commit();
            }
            Check(unusedId.IsErased&&nestedId.IsErased,"Purge left block definitions");
            bool blocked=false;using(var tx=db.TransactionManager.StartTransaction())try{LibrarySyncNative.DeleteResources(db,tx,new(){new(){Id=Guid.NewGuid().ToString("N"),Kind="layers",Name="0"}});tx.Commit();}catch(InvalidOperationException){blocked=true;}
            Check(blocked,"System layer deletion not blocked");
            using(var tx=db.TransactionManager.StartOpenCloseTransaction())Check(!targetId.IsErased&&((Hatch)tx.GetObject(targetId,OpenMode.ForRead)).PatternName.Equals("SOLID",StringComparison.OrdinalIgnoreCase),"Failed delete did not roll back: erased="+targetId.IsErased);
            ObjectId protectedText;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var texts=(TextStyleTable)tx.GetObject(db.TextStyleTableId,OpenMode.ForWrite);var style=new TextStyleTableRecord{Name="QA_REFERENCED_TEXT",FileName="txt.shx"};texts.Add(style);tx.AddNewlyCreatedDBObject(style,true);
                var dims=(DimStyleTable)tx.GetObject(db.DimStyleTableId,OpenMode.ForWrite);var dim=new DimStyleTableRecord{Name="QA_REFERENCING_DIM"};dims.Add(dim);tx.AddNewlyCreatedDBObject(dim,true);dim.Dimtxsty=style.ObjectId;db.Textstyle=style.ObjectId;
                var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var text=new DBText{TextString="KEEP",TextStyleId=style.ObjectId,Height=100};model.AppendEntity(text);tx.AddNewlyCreatedDBObject(text,true);protectedText=text.ObjectId;tx.Commit();
            }
            bool dependencyBlocked=false;
            using(var tx=db.TransactionManager.StartTransaction())try{LibrarySyncNative.DeleteResources(db,tx,new(){new(){Id=Guid.NewGuid().ToString("N"),Kind="texts",Name="QA_REFERENCED_TEXT"}});tx.Commit();}catch(InvalidOperationException){dependencyBlocked=true;}
            Check(dependencyBlocked&&!protectedText.IsErased,"Dependency failure rollback: blocked="+dependencyBlocked+", erased="+protectedText.IsErased);
            JsonUtil.Atomic(Path.Combine(evidence,"mutations.json"),new{passed=true,nestedMerge=true,boundaryPreserved=true,targetIdPreserved=true,definitionPurge=true,nestedReferencesDeleted=true,protectedResourceRollback=true});
        }finally{HostApplicationServices.WorkingDatabase=previous;}
        var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
        string testRoot=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??"";
        Check(doc!=null&&Path.GetFullPath(doc.Name).StartsWith(Path.GetFullPath(testRoot)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Save test requires the owned Core drawing");
        using(var locked=doc!.LockDocument())
        {
            using(var tx=doc.Database.TransactionManager.StartTransaction())
            {
                var bt=(BlockTable)tx.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var line=new Line(Point3d.Origin,new Point3d(200,100,0));model.AppendEntity(line);tx.AddNewlyCreatedDBObject(line,true);tx.Commit();
            }
            doc.Database.SaveAs(doc.Name,true,DwgVersion.Current,doc.Database.SecurityParameters);
        }
        QaCadEditor.DemandSaved(doc.Name);
        JsonUtil.Atomic(Path.Combine(evidence,"save-source.json"),new{passed=true,dbmod=Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Core.Application.GetSystemVariable("DBMOD"))});
    }
}
