using System;
using System.IO;
using AICad.Contracts;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;
[assembly:CommandClass(typeof(AICad.Bridge.RecoveryCommands))]
namespace AICad.Bridge;
public sealed class RecoveryCommands {
 [CommandMethod("AICADRESTORE",CommandFlags.Session)]public void Restore(){var d=AcApp.DocumentManager.MdiActiveDocument;if(d==null)return;try{var plugin=Plugin.Current??throw new CadException("PLUGIN_OFFLINE","Load the plugin first.");var options=new PromptOpenFileOptions("\nChọn checkpoint để mở thành bản phục hồi mới: "){Filter="DWG checkpoint (*.dwg)|*.dwg",InitialDirectory=Path.Combine(plugin.Config.DataRoot,"checkpoints")};var selected=d.Editor.GetFileNameForOpen(options);if(selected.Status!=PromptStatus.OK)return;string source=Path.GetFullPath(selected.StringResult);string allowed=Path.GetFullPath(Path.Combine(plugin.Config.DataRoot,"checkpoints"))+Path.DirectorySeparatorChar;if(!source.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(source).ToLowerInvariant()!=".dwg")throw new CadException("PATH_DENIED","Select a checkpoint inside this app's checkpoint folder.");string folder=Path.Combine(plugin.Config.DataRoot,"recovery");Directory.CreateDirectory(folder);string copy=Path.Combine(folder,"Recovered-"+Guid.NewGuid().ToString("N")+".dwg");File.Copy(source,copy,false);AcApp.DocumentManager.Open(copy,false);d.Editor.WriteMessage("\nCheckpoint opened as a NEW recovery DWG. Original drawing was not overwritten.");}catch(System.Exception e){d.Editor.WriteMessage("\nAI CAD: "+e.Message);}}
 [CommandMethod("AICADCHECK",CommandFlags.Modal|CommandFlags.NoUndoMarker)]public void Check(){Inspect(false);}
 [CommandMethod("AICADRECONCILE",CommandFlags.Modal|CommandFlags.NoUndoMarker)]public void Reconcile(){Inspect(true);}
 void Inspect(bool reconcile){var d=AcApp.DocumentManager.MdiActiveDocument;if(d==null)return;try{var p=Plugin.Current??throw new CadException("PLUGIN_OFFLINE","Load plugin first.");var input=d.Editor.GetString("\nPlan ID (32 ký tự): ");if(input.Status!=PromptStatus.OK)return;string id=AICad.Contracts.Json.Id(input.StringResult.Trim());var record=p.Plans.Read(id);if(record.Plan.DocumentId!=p.Context.Open[d].Id)throw new CadException("WRONG_DOCUMENT","Activate the plan's original open DWG.");var result=Reader.Inspect(d,p.Plans,id);if(reconcile&&result.Value<string>("status")=="PRESENT"&&(record.Status=="UNKNOWN"||record.Status=="DISPATCHED")){using(p.Plans.Lock(id)){record.Status="DONE";record.QaStatus="RECONCILED";record.Message="Native transactional receipt and entities verified by local reconciliation.";p.Plans.Write(record);new ExecutionBarrier(p.Config.DataRoot,p.Context.DrawingKey(d)).Finish(id,record.ExecutionId,"DONE");}}d.Editor.WriteMessage("\n"+result.ToString());}catch(System.Exception e){d.Editor.WriteMessage("\nAI CAD: "+e.Message);}}
}
