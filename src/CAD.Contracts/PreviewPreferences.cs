using System;
using System.IO;
using System.Linq;
namespace AICad.Contracts;
public sealed class PreviewPreferences {
 public string Mode {get;set;}="ALWAYS";
 public static bool Supports(string version)=>Version.TryParse(version,out var v)&&v>=new Version(0,3,0);
 public static PreviewPreferences Load(string root)=>new(){Mode="ALWAYS"};
 public static void Validate(string mode){if(mode!="ALWAYS")throw new CadException("INVALID_PREVIEW_POLICY","Chế độ xem trước được cố định ALWAYS.");}
 public static PreviewPreferences Save(string root,string mode){Validate(mode);var p=new PreviewPreferences();Json.Atomic(Path.Combine(root,"preview-preferences.json"),p);return p;}
 public bool DrawGraphics(DrawingPlan plan)=>true;
}
