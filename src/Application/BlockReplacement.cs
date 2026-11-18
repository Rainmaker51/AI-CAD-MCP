using System;
using System.Collections.Generic;
using System.Linq;
using AICad.Contracts;
using AICad.Geometry;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

public sealed class BlockReplacementChange
{
    public TemplateProfile Template { get; set; } = new();
    public List<SkillDefinition> Skills { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<object> References { get; set; } = new();
}

public static class BlockReplacement
{
    // Build on copies: preview never edits live resources or published versions.
    public static BlockReplacementChange Build(TemplateProfile source, IEnumerable<SkillDefinition> skills,
        string oldName, string targetId, Dictionary<string,string> parameterMap, double dx = 0, double dy = 0, double degrees = 0)
    {
        var result = new BlockReplacementChange { Template = Json.Read<TemplateProfile>(Json.Write(source)) };
        var template = result.Template;
        var oldBlocks = template.Blocks.Where(b => b.NativeSource?.BlockName == oldName).ToList();
        var oldIds = oldBlocks.Select(b => b.Id).ToHashSet();
        var target = template.Blocks.SingleOrDefault(b => b.Id == targetId);
        if (oldIds.Count == 0 || target?.NativeSource == null || oldIds.Contains(targetId))
            throw new CadException("REPLACEMENT_TARGET", "Chọn một block khác đã đồng bộ trong template.");
        if (target.Geometry.Any(o => o.Kind == "insert_block" && oldIds.Contains(o.AssetId)))
            result.Errors.Add("Block thay thế đang chứa block cũ; cần gỡ liên kết lồng nhau trước để tránh vòng lặp.");
        Validation.Finite(dx); Validation.Finite(dy); Validation.Finite(degrees);
        if (Math.Abs(dx) > 20000 || Math.Abs(dy) > 20000 || Math.Abs(degrees) > 360)
            throw new CadException("REPLACEMENT_OFFSET", "Độ dịch tối đa 20.000 mm; góc xoay từ -360 đến 360 độ.");
        try { DraftingRules.Asset(target, template); }
        catch (CadException e) { result.Errors.Add(e.Message); }
        foreach (var block in oldBlocks.Where(b => b.Usage != null))
        {
            if (target.Usage?.Reviewed != true) result.Errors.Add("Duyệt cấu hình block thay thế trong danh mục block trước.");
            if (target.Usage?.Role != block.Usage!.Role) result.Errors.Add("Vai trò block thay thế phải là " + block.Usage.Role + " (" + block.Id + ").");
            if (target.Usage?.Layer != block.Usage.Layer) result.Errors.Add("Layer block thay thế phải là " + block.Usage.Layer + " để giữ quy ước Skill.");
        }
        void ReplaceOperation(Operation op, string owner)
        {
            if (op.Kind != "insert_block" || !oldIds.Contains(op.AssetId)) return;
            result.References.Add(new { owner, kind = "insert_block", oldId = op.AssetId, newId = targetId, layer = target.Usage?.Layer ?? op.Layer });
            op.AssetId = targetId;
            if (target.Usage != null) op.Layer = target.Usage.Layer;
            if (op.DynamicJson != null)
            {
                var mapped = new JObject();
                foreach (var p in Json.Read<JObject>(op.DynamicJson).Properties())
                {
                    string name = parameterMap.TryGetValue(p.Name, out var rename) ? rename : p.Name;
                    if (string.IsNullOrWhiteSpace(name) || mapped[name] != null)
                        throw new CadException("REPLACEMENT_MAP", "Không được bỏ hoặc gộp tham số: " + p.Name);
                    mapped[name] = p.Value.DeepClone();
                }
                op.DynamicJson = Json.Write(mapped);
            }
            if (op.Points.Count == 1)
            {
                var p = op.Points[0];
                op.Points[0] = new(p.X + dx * Math.Cos(op.Rotation) - dy * Math.Sin(op.Rotation),
                    p.Y + dx * Math.Sin(op.Rotation) + dy * Math.Cos(op.Rotation));
            }
            op.Rotation += degrees * Math.PI / 180;
            try { Validation.Operation(op, template, true, false); }
            catch (CadException e) { result.Errors.Add(owner + ": " + e.Code + " — " + e.Message); }
        }
        foreach (var skillSource in skills.Where(s => s.TemplateId == template.Id))
        {
            var skill = Json.Read<SkillDefinition>(Json.Write(skillSource));
            bool recipe = skill.Recipe?.AllowedBlocks.Any(oldIds.Contains) == true;
            bool workflow = skill.Workflow.Any(o => o.Kind == "insert_block" && oldIds.Contains(o.AssetId));
            if (!recipe && !workflow) continue;
            if (recipe)
            {
                result.References.Add(new { owner = skill.Id, kind = "Recipe.AllowedBlocks", oldIds, newId = targetId });
                skill.Recipe!.AllowedBlocks = skill.Recipe.AllowedBlocks.Select(id => oldIds.Contains(id) ? targetId : id).Distinct().ToList();
                try { DraftingRules.Recipe(skill.Recipe, template); }
                catch (CadException e) { result.Errors.Add(skill.Id + ": " + e.Code + " — " + e.Message); }
            }
            foreach (var op in skill.Workflow) ReplaceOperation(op, skill.Id);
            result.Skills.Add(skill);
        }
        foreach (var block in template.Blocks.Where(b => !oldIds.Contains(b.Id)))
            foreach (var op in block.Geometry) ReplaceOperation(op, "Block " + block.Id);
        template.Blocks.RemoveAll(b => oldIds.Contains(b.Id));
        result.Errors = result.Errors.Distinct().ToList();
        return result;
    }
}
