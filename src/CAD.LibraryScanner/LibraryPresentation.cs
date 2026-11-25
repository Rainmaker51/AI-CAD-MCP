using System;
using System.Globalization;
using System.Linq;
using AICad.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;

namespace AICad.LibraryScanner;
public sealed partial class LibrarySyncNative
{
    // Update only the selected resource's tagged presentation, without regrouping,
    // moving cells or recreating a sample previously erased by the user.
    static void RefreshResourcePresentation(Database db,Transaction tx,LibrarySyncItem item)
    {
        foreach(var entity in LibraryFrames.Model(db,tx).Where(e=>SampleMetadata(e,tx)?.Id==item.Id).ToArray())
        {
            tx.GetObject(entity.ObjectId,OpenMode.ForWrite);
            if(entity is MText label&&IsPresentationHelper(entity,tx))
            {
                var lines=label.Text.Replace("\r","").Split('\n').ToList();
                if(lines.Count==0)lines.Add(item.Name);else lines[0]=item.Name;
                string? summary=null;
                if(item.Kind=="layers")
                {
                    var v=item.Values;string color=v.Value<int?>("rgb") is int rgb?"RGB "+((rgb>>16)&255)+","+((rgb>>8)&255)+","+(rgb&255):"ACI "+v["color"];
                    int lineWeight=v.Value<int>("lineWeight");string weight=lineWeight>=0?(lineWeight/100.0).ToString("0.##",CultureInfo.InvariantCulture)+" mm":((LineWeight)lineWeight).ToString();
                    summary=color+" | LW "+weight+" | "+v["lineType"]+" | In "+v["plottable"];
                }
                else if(item.Kind=="hatches")summary="Pattern "+item.Values["pattern"]+" | Scale "+item.Values["scale"]+" | Góc "+(item.Values.Value<double>("angle")*180/Math.PI).ToString("0.##",CultureInfo.InvariantCulture)+"° | ACI "+item.Values["color"]+" | RGB "+item.Values["rgb"]+" | Layer "+item.Values["layer"];
                else if(item.Kind=="texts")summary="Font "+item.Values["font"]+" | BigFont "+item.Values["bigFont"]+" | Cao "+item.Values["height"]+" | Rộng "+item.Values["widthFactor"]+" | Nghiêng "+item.Values["obliqueAngle"];
                else if(item.Kind=="dims")summary="Text "+item.Values["textStyle"]+" | Scale "+item.Values["scale"]+" | Chữ "+item.Values["textHeight"]+" | Mũi tên "+item.Values["arrowSize"]+" | Annotative "+item.Values["annotative"];
                else if(item.Kind=="blocks")summary="Đơn vị "+item.Values["units"]+" | Block động "+item.Values["dynamic"]+" | Tham số "+(item.Values["properties"] as JArray??new()).Count+" | ATT "+(item.Values["attributes"] as JArray??new()).Count;
                if(summary!=null)
                {
                    bool generated=lines.Count>1&&(new[]{"ACI ","RGB ","Font ","Text ","Đơn vị ","Pattern "}.Any(p=>lines[1].StartsWith(p,StringComparison.Ordinal))||lines[1].Contains(" | Scale "));
                    if(generated)lines[1]=summary;else lines.Insert(1,summary);
                }
                foreach(var field in new[]{new{Key="Nhóm | ",Value=item.Group},new{Key="Công dụng | ",Value=item.Purpose},new{Key="Ghi chú | ",Value=item.Notes}})
                {
                    int index=lines.FindIndex(line=>line.StartsWith(field.Key,StringComparison.Ordinal));
                    if(index>=0)lines[index]=field.Key+field.Value;else lines.Add(field.Key+field.Value);
                }
                label.Contents=string.Join("\\P",lines.Select(Safe));
            }
            MarkSample(entity,tx,item);
        }
    }
}
