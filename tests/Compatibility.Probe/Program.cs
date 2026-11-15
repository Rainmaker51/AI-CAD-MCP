using System;
using System.IO;
using AICad.Contracts;
using AICad.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
class Program{static int Main(string[] args){try{if(args[0]=="--corpus"){
 var random=new Random(7341);var plan=new DrawingPlan{PlanId="00000000000000000000000000000001",CreatedUtc=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc)};
 var op=new Operation();plan.Operations.Add(op);
 foreach(double value in new[]{2788.3372641367532,5244.9304935034306,0.15,1e-5,-0d,1e15,1e-12,Math.PI})op.Points.Add(new Point2(value,-value));
 for(int i=0;i<10000;i++){double value=(random.NextDouble()-.5)*Math.Pow(10,random.Next(-6,10));op.Points.Add(new Point2(value,-value));}
 Json.Atomic(args[1],new PlanRecord{Plan=plan,Hash=Json.Hash(plan)});Console.WriteLine("Corpus created");return 0;
 }var r=Json.Read<PlanRecord>(File.ReadAllText(args[0]));if(args.Length>1){JToken Sort(JToken t)=>t is JObject o?new JObject(o.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new JProperty(p.Name,Sort(p.Value)))):t is JArray a?new JArray(a.Select(Sort)):t.DeepClone();File.WriteAllText(args[1],Json.Canonical(r.Plan));}Console.WriteLine("Computed hash: "+Json.Hash(r.Plan));Console.WriteLine("Plan hash matches runtime: "+(r.Hash==Json.Hash(r.Plan)));Console.WriteLine("Template snapshot matches: "+(r.Plan.TemplateHash==Json.Hash(r.Plan.Template)));Validation.Plan(r.Plan);Console.WriteLine("Plan validation .NET48: PASS");return 0;}catch(Exception e){Console.WriteLine(e.ToString());return 1;}}}
