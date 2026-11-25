using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AICad.Contracts;
namespace AICad.Bridge;

internal static class CadReaderDependency {
 static CadSharedComponentStore? store;static Assembly? loaded;static string loadedHash="";
 internal static void Configure(string dataRoot){store=new CadSharedComponentStore(dataRoot);AppDomain.CurrentDomain.AssemblyResolve-=Resolve;AppDomain.CurrentDomain.AssemblyResolve+=Resolve;}
 internal static void Stop(){AppDomain.CurrentDomain.AssemblyResolve-=Resolve;}
 static Assembly? Resolve(object sender,ResolveEventArgs args)=>new AssemblyName(args.Name).Name==CadSharedComponentStore.ReaderName?Load(new AssemblyName(args.Name)):null;
 internal static Assembly Load(AssemblyName? requested=null){
  if(requested!=null&&requested.Version!=new Version(1,0,0,0))throw new CadException("LIBRARY_PROTOCOL","Cài Reader tương thích giao thức V1 trong Thành phần dùng chung.");
  var selected=store?.Read()??throw new CadException("LIBRARY_READER_MISSING","Chưa cài Reader. Mở Manager → Thành phần dùng chung → Cài / cập nhật Reader.");
  string path=store!.Demand(selected),hash=selected.Files.Single(f=>f.Name==CadSharedComponentStore.ReaderName+".dll").Hash;
  if(loaded!=null){if(loadedHash!=hash)throw new CadException("TOOL_RESTART_REQUIRED","Reader đã đổi. Mở lại AutoCAD để nạp bản vừa cài.");return loaded;}
  if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name==CadSharedComponentStore.ReaderName))throw new CadException("TOOL_RESTART_REQUIRED","Reader đã nạp ngoài thành phần dùng chung. Mở lại AutoCAD; không NETLOAD Reader riêng.");
  using var guard=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);loaded=Assembly.LoadFrom(path);loadedHash=hash;return loaded;
 }
 internal static string ResolveAsset(string root,string hash){
  try{return (string)Load().GetType("Rainmaker.Library.Reader.LibraryReader")!.GetMethod("ResolveAssetFile")!.Invoke(null,new object[]{root,hash})!;}
  catch(TargetInvocationException error){var inner=error.InnerException!;throw new CadException(inner.GetType().GetProperty("Code")?.GetValue(inner)?.ToString()??"LIBRARY_READER",inner.Message);}
 }
}
