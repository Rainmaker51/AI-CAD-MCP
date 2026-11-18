using System;
using System.Collections.Generic;
using System.IO;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;
namespace AICad.Bridge;
public sealed class Commands {
 static Plugin P=>Plugin.Current??throw new CadException("PLUGIN_OFFLINE","Plugin is not initialized.");
 static void Run(Action action){try{action();}catch(System.Exception e){try{if(Plugin.Current!=null)JsonUtil.Atomic(Path.Combine(Plugin.Current.Config.DataRoot,"last-native-error.json"),new{at=DateTime.UtcNow,code=e is CadException ce?ce.Code:"CAD_ERROR",message=e.Message});}catch{}AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nAI CAD: "+e.Message);}}
 [CommandMethod("AICADTOOLS",CommandFlags.Modal)]public void ToolManager()=>Run(()=>(P.Tools??=new CadToolPalette(P)).Show());
 [CommandMethod("AICADADDINS",CommandFlags.Modal)]public void AddinManager()=>Run(()=>(P.Tools??=new CadToolPalette(P)).Show());
 [CommandMethod("AICADRIBBON",CommandFlags.Modal)]public void RibbonManager()=>Run(()=>(P.Tools??=new CadToolPalette(P)).Show(2));
 [CommandMethod("AICADMCP",CommandFlags.Modal)]public void McpConnector()=>Run(()=>(P.Tools??=new CadToolPalette(P)).Show(1));
 [CommandMethod("AICADTOOLRUN",CommandFlags.Modal)]public void RunTool()=>Run(()=>P.Tools?.ExecutePending());
 [CommandMethod("AICADTOOLSESSION",CommandFlags.Session)]public void SessionTool()=>Run(()=>P.Tools?.ExecutePending());
 [CommandMethod("AICAD",CommandFlags.Modal|CommandFlags.NoUndoMarker)]public void Palette()=>Run(()=>P.Review.ShowPalette());
 [CommandMethod("AICADREVIEW",CommandFlags.Modal|CommandFlags.NoUndoMarker)]public void Preview()=>Run(()=>{string id=P.NextPlan??throw new CadException("PLAN_REQUIRED","Create a plan in the app first.");P.NextPlan=null;try{P.Review.Prepare(id);}catch(System.Exception e){using(P.Plans.Lock(id)){var record=P.Plans.Read(id);if(record.Status=="READY"||record.Status=="PREVIEW"){record.Status="FAILED";record.Message=(e is CadException ce?ce.Code:"CAD_ERROR")+": "+e.Message;P.Plans.Write(record);}}throw;}});
 [CommandMethod("AICADAPPLY",CommandFlags.Modal|CommandFlags.NoUndoMarker)]public void Apply()=>Run(()=>P.Review.ApplyWithFeedback());
 [CommandMethod("AICADCANCEL",CommandFlags.Modal|CommandFlags.NoUndoMarker)]public void Cancel()=>Run(()=>P.Review.Cancel());
 [CommandMethod("AICADDEMO",CommandFlags.Session)]public void Demo()=>Run(()=>{var d=AcApp.DocumentManager.Add("");AcApp.DocumentManager.MdiActiveDocument=d;using(var docLock=d.LockDocument())using(var tx=d.Database.TransactionManager.StartTransaction()){d.Database.Insunits=UnitsValue.Millimeters;var bt=(BlockTable)tx.GetObject(d.Database.BlockTableId,OpenMode.ForRead);bt.UpgradeOpen();var def=new BlockTableRecord{Name="AI_TASK"};var defId=bt.Add(def);tx.AddNewlyCreatedDBObject(def,true);var boundary=new Polyline(4);boundary.AddVertexAt(0,new Point2d(0,0),0,0,0);boundary.AddVertexAt(1,new Point2d(6000,0),0,0,0);boundary.AddVertexAt(2,new Point2d(6000,4200),0,0,0);boundary.AddVertexAt(3,new Point2d(0,4200),0,0,0);boundary.Closed=true;def.AppendEntity(boundary);tx.AddNewlyCreatedDBObject(boundary,true);int i=0;foreach(string tag in new[]{"TASK_ID","SKILL_ID","TEMPLATE_ID","STATUS","DEPENDS_ON","PROMPT"}){var attr=new AttributeDefinition{Tag=tag,Prompt=tag,TextString="",Position=new Point3d(100,100+200*i++,0),Height=120,Invisible=true};def.AppendEntity(attr);tx.AddNewlyCreatedDBObject(attr,true);}var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);for(int n=0;n<3;n++){var block=new BlockReference(new Point3d(n*6800,0,0),defId);ms.AppendEntity(block);tx.AddNewlyCreatedDBObject(block,true);var attrs=new Dictionary<string,string>{{"TASK_ID","T00"+(n+1)},{"SKILL_ID",n==1?"DRAW_PLAN":"INSERT_DOOR_AXIS_DEMO"},{"TEMPLATE_ID","STARTER"},{"STATUS","PENDING"},{"DEPENDS_ON",n==2?"T001":""},{"PROMPT","Starter demo task"}};foreach(ObjectId child in def){if(!(tx.GetObject(child,OpenMode.ForRead) is AttributeDefinition a))continue;var reference=new AttributeReference();reference.SetAttributeFromBlock(a,block.BlockTransform);reference.TextString=attrs[a.Tag];block.AttributeCollection.AppendAttribute(reference);tx.AddNewlyCreatedDBObject(reference,true);}}tx.Commit();}d.SendStringToExecute("_ZOOM _E\n",true,false,false);d.Editor.WriteMessage("\nCreated a NEW unsaved demo DWG with 3 AI_TASK blocks. Existing DWGs unchanged.");});
 [CommandMethod("AICADDOCTOR",CommandFlags.Modal|CommandFlags.NoUndoMarker)]public void Doctor()=>Run(()=>{var d=AcApp.DocumentManager.MdiActiveDocument;var report=new{sessionId=P.Context.SessionId,documentCount=P.Context.Open.Count,document=d==null?null:P.Context.Info(d),tasks=d==null?null:Reader.Scan(d)};JsonUtil.Atomic(Path.Combine(P.Config.DataRoot,"native-doctor.json"),report);d?.Editor.WriteMessage("\nAI CAD doctor report written.");});
}
