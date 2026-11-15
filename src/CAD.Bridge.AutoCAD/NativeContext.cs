using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;
namespace AICad.Bridge;
internal sealed class NativeContext:IDisposable {
 internal sealed class Entry{public string Id=Guid.NewGuid().ToString("N");public string Epoch=Guid.NewGuid().ToString("N");public long Revision;}
 internal readonly Dictionary<Document,Entry> Open=new();internal string SessionId=Guid.NewGuid().ToString("N");internal bool OwnedWrite;internal readonly System.Collections.Generic.List<string> RevisionCauses=new();
 public NativeContext(){foreach(Document d in AcApp.DocumentManager)Register(d);AcApp.DocumentManager.DocumentCreated+=Created;AcApp.DocumentManager.DocumentToBeDestroyed+=Closing;}
 void Created(object sender,DocumentCollectionEventArgs e)=>Register(e.Document);void Closing(object sender,DocumentCollectionEventArgs e)=>Unregister(e.Document);
 void Register(Document d){if(Open.ContainsKey(d))return;Open[d]=new();d.Database.ObjectModified+=Changed;d.Database.ObjectAppended+=Changed;d.Database.ObjectErased+=Erased;d.CommandEnded+=Command;}
 void Unregister(Document d){d.Database.ObjectModified-=Changed;d.Database.ObjectAppended-=Changed;d.Database.ObjectErased-=Erased;d.CommandEnded-=Command;Open.Remove(d);}
 void Changed(object sender,ObjectEventArgs e){if(!OwnedWrite&&!(e.DBObject is ViewportTableRecord)&&!(e.DBObject is ViewTableRecord)){foreach(var pair in Open)if(pair.Key.Database.Equals(sender)){if(RevisionCauses.Count>=50)RevisionCauses.RemoveAt(0);RevisionCauses.Add(e.DBObject.GetType().Name+"/"+e.DBObject.Handle);}Bump(sender);}}void Erased(object sender,ObjectErasedEventArgs e){if(!OwnedWrite)Bump(sender);}
 void Bump(object sender){foreach(var pair in Open)if(pair.Key.Database.Equals(sender))pair.Value.Revision++;}
 void Command(object sender,CommandEventArgs e){if(sender is Document d&&Open.TryGetValue(d,out var entry)&&new[]{"U","UNDO","REDO","MREDO","SAVE","QSAVE","SAVEAS"}.Contains(e.GlobalCommandName.TrimStart('_','.').ToUpperInvariant()))entry.Revision++;}
 internal string Revision(Document d)=>Open[d].Epoch+":"+Open[d].Revision;
 internal Document Resolve(string id,string revision=""){var d=Open.Keys.SingleOrDefault(x=>Open[x].Id==id)??throw new CadException("DOCUMENT_NOT_FOUND","DWG was closed or reopened.");if(revision!=""&&Revision(d)!=revision)throw new CadException("STALE_REVISION","DWG changed. Create a new plan and preview.");return d;}
 internal DocumentInfo Info(Document d)=>new(){SessionId=SessionId,DocumentId=Open[d].Id,Name=Path.GetFileName(d.Name),Path=d.Database.Filename,Revision=Revision(d),Space=d.Database.TileMode?"Model":"Layout",IsModified=d==AcApp.DocumentManager.MdiActiveDocument&&Convert.ToInt32(AcApp.GetSystemVariable("DBMOD"))!=0,IsActive=AcApp.DocumentManager.MdiActiveDocument==d,Units=d.Database.Insunits.ToString()};
 internal string DrawingKey(Document d){string path=d.Database.Filename;if(string.IsNullOrEmpty(path)||!Path.IsPathRooted(path))return SessionId+":"+Open[d].Id;try{using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);if(GetFileInformationByHandle(stream.SafeFileHandle,out var info))return info.Volume+":"+info.IndexHigh+":"+info.IndexLow;}catch(IOException){}throw new CadException("FILE_IDENTITY_UNAVAILABLE","Cannot identify physical DWG for writer lock.");}
 [StructLayout(LayoutKind.Sequential)]struct FileInfo{public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
 [DllImport("kernel32",SetLastError=true)]static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file,out FileInfo info);
 public void Dispose(){AcApp.DocumentManager.DocumentCreated-=Created;AcApp.DocumentManager.DocumentToBeDestroyed-=Closing;foreach(var d in Open.Keys.ToArray())Unregister(d);}
}
