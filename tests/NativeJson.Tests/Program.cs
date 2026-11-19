using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using AICad.Contracts;
using AICad.LibraryScanner;
using Newtonsoft.Json.Linq;

const string data="{\"version\":1,\"nodes\":[{\"Id\":\"group-1\",\"Path\":\"Kiến trúc / Cửa\"}]}";
using var context=new SplitJsonContext();
var fixture=context.LoadFromAssemblyPath(typeof(Fixture).Assembly.Location).GetType(typeof(Fixture).FullName!)!;
try
{
    fixture.GetMethod(nameof(Fixture.LegacyRead))!.Invoke(null,new object[]{data});
    throw new Exception("The legacy shared-token boundary did not reproduce the cross-context error.");
}
catch(TargetInvocationException error)when(error.InnerException?.Message.Contains("Cannot create and populate list type Newtonsoft.Json.Linq.JToken")==true)
{
    Console.WriteLine("PASS: reproduced the reported JToken / nodes error with two Json.NET contexts");
}
var parsed=fixture.GetMethod(nameof(Fixture.Read))!.Invoke(null,new object[]{data})!;
if(parsed.GetType()==typeof(JObject)||parsed.ToString()!.Contains("Kiến trúc")==false)
    throw new Exception("Parser did not preserve the consumer token identity and group data.");
string deep="0";for(int i=0;i<40;i++)deep="{\"x\":"+deep+"}";
foreach(string invalid in new[]{"{\"nodes\":[],\"nodes\":[]}","{\"nodes\":[]} {}","[]","{\"nodes\":",deep})
{
    try{NativeJson.ReadObject(invalid);throw new Exception("Invalid JSON was accepted");}
    catch(CadException error)when(error.Code=="INVALID_JSON"){}
}
try{NativeJson.ReadObject(new string(' ',2*1024*1024+1));throw new Exception("Oversized input was accepted");}
catch(CadException error)when(error.Code=="PAYLOAD_TOO_LARGE"){}
if(NativeJson.ReadObject("{\"nodes\":[],\"date\":\"2026-11-19T00:00:00Z\"}")["date"]!.Type!=JTokenType.String)
    throw new Exception("Date strings were converted");
Console.WriteLine("PASS: local token parsing, Vietnamese groups, duplicate/trailing/malformed/deep/oversized JSON rejection and date preservation");

public static class Fixture
{
    public static object LegacyRead(string text)=>Json.Read<JObject>(text);
    public static object Read(string text)
    {
        var data=NativeJson.ReadObject(text);var nodes=data["nodes"]!.ToObject<List<FixtureNode>>()!;
        if(nodes.Count!=1||nodes[0].Id!="group-1"||nodes[0].Path!="Kiến trúc / Cửa")throw new Exception("Group conversion lost metadata");
        return data;
    }
}
public sealed class FixtureNode{public string Id{get;set;}="";public string Path{get;set;}="";}
sealed class SplitJsonContext:AssemblyLoadContext,IDisposable
{
    public SplitJsonContext():base(isCollectible:true){}
    protected override Assembly? Load(AssemblyName name)
    {
        if(name.Name==typeof(CadException).Assembly.GetName().Name)return typeof(CadException).Assembly;
        if(name.Name==typeof(JObject).Assembly.GetName().Name)return LoadFromAssemblyPath(typeof(JObject).Assembly.Location);
        return null;
    }
    public void Dispose()=>Unload();
}
