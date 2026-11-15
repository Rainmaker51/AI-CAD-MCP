using AICad.Contracts;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
namespace AICad.Bridge;
// Adapted from the old DrawingWriteGuard: transaction rollback requires Undo recording.
internal static class NativeWriteGuard {
 internal static void CheckDatabase(Database db){if(!db.UndoRecording)throw new CadException("UNDO_DISABLED","DWG Undo recording is disabled. Writes are blocked; enable Undo before creating a new preview.");}
 internal static void Check(Document d){if(d.IsReadOnly)throw new CadException("READ_ONLY","Target DWG is read-only.");CheckDatabase(d.Database);}
}
