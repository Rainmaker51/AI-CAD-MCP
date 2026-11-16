using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

Console.OutputEncoding=new UTF8Encoding(false);
var projectFolder=new DirectoryInfo(AppContext.BaseDirectory);while(projectFolder!=null&&!File.Exists(Path.Combine(projectFolder.FullName,"AI-CAD-MCP.slnx")))projectFolder=projectFolder.Parent;
string root=projectFolder?.FullName??throw new IOException("Không tìm thấy thư mục dự án AI CAD.");
string buildRoot=Directory.Exists(Path.Combine(root,"storage","04-builds","artifacts"))?Path.Combine(root,"storage","04-builds","artifacts"):Path.Combine(root,"artifacts");
string log=Path.Combine(root,"data-p0-p6","launcher-last.json");
try {
 var flags=args.ToHashSet(StringComparer.Ordinal);
 if(flags.Any(a=>!new[]{"--cad","--no-browser","--account"}.Contains(a)))throw new Exception("Tham số khởi chạy không hợp lệ.");
 string hostExe=Path.Combine(buildRoot,"p0-p6","Host","AICad.Host.exe");
 var config=Json.Read<InstallConfig>(File.ReadAllText(Path.Combine(buildRoot,"p0-p6","install.json")));
 log=Path.Combine(config.DataRoot,"launcher-last.json");
 if(!string.Equals(Path.GetFullPath(config.HostExecutable),hostExe,StringComparison.OrdinalIgnoreCase)||!File.Exists(hostExe))throw new Exception("Cấu hình Host không khớp bản app mới.");
 string runtime=Path.GetDirectoryName(Environment.ProcessPath)!;
 Environment.SetEnvironmentVariable("DOTNET_ROOT",runtime);
 Directory.CreateDirectory(config.DataRoot);
 string descriptor=Path.Combine(config.DataRoot,"host.json");
 using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(2)};
 async Task<JObject?> Running(){
  try {
   var info=Json.Read<JObject>(File.ReadAllText(descriptor));
   using var process=Process.GetProcessById(info.Value<int>("processId"));
   if(!string.Equals(process.MainModule?.FileName,hostExe,StringComparison.OrdinalIgnoreCase))return null;
   if(!Uri.TryCreate(info.Value<string>("url"),UriKind.Absolute,out var uri)||uri.Scheme!="http"||uri.Host!="127.0.0.1")return null;
   using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(uri,"/api/health"));
   request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",info.Value<string>("token"));
   using var response=await http.SendAsync(request);
   return response.IsSuccessStatusCode?info:null;
  }catch(Exception e)when(e is IOException||e is ArgumentException||e is InvalidOperationException||e is System.ComponentModel.Win32Exception||e is HttpRequestException||e is TaskCanceledException||e is CadException){return null;}
 }
 var info=await Running();
 if(info==null){
  Console.WriteLine("Đang khởi động dịch vụ AI CAD…");
  var host=new ProcessStartInfo(hostExe){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(hostExe)!};
  using var child=Process.Start(host)??throw new Exception("Không khởi động được Host.");
  for(int i=0;i<60&&info==null;i++){await Task.Delay(250);info=await Running();}
  if(info==null)throw new Exception("Host chưa sẵn sàng sau 15 giây. Kiểm tra .NET và cấu hình cài đặt.");
 }
 bool appOpened=false;int? cadPid=null;bool reused=false;
 if(!flags.Contains("--no-browser")){
  string page=flags.Contains("--account")?"account":"work";
  using var browser=Process.Start(new ProcessStartInfo(info.Value<string>("url")+"/?page="+page+"#token="+info.Value<string>("token")){UseShellExecute=true});
  appOpened=true;Console.WriteLine("Đã mở giao diện AI CAD trong trình duyệt.");
 }
 if(flags.Contains("--cad")){
  string sessions=Path.Combine(config.DataRoot,"sessions");
  if(Directory.Exists(sessions))foreach(string file in Directory.EnumerateFiles(sessions,"*.json"))try{
   var session=Json.Read<SessionInfo>(File.ReadAllText(file));
   if((DateTime.UtcNow-session.HeartbeatUtc).TotalSeconds>20||(!Version.TryParse(session.PluginVersion,out var pluginVersion)||pluginVersion<new Version(0,7,2)))continue;
   using var cad=Process.GetProcessById(session.ProcessId);
   if(cad.ProcessName.Equals("acad",StringComparison.OrdinalIgnoreCase)&&cad.StartTime.ToUniversalTime().Ticks==session.ProcessStartTicks&&cad.MainWindowHandle!=IntPtr.Zero){cadPid=cad.Id;reused=true;break;}
  }catch(Exception e)when(e is IOException||e is ArgumentException||e is InvalidOperationException||e is CadException){}
  if(!reused){
   string cadExe=@"C:\Program Files\Autodesk\AutoCAD 2021\acad.exe";
   var nativeRelease=new[]{"p0-p6-tools-dark","p0-p6"}.Select(name=>new{Path=Path.Combine(buildRoot,name),Marker=Path.Combine(buildRoot,name,"AutoCAD","plugin-version.txt")}).Where(x=>File.Exists(x.Marker)).Select(x=>new{Root=x.Path,Version=Version.TryParse(File.ReadAllText(x.Marker).Trim(),out var version)?version:new Version(0,0)}).Where(x=>x.Version>=new Version(0,7,2)).OrderByDescending(x=>x.Version).FirstOrDefault()??throw new Exception("Build lại plugin AutoCAD 0.7.2 để mở CAD Tool Manager dark mode.");
   string plugin=Path.Combine(nativeRelease.Root,"AutoCAD","CAD.Bridge.AutoCAD.dll");
   string pluginConfig=Path.Combine(nativeRelease.Root,"install.json");
   if(!File.Exists(cadExe)||!File.Exists(plugin))throw new Exception("Không tìm thấy AutoCAD 2021 hoặc plugin 0.7.2.");
   var native=Json.Read<InstallConfig>(File.ReadAllText(pluginConfig));
   if(native.DataRoot!=config.DataRoot||native.HostExecutable!=config.HostExecutable)throw new Exception("Plugin và app chưa dùng chung cấu hình kết nối.");
   string launch=Path.Combine(config.DataRoot,"drawings","launch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(launch);
   string drawing=Path.Combine(launch,"AI-CAD-START.dwg"),script=Path.Combine(launch,"load-plugin.scr");
   File.Copy(Path.Combine(root,"content","launch","blank-mm.dwg"),drawing);
   File.WriteAllText(script,"_NETLOAD\n\""+plugin.Replace('\\','/')+"\"\nAICADTOOLS\n",Encoding.ASCII);
   var cad=new ProcessStartInfo(cadExe){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Normal};
   cad.ArgumentList.Add(drawing);cad.ArgumentList.Add("/nologo");cad.ArgumentList.Add("/b");cad.ArgumentList.Add(script);
   using var child=Process.Start(cad)??throw new Exception("Không mở được AutoCAD.");cadPid=child.Id;
   Console.WriteLine("Đã mở AutoCAD 2021 với plugin AI CAD mới.");
  }else Console.WriteLine("AutoCAD đã kết nối; giữ nguyên phiên và bản vẽ đang mở.");
 }
 Json.Atomic(log,new{at=DateTime.UtcNow,status="STARTED",appOpened,hostUrl=info.Value<string>("url"),cadPid,reused});
 Console.WriteLine("AI CAD đã sẵn sàng.");
 return 0;
}catch(Exception e){
 Console.Error.WriteLine("Không mở được AI CAD: "+e.Message);
 try{Json.Atomic(log,new{at=DateTime.UtcNow,status="FAILED",message=e.Message});}catch{}
 return 1;
}
