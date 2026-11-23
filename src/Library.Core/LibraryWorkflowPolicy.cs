using AICad.Contracts;

namespace AICad.Application;

/// <summary>Legacy image/PDF reconstruction is retained as development material only.</summary>
public static class LibraryWorkflowPolicy
{
    public static bool IsArchivedBuilder(string builder) => builder == "source";
    public static void DemandActiveBuilder(string builder)
    {
        if (IsArchivedBuilder(builder)) throw new CadException("PDF_CAD_ARCHIVED", "PDF / ảnh sang CAD đã ngừng sử dụng. Dữ liệu cũ được lưu trong thư mục Dọn dẹp của kho.");
    }
}
