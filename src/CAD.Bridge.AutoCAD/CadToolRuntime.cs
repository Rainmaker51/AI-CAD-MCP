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
 internal CadRibbonPin? SuggestedPin;
 public override string ToString()=>Name;
}
internal sealed class CadLoadedTool {
 internal string Package="";internal Assembly Assembly=null!;internal List<CadToolCommand> Commands=new();internal List<IExtensionApplication> Initializers=new();internal Dictionary<Type,object> Instances=new();
}
internal sealed class CadToolRuntime:IDisposable {
 static readonly HashSet<Assembly> managerAssemblies=new();
 readonly CadToolCatalog catalog;readonly string cache;readonly Dictionary<string,CadLoadedTool> loaded=new();readonly Dictionary<Assembly,string> folders=new();readonly Dictionary<string,string> dependencies=new(StringComparer.OrdinalIgnoreCase);
 readonly Action<string,string>? dispatch;
 internal CadToolRuntime(InstallConfig config,Action<string,string>? dispatch=null){catalog=new CadToolCatalog(config.ContentRoot);cache=Path.Combine(config.DataRoot,"tool-manager","runtime-cache");this.dispatch=dispatch;AppDomain.CurrentDomain.AssemblyResolve+=Resolve;}
 Assembly? Resolve(object sender,ResolveEventArgs args){
  if(args.RequestingAssembly==null||!folders.TryGetValue(args.RequestingAssembly,out string folder))return null;
  var shared=new[]{typeof(Plugin).Assembly,typeof(CadToolCatalog).Assembly,typeof(Newtonsoft.Json.Linq.JObject).Assembly}
   .FirstOrDefault(assembly=>string.Equals(assembly.FullName,args.Name,StringComparison.OrdinalIgnoreCase));
  if(shared!=null)return shared;
  var name=new AssemblyName(args.Name);string file=Path.Combine(folder,name.Name+".dll");
  if(!File.Exists(file))return null;
  string hash=JsonUtil.FileHash(file);if(dependencies.TryGetValue(args.Name,out string previous)&&previous!=hash)throw new CadException("TOOL_RESTART_REQUIRED","Dependency đã đổi: "+name.Name+". Mở lại AutoCAD để nạp đúng phiên bản.");
  var existing=folders.Keys.FirstOrDefault(a=>a.FullName==args.Name&&dependencies.TryGetValue(args.Name,out string value)&&value==hash);if(existing!=null)return existing;
  var assembly=Assembly.LoadFile(file);folders[assembly]=folder;dependencies[args.Name]=hash;return assembly;
 }
 internal CadLoadedTool Load(CadToolEntry tool,bool reload=false){
  string file=catalog.Demand(tool);if(tool.Kind!="dotnet")throw new CadException("TOOL_TYPE","Chọn công cụ DLL.");
  if(loaded.TryGetValue(tool.Id,out var current)&&current.Package==tool.PackageHash&&!reload)return current;
  string mainIdentity=AssemblyName.GetAssemblyName(file).Name;
  if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>!a.IsDynamic&&a.GetName().Name==mainIdentity&&!managerAssemblies.Contains(a)))throw new CadException("TOOL_RESTART_REQUIRED","DLL đã được nạp ngoài Manager. Gỡ tự nạp/NETLOAD cũ và mở lại AutoCAD rồi nạp qua Manager.");
  // A fresh shadow folder leaves the imported/source DLL unlocked. CLR assemblies cannot be unloaded individually in AutoCAD 2021.
  string folder=Path.Combine(cache,tool.Id,tool.PackageHash,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  foreach(string source in Directory.GetFiles(Path.GetDirectoryName(file)!))File.Copy(source,Path.Combine(folder,Path.GetFileName(source)));
  foreach(string dependency in Directory.GetFiles(folder,"*.dll").Where(p=>Path.GetFileName(p)!=Path.GetFileName(file))){string identity;try{identity=AssemblyName.GetAssemblyName(dependency).FullName;}catch(BadImageFormatException){continue;}if(dependencies.TryGetValue(identity,out string hash)&&hash!=JsonUtil.FileHash(dependency))throw new CadException("TOOL_RESTART_REQUIRED","Dependency đã đổi. Mở lại AutoCAD: "+Path.GetFileName(dependency));}
  Assembly assembly;try{assembly=Assembly.LoadFile(Path.Combine(folder,Path.GetFileName(file)));}catch(System.Exception e){throw new CadException("TOOL_LOAD","Không nạp được DLL .NET Framework 4.8 x64: "+e.Message);}
  folders[assembly]=folder;managerAssemblies.Add(assembly);var result=new CadLoadedTool{Package=tool.PackageHash,Assembly=assembly};
  Type[] types;try{types=assembly.GetExportedTypes();}catch(ReflectionTypeLoadException e){throw new CadException("TOOL_DEPENDENCY",string.Join("; ",e.LoaderExceptions.Where(x=>x!=null).Select(x=>x.Message).Take(4)));}
  foreach(var type in types.Where(t=>!t.IsAbstract&&!t.ContainsGenericParameters))foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance).Where(m=>m.ReturnType==typeof(void)&&m.GetParameters().Length==0&&!m.ContainsGenericParameters))foreach(CommandMethodAttribute attr in method.GetCustomAttributes(typeof(CommandMethodAttribute),false)){
   if(!method.IsStatic&&type.GetConstructor(Type.EmptyTypes)==null)continue;result.Commands.Add(new CadToolCommand{Name=attr.GlobalName,Method=method,Flags=attr.Flags});
  }
  if(result.Commands.GroupBy(c=>c.Name,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new CadException("TOOL_COMMAND","DLL có tên lệnh trùng. Chỉnh DLL trước khi chạy.");
  if(current!=null){foreach(var init in current.Initializers)init.Terminate();loaded.Remove(tool.Id);}
  try{foreach(var type in types.Where(t=>typeof(IExtensionApplication).IsAssignableFrom(t)&&!t.IsAbstract&&t.GetConstructor(Type.EmptyTypes)!=null)){
   var init=(IExtensionApplication)Activator.CreateInstance(type);result.Initializers.Add(init);
   if(init is ICadManagedAddin managed){
    var descriptors=managed.ManagedCommands.ToList();
    if(descriptors.Count==0||descriptors.Count>200||descriptors.Select(p=>p.Command).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=descriptors.Count||descriptors.Any(p=>result.Commands.All(c=>c.Name!=p.Command)))throw new CadException("TOOL_COMMAND","Danh sách lệnh quản lý không khớp DLL.");
    result.Commands=result.Commands.Where(c=>descriptors.Any(p=>p.Command==c.Name)).ToList();foreach(var command in result.Commands)command.SuggestedPin=descriptors.Single(p=>p.Command==command.Name);
    managed.InitializeManaged(command=>{if(dispatch==null)throw new CadException("TOOL_COMMAND","Lệnh palette cần chạy qua CAD Tool Manager.");dispatch(tool.Id,command);});
   }else init.Initialize();
  }}catch{foreach(var init in result.Initializers)try{init.Terminate();}catch{}throw;}
  loaded[tool.Id]=result;return result;
 }
 internal void Disable(string id){if(!loaded.TryGetValue(id,out var item))return;foreach(var init in item.Initializers)try{init.Terminate();}catch{}loaded.Remove(id);}
 internal void Synchronize(CadToolIndex index){foreach(string id in loaded.Keys.ToArray()){var tool=index.Items.FirstOrDefault(t=>t.Id==id);if(tool==null||!tool.Enabled||tool.DeletedUtc.HasValue||tool.PackageHash!=loaded[id].Package)Disable(id);}}
 internal void Run(CadToolEntry tool,string command){
  catalog.Demand(tool);var item=Load(tool);var c=item.Commands.SingleOrDefault(x=>x.Name==command)??throw new CadException("TOOL_COMMAND","Chọn lệnh thuộc DLL đang nạp.");object? instance=null;
  if(!c.Method.IsStatic){var type=c.Method.DeclaringType!;if(!item.Instances.TryGetValue(type,out instance)){instance=Activator.CreateInstance(type);item.Instances[type]=instance;}}
  try{c.Method.Invoke(instance,null);}catch(TargetInvocationException e){throw new CadException("TOOL_EXECUTION",e.InnerException?.Message??e.Message);}
 }
 public void Dispose(){AppDomain.CurrentDomain.AssemblyResolve-=Resolve;foreach(var tool in loaded.Values)foreach(var init in tool.Initializers)try{init.Terminate();}catch{}loaded.Clear();}
}
