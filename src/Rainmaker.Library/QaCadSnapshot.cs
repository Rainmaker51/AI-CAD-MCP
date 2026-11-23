using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Library;

internal static class QaCadSnapshot
{
    internal static void Discard(InstallConfig config,JObject snapshot)
    {
        string id=snapshot.Value<string>("id")??"";
        if(!Guid.TryParseExact(id,"N",out _))return;
        string root=Path.GetFullPath(Path.Combine(config.DataRoot,"library-sync")),folder=Path.GetFullPath(Path.Combine(root,id));
        if(!folder.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(Path.Combine(folder,".qa-native-snapshot"))||File.Exists(Path.Combine(folder,"review.json")))return;
        // Only the flat files created by this class; never recursively delete foreign files.
        if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return;
        var owned=new[]{"source.dwg","snapshot.json","live-review.json",".qa-native-snapshot",".aicad-library-job","blocks.json","previews.json"}.Concat(Directory.EnumerateFiles(folder,"*.png").Select(Path.GetFileName).Where(n=>System.Text.RegularExpressions.Regex.IsMatch(n!,"^[a-f0-9]{64}\\.png$"))).ToArray();
        foreach(string name in owned)
        {
            string file=Path.Combine(folder,name);if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)==0)File.Delete(file);
        }
        if(!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);
    }
    internal static string Folder(InstallConfig config,JObject snapshot)=>Path.Combine(config.DataRoot,"library-sync",snapshot.Value<string>("id")!);
    internal static string PreviewKey(JObject row)=>row.Value<string>("resourceId")??row.Value<string>("id")!;
    internal static void Previews(InstallConfig config,JObject snapshot,IEnumerable<JObject> rows)
    {
        string folder=Folder(config,snapshot);
        if(JsonUtil.FileHash(Path.Combine(folder,"source.dwg"))!=snapshot.Value<string>("snapshotHash"))throw new InvalidOperationException("Bản đọc CAD đã đổi. Làm mới trước khi tạo ảnh.");
        var blocks=rows.Where(r=>r.Value<string>("liveStatus")!="MISSING").Select(r=>
        {
            var block=r["profile"]?.ToObject<BlockAsset>()??new();block.Id=PreviewKey(r);
            block.NativeSource??=new();block.NativeSource.BlockName=r.Value<string>("name")!;return block;
        }).ToList();
        JsonUtil.Atomic(Path.Combine(folder,"blocks.json"),blocks);File.WriteAllText(Path.Combine(folder,".aicad-library-job"),"QA transient preview cache");
        BlockPreviews.Generate(folder);
    }
    internal static bool Same(string a,string b)=>string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);
    internal static Document? OpenDocument(string source)=>AcApp.DocumentManager.Cast<Document>().SingleOrDefault(d=>Same(d.Name,source));
    internal static JObject Capture(InstallConfig config,string source,List<LibrarySyncItem> hints,Database? fixtureDatabase=null)
    {
        string id=Guid.NewGuid().ToString("N"),folder=Path.Combine(config.DataRoot,"library-sync",id);Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,".qa-native-snapshot"),"QA owned native snapshot");
        string file=Path.Combine(folder,"source.dwg");
        var document=OpenDocument(source);List<LibrarySyncItem> items;JObject presence;
        var previous=HostApplicationServices.WorkingDatabase;
        try
        {
            if(document!=null||fixtureDatabase!=null)
            {
                using var locked=document?.LockDocument();var db=fixtureDatabase??document!.Database;
                // A local copy supports insertion/preview while another drawing is active.
                // This is not a warehouse publication and never saves the user's DWG.
                var mapped=new Dictionary<ObjectId,ObjectId>();
                void Remember(IdMapping mapping){foreach(IdPair pair in mapping)if(!pair.Value.IsNull)mapped[pair.Key]=pair.Value;}
                IdMappingEventHandler collect=(_,e)=>Remember(e.IdMapping);
                db.BeginDeepCloneTranslation+=collect;Database clone;
                try{clone=db.Wblock();}finally{db.BeginDeepCloneTranslation-=collect;}
                using var ownedClone=clone;
                using(var tx=db.TransactionManager.StartOpenCloseTransaction())
                {
                    foreach(var pair in new[]{new{from=db.LayerTableId,to=clone.LayerTableId},new{from=db.TextStyleTableId,to=clone.TextStyleTableId},new{from=db.DimStyleTableId,to=clone.DimStyleTableId},new{from=db.BlockTableId,to=clone.BlockTableId}})
                    {
                        var ids=new ObjectIdCollection(((SymbolTable)tx.GetObject(pair.from,OpenMode.ForRead)).Cast<ObjectId>().Where(i=>tx.GetObject(i,OpenMode.ForRead) is not BlockTableRecord b||!b.IsLayout&&!b.IsAnonymous&&!b.IsFromExternalReference).ToArray());
                        var mapping=new IdMapping();db.WblockCloneObjects(ids,pair.to,mapping,DuplicateRecordCloning.Ignore,false);Remember(mapping);
                    }
                }
                HostApplicationServices.WorkingDatabase=clone;
                using(var tx=clone.TransactionManager.StartTransaction()){LibraryPresence.RebindSnapshotHandles(clone,tx);tx.Commit();}
                using var originalRead=db.TransactionManager.StartOpenCloseTransaction();
                var cloneHints=hints.Select(i=>
                {
                    var c=i.Copy();c.Handle="";
                    if(long.TryParse(i.Handle,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out long number))
                    {
                        try
                        {
                            var original=db.GetObjectId(false,new Handle(number),0);
                            if(i.Kind=="hatches"&&!original.IsErased&&originalRead.GetObject(original,OpenMode.ForRead) is Hatch hatch)c.Values=LibrarySyncNative.HatchValues(hatch);
                            else if(mapped.TryGetValue(original,out var copied))c.Handle=copied.Handle.ToString();
                        }
                        catch(Autodesk.AutoCAD.Runtime.Exception){}
                    }
                    return c;
                }).ToList();
                using(var tx=clone.TransactionManager.StartTransaction()){items=LibrarySyncNative.ReadDrawing(clone,tx,cloneHints);presence=LibraryPresence.Read(clone,tx,items);tx.Abort();}
                clone.SaveAs(file,DwgVersion.AC1032);
            }
            else
            {
                CopyShared(source,file);
                using var db=new Database(false,true);db.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);HostApplicationServices.WorkingDatabase=db;
                using var tx=db.TransactionManager.StartTransaction();items=LibrarySyncNative.ReadDrawing(db,tx,hints);presence=LibraryPresence.Read(db,tx,items);tx.Abort();
            }
        }
        finally{HostApplicationServices.WorkingDatabase=previous;}
        if(items.Select(i=>i.Id).Distinct().Count()!=items.Count)throw new InvalidOperationException("DWG có ID thư viện trùng nhau. Kiểm tra block đã sao chép trước khi lưu danh mục.");
        string signature=Signature(items,presence);
        var result=new JObject{["id"]=id,["source"]=Path.GetFullPath(source),["readMode"]=document==null&&fixtureDatabase==null?"FILE":"OPEN_DOCUMENT",["items"]=JArray.FromObject(items),["presence"]=presence,["signatureFormat"]=2,["signature"]=signature,["snapshotHash"]=JsonUtil.FileHash(file)};
        JsonUtil.Atomic(Path.Combine(folder,"snapshot.json"),result);return result;
    }
    internal static string Signature(List<LibrarySyncItem> items,JObject presence)=>AICad.Application.LibraryRepository.SnapshotSignature(items,presence);
    internal static void CopyShared(string source,string target)
    {
        using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);input.CopyTo(output);
    }
    internal static JObject CaptureSaved(InstallConfig config,string source,List<LibrarySyncItem> hints)
    {
        // Called after QSAVE completes. The byte-for-byte source copy preserves handles
        // used by hatch catalogs; Wblock clones are only for the transient live browser.
        string id=Guid.NewGuid().ToString("N"),folder=Path.Combine(config.DataRoot,"library-sync",id);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,".qa-native-snapshot"),"QA saved native snapshot");
        string file=Path.Combine(folder,"source.dwg");CopyShared(source,file);
        var previous=HostApplicationServices.WorkingDatabase;
        try
        {
            using var db=new Database(false,true);db.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);HostApplicationServices.WorkingDatabase=db;
            using var tx=db.TransactionManager.StartTransaction();var items=LibrarySyncNative.ReadDrawing(db,tx,hints);var presence=LibraryPresence.Read(db,tx,items);tx.Abort();
            var result=new JObject{["id"]=id,["source"]=Path.GetFullPath(source),["readMode"]="SAVED_FILE",["items"]=JArray.FromObject(items),["presence"]=presence,["signatureFormat"]=2,["signature"]=Signature(items,presence),["snapshotHash"]=JsonUtil.FileHash(file)};
            JsonUtil.Atomic(Path.Combine(folder,"snapshot.json"),result);return result;
        }
        finally{HostApplicationServices.WorkingDatabase=previous;}
    }
}
