using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace Rainmaker.Axis;

internal static class DimensionLibrary
{
 internal static string Signature(DBObject value,Transaction tx,int depth=0)
 {
  var data=new JObject{["type"]=value.GetType().Name};
  foreach(var property in value.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(p=>p.CanRead&&p.GetIndexParameters().Length==0).OrderBy(p=>p.Name)){
   bool include=value is DimStyleTableRecord?property.Name.StartsWith("Dim",StringComparison.Ordinal)||property.Name=="Annotative":value is TextStyleTableRecord?new[]{"FileName","BigFontFileName","TextSize","XScale","ObliquingAngle","IsVertical","FlagBits","Annotative"}.Contains(property.Name):value is LinetypeTableRecord?new[]{"AsciiDescription","PatternLength","NumDashes"}.Contains(property.Name):false;
   if(!include)continue;var v=property.GetValue(value,null);if(v is ObjectId id){data[property.Name]=id.IsNull?JValue.CreateNull():depth>6?new JValue("depth"):new JValue(Signature(tx.GetObject(id,OpenMode.ForRead),tx,depth+1));}else data[property.Name]=v==null?JValue.CreateNull():JToken.FromObject(v);
  }
  if(value is BlockTableRecord block){if(depth>6)throw new CadException("DIM_DEPENDENCY","Block phụ thuộc dim lồng quá sâu.");data["entities"]=new JArray(block.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Entity>().Select(e=>GridStore.EntityHash(e,tx)));}
  if(value is LinetypeTableRecord line){data["dashes"]=new JArray(Enumerable.Range(0,line.NumDashes).Select(i=>line.DashLengthAt(i)));}
  return JsonUtil.Hash(data);
 }
 static IEnumerable<ObjectId> Dependencies(DimStyleTableRecord record)=>typeof(DimStyleTableRecord).GetProperties().Where(p=>p.Name.StartsWith("Dim",StringComparison.Ordinal)&&p.PropertyType==typeof(ObjectId)).Select(p=>(ObjectId)p.GetValue(record,null)!).Where(id=>!id.IsNull).Distinct();
 static string Difference(DBObject existing,DBObject source){var names=existing is DimStyleTableRecord?new[]{"Dimtxt","Dimasz","Dimscale","Annotative","Dimlfac","Dimdec","Dimexo","Dimexe","Dimgap"}:new[]{"FileName","BigFontFileName","TextSize","XScale","ObliquingAngle","PatternLength"};var differences=new List<string>();foreach(var name in names){var property=existing.GetType().GetProperty(name);if(property==null)continue;var oldValue=Convert.ToString(property.GetValue(existing,null),System.Globalization.CultureInfo.InvariantCulture);var newValue=Convert.ToString(property.GetValue(source,null),System.Globalization.CultureInfo.InvariantCulture);if(oldValue!=newValue)differences.Add(name+": DWG "+oldValue+" → kho "+newValue);}return differences.Count>0?" ("+string.Join("; ",differences)+")":" (khác định dạng/phụ thuộc/hình học)";}
 internal static Dictionary<string,ObjectId> Import(Database target,Transaction tx,AxisResolvedLibrary library,IEnumerable<AxisLibraryOption> choices,bool replace)
 {
  if(JsonUtil.FileHash(library.DimFile)!=library.DimHash)throw new CadException("RESOURCE_CHANGED","Nguồn Dim đã thay đổi.");
  using var source=new Database(false,true);source.ReadDwgFile(library.DimFile,FileOpenMode.OpenForReadAndAllShare,true,"");source.CloseInput(true);using var read=source.TransactionManager.StartOpenCloseTransaction();var from=(DimStyleTable)read.GetObject(source.DimStyleTableId,OpenMode.ForRead);var to=(DimStyleTable)tx.GetObject(target.DimStyleTableId,OpenMode.ForRead);var requested=choices.GroupBy(c=>c.Id).Select(g=>g.First()).ToArray();var conflicts=new List<string>();
  foreach(var choice in requested){if(!from.Has(choice.Name))throw new CadException("AXIS_DIM_SOURCE","Thiếu dim "+choice.Name);var record=(DimStyleTableRecord)read.GetObject(from[choice.Name],OpenMode.ForRead);if(to.Has(choice.Name)&&Signature(record,read)!=Signature(tx.GetObject(to[choice.Name],OpenMode.ForRead),tx))conflicts.Add("Dim: "+choice.Name+Difference(tx.GetObject(to[choice.Name],OpenMode.ForRead),record));
   foreach(var depId in Dependencies(record)){var dep=read.GetObject(depId,OpenMode.ForRead);ObjectId tableId=dep is TextStyleTableRecord?target.TextStyleTableId:dep is BlockTableRecord?target.BlockTableId:dep is LinetypeTableRecord?target.LinetypeTableId:ObjectId.Null;if(tableId.IsNull)continue;var table=(SymbolTable)tx.GetObject(tableId,OpenMode.ForRead);string name=((SymbolTableRecord)dep).Name;if(table.Has(name)&&Signature(dep,read)!=Signature(tx.GetObject(table[name],OpenMode.ForRead),tx))conflicts.Add("Phụ thuộc: "+name+Difference(tx.GetObject(table[name],OpenMode.ForRead),dep));}
  }
  if(conflicts.Count>0&&!replace)throw new CadException("DIM_CONFLICT","Trùng tên nhưng khác thông số:\n"+string.Join("\n",conflicts.Distinct())+"\nCập nhật định nghĩa sẽ ảnh hưởng cả đối tượng khác đang tham chiếu các tên này.");
  // Replacing an identical live style/dependency on every placement needlessly
  // redefines anonymous dimension resources, including inside an Undo group.
  // Existing matching resources are already verified above; keep their ObjectIds.
  bool replacing=replace&&conflicts.Count>0;var import=requested.Where(c=>replacing||!to.Has(c.Name)).ToArray();
  if(import.Length>0){var ids=new ObjectIdCollection(import.Select(c=>from[c.Name]).ToArray());using var map=new IdMapping();source.WblockCloneObjects(ids,target.DimStyleTableId,map,replacing?DuplicateRecordCloning.Replace:DuplicateRecordCloning.Ignore,false);
   if(replacing)foreach(var choice in import){var original=(DimStyleTableRecord)read.GetObject(from[choice.Name],OpenMode.ForRead);var copied=(DimStyleTableRecord)tx.GetObject(to[choice.Name],OpenMode.ForWrite);copied.Annotative=original.Annotative;copied.Dimscale=original.Dimscale;}
  }
  return requested.ToDictionary(c=>c.Id,c=>to[c.Name]);
 }
 internal static int Migrate(Database db,AxisResolvedLibrary library,AxisLibrary index,ObjectId[] selection,bool replace)
 {
  using var tx=db.TransactionManager.StartTransaction();var dims=selection.Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Dimension>().ToArray();if(dims.Length==0)throw new CadException("DIM_MIGRATE","Chọn dimension RMAX cần chuyển.");
  var requested=new List<AxisLibraryOption>();foreach(var d in dims){var style=(DimStyleTableRecord)tx.GetObject(d.DimensionStyle,OpenMode.ForRead);var match=Regex.Match(style.Name,@"^RMAX_(DIM-\d+)_",RegexOptions.IgnoreCase);var choice=index.Options.SingleOrDefault(o=>o.Kind=="dims"&&match.Success&&o.Code.Equals(match.Groups[1].Value,StringComparison.OrdinalIgnoreCase));if(choice==null)throw new CadException("DIM_MIGRATE","Không xác nhận được nguồn: "+style.Name+". Chỉ chọn RMAX có mã kho hợp lệ.");requested.Add(choice);}
  var imported=Import(db,tx,library,requested,replace);var oldIds=dims.Select(d=>d.DimensionStyle).Distinct().ToArray();
  for(int i=0;i<dims.Length;i++){var d=dims[i];double scale=d.Dimscale;var contexts=db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");var supported=contexts.Cast<Autodesk.AutoCAD.DatabaseServices.ObjectContext>().OfType<AnnotationScale>().Where(s=>d.HasContext(s)).ToArray();if(d.Annotative==AnnotativeStates.True&&supported.Length!=1)throw new CadException("DIM_MIGRATE","Dim có nhiều/thiếu annotation context; chọn tỷ lệ rõ ràng trước khi chuyển.");
   if(d.Annotative==AnnotativeStates.True)scale=supported[0].DrawingUnits/supported[0].PaperUnits;if(scale<=0)throw new CadException("DIM_MIGRATE","Không xác định được tỷ lệ dim.");d.UpgradeOpen();d.DimensionStyle=imported[requested[i].Id];var record=(DimStyleTableRecord)tx.GetObject(d.DimensionStyle,OpenMode.ForRead);d.SetDimstyleData(record);d.Annotative=record.Annotative==AnnotativeStates.True?AnnotativeStates.True:AnnotativeStates.False;d.Dimscale=record.Annotative==AnnotativeStates.True?0:scale;
   if(record.Annotative==AnnotativeStates.True){var context=AICad.Bridge.EntityFactory.GetScale(db,(int)Math.Round(scale));if(!d.HasContext(context))d.AddContext(context);}d.RecomputeDimensionBlock(true);
  }
  var purge=new ObjectIdCollection(oldIds);db.Purge(purge);foreach(ObjectId id in purge){var record=(DimStyleTableRecord)tx.GetObject(id,OpenMode.ForWrite);if(record.Name.StartsWith("RMAX_",StringComparison.OrdinalIgnoreCase))record.Erase();}tx.Commit();return dims.Length;
 }
}
