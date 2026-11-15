using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICad.Contracts;
using JsonUtil=AICad.Contracts.Json;
using Newtonsoft.Json.Linq;
namespace AICad.Application;
public sealed class CadClient(InstallConfig config) {
 [DllImport("kernel32",SetLastError=true)]static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint pid);
 public List<SessionInfo> Sessions(){string root=Path.Combine(config.DataRoot,"sessions");Directory.CreateDirectory(root);var result=new List<SessionInfo>();foreach(var file in Directory.EnumerateFiles(root,"*.json").Take(50))try{var s=JsonUtil.Read<SessionInfo>(File.ReadAllText(file));if((DateTime.UtcNow-s.HeartbeatUtc).TotalSeconds>20)continue;using var process=Process.GetProcessById(s.ProcessId);if(process.StartTime.ToUniversalTime().Ticks!=s.ProcessStartTicks||!process.ProcessName.Equals("acad",StringComparison.OrdinalIgnoreCase)&&!process.ProcessName.Equals("accoreconsole",StringComparison.OrdinalIgnoreCase))continue;JsonUtil.Id(s.SessionId);if(s.PipeName!="aicad-"+s.SessionId)continue;result.Add(s);}catch(Exception e)when(e is IOException||e is ArgumentException||e is InvalidOperationException||e is CadException){}return result;}
 public async Task<JToken> Call(string sessionId,string method,string documentId="",string revision="",JObject? args=null,CancellationToken cancellation=default){JsonUtil.Id(sessionId);var s=Sessions().SingleOrDefault(x=>x.SessionId==sessionId)??throw new CadException("SESSION_OFFLINE","AutoCAD session is no longer connected.");using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(12));using var pipe=new NamedPipeClientStream(".",s.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);try{await pipe.ConnectAsync(timeout.Token);if(!GetNamedPipeServerProcessId(pipe.SafePipeHandle,out var pid)||pid!=s.ProcessId)throw new CadException("UNTRUSTED_PLUGIN","Named pipe server identity differs.");var request=new NativeRequest{Secret=s.Secret,Method=method,DocumentId=documentId,Revision=revision,Params=args??new()};byte[] data=Encoding.UTF8.GetBytes(JsonUtil.Write(request));await pipe.WriteAsync(BitConverter.GetBytes(data.Length),timeout.Token);await pipe.WriteAsync(data,timeout.Token);await pipe.FlushAsync(timeout.Token);byte[] size=new byte[4];await pipe.ReadExactlyAsync(size,timeout.Token);int length=BitConverter.ToInt32(size);if(length<1||length>2*1024*1024)throw new CadException("PAYLOAD_TOO_LARGE","Invalid plugin response.");byte[] buffer=new byte[length];await pipe.ReadExactlyAsync(buffer,timeout.Token);var response=JsonUtil.Read<NativeResponse>(Encoding.UTF8.GetString(buffer));if(!response.Ok)throw new CadException(response.Code,response.Message);return response.Data??JValue.CreateNull();}catch(OperationCanceledException){throw new CadException("CAD_TIMEOUT","AutoCAD is busy, disconnected or awaiting input. Retry reads when idle; never automatically retry a write.");}}
 public async Task<List<DocumentInfo>> Documents(CancellationToken ct=default){var result=new List<DocumentInfo>();foreach(var s in Sessions())try{result.AddRange((await Call(s.SessionId,"list_documents",cancellation:ct)).ToObject<List<DocumentInfo>>()!);}catch(CadException){}return result;}
}
