using System;
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
    static void VerifyLayerRename(string evidence)
    {
        using var db=new Database(true,true);var previous=HostApplicationServices.WorkingDatabase;HostApplicationServices.WorkingDatabase=db;
        try
        {
            string original="QA_KH_KC-01",renamed="QA_KC_01",id=Guid.NewGuid().ToString("N");
            var item=new LibrarySyncItem{Id=id,Kind="layers",Name=original,Group="Kết cấu",Purpose="KEEP PURPOSE",Notes="KEEP NOTES",Values=JObject.FromObject(new{color=150,rgb=(int?)null,lineWeight=13,lineType="Continuous",plottable=true,qaDisplayName=original})};
            ObjectId layerId,labelId,sampleId;Point3d labelPosition,sampleStart;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                LibrarySyncNative.EditResource(db,tx,item,new());
                layerId=((LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForRead))[original];
                var model=LibraryFrames.Model(db,tx).ToList();var label=model.OfType<MText>().Single(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==id);
                var sample=model.OfType<Line>().Single(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==id);sampleId=sample.ObjectId;sampleStart=sample.StartPoint;
                tx.GetObject(label.ObjectId,OpenMode.ForWrite);label.Contents=original+"\\PACI 150 | LW 13 | Continuous | In True\\PNhóm | Kết cấu\\PCông dụng | KEEP PURPOSE\\PGhi chú | KEEP NOTES\\PCUSTOM EXTRA LINE";
                labelId=label.ObjectId;labelPosition=label.Location;tx.Commit();
            }
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var hints=LibrarySyncNative.ReadDrawing(db,tx);var edit=hints.Single(i=>i.Id==id).Copy();edit.Name=renamed;edit.Values["color"]=6;edit.Values["lineWeight"]=25;
                LibrarySyncNative.EditResource(db,tx,edit,hints);
                var table=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForRead);Check(table.Has(renamed)&&!table.Has(original)&&table[renamed]==layerId,"Native layer name/identity did not change");
                var label=(MText)tx.GetObject(labelId,OpenMode.ForRead);Check(label.Text.Replace("\r","").Split('\n')[0]==renamed,"Layer renamed but CAD presentation caption stayed old");
                Check(label.Text.Contains("ACI 6")&&!label.Text.Contains("ACI 150"),"Layer presentation retained old color");
                Check(label.Text.Contains("LW 0.25 mm"),"Layer presentation retained old lineweight");
                Check(label.Location==labelPosition&&((Line)tx.GetObject(sampleId,OpenMode.ForRead)).StartPoint==sampleStart,"Rename moved presentation");
                Check(label.Text.Contains("KEEP PURPOSE")&&label.Text.Contains("KEEP NOTES")&&label.Text.Contains("CUSTOM EXTRA LINE"),"Rename lost metadata or custom label text");
                var read=LibrarySyncNative.ReadDrawing(db,tx,hints).Single(i=>i.Id==id);Check(read.Name==renamed&&read.Values.Value<string>("qaDisplayName")==renamed,"Default display alias masked renamed layer");tx.Commit();
            }
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var hints=LibrarySyncNative.ReadDrawing(db,tx);var edit=hints.Single(i=>i.Id==id).Copy();edit.Name="QA_KC_01_FINAL";edit.Values["qaDisplayName"]="Custom layer alias";
                LibrarySyncNative.EditResource(db,tx,edit,hints);
                var current=LibrarySyncNative.ReadDrawing(db,tx,hints).Single(i=>i.Id==id);Check(current.Name==edit.Name&&current.Values.Value<string>("qaDisplayName")=="Custom layer alias","Repeated rename lost custom alias/ID");
                Check(((MText)tx.GetObject(labelId,OpenMode.ForRead)).Text.StartsWith(edit.Name,StringComparison.Ordinal),"Second rename did not refresh caption");tx.Commit();
            }
            using(var tx=db.TransactionManager.StartTransaction())
            {
                tx.GetObject(sampleId,OpenMode.ForWrite).Erase();
                var hints=LibrarySyncNative.ReadDrawing(db,tx);var edit=hints.Single(i=>i.Id==id).Copy();edit.Values["lineWeight"]=35;LibrarySyncNative.EditResource(db,tx,edit,hints);
                Check(!LibraryFrames.Model(db,tx).OfType<Line>().Any(e=>LibrarySyncNative.SampleMetadata(e,tx)?.Id==id),"Editing resurrected an erased presentation sample");tx.Commit();
            }
            string file=Path.Combine(evidence,"rename-fixture.dwg");db.SaveAs(file,DwgVersion.AC1032);
            using var reopened=new Database(false,true);reopened.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,"");reopened.CloseInput(true);
            using(var tx=reopened.TransactionManager.StartOpenCloseTransaction())Check(LibrarySyncNative.ReadDrawing(reopened,tx).Single(i=>i.Id==id).Name=="QA_KC_01_FINAL","Saved/reopened layer lost stable ID");
            JsonUtil.Atomic(Path.Combine(evidence,"layer-rename.json"),new{passed=true,nativeName=true,presentationCaption=true,presentationColor=true,presentationLineweight=true,stableId=true,customAliasPreserved=true,positionPreserved=true,erasedSampleNotRecreated=true,savedAndReopened=true});
        }
        finally{HostApplicationServices.WorkingDatabase=previous;}
    }
}
