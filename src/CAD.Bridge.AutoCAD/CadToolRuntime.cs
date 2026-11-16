using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;
namespace AICad.Bridge;
internal sealed class CadToolCommand {
 internal string Name="";internal MethodInfo Method=null!;internal CommandFlags Flags;
 public override string ToString()=>Name;
}
internal sealed class CadLoadedTool {
 internal string Package="";internal Assembly Assembly=null!;internal List<CadToolCommand> Commands=new();internal List<IExtensionApplication> Initializers=new();internal Dictionary<Type,object> Instances=new();
}
internal sealed class CadToolRuntime:IDisposable {
 readonly CadToolCatalog catalog;readonly string cache;readonly Dictionary<string,CadLoadedTool> loaded=new();readonly Dictionary<Assembly,string> folders=new();readonly Dictionary<string,string> dependencies=new(StringComparer.OrdinalIgnoreCase);
 internal CadToolRuntime(InstallConfig config){catalog=new CadToolCatalog(config.ContentRoot);cache=Path.Combine(config.DataRoot,"tool-manager","runtime-cache");AppDomain.CurrentDomain.AssemblyResolve+=Resolve;}
 Assembly? Resolve(object sender,ResolveEventArgs args){
  if(args.RequestingAssembly==null||!folders.TryGetValue(args.RequestingAssembly,out string folder))return null;
  var name=new AssemblyName(args.Name);string file=Path.Combine(folder,name.Name+".dll");if(!File.Exists(file))return null;
  string hash=JsonUtil.FileHash(file);if(dependencies.TryGetValue(args.Name,out string previous)&&previous!=hash)throw new CadException("TOOL_RESTART_REQUIRED","Dependency đã đổi: "+name.Name+". Mở lại AutoCAD để nạp đúng phiên bản.");
  var existing=folders.Keys.FirstOrDefault(a=>a.FullName==args.Name&&dependencies.TryGetValue(args.Name,out string value)&&value==hash);if(existing!=null)return existing;
  var assembly=Assembly.LoadFile(file);folders[assembly]=folder;dependencies[args.Name]=hash;return assembly;
 }
 internal CadLoadedTool Load(CadToolEntry tool,bool reload=false){
  string file=catalog.Demand(tool);if(tool.Kind!="dotnet")throw new CadException("TOOL_TYPE","Chọn công cụ DLL.");
  if(loaded.TryGetValue(tool.Id,out var current)&&current.Package==tool.PackageHash&&!reload)return current;
  // A fresh shadow folder leaves the imported/source DLL unlocked. CLR assemblies cannot be unloaded individually in AutoCAD 2021.
  string folder=Path.Combine(cache,tool.Id,tool.PackageHash,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  foreach(string source in Directory.GetFiles(Path.GetDirectoryName(file)!))File.Copy(source,Path.Combine(folder,Path.GetFileName(source)));
  foreach(string dependency in Directory.GetFiles(folder,"*.dll").Where(p=>Path.GetFileName(p)!=Path.GetFileName(file))){string identity;try{identity=AssemblyName.GetAssemblyName(dependency).FullName;}catch(BadImageFormatException){continue;}if(dependencies.TryGetValue(identity,out string hash)&&hash!=JsonUtil.FileHash(dependency))throw new CadException("TOOL_RESTART_REQUIRED","Dependency đã đổi. Mở lại AutoCAD: "+Path.GetFileName(dependency));}
  Assembly assembly;try{assembly=Assembly.LoadFile(Path.Combine(folder,Path.GetFileName(file)));}catch(System.Exception e){throw new CadException("TOOL_LOAD","Không nạp được DLL .NET Framework 4.8 x64: "+e.Message);}
  folders[assembly]=folder;var result=new CadLoadedTool{Package=tool.PackageHash,Assembly=assembly};
  Type[] types;try{types=assembly.GetExportedTypes();}catch(ReflectionTypeLoadException e){throw new CadException("TOOL_DEPENDENCY",string.Join("; ",e.LoaderExceptions.Where(x=>x!=null).Select(x=>x.Message).Take(4)));}
  foreach(var type in types.Where(t=>!t.IsAbstract&&!t.ContainsGenericParameters))foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance).Where(m=>m.ReturnType==typeof(void)&&m.GetParameters().Length==0&&!m.ContainsGenericParameters))foreach(CommandMethodAttribute attr in method.GetCustomAttributes(typeof(CommandMethodAttribute),false)){
   if(!method.IsStatic&&type.GetConstructor(Type.EmptyTypes)==null)continue;result.Commands.Add(new CadToolCommand{Name=attr.GlobalName,Method=method,Flags=attr.Flags});
  }
  if(result.Commands.GroupBy(c=>c.Name,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new CadException("TOOL_COMMAND","DLL có tên lệnh trùng. Chỉnh DLL trước khi chạy.");
  if(current!=null){foreach(var init in current.Initializers)init.Terminate();loaded.Remove(tool.Id);}
  try{foreach(var type in types.Where(t=>typeof(IExtensionApplication).IsAssignableFrom(t)&&!t.IsAbstract&&t.GetConstructor(Type.EmptyTypes)!=null)){var init=(IExtensionApplication)Activator.CreateInstance(type);init.Initialize();result.Initializers.Add(init);}}catch{foreach(var init in result.Initializers)try{init.Terminate();}catch{}throw;}
  loaded[tool.Id]=result;return result;
 }
 internal void Run(CadToolEntry tool,string command){
  catalog.Demand(tool);var item=Load(tool);var c=item.Commands.SingleOrDefault(x=>x.Name==command)??throw new CadException("TOOL_COMMAND","Chọn lệnh thuộc DLL đang nạp.");object? instance=null;
  if(!c.Method.IsStatic){var type=c.Method.DeclaringType!;if(!item.Instances.TryGetValue(type,out instance)){instance=Activator.CreateInstance(type);item.Instances[type]=instance;}}
  try{c.Method.Invoke(instance,null);}catch(TargetInvocationException e){throw new CadException("TOOL_EXECUTION",e.InnerException?.Message??e.Message);}
 }
 internal static string LispExpression(string path,string? command){
  string quoted=path.Replace('\\','/').Replace("\"","\\\"");if(quoted.Contains("\n")||quoted.Contains("\r"))throw new CadException("TOOL_PATH","Đường dẫn không hợp lệ.");
  return "(progn (load \""+quoted+"\") "+(string.IsNullOrEmpty(command)?"":"(c:"+CadToolCatalog.CommandName(command!)+") ")+"(princ))\n";
 }
 public void Dispose(){AppDomain.CurrentDomain.AssemblyResolve-=Resolve;foreach(var tool in loaded.Values)foreach(var init in tool.Initializers)try{init.Terminate();}catch{}loaded.Clear();}
}
