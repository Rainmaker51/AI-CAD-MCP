# Phát triển trên hai máy

## Đồng bộ mã nguồn

Commit mã nguồn `src/`, `tools/`, `tests/`, `content/`, `schemas/`, tài liệu và các package lock. Push trước khi chuyển máy; trên máy tiếp theo, commit hoặc stash công việc đang dở rồi pull trước khi sửa tiếp. Dùng nhánh riêng nếu phát triển đồng thời để xử lý merge rõ ràng.

Không đồng bộ qua Git: `.tools/`, `artifacts/`, `storage/` (trừ README), `data*/`, `packages/`, `skills/`, `template-library/`, `content-assets/`, `warehouse.local.json`, bin/obj. Đây là môi trường, dữ liệu hoặc đường dẫn riêng của máy; một số tệp chứa token cục bộ. `.gitignore` không tự bỏ theo dõi tệp đã commit trước đó.

## Thiết lập mỗi máy

1. Cài AutoCAD 2021 tại đường dẫn mà dự án hiện hỗ trợ: `C:\Program Files\Autodesk\AutoCAD 2021`.
2. Cài .NET SDK phù hợp `global.json` (10.0.100 hoặc bản vá tương thích), trên hệ thống hoặc tại `.tools/dotnet`.
3. Đóng AutoCAD và AI CAD Host của dự án trước khi build lại. Không ghi đè DLL đang được nạp.
4. Chạy từ thư mục dự án:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Setup-Local.ps1
```

5. Mở `Mo AI CAD.cmd`. Script thiết lập tạo Host và plugin đúng hai đường dẫn mà launcher yêu cầu, đồng thời ghi cấu hình tuyệt đối cho máy hiện tại. Chạy lại sau khi pull thay đổi cần build.

## Thư viện trên hai máy

### Cập nhật Library Manager và Ribbon

Manager 0.8.0 dùng catalog `StorageVersion=2`. Hai máy phải cùng nâng cấp Manager/Host/MCP; không để client cũ ghi vào kho mới. Một máy chuyển cấu trúc bằng `-MigrateInstalled`, máy kia chỉ cài phần mềm và chờ đồng bộ xong. Mỗi add-in có đúng một gói trong `_Installed`; lịch sử ở `_Development`. Kiểm tra SHA-256 không thay thế việc chờ Drive/OneDrive đồng bộ bản mới nhất. Xem [quy trình đồng bộ chi tiết](ADDIN-DEVELOPMENT-WORKFLOW.md#7-chuyển-máy-và-khôi-phục).

Sau khi pull bản nâng cấp, đóng AutoCAD và chạy từ thư mục repo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Install-CadRibbonManager.ps1 -Build
```

Script build lại DLL trên máy hiện tại, cập nhật Host/MCP và đăng ký CAD Manager. Gõ `AICADRIBBON` để quản lý tab. Library được tự cài qua `AICADTOOLS` bằng DLL trong `artifacts/p0-p6-ribbon-manager/LibraryPackage`; giữ các DLL đi kèm cùng thư mục. Gói ZIP được tạo lại khi cài, không cần đưa binary lên Git. Xem [hướng dẫn Ribbon](CAD-RIBBON-MANAGER.md).

Git không mang theo DWG nguồn, nhóm thư viện hoặc danh mục add-in đã cài. Đồng bộ kho riêng chứa `CAD/Originals/ISO_QuangAnh_ThuVien.dwg`, `Catalog`, `Tools` và các thư mục dữ liệu thư viện liên quan; kết nối đúng kho trên máy còn lại. Không chép `warehouse.local.json` hoặc token/session từ máy này sang máy kia.

Để dùng phân tích ảnh/OCR, cài Python 3.12 64-bit và chạy `powershell -ExecutionPolicy Bypass -File tools/Setup-Ocr.ps1` trước khi build. Script tạo `.tools/ocr-python` và cài các phiên bản trong `tools/requirements-ocr-lock.txt`; commit tệp lock, không commit môi trường Python. Python 3.15 có thể cài song song. PDF scan còn cần Poppler (`pdftoppm.exe`) trong PATH.

`content/` là định nghĩa mẫu được quản lý bằng Git. `packages/` là kho phiên bản do ứng dụng tạo; không dùng commit kho này để thay thế quản lý mã nguồn.

Với thư viện chỉnh sửa trong ứng dụng, dùng chức năng kho chung trong Cài đặt để chuyển/kết nối kho ngoài thư mục repo. Đồng bộ kho riêng giữa hai máy; mỗi máy chọn đường dẫn cục bộ của mình. Chỉ cập nhật một tài nguyên từ một máy tại một thời điểm và chờ đồng bộ hoàn tất. Không sao chép token, session hay cấu hình đường dẫn từ máy kia.

Lưu ý: bootstrap hiện chỉ thêm phiên bản chưa tồn tại. Khi phát hành thay đổi mẫu trong `content/`, tăng phiên bản và tên tệp phù hợp, không chỉ sửa nội dung của phiên bản đã xuất bản.

## Giới hạn kiểm thử hiện tại

Các kiểm tra hồi quy độc lập với kho riêng:

```powershell
dotnet run --project tests/Source.Regression/Source.Regression.csproj -c Release
node tests/library-tree.test.cjs
.tools/ocr-python/Scripts/python.exe tests/test_hough_compat.py
```

`outputs/` chứa bảng rà soát, báo cáo và bản sao lưu; không commit. Nhóm block/layer và trạng thái khôi phục/xóa Skill nằm trong kho chung, được đồng bộ theo kho riêng chứ không theo Git.

Bộ managed tests hiện phụ thuộc dữ liệu ngoài Git, gồm `template-library/STARTER.json`. Clone mới chưa thể chạy trọn bộ chỉ với source hiện có; cần bổ sung fixture độc lập trước khi coi đây là kiểm tra CI trên cả hai máy. Không sao chép cấu hình hoặc dữ liệu phiên thật để khắc phục test.
