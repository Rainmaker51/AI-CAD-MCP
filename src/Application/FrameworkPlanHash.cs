using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AICad.Contracts;
namespace AICad.Application;
public static class FrameworkPlanHash {
 public static string Compute(DrawingPlan plan,string helper)=>ComputeValue(plan,helper,"plan");
 public static string ComputeValue(object plan,string helper,string kind){
  if(!File.Exists(helper))throw new CadException("HASH_RUNTIME_MISSING","Thiếu công cụ kiểm tra tương thích AutoCAD 2021. Hãy build lại Host.");
  string json=Json.Write(plan);
  if(Encoding.UTF8.GetByteCount(json)>2*1024*1024)throw new CadException("PAYLOAD_TOO_LARGE","Plan exceeds 2 MB.");
  var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
  start.ArgumentList.Add(kind);
  using var child=Process.Start(start)??throw new CadException("HASH_RUNTIME_FAILED","Cannot start Framework digest checker.");
  var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();
  child.StandardInput.Write(json);child.StandardInput.Close();
  if(!child.WaitForExit(10000)){child.Kill(true);throw new CadException("HASH_RUNTIME_FAILED","Framework digest checker timed out.");}
  string digest=output.GetAwaiter().GetResult().Trim();_=error.GetAwaiter().GetResult();
  if(child.ExitCode!=0||!Regex.IsMatch(digest,"^[0-9a-f]{64}$"))throw new CadException("HASH_RUNTIME_FAILED","Framework digest checker failed.");
  return digest;
 }
}
