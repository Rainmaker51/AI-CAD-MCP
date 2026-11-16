# CAD Tool Manager — nguồn tham khảo

Hai repo đã được tải về `third_party/reference/` ngày 16/11/2026. Thư mục clone không đưa vào Git của dự án; chạy `tools/Get-ToolManagerReferences.ps1` trên máy khác để tải đúng commit.

| Repo | Commit | Giấy phép | Áp dụng |
|---|---|---|---|
| [Autodesk-AutoCAD/AutoLispExt](https://github.com/Autodesk-AutoCAD/AutoLispExt) | `74f59ee220a3689b6b13d26c7be8107c985b93dd` | Apache-2.0 | Tham khảo phân tích LISP, bỏ qua comment/chuỗi khi tìm `defun c:`, quản lí file và luồng soạn thảo/gỡ lỗi với AutoCAD 2021. |
| [chuongmep/CadAddinManager](https://github.com/chuongmep/CadAddinManager) | `cea835872e94c53f5f61f0d22436d6a476c1e64b` | MIT | Tham khảo shadow copy DLL, tra cứu `CommandMethod`, chọn và thực thi lệnh từ WPF. |

CAD Tool Manager trong `src/CAD.Bridge.AutoCAD/CadTool*.cs` và danh mục `src/CAD.Contracts/CadToolCatalog.cs` là phần tích hợp riêng của AI CAD. Không nhúng toàn bộ extension VS Code hoặc sao chép engine Mono.Cecil của CadAddinManager. Không cài các binary nằm trong repo tham khảo. Bản giấy phép và NOTICE tham khảo được giữ trong `third_party/licenses/`.

AutoLispExt là extension VS Code, không phải một palette quản lí LISP có sẵn để nhúng. Tính năng debug/breakpoint của extension vẫn thuộc VS Code; palette AI CAD hiện quản lí file, hồ sơ, nạp và chạy lệnh.
