using System;
using System.IO;
using AICad.Contracts;
using Newtonsoft.Json.Linq;
if(args.Length!=1)throw new Exception("Supply the Reader package DLL");
string run=Path.Combine(Directory.GetCurrentDirectory(),".tools/test-runs/components-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(run);Json.Atomic(Path.Combine(run,"test-run.json"),new{component="CAD.Manager",purpose="Detached Reader component tests"});File.WriteAllText(".tools/shared-components-test-run.txt",run);
void Check(bool value,string message){if(!value)throw new Exception(message);}
void Reject(Action work,string code){try{work();}catch(CadException e)when(e.Code==code){return;}throw new Exception("Expected "+code);}
var store=new CadSharedComponentStore(Path.Combine(run,"data"));Check(store.Read()==null&&!Directory.Exists(store.Root),"Read must not create an installation");
string source=Path.Combine(run,"source");Directory.CreateDirectory(source);string dll=Path.Combine(source,CadSharedComponentStore.ReaderName+".dll");File.Copy(args[0],dll);File.Copy(Path.ChangeExtension(args[0],".pdb"),Path.ChangeExtension(dll,".pdb"));string sourceHash=Json.FileHash(dll);
var selected=store.Install(dll,0);Check(selected.Revision==1&&Json.FileHash(store.Demand(selected))==sourceHash&&Json.FileHash(dll)==sourceHash,"Install copied incorrectly or changed source");Reject(()=>store.Install(dll,0),"STALE_EDIT");
File.AppendAllText(Path.ChangeExtension(dll,".pdb"),"new debug symbols");var next=store.Install(dll,1);Check(next.Revision==2&&store.Read()!.PackageHash==next.PackageHash&&File.Exists(store.Demand(selected)),"Update did not keep history or current pointer");
string installed=store.Demand(next);File.AppendAllText(installed,"tampered");Reject(()=>store.Demand(next),"COMPONENT_CHANGED");
var invalid=JObject.FromObject(next);invalid["Files"]![0]!["Name"]="../../outside.dll";Json.Atomic(Path.Combine(store.Root,"current.json"),invalid);Reject(()=>store.Read(),"COMPONENT_INVALID");Json.Atomic(Path.Combine(store.Root,"current.json"),next);
Check(!Directory.Exists(Path.Combine(run,"Tools")),"Local dependency installation touched shared catalog");Json.Atomic(Path.Combine(run,"results.json"),new{passed=true,independentInstall=true,noReaderRequiredToOpenManager=true,sourceUnchanged=true,revisionGuard=true,history=true,hashGuard=true,pathGuard=true});Console.WriteLine("PASS: detached Reader install/update, source integrity, stale revision, history, tamper and path guards");
