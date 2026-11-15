using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICad.Application;
using AICad.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

internal sealed class SourceQueue(InstallConfig config, WorkspaceService workspace, ImagePlanService images,
    ILogger<SourceQueue> logger) : BackgroundService
{
    private readonly object sync = new();
    private string Folder => Path.Combine(config.DataRoot, "source-queue");
    private string PathFor(string id) => Path.Combine(Folder, Json.Id(id) + ".json");

    public Job[] List()
    {
        lock (sync)
        {
            Directory.CreateDirectory(Folder);
            return Directory.EnumerateFiles(Folder, "*.json")
                .Select(f => Json.Read<Job>(File.ReadAllText(f))).OrderBy(j => j.CreatedUtc).ToArray();
        }
    }

    public Job Add(JObject request)
    {
        string id = Json.Id(request.Value<string>("id") ?? "");
        var meta = Json.Read<JObject>(File.ReadAllText(Path.Combine(workspace.Intelligence.Folder, id + ".json")));
        string name = meta.Value<string>("name") ?? id;
        if (Path.GetExtension(name).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new CadException("PDF_PAGE_REQUIRED", "Tách trang PDF cần dựng, rồi hiệu chuẩn từng trang.");
        if (request.Value<double?>("widthMm") is not (>= 1000 and <= 100000) ||
            request.Value<double?>("heightMm") is not (>= 1000 and <= 100000) ||
            request["calibration"] is not JObject cal || cal.Value<bool?>("confirmed") != true ||
            cal["p1"] is not JObject a || cal["p2"] is not JObject b ||
            a["x"] == null || a["y"] == null || b["x"] == null || b["y"] == null ||
            double.Hypot(a.Value<double>("x") - b.Value<double>("x"), a.Value<double>("y") - b.Value<double>("y")) < 1 ||
            cal.Value<double?>("distanceMm") != request.Value<double?>("widthMm") ||
            request["imageRect"] is not JObject rect || rect.Value<double>("right") <= rect.Value<double>("left") ||
            rect.Value<double>("bottom") <= rect.Value<double>("top"))
            throw new CadException("NEEDS_CALIBRATION", "Nhập hai kích thước bao, vùng mặt bằng và xác nhận hiệu chuẩn trước.");
        var template = workspace.Template(request.Value<string>("templateId") ?? "");
        var skill = workspace.Store.Skill(request.Value<string>("skillId") ?? "");
        if (skill.TemplateId != template.Id || skill.Builder != "source" || !workspace.Store.Enabled(skill.Id))
            throw new CadException("SKILL_MISMATCH", "Chọn Skill nguồn đang bật và liên kết với Template.");
        lock (sync)
        {
            var jobs = List();
            if (jobs.Any(j => j.SourceId == id && j.Status is "QUEUED" or "RUNNING"))
                throw new CadException("SOURCE_ALREADY_QUEUED", "Nguồn này đã có trong hàng đợi.");
            var job = new Job { SourceId = id, Name = name, Input = request, TemplateVersion = template.Version, SkillVersion = skill.Version };
            Json.Atomic(PathFor(job.Id), job);
            return job;
        }
    }

    public void Cancel(string id)
    {
        lock (sync)
        {
            var job = Json.Read<Job>(File.ReadAllText(PathFor(id)));
            if (job.Status != "QUEUED")
                throw new CadException("INVALID_STATE", "Chỉ có thể bỏ mục đang chờ khỏi hàng đợi.");
            job.Status = "CANCELLED";
            Json.Atomic(PathFor(job.Id), job);
        }
    }

    public bool HasCandidate(Job job)
    {
        string path = Path.Combine(workspace.Intelligence.Folder, Json.Id(job.SourceId) + ".ai-candidate.json");
        if (!File.Exists(path))
            return false;
        var candidate = Json.Read<JObject>(File.ReadAllText(path));
        return candidate.Value<DateTime>("at") >= (job.AttemptUtc ?? job.CreatedUtc) &&
            candidate.Value<string>("templateId") == job.Input.Value<string>("templateId") &&
            candidate.Value<string>("skillId") == job.Input.Value<string>("skillId") &&
            candidate.Value<string>("templateVersion") == job.TemplateVersion &&
            candidate.Value<string>("skillVersion") == job.SkillVersion &&
            candidate.Value<double?>("width") == job.Input.Value<double?>("widthMm") &&
            candidate.Value<double?>("height") == job.Input.Value<double?>("heightMm") &&
            JToken.DeepEquals(candidate["imageRect"], job.Input["imageRect"]);
    }

    public void Retry(string id, bool requireCandidate = false)
    {
        lock (sync)
        {
            var job = Json.Read<Job>(File.ReadAllText(PathFor(id)));
            if (job.Status != "FAILED")
                throw new CadException("INVALID_STATE", "Chỉ kiểm tra lại mục đã báo lỗi.");
            if (List().Any(j => j.SourceId == job.SourceId &&
                (j.Status is "QUEUED" or "RUNNING" || j.CreatedUtc > job.CreatedUtc)))
                throw new CadException("SOURCE_ALREADY_QUEUED", "Chọn lượt gần nhất của nguồn để kiểm tra lại.");
            bool hasCandidate = HasCandidate(job);
            if (requireCandidate && !hasCandidate)
                throw new CadException("NO_SAVED_DRAFT", "Chưa có phản hồi AI để kiểm tra lại. Hãy xếp hàng dựng lại.");
            job.ValidationOnly = hasCandidate;
            job.QueuedUtc = DateTime.UtcNow;
            job.Status = "QUEUED";
            job.Message = "";
            Json.Atomic(PathFor(id), job);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lock (sync)
            foreach (var job in List().Where(j => j.Status == "RUNNING"))
            {
                job.Status = "FAILED";
                job.Message = "Dịch vụ đã đóng giữa lượt dựng. Có thể đưa nguồn vào hàng đợi lại.";
                Json.Atomic(PathFor(job.Id), job);
            }

        while (!stoppingToken.IsCancellationRequested)
        {
            Job? job;
            lock (sync)
            {
                job = List().Where(j => j.Status == "QUEUED").OrderBy(j => j.QueuedUtc ?? j.CreatedUtc).FirstOrDefault();
                if (job != null)
                {
                    job.Status = "RUNNING";
                    job.Message = "";
                    if (!job.ValidationOnly)
                        job.AttemptUtc = DateTime.UtcNow;
                    Json.Atomic(PathFor(job.Id), job);
                }
            }
            if (job == null)
            {
                await Task.Delay(800, stoppingToken);
                continue;
            }

            try
            {
                var input = job.Input;
                string template = input.Value<string>("templateId")!, skill = input.Value<string>("skillId")!;
                if (workspace.Template(template).Version != job.TemplateVersion || workspace.Store.Skill(skill).Version != job.SkillVersion)
                    throw new CadException("RESOURCE_CHANGED", "Template hoặc Skill đã đổi. Kiểm tra cấu hình và đưa nguồn vào hàng đợi lại.");
                double width = input.Value<double>("widthMm"), height = input.Value<double>("heightMm");
                var rect = (JObject)input["imageRect"]!;
                if (job.ValidationOnly)
                    await images.RetryValidation(job.SourceId, width, height, rect, stoppingToken);
                else
                {
                    await workspace.Intelligence.Analyze(job.SourceId, (JObject)input["calibration"]!, 0, stoppingToken);
                    await images.Build(job.SourceId, width, height, rect, true, stoppingToken, template, skill, job.TemplateVersion, job.SkillVersion);
                }
                job.Status = "DONE";
            }
            catch (CadException e) when (e.Code == "AI_BUSY" && !stoppingToken.IsCancellationRequested)
            {
                job.Status = "QUEUED";
            }
            catch (Exception e)
            {
                job.Status = "FAILED";
                job.Message = stoppingToken.IsCancellationRequested ? "Dịch vụ đã đóng giữa lượt dựng." :
                    e is CadException cad ? cad.Code + ": " + cad.Message : "Lượt dựng bị lỗi. Kiểm tra tệp nguồn rồi thử lại.";
                logger.LogWarning(e, "Source queue item {JobId} failed", job.Id);
            }
            lock (sync)
                Json.Atomic(PathFor(job.Id), job);
            if (job.Status == "QUEUED")
                await Task.Delay(1500, stoppingToken);
        }
    }

    internal sealed class Job
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SourceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "QUEUED";
        public string Message { get; set; } = "";
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? QueuedUtc { get; set; }
        public DateTime? AttemptUtc { get; set; }
        public JObject Input { get; set; } = new();
        public string TemplateVersion { get; set; } = "";
        public string SkillVersion { get; set; } = "";
        public bool ValidationOnly { get; set; }
    }
}
