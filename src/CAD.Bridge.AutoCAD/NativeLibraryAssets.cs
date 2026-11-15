using System;
using System.IO;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using JsonUtil=AICad.Contracts.Json;
namespace AICad.Bridge;
internal static class NativeLibraryAssets {
 internal static string ContentRoot="";
 internal static string PathFor(NativeBlockSource source){if(ContentRoot==""||!System.Text.RegularExpressions.Regex.IsMatch(source.FileHash,"^[a-f0-9]{64}$"))throw new CadException("MISSING_ASSET","Native library source is not configured.");return Path.Combine(WarehousePaths.Folder(ContentRoot,"content-assets"),source.FileHash+".dwg");}
 internal static void Demand(DrawingPlan p){foreach(var source in p.Template.Blocks.Where(b=>b.NativeSource!=null).Select(b=>b.NativeSource!).Concat(p.Template.Layers.Where(l=>l.Appearance?.SourceHash!=""&&l.Appearance?.SourceHash!=null).Select(l=>new NativeBlockSource{FileHash=l.Appearance!.SourceHash})).GroupBy(s=>s.FileHash).Select(g=>g.First())){string path=PathFor(source);if(!File.Exists(path)||JsonUtil.FileHash(path)!=source.FileHash)throw new CadException("RESOURCE_CHANGED","DWG library source changed.");}}
 internal static ObjectId Clone(BlockAsset asset,Database target,Transaction tx,string name){
  var source=asset.NativeSource!;string file=PathFor(source);
  using var guard=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
  using(var sha=System.Security.Cryptography.SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(guard)).Replace("-","").ToLowerInvariant()!=source.FileHash)throw new CadException("RESOURCE_CHANGED","DWG library hash differs.");
  using var db=new Database(false,true);db.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);
  using var read=db.TransactionManager.StartOpenCloseTransaction();var sourceTable=(BlockTable)read.GetObject(db.BlockTableId,OpenMode.ForRead);
  if(!sourceTable.Has(source.BlockName))throw new CadException("MISSING_ASSET",source.BlockName);
  ObjectId sourceId=sourceTable[source.BlockName],result;
  var definition=(BlockTableRecord)read.GetObject(sourceId,OpenMode.ForRead);
  if(definition.IsFromExternalReference||definition.IsFromOverlayReference)throw new CadException("UNSUPPORTED_ASSET","Bind xrefs before importing.");
  var mapping=new IdMapping();
  if(source.ModelSpace){
   var table=(BlockTable)tx.GetObject(target.BlockTableId,OpenMode.ForWrite);var block=new BlockTableRecord{Name=name,Origin=db.Insbase};result=table.Add(block);tx.AddNewlyCreatedDBObject(block,true);
   db.WblockCloneObjects(new ObjectIdCollection(definition.Cast<ObjectId>().ToArray()),result,mapping,DuplicateRecordCloning.MangleName,false);
  }else{
   db.WblockCloneObjects(new ObjectIdCollection(new[]{sourceId}),target.BlockTableId,mapping,DuplicateRecordCloning.MangleName,false);
   result=mapping[sourceId].Value;var block=(BlockTableRecord)tx.GetObject(result,OpenMode.ForWrite);block.Name=name;
  }
  NativeLayerStandards.RestoreLayers(db,read,target,tx,mapping);
  var imported=(BlockTableRecord)tx.GetObject(result,OpenMode.ForWrite);if(source.LabelAttribute!=null)imported.Annotative=AnnotativeStates.False;if(imported.ExtensionDictionary.IsNull)imported.CreateExtensionDictionary();var dictionary=(DBDictionary)tx.GetObject(imported.ExtensionDictionary,OpenMode.ForWrite);if(dictionary.Contains("AICAD_HASH")){var old=tx.GetObject(dictionary.GetAt("AICAD_HASH"),OpenMode.ForWrite);dictionary.Remove("AICAD_HASH");old.Erase();}var receipt=new Xrecord{Data=new ResultBuffer(new TypedValue((int)DxfCode.Text,JsonUtil.Hash(asset)))};dictionary.SetAt("AICAD_HASH",receipt);tx.AddNewlyCreatedDBObject(receipt,true);
  return result;
 }
 internal static void Attributes(BlockReference reference,Transaction tx,Operation? op=null,string? labelTag=null){bool found=false;var definition=(BlockTableRecord)tx.GetObject(reference.BlockTableRecord,OpenMode.ForRead);foreach(ObjectId id in definition)if(tx.GetObject(id,OpenMode.ForRead) is AttributeDefinition a&&!a.Constant){var attr=new AttributeReference();attr.SetAttributeFromBlock(a,reference.BlockTransform);if(op?.AttributeText!=null&&a.Tag==labelTag){attr.TextString=op.AttributeText;found=true;}else attr.TextString=a.TextString;reference.AttributeCollection.AppendAttribute(attr);tx.AddNewlyCreatedDBObject(attr,true);}if(op?.AttributeText!=null&&!found)throw new CadException("MISSING_ATTRIBUTE",labelTag??"");}
}
