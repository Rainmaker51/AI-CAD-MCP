using System.Collections.Generic;
using System;
using AICad.Geometry;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
namespace AICad.Application;
public static class AiOperationDefaults {
 // Structured replies sometimes encode an unused nullable field as zero/empty.
 // Only neutral values are normalized; meaningful invalid values still fail validation.
 public static void Normalize(IEnumerable<Operation> ops){foreach(var op in ops){if(op.Kind=="add_dimension"&&op.Points.Count==3&&double.TryParse(op.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var label)&&System.Math.Abs(label-AICad.Geometry.Validation.Distance(op.Points[0],op.Points[1]))<1e-6)op.Text="";if(op.OpeningWidth==0)op.OpeningWidth=null;if(op.WallThickness==0)op.WallThickness=null;if(string.IsNullOrWhiteSpace(op.DynamicJson)||op.DynamicJson.Trim()=="{}")op.DynamicJson=null;}}

 public static bool UseCatalogWindowDepth(IEnumerable<Operation> operations, TemplateProfile template)
 {
  if(template.Id!="ISO_QUANGANH"||!template.Blocks.Exists(b=>b.Id=="QA_CUA_SO_1_CANH"&&
      b.Usage?.Reviewed==true&&b.Usage.ThicknessParameter==""&&b.Usage.Values["DT"]!=null))
   return false;
  bool changed=false;
  foreach(var op in operations)
  {
   if(op.Kind!="insert_block"||op.AssetId!="QA_CUA_SO_1_CANH")
    continue;
   // This asset has a fixed frame depth. Wall thickness belongs to the wall.
   if(op.WallThickness is 110 or 220)
   {
    op.WallThickness=null;
    changed=true;
   }
   if(string.IsNullOrWhiteSpace(op.DynamicJson))
    continue;
   var values=Json.Read<JObject>(op.DynamicJson);
   if(!values.Remove("DT"))
    continue;
   op.DynamicJson=values.Count==0?null:Json.Write(values);
   changed=true;
  }
  return changed;
 }

 public static void FitEstimatedWindowWidths(DrawingPlan plan)
 {
  if(plan.Template.Id!="ISO_QUANGANH")
   return;
  var asset=plan.Template.Blocks.Find(b=>b.Id=="QA_CUA_SO_1_CANH");
  if(asset?.NativeSource?.BlockName!="MBCUASO"||
     asset.Usage is not {Reviewed:true,ConversionConfirmed:true,WidthParameter:"RCUA",WidthFactor:1,WidthOffset:-100})
   return;

  var changes=new List<string>();
  foreach(var op in plan.Operations)
  {
   if(op.Kind!="insert_block"||op.AssetId!=asset.Id||op.OpeningWidth is not double opening)
    continue;
   double requested=opening-100;
   // RCUA on this block uses a measured 10 mm step. Keep an inferred
   // frame within the original wall opening by taking the lower step.
   if(requested<300||requested>3000)
    continue;
   double fitted=Math.Floor((requested+1e-6)/10)*10;
   if(Math.Abs(fitted-requested)<1e-6)
    continue;
   _=DraftingRules.Values(op,asset);
   op.OpeningWidth=fitted+100;
   if(op.DynamicJson is string json)
   {
    var values=Json.Read<JObject>(json);
    values.Remove("RCUA");
    op.DynamicJson=values.Count==0?null:Json.Write(values);
   }
   changes.Add(FormattableString.Invariant($"{opening:0.##} → {op.OpeningWidth:0.##} mm"));
  }
  if(changes.Count>0)
   plan.Assumptions.Add("Cửa sổ theo cỡ block mà AutoCAD nhận (ô chờ ảnh → phủ bì block): "+
    string.Join(", ",changes)+". Giữ nguyên khoảng hở tường và kiểm tra mép khung trên preview.");
 }
}
