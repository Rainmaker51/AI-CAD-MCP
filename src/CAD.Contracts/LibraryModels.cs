using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace AICad.Contracts;
public sealed class NativeBlockSource {
 [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public string? LabelAttribute {get;set;}
 [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public List<DynamicPropertySpec>? DynamicProperties{get;set;}
 public string FileHash{get;set;}="";
 public string BlockName{get;set;}="";
 public bool ModelSpace{get;set;}
 public Point2 Min{get;set;}=new();
 public Point2 Max{get;set;}=new();
}
public sealed class LibraryBlock {
 public List<DynamicPropertySpec> DynamicProperties{get;set;}=new();
 public List<VisibilitySample> VisibilityStates{get;set;}=new();
 public List<string> InternalLayers{get;set;}=new();
 public string DynamicError{get;set;}="";
 public string Name{get;set;}="";public string SuggestedId{get;set;}="";
 public bool ModelSpace{get;set;}public bool Supported{get;set;}public string Reason{get;set;}="";
 public bool Dynamic{get;set;}public int Attributes{get;set;}public int EntityCount{get;set;}
 public Point2 Min{get;set;}=new();public Point2 Max{get;set;}=new();
 public List<Operation> Preview{get;set;}=new();public bool PreviewComplete{get;set;}
}
public sealed class LibraryLayer { public List<double> Dashes{get;set;}=new();public double PatternLength{get;set;} 
 public string LineType{get;set;}="Continuous";public int LineWeight{get;set;}=-3;public int? Rgb{get;set;}public bool Plottable{get;set;}=true;
 public string Name{get;set;}="";public string SuggestedId{get;set;}="";public short Color{get;set;}=7;
 public bool Supported{get;set;}public string Reason{get;set;}="";
}
public sealed class DynamicPropertySpec {
 public string Name{get;set;}="";public string Description{get;set;}="";public string Units{get;set;}="";public string ValueType{get;set;}="";public bool ReadOnly{get;set;}public bool Visible{get;set;}
 public JToken? DefaultValue{get;set;}public List<JToken> AllowedValues{get;set;}=new();
}
public sealed class VisibilitySample {public string Property{get;set;}="";public string Value{get;set;}="";public Point2 Min{get;set;}=new();public Point2 Max{get;set;}=new();}
public sealed class LibraryStyle {
 public string Name{get;set;}="";public string Font{get;set;}="txt.shx";public double TextHeight{get;set;}=100;public double ArrowSize{get;set;}=80;
 public bool Supported{get;set;}public string Reason{get;set;}="";
}
public sealed class LibraryAnalysis {
 public string Id{get;set;}="";public string SourceHash{get;set;}="";public string Units{get;set;}="";public string Name{get;set;}="";
 public List<LibraryBlock> Blocks{get;set;}=new();public List<LibraryLayer> Layers{get;set;}=new();
 public List<LibraryStyle> TextStyles{get;set;}=new();public List<LibraryStyle> DimensionStyles{get;set;}=new();
 public List<string> Warnings{get;set;}=new();
}
