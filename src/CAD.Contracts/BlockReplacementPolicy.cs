using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace AICad.Contracts;

public static class BlockReplacementPolicy
{
    public static void Demand(string appRoot, DrawingPlan plan)
    {
        string file = Path.Combine(WarehousePaths.Resolve(appRoot), "Catalog", "block-replacements.json");
        if (!File.Exists(file)) return;
        foreach (var entry in Json.Read<JArray>(File.ReadAllText(file)).OfType<JObject>())
        {
            if (entry.Value<string>("templateId") != plan.Template.Id ||
                !Version.TryParse(plan.Template.Version, out var version) ||
                !Version.TryParse(entry.Value<string>("throughVersion"), out var through) || version > through) continue;
            var ids = new System.Collections.Generic.HashSet<string>(entry["oldIds"]!.Values<string>().Select(id => id!));
            // Include nested block geometry; a container can still refer to the retired asset.
            var pending = plan.Operations.Where(o => o.Kind == "insert_block").Select(o => o.AssetId).ToList();
            var visited = new System.Collections.Generic.HashSet<string>();
            while (pending.Count > 0)
            {
                string id = pending[pending.Count - 1]; pending.RemoveAt(pending.Count - 1);
                if (!visited.Add(id)) continue;
                if (ids.Contains(id)) throw new CadException("RESOURCE_REPLACED", "Block trong kế hoạch đã được thay thế. Lập kế hoạch mới bằng Skill và Template hiện hành.");
                pending.AddRange(plan.Template.Blocks.Where(b => b.Id == id).SelectMany(b => b.Geometry).Where(o => o.Kind == "insert_block").Select(o => o.AssetId));
            }
        }
    }
}
