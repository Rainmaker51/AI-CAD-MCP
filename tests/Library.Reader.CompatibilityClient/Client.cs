using System.Linq;
using Rainmaker.Library.Reader;
namespace ReaderCompatibilityClient;
// Consumer compiled against V1, unchanged when the storage layout is migrated.
public static class Client
{
    public static string[] List(ILibraryReaderV1 reader)=>reader.Read().ListResources().Select(r=>r.Key.SourceId+"|"+r.Key.Kind+"|"+r.Key.Id+"|"+r.Code).OrderBy(s=>s).ToArray();
    public static string Resolve(ILibraryReaderV1 reader,string code){var snapshot=reader.Read();var resource=snapshot.FindByCode(code);return snapshot.ResolveResources(new[]{resource.Key})[0].SourceHash;}
}
