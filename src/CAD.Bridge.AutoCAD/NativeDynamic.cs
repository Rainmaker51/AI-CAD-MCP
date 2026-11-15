using System;
using System.Linq;
using AICad.Contracts;
using AICad.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;
namespace AICad.Bridge;
internal static class NativeDynamic {
 internal static void Apply(BlockReference reference,Operation op,BlockAsset asset){var values=DraftingRules.Values(op,asset);if(values.Count==0)return;if(!reference.IsDynamicBlock)throw new CadException("NOT_DYNAMIC",asset.Id);foreach(var entry in values.Properties().OrderBy(p=>p.Value.Type==JTokenType.String?0:1)){var matches=reference.DynamicBlockReferencePropertyCollection.Cast<DynamicBlockReferenceProperty>().Where(p=>p.PropertyName==entry.Name&&!p.ReadOnly).ToArray();if(matches.Length!=1)throw new CadException("INVALID_DYNAMIC_PROPERTY",entry.Name);var property=matches[0];try{property.Value=entry.Value.ToObject(property.Value.GetType());}catch(Exception e){throw new CadException("INVALID_DYNAMIC_VALUE",entry.Name+": "+e.Message);}}reference.RecordGraphicsModified(true);foreach(var entry in values.Properties()){var p=reference.DynamicBlockReferencePropertyCollection.Cast<DynamicBlockReferenceProperty>().Single(x=>x.PropertyName==entry.Name&&!x.ReadOnly);bool equal=entry.Value.Type==JTokenType.Boolean?entry.Value.Value<bool>()==Convert.ToBoolean(p.Value):entry.Value.Type==JTokenType.String?entry.Value.Value<string>()==Convert.ToString(p.Value):Math.Abs(entry.Value.Value<double>()-Convert.ToDouble(p.Value))<1e-6;if(!equal)throw new CadException("DYNAMIC_VALUE_REJECTED","AutoCAD không nhận đúng giá trị "+entry.Name);}double scale=op.SymbolScale??1;if(reference.ScaleFactors.X!=scale||reference.ScaleFactors.Y!=scale||reference.ScaleFactors.Z!=scale)throw new CadException("INVALID_SCALE","Block phải giữ tỷ lệ 1:1.");}
}
