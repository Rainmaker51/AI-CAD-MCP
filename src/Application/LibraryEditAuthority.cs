using System;
using System.IO;
using System.Linq;
using AICad.Contracts;

namespace AICad.Application;

public static class LibraryEditAuthority
{
    public static bool Active(InstallConfig config) => File.Exists(Path.Combine(WarehousePaths.Resolve(config.ContentRoot), "Catalog", "library-authority.json"));
    // Read operations and skill authoring remain available. A header supplied by a web
    // caller cannot bypass this rule; QA has explicit, authenticated native endpoints.
    public static bool IsLibraryWrite(string path, string kind)
    {
        if (path.StartsWith("/api/qa-library/", StringComparison.Ordinal)) return false;
        if (path.StartsWith("/api/library-management/", StringComparison.Ordinal)) return kind != "skills";
        if (path == "/api/content/activate") return kind == "templates";
        return new[] { "/api/library-sync/", "/api/layer-management/", "/api/block-management/", "/api/style-library/", "/api/hatch-library/", "/api/templates/", "/api/library/cad/" }.Any(p => path.StartsWith(p, StringComparison.Ordinal))
            || path is "/api/drafting/catalog" or "/api/drafting/source-sync" or "/api/drafting/standards";
    }
}
