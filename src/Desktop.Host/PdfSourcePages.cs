using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICad.Application;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

internal sealed class PdfSourcePages(InstallConfig config, WorkspaceService workspace)
{
    public async Task<JObject> Extract(string id, int page, CancellationToken ct)
    {
        id = Json.Id(id);
        if (page is < 1 or > 100)
            throw new CadException("INVALID_PAGE", "Chọn trang PDF từ 1 đến 100.");
        string folder = workspace.Intelligence.Folder;
        var source = Json.Read<JObject>(File.ReadAllText(Path.Combine(folder, id + ".json")));
        string name = source.Value<string>("name") ?? "";
        if (!Path.GetExtension(name).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new CadException("UNSUPPORTED_SOURCE", "Chọn tài liệu PDF để tách trang.");
        string pdf = Path.Combine(folder, id + ".pdf");
        if (Json.FileHash(pdf) != source.Value<string>("hash"))
            throw new CadException("SOURCE_CHANGED", "PDF nguồn đã thay đổi.");
        string childId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id + ":" + page)))[..32].ToLowerInvariant();
        string metadata = Path.Combine(folder, childId + ".json");
        if (File.Exists(metadata))
            return Json.Read<JObject>(File.ReadAllText(metadata));

        string renderer = SourceRuntime.Resolve(config).Renderer ??
            throw new CadException("PDF_RENDERER_MISSING", "App cần Poppler để mở trang PDF.");
        string prefix = Path.Combine(folder, childId + "." + Guid.NewGuid().ToString("N"));
        string png = prefix + ".png", destination = Path.Combine(folder, childId + ".png");
        try
        {
            var start = new ProcessStartInfo(renderer) { UseShellExecute = false, CreateNoWindow = true };
            foreach (string arg in new[] { "-f", page.ToString(), "-l", page.ToString(), "-singlefile", "-scale-to", "2200", "-png", pdf, prefix })
                start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new CadException("PDF_RENDER_FAILED", "Không mở được trình đọc PDF.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                    process.Kill(true);
                ct.ThrowIfCancellationRequested();
                throw new CadException("PDF_RENDER_TIMEOUT", "Tách trang PDF quá thời gian.");
            }
            if (process.ExitCode != 0 || !File.Exists(png))
                throw new CadException("PDF_PAGE_FAILED", $"Không đọc được trang {page}. Kiểm tra số trang và quyền mở PDF.");
            try { File.Move(png, destination); }
            catch (IOException) when (File.Exists(destination)) { }
            var meta = JObject.FromObject(new { id = childId, name = $"{Path.GetFileNameWithoutExtension(name)} · trang {page}.png",
                hash = Json.FileHash(destination), size = new FileInfo(destination).Length, status = "NEEDS_CALIBRATION", pdfId = id, pdfPage = page });
            if (!File.Exists(metadata))
                Json.Atomic(metadata, meta);
            return Json.Read<JObject>(File.ReadAllText(metadata));
        }
        finally
        {
            if (File.Exists(png))
                File.Delete(png);
        }
    }
}
