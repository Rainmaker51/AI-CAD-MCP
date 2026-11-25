using System;
using System.Collections.Generic;
using System.Linq;

namespace Rainmaker.Library.Reader;

// Public V1 is BCL-only: no AutoCAD, Manager, QA, or Json.NET types cross this boundary.
public sealed class LibraryReadException:Exception
{
    public string Code{get;}
    public LibraryReadException(string code,string message):base(message){Code=code;}
}
public sealed class ResourceKeyV1
{
    public string SourceId{get;}public string Kind{get;}public string Id{get;}
    public ResourceKeyV1(string sourceId,string kind,string id){SourceId=sourceId;Kind=kind;Id=id;}
}
public sealed class ResourceV1
{
    public ResourceKeyV1 Key{get;internal set;}=null!;
    public string Code{get;internal set;}="";
    public string Name{get;internal set;}="";
    public string DisplayName{get;internal set;}="";
    public string GroupPath{get;internal set;}="";
    public string Purpose{get;internal set;}="";
    public string Notes{get;internal set;}="";
    public bool Reviewed{get;internal set;}
    public string Availability{get;internal set;}="RETIRED";
    public bool Available=>Availability=="AVAILABLE";
    public string Version{get;internal set;}="";
    public string ParametersJsonV1{get;internal set;}="{}";
    public string ProfileJsonV1{get;internal set;}="{}";
    public string SourceFile{get;internal set;}="";
    public string SourceHash{get;internal set;}="";
}
public interface ILibraryReaderV1
{
    int ProtocolVersion{get;}
    LibrarySnapshotV1 Read();
    string GetCatalogRevision();
}
public sealed class LibrarySnapshotV1
{
    readonly IReadOnlyList<ResourceV1> resources;
    readonly Action verify;
    readonly Action<ResourceV1> resolve;
    internal LibrarySnapshotV1(List<ResourceV1> resources,Action verify,Action<ResourceV1> resolve){this.resources=resources.AsReadOnly();this.verify=verify;this.resolve=resolve;}
    public int ProtocolVersion=>1;
    public string Revision{get;internal set;}="";
    public string TemplateJsonV1{get;internal set;}="{}";
    public string SourceId{get;internal set;}="";
    public IReadOnlyList<ResourceV1> ListResources(string kind="")=>resources.Where(r=>r.Available&&(kind==""||r.Key.Kind==kind)).ToList().AsReadOnly();
    public ResourceV1 GetResource(ResourceKeyV1 key)=>resources.SingleOrDefault(r=>r.Key.SourceId==key.SourceId&&r.Key.Kind==key.Kind&&r.Key.Id==key.Id)??throw new LibraryReadException("RESOURCE_NOT_FOUND","Không có ID tài nguyên trong kho: "+key.Id);
    public ResourceV1 FindByCode(string code)=>resources.SingleOrDefault(r=>string.Equals(r.Code,code,StringComparison.OrdinalIgnoreCase))??throw new LibraryReadException("RESOURCE_NOT_FOUND","Không có mã tài nguyên: "+code);
    public IReadOnlyList<ResourceV1> SearchResources(string text,string kind="")=>ListResources(kind).Where(r=>(r.Code+" "+r.Name+" "+r.DisplayName+" "+r.GroupPath+" "+r.Purpose+" "+r.Notes).IndexOf(text??"",StringComparison.CurrentCultureIgnoreCase)>=0).ToList().AsReadOnly();
    public void VerifyUnchanged()=>verify();
    public IReadOnlyList<ResourceV1> ResolveResources(IEnumerable<ResourceKeyV1> keys)
    {
        verify();var result=keys.Select(GetResource).ToList();
        foreach(var resource in result){if(!resource.Available)throw new LibraryReadException("RESOURCE_UNAVAILABLE",resource.Code+" · "+resource.Name+": "+resource.Availability);resolve(resource);}
        verify();return result.AsReadOnly();
    }
}
