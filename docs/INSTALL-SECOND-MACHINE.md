# Cài trên máy thứ hai — AutoCAD 2021

Mã nguồn trên GitHub và dữ liệu trong kho là hai phần cần mang sang máy mới. Thực hiện sau khi bộ cập nhật đã được commit và push.

## 1. Chuẩn bị

- Windows 64-bit, Git, AutoCAD 2021 đã cài và chạy được tại `C:\Program Files\Autodesk\AutoCAD 2021`.
- Python **3.12 64-bit**, có lệnh `py -3.12`. Python 3.15 có thể cùng tồn tại nhưng không dùng cho môi trường OCR này.
- SDK .NET theo `global.json` (hiện là 10.0.100). Tùy chọn `-InstallSdk` tải installer chính thức Microsoft và cài vào `.tools/dotnet` riêng của repo nếu thiếu SDK phù hợp.
- Đăng nhập Codex trên máy mới hoặc đăng nhập qua mục Tài khoản AI sau khi mở app.

Không cần copy `.tools`, `artifacts` hoặc `install.json` từ máy cũ. Setup tạo lại các file đó bằng đường dẫn của máy mới. Các package NuGet và OCR được khôi phục theo file khóa trong repo.

## 2. Đồng bộ kho thư viện

Đồng bộ hoặc copy **toàn bộ `KHO-AI-CAD`**, giữ nguyên cấu trúc. Có thể đặt ở ổ khác trên máy mới.

| Thư mục/file trong kho | Nội dung cần giữ |
|---|---|
| `warehouse.json` | Dấu nhận dạng kho |
| `Versions`, `Templates`, `Skills` | Template, Skill và phiên bản đã xuất bản |
| `Catalog` | Cây nhóm, tên, công dụng, ghi chú, duyệt và thùng rác |
| `CAD/Originals` | File nguồn `ISO_QuangAnh_ThuVien.dwg` đã dọn và bố trí lại |
| `CAD/Assets` | Các bản DWG bất biến đang dùng và phục vụ lịch sử |
| `Hatches`, `Styles` | Danh mục Hatch/Text/Dim và ảnh hatch |
| `Tools` nếu đã có | File LISP/DLL, hồ sơ và nhóm công cụ |
| `Sources`, `Backups` nếu đã có | Nguồn PDF/ảnh dùng chung và bản sao lưu |

Chờ đồng bộ hoàn tất và để các file cần dùng có sẵn trên ổ đĩa. Chỉ mang DWG gốc sẽ thiếu danh mục và những ghi chú đã quản lí trong app. Các font SHX/TTF mà DWG sử dụng cũng cần có trên máy mới.

## 3. Lấy mã nguồn và cài

```powershell
git clone https://github.com/Rainmaker51/AI-CAD-MCP.git
cd AI-CAD-MCP
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Setup-Local.ps1 -WarehousePath "D:\KHO-AI-CAD" -InstallSdk
```

Thay `D:\KHO-AI-CAD` bằng vị trí thực tế trên máy mới. Script sẽ kiểm tra kho/AutoCAD, chuẩn bị OCR Python 3.12, tạo `warehouse.local.json`, build Host + AutoCAD + MCP ở release `p0-p6` và tạo marker phiên bản native cho launcher.

Muốn kiểm tra điều kiện trước khi build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Setup-Local.ps1 -WarehousePath "D:\KHO-AI-CAD" -CheckOnly
```

`-SkipOcr` chỉ dành cho cài tạm chưa dùng PDF/ảnh. `-DownloadReferences` tải thêm đúng hai repo tham khảo AutoLispExt/CadAddinManager; không bắt buộc để chạy CAD Tool Manager. `-Test` chạy bộ kiểm tra managed sau build.

## 4. Mở và kiểm tra

1. Nhấp đúp **`Mo AI CAD.cmd`**. Launcher mở đúng plugin mới trong AutoCAD và app trong trình duyệt.
2. Gõ **`AICADTOOLS`**: ba tab AutoLISP, .NET Add-in, MCP có dark mode.
3. Trong app, kiểm tra kho đã kết nối, danh mục Block/Layer/Hatch/Text/Dim và các nhóm/ghi chú có đủ.
4. Mở hồ sơ một mẫu và kiểm tra tên file nguồn/đường dẫn. Đường dẫn phải là ổ đĩa trên máy mới.
5. Với Codex/Claude, sao chép cấu hình từ tab MCP trên **máy mới** sang client rồi kết nối lại. Không dùng nguyên đường dẫn cấu hình của máy cũ.
6. Đăng nhập tài khoản AI riêng trên máy mới. Phiên đăng nhập, token Host, IPC, chat và tiến trình CAD của máy cũ không đi cùng Git/kho.

## Cập nhật một máy đã cài

Hoàn tất tác vụ, lưu và đóng phiên AutoCAD đang dùng DLL cần build, rồi:

```powershell
git pull --ff-only
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Setup-Local.ps1 -StopHost
```

Script dùng lại `warehouse.local.json` của máy đó. `-StopHost` chỉ dừng đúng Host thuộc checkout này; không đóng DWG. Không chạy setup nếu vẫn còn tác vụ app cần xử lí.

## Những gì không đưa lên GitHub

Không commit `.tools/`, `artifacts/`, `data*/`, `storage/` sinh ra tại máy, `outputs/`, `warehouse.local.json`, dữ liệu kho, file tài khoản/token hoặc toàn bộ clone trong `third_party/reference/`. Giữ mã nguồn, tài liệu, script cài/build, file khóa package, ví dụ cấu hình và giấy phép bên thứ ba.
