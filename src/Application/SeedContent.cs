using System;
using System.IO;
using AICad.Contracts;
using AICad.Geometry;
namespace AICad.Application;
public static class SeedContent {
 public static void Ensure(InstallConfig config){var store=new ContentStore(config.ContentRoot);string folder=Path.Combine(config.ContentRoot,"content");if(!Directory.Exists(folder))return;foreach(string path in Directory.EnumerateFiles(Path.Combine(folder,"templates"),"*.json")){var t=Json.Read<TemplateProfile>(File.ReadAllText(path));Validation.Template(t);if(!store.Versions("templates",t.Id).Contains(t.Version))store.Publish("templates",t.Id,t.Version,t);}var workspace=new WorkspaceService(config,new CadClient(config));foreach(string path in Directory.EnumerateFiles(Path.Combine(folder,"skills"),"*.json")){var s=Json.Read<SkillDefinition>(File.ReadAllText(path));workspace.ValidateSkill(s);if(!store.Versions("skills",s.Id).Contains(s.Version))store.Publish("skills",s.Id,s.Version,s);}}
}
