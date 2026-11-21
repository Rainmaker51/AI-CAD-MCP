using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Rainmaker.Axis;
internal static class AxisUiState
{
 internal const double DefaultRatio=3.0/7;
 internal static double ReadRatio(string path){try{var value=JObject.Parse(File.ReadAllText(path)).Value<double>("ratio");if(double.IsNaN(value)||double.IsInfinity(value)||value<=0)return DefaultRatio;return Math.Max(.2,Math.Min(4,value));}catch{return DefaultRatio;}}
 internal static void SaveRatio(string path,double ratio){if(double.IsNaN(ratio)||double.IsInfinity(ratio)||ratio<=0)return;Directory.CreateDirectory(Path.GetDirectoryName(path)!);AICad.Contracts.Json.Atomic(path,new JObject{{"ratio",ratio}});}
}
