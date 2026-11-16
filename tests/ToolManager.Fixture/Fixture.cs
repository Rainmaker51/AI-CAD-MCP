using System;
using System.IO;
using Autodesk.AutoCAD.Runtime;
public sealed class Fixture {
 [CommandMethod("QATOOLFIXTURE")]
 public static void Hello(){
#if FIXTURE_V2
  const string value="VERSION_2";
#else
  const string value="VERSION_1";
#endif
  string root=Environment.GetEnvironmentVariable("AICAD_TOOL_TEST_ROOT")??throw new System.Exception("Isolated test root required");File.WriteAllText(Path.Combine(root,"dotnet-ok.txt"),value);
 }
 [CommandMethod("QATOOLUNSUPPORTED")]
 public static int Unsupported()=>42;
}
