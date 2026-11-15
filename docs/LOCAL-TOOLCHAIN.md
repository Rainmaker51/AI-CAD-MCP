# Bộ công cụ độc lập của AI CAD

Dự án dùng bộ công cụ trong chính thư mục AI-CAD-MCP:

- `.tools/dotnet`: SDK .NET 10.0.100, runtime .NET/ASP.NET/Windows Desktop 10.0.0.
- `.tools/nuget/packages`: cache gói phục vụ restore/build; NuGet.Config và Build.ps1 đã trỏ tới đây.
- `Mo AI CAD.cmd` và `Tai khoan AI.cmd`: ưu tiên `.tools/dotnet/dotnet.exe` trong dự án.

Không còn phụ thuộc vào bộ công cụ ở dự án trước. Khi sao chép dự án để phát triển trên máy khác, giữ cả thư mục ẩn `.tools` nếu muốn dùng bộ công cụ đi kèm. Kho Quang Anh vẫn được cấu hình riêng bằng warehouse.local.json.

Phần mềm cài trên máy như AutoCAD, Python/OCR và Codex vẫn dùng cấu hình tương ứng trong install.json. Đây là các phụ thuộc của máy, không nằm trong bộ công cụ dự án.

Di chuyển ngày 12/11/2026: đã đối chiếu SHA-256 của 8.381 tệp công cụ/gói; restore/build bằng đường dẫn mới; chạy 64 kiểm tra; khởi động Host và xác nhận nạp runtime từ `.tools/dotnet` khi đường dẫn dự án trước không còn tồn tại.

Thư mục dự án trước đã chuyển vào Thùng rác Windows. Bằng chứng kiểm tra: tests/results/old-project-migration-complete.json.
