# CAD Manager — quản lý tab Ribbon

Từ Manager **0.8.0**, gói được cài trong `Tools/dotnet/_Installed/<TênAddin>/<version>-<fingerprint>`, một gói cho mỗi add-in. Manager giữ lịch sử trong `_Development`, tự kiểm tra gói mới rồi thay bản cài và dọn bản cũ. Hai máy phải cùng cập nhật Manager/Host/MCP trước khi dùng catalog mới. Xem [quy trình add-in và đồng bộ hai máy](ADDIN-DEVELOPMENT-WORKFLOW.md#7-chuyển-máy-và-khôi-phục).

## Cài bản nâng cấp

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Install-CadRibbonManager.ps1 -Build
```

Lưu bản vẽ và mở lại AutoCAD 2021 một lần. Trình cài cập nhật CAD Manager và gỡ đăng ký tự nạp riêng của `Rainmaker.Library.dll`; giữ lại gói DLL và dữ liệu thư viện. DLL đã nạp trong phiên AutoCAD cũ cần khởi động lại để giải phóng.

## Tạo và sử dụng tab

1. Gõ `AICADRIBBON` để mở trang Ribbon của Manager.
2. Chọn **Tab mới**, nhập tên và thứ tự, rồi **Lưu tab**. Có thể đổi tên, ẩn/hiện hoặc xóa tab tại đây.
3. Sang **.NET Add-in**, chọn add-in, chọn **Tab mặc định của add-in**, rồi **Lưu hồ sơ**.
4. Nạp add-in rồi chọn **Ghim toàn bộ lệnh vào tab add-in**. Muốn tùy chỉnh từng nút, chọn lệnh trong mục Ribbon, đặt nhãn/nhóm/thứ tự/tab rồi lưu ghim.

Xóa tab chuyển add-in dùng tab đó về tab mặc định; các nút gán tab riêng quay về tab của add-in. Thao tác này không xóa DLL. Tab mặc định không thể xóa hoặc ẩn. Manager chỉ quản lý các tab do chính nó tạo; không xóa tab Home hoặc tab riêng do add-in khác tự tạo.

## Tự cài Rainmaker Library qua Manager

1. Giải nén `artifacts/Rainmaker.Library.AutoCAD2021.zip` vào một thư mục riêng. Giữ các DLL đi kèm cùng thư mục.
2. Gõ `AICADTOOLS`, vào **.NET Add-in → Thêm file**, chọn `Rainmaker.Library.dll`.
3. Chọn **Nạp**, chọn tab muốn dùng và **Lưu hồ sơ**.
4. Với Library 1.2.0, chọn lệnh `QALIBRARY`, ghim một nút **Thư viện QA** rồi lưu. Nút mở bảng riêng gồm chọn Block/Layer/Hatch/Text/Dim, danh sách nhóm và toàn bộ thao tác quản lý. Các lệnh còn lại được gọi từ bảng.

Library không tự tạo tab riêng nữa. Các nút trong bảng Library cũng chạy qua Manager. Tắt/xóa add-in trong Manager dừng bảng Library và grip quản lý; dữ liệu trong DWG vẫn được giữ. Khi cập nhật DLL hoặc dependency, mở lại AutoCAD nếu Manager yêu cầu.

## Kiểm tra

`tests/ToolManager.Tests` kiểm tra dữ liệu tab, phân tab, đổi tên, xóa và tương thích danh mục cũ. `AICADRIBBONMANAGERTEST` dùng thư mục cách ly có marker `.aicad-tools-test` để kiểm tra manifest Library, bộ điều phối lệnh, tab Ribbon và vòng đời add-in trong AutoCAD.

## Nhóm công cụ và nhóm Ribbon

**Nhóm công cụ** trong hồ sơ phân loại add-in ở danh sách Manager. Trong mục **Ribbon · Tab và nút lệnh**, bật **Dùng nhóm công cụ trên Ribbon** để nút đi theo nhóm đó, kể cả khi đổi tên nhóm sau này. Bỏ chọn nếu muốn nhóm riêng cho một lệnh, nhập **Nhóm trên Ribbon** rồi **Lưu nút Ribbon**. **Lưu hồ sơ** lưu tên/nhóm công cụ; **Lưu nút Ribbon** lưu cấu hình riêng của nút đang chọn.

Mục **Ribbon · Tab và nút lệnh** có thể mở ngay khi chọn add-in. Danh sách lệnh lấy từ danh mục đã lưu; không cần nạp DLL lại mỗi lần mở AutoCAD để chỉnh ghim. Chọn lệnh tại **Lệnh cần cấu hình** trong mục này. Khi chạy một lệnh chưa nạp, Manager đọc lại DLL, kiểm tra lệnh và chọn ngữ cảnh AutoCAD phù hợp trước khi thực thi. DLL mới chưa có danh sách lệnh vẫn cần **Nạp / nạp lại** một lần.
