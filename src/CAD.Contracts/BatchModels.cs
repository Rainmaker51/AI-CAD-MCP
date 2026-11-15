using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace AICad.Contracts;
public sealed class BatchNode {public string TaskId{get;set;}="";public string Status{get;set;}="PENDING";public string PlanId{get;set;}="";public string Error{get;set;}="";public List<string> Dependencies{get;set;}=new();public string SkillVersion{get;set;}="";public string TemplateVersion{get;set;}="";public JObject Inputs{get;set;}=new();}
public sealed class BatchRecord {public string Id{get;set;}=Guid.NewGuid().ToString("N");public string SessionId{get;set;}="";public string DocumentId{get;set;}="";public DateTime UpdatedUtc{get;set;}=DateTime.UtcNow;public string Status{get;set;}="ACTIVE";public List<BatchNode> Nodes{get;set;}=new();}
