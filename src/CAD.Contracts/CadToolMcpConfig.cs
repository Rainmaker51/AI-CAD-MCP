using System;
using System.IO;
using Newtonsoft.Json.Linq;
namespace AICad.Contracts;
public static class CadToolMcpConfig {
 public static JObject Create(InstallConfig config){
  string runtime=Path.Combine(config.ContentRoot,".tools","dotnet","dotnet.exe"),server=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(config.HostExecutable)!)!,"MCP","AICad.Mcp.dll");
  if(!File.Exists(runtime)){string configured=Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")??"","dotnet.exe");runtime=File.Exists(configured)?Path.GetFullPath(configured):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");}
  var registration=new JObject{["command"]=runtime,["args"]=new JArray(server)};
  return new JObject{["serverPath"]=server,["installed"]=File.Exists(runtime)&&File.Exists(server),["claudeDesktop"]=new JObject{["mcpServers"]=new JObject{["ai_cad"]=registration}},["codexToml"]="[mcp_servers.ai_cad]\ncommand = "+Json.Write(runtime)+"\nargs = ["+Json.Write(server)+"]\n"};
 }
}
