using System.IO;
using System.Text;
using AICad.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AICad.LibraryScanner;

/// <summary>Parse tokens in the consumer's Json.NET context, never through a shared generic boundary.</summary>
public static class NativeJson
{
    public static JObject ReadObject(string text)
    {
        if(Encoding.UTF8.GetByteCount(text)>2*1024*1024)
            throw new CadException("PAYLOAD_TOO_LARGE","Payload exceeds 2 MB.");
        try
        {
            using var input=new StringReader(text);
            using var reader=new JsonTextReader(input){MaxDepth=32,DateParseHandling=DateParseHandling.None};
            var result=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            if(reader.Read())throw new JsonException("Trailing JSON");
            return result;
        }
        catch(JsonException error){throw new CadException("INVALID_JSON",error.Message);}
    }
}
