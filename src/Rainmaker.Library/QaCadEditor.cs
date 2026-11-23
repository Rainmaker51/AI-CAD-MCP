using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;
using AICad.LibraryScanner;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Rainmaker.Library;

internal static class QaCadEditor
{
    internal static void Open(string source)
    {
        var docs = AcApp.DocumentManager;
        var found = docs.Cast<Autodesk.AutoCAD.ApplicationServices.Document>().FirstOrDefault(d => Same(d.Name, source));
        if (found != null) docs.MdiActiveDocument = found;
        else docs.Open(source, false);
    }
    static bool Same(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    internal static void Edit(string source, LibrarySyncItem item, List<LibrarySyncItem> hints)
    {
        var doc = Commands.Document;
        if (!Same(doc.Name, source)) throw new InvalidOperationException("Mở đúng DWG nguồn thư viện trước khi sửa: " + source);
        using var locked = doc.LockDocument();
        using var tx = doc.Database.TransactionManager.StartTransaction();
        LibrarySyncNative.EditResource(doc.Database, tx, item, hints);
        tx.Commit(); doc.Editor.Regen();
    }
    internal static void DemandSaved(string source)
    {
        var open = AcApp.DocumentManager.Cast<Autodesk.AutoCAD.ApplicationServices.Document>().FirstOrDefault(d => Same(d.Name, source));
        if (open != null && Convert.ToInt32(AcApp.GetSystemVariable("DBMOD")) != 0 && AcApp.DocumentManager.MdiActiveDocument == open)
            throw new InvalidOperationException("DWG có thay đổi chưa lưu. Nhấn Ctrl+S trên DWG nguồn rồi đọc kiểm tra lại.");
        if (open != null && AcApp.DocumentManager.MdiActiveDocument != open)
            throw new InvalidOperationException("Chuyển sang DWG nguồn và lưu Ctrl+S trước khi đọc kiểm tra.");
    }
    internal static string[] BlockNames(string file)
    {
        using var db=new Database(false,true);db.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);
        if(db.Insunits!=UnitsValue.Millimeters)throw new InvalidOperationException("DWG nhập block phải có đơn vị mm. Chỉnh đơn vị trước khi nhập.");
        using var tx=db.TransactionManager.StartOpenCloseTransaction();return ((BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead)).Cast<ObjectId>().Select(id=>(BlockTableRecord)tx.GetObject(id,OpenMode.ForRead)).Where(b=>!b.IsLayout&&!b.IsAnonymous&&!b.IsFromExternalReference&&!b.Name.StartsWith("AICAD_LIB_",StringComparison.OrdinalIgnoreCase)).Select(b=>b.Name).OrderBy(n=>n).ToArray();
    }
    internal static void ImportBlock(string source,string donor,string name,string group,List<LibrarySyncItem> hints)
    {
        var doc=Commands.Document;if(!Same(doc.Name,source))throw new InvalidOperationException("Mở đúng DWG nguồn thư viện trước khi nhập block.");
        using var locked=doc.LockDocument();
        using(var tx=doc.Database.TransactionManager.StartOpenCloseTransaction())if(((BlockTable)tx.GetObject(doc.Database.BlockTableId,OpenMode.ForRead)).Has(name))throw new InvalidOperationException("DWG nguồn đã có block này. Dùng BEDIT để sửa rồi đọc kiểm tra; nhập mới không ghi đè định nghĩa cũ.");
        using(var db=new Database(false,true))
        {
            db.ReadDwgFile(donor,FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);
            using var tx=db.TransactionManager.StartOpenCloseTransaction();var table=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);
            db.WblockCloneObjects(new ObjectIdCollection(new[]{table[name]}),doc.Database.BlockTableId,new IdMapping(),DuplicateRecordCloning.Ignore,false);
        }
        using(var tx=doc.Database.TransactionManager.StartTransaction())
        {
            LibrarySyncNative.EditResource(doc.Database,tx,new LibrarySyncItem{Id=Guid.NewGuid().ToString("N"),Kind="blocks",Name=name,Group=group},hints);tx.Commit();
        }
        doc.Editor.Regen();
    }
    internal static LibrarySyncNative.LayoutReport Arrange(string source, string kind, List<LibrarySyncItem> items,List<string> groupPaths)
    {
        var doc = Commands.Document;
        if (!Same(doc.Name, source)) throw new InvalidOperationException("Mở đúng DWG nguồn trước khi bố trí.");
        using var locked = doc.LockDocument(); using var tx = doc.Database.TransactionManager.StartTransaction();
        var actual = LibrarySyncNative.ReadDrawing(doc.Database, tx, items);
        var selected = new List<LibrarySyncItem>();
        foreach (var hint in items)
        {
            var resource = actual.SingleOrDefault(i => i.Id == hint.Id); if (resource == null) continue;
            resource.Group = hint.Group;
            LibrarySyncNative.AssignGroup(doc.Database, tx, resource, hint.Group); selected.Add(resource);
        }
        var nodes=LibraryFrames.Nodes(doc.Database,tx);
        nodes.RemoveAll(n=>n.Kind==kind&&n.Path!=""&&!groupPaths.Contains(n.Path)&&!actual.Any(i=>i.Kind==kind&&LibraryFrames.Under(i.Group,n.Path)));
        foreach(string path in groupPaths)LibraryFrames.Ensure(nodes,kind,path);
        LibraryFrames.Save(doc.Database,tx,nodes);
        var presentation=selected.Concat(actual.Where(i=>selected.All(s=>s.Id!=i.Id)&&LibrarySyncNative.Managed(doc.Database,tx,i))).ToList();
        var report = LibrarySyncNative.ArrangeExistingDrawing(doc.Database, tx, presentation, kind);
        var root=LibraryFrames.Nodes(doc.Database,tx).Single(n=>n.Kind==kind&&n.Path=="");
        LibraryFrames.PackGroup(doc.Database,tx,root,selected,presentation);
        tx.Commit(); doc.Editor.Regen(); return report;
    }
}
