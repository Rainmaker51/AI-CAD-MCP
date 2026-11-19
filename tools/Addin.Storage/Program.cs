using System;
using System.IO;
using System.Linq;
using AICad.Contracts;

if(args.Length!=3||args[1]!="--app-root"||!new[]{"inspect","verify","migrate"}.Contains(args[0]))
{
    Console.Error.WriteLine("Usage: Addin.Storage inspect|verify|migrate --app-root <repo>");return 2;
}
try
{
    var catalog=new CadToolCatalog(Path.GetFullPath(args[2]));var index=catalog.Read();
    if(args[0]=="migrate"){catalog.MigrateInstalled(index.Revision);index=catalog.Read();}
    if(args[0]!="inspect")foreach(var tool in index.Items.Where(t=>t.Kind=="dotnet"&&t.Enabled&&!t.DeletedUtc.HasValue))catalog.Demand(tool);
    Console.WriteLine(Json.Write(new{revision=index.Revision,storageVersion=index.StorageVersion,tools=index.Items.Where(t=>t.Kind=="dotnet").Select(t=>new{t.Id,t.Name,t.RelativePath,t.FileHash,t.PackageHash,t.Enabled,t.DeletedUtc})}));return 0;
}
catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
