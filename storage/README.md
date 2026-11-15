# Bốn nhóm dữ liệu dự án

Đường dẫn dưới đây tính từ thư mục `AI-CAD-MCP`.

| Thư mục thật | Nội dung | Dung lượng khi phân chia |
|---|---|---:|
| `storage/01-test-data` | Fixtures và kết quả kiểm thử; mã nguồn test vẫn ở `tests` | 1.317 MB |
| `storage/02-app-data` | Bản vẽ, ảnh nguồn, lịch sử, phiên và dữ liệu app | 923 MB |
| `storage/03-python-ocr` | Các thư viện Python/OCR | 322 MB |
| `storage/04-builds/artifacts` | Bộ build mà trình mở app hiện sử dụng | 28 MB |

`tests/fixtures`, `tests/results`, `data-p0-p6` và `.tools/python-packages` là junction trỏ về thư mục thật tương ứng. Chúng không chứa thêm một bản sao dữ liệu. Không tự xóa hoặc đổi đích các junction này. Riêng `artifacts` cũ được giữ làm bản dự phòng khoảng 28 MB vì Windows không cho di chuyển cây thư mục này; trình mở app đã được cập nhật để dùng `storage/04-builds/artifacts`.

Mở app bằng `Mo AI CAD.cmd` ở gốc dự án như trước. `Tai khoan AI.cmd` cũng dùng bộ build mới. `tools/Build.ps1` nhận biết bố cục này, xuất bản build mới vào nhóm số 4 và ghi DataRoot vật lý để plugin AutoCAD không phải khởi động qua junction.

## Kiểm tra bố cục

Chạy bằng .NET 10:

```powershell
dotnet tools/storage-organizer/AICad.Storage.dll --verify
```

Mã nguồn công cụ: `src/Desktop.Storage`. `--plan` không sửa dữ liệu. `--apply` yêu cầu đóng toàn bộ AutoCAD, chỉ dừng Host nằm trong cây build của dự án, giữ các bản build cũ và không ghi đè bản build mới đã được tổ chức. Không chạy lại `--apply` khi chỉ cần kiểm tra.

## Khi sao lưu hoặc chuyển máy

Đây là bố cục đã kiểm tra trên máy hiện tại, chưa phải bộ phát triển portable. Junction đang dùng đích tuyệt đối trên ổ G. Khi chuyển máy hoặc đổi ổ đĩa cần dựng lại junction và cấu hình đường dẫn; không mang nguyên junction rồi giả định đích đã tự đổi. Chỉ copy dữ liệu thật một lần, tránh công cụ sao lưu đi theo junction rồi chép lặp.

Giữ mã nguồn, solution, công cụ build, `content` và `schemas` để tiếp tục phát triển. Thư viện Quang Anh, các gói phiên bản và tài nguyên CAD hiện đã chuyển sang kho ngoài `G:\Du An MCP\KHO-AI-CAD`; xem `docs/WAREHOUSE-AND-PLACEMENT.md`. Bốn thư mục storage không thay thế các phần này. Môi trường .NET/Python/AutoCAD/Codex của máy đích vẫn cần cấu hình riêng. Không xóa bản gốc trước khi bản trên máy đích đã được kiểm tra.
