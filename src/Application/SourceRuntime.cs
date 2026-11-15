using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AICad.Contracts;

namespace AICad.Application;

public static class SourceRuntime
{
    public static (string Python, string? Renderer) Resolve(InstallConfig config)
    {
        var onPath = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().Trim('"'))
            .Where(Path.IsPathFullyQualified)
            .Select(p => Path.Combine(p, "pdftoppm.exe"))
            .Where(File.Exists)
            .ToArray();
        var candidates = new List<string> {
            config.PythonExecutable,
            Path.Combine(config.ContentRoot, "storage", "03-python-ocr", "python", "python.exe")
        };
        foreach (var renderer in onPath)
        {
            var bin = new DirectoryInfo(Path.GetDirectoryName(renderer)!);
            if (bin.Parent?.Name == "Library" && bin.Parent.Parent?.Name == "poppler" &&
                bin.Parent.Parent.Parent?.Name == "native" && bin.Parent.Parent.Parent.Parent is DirectoryInfo dependencies)
                candidates.Add(Path.Combine(dependencies.FullName, "python", "python.exe"));
        }
        string python = candidates.FirstOrDefault(File.Exists) ??
            throw new CadException("PYTHON_MISSING", "Không tìm thấy Python 3.12 cho phân tích ảnh/PDF. Kiểm tra đường dẫn Python của app hoặc bộ Python/OCR trong storage.");
        string bundledRenderer = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(python)!, "..", "native", "poppler", "Library", "bin", "pdftoppm.exe"));
        return (python, File.Exists(bundledRenderer) ? bundledRenderer : onPath.FirstOrDefault());
    }
}
