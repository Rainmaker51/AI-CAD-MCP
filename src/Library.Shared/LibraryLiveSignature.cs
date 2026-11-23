using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
using JsonUtil=AICad.Contracts.Json;

namespace AICad.Library;

// The native add-in runs on .NET Framework, Host on .NET 10. Their default
// shortest-round-trip double formatting differs. Never use it as a wire hash.
internal static class LibraryLiveSignature
{
    static JObject Payload(List<LibrarySyncItem> items,JObject presence)=>JObject.FromObject(new
    {
        items=items.OrderBy(i=>i.Id,StringComparer.Ordinal).Select(i=>new{i.Id,i.Kind,i.Name,i.Group,i.Purpose,i.Notes,i.Values}),
        presence=new JObject(presence.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new JProperty(p.Name,p.Value.DeepClone())))
    });
    static JToken Normalize(JToken token,bool legacy)
    {
        if(token is JObject obj)return new JObject(obj.Properties().Select(p=>new JProperty(p.Name,Normalize(p.Value,legacy))));
        if(token is JArray array)return new JArray(array.Select(v=>Normalize(v,legacy)));
        if(token.Type!=JTokenType.Float)return token.DeepClone();
        double number=token.Value<double>();
        if(number==0)number=0; // Canonical positive zero on both Framework and modern .NET.
        if(double.IsNaN(number)||double.IsInfinity(number))throw new InvalidOperationException("Thông số CAD chứa số không hữu hạn.");
        if(!legacy)return new JValue("float:"+number.ToString("G15",CultureInfo.InvariantCulture));
        // Newtonsoft on Framework uses Framework's R format: G15 if it round trips,
        // otherwise G17, then a decimal suffix for floating-point whole numbers.
        string value=number.ToString("G15",CultureInfo.InvariantCulture);
        if(double.Parse(value,CultureInfo.InvariantCulture)!=number)value=number.ToString("G17",CultureInfo.InvariantCulture);
        if(value.IndexOf('.')<0&&value.IndexOf('E')<0&&value.IndexOf('e')<0)value+=".0";
        return new JRaw(value);
    }
    internal static string Compute(List<LibrarySyncItem> items,JObject presence)=>JsonUtil.Hash(Normalize(Payload(items,presence),false));
    internal static string Framework150(List<LibrarySyncItem> items,JObject presence)=>JsonUtil.Hash(Normalize(Payload(items,presence),true));
}
