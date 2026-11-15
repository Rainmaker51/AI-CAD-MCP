using System;
using System.Text;
using AICad.Contracts;
// Runs under the exact Framework runtime used by AutoCAD 2021.
// Only parses a bounded typed plan and computes its integrity digest.
class Program {
 static int Main(string[] args){try{
  Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
  string input=Console.In.ReadToEnd();object value=args.Length==0||args[0]=="plan"?Json.Read<DrawingPlan>(input):args[0]=="template"?(object)Json.Read<TemplateProfile>(input):args[0]=="skill"?(object)Json.Read<SkillDefinition>(input):throw new Exception("Unknown digest type.");
  Console.Write(Json.Hash(value));return 0;
 }catch{Console.Error.Write("Plan digest could not be computed.");return 1;}}
}
