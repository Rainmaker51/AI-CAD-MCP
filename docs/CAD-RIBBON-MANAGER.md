# CAD Manager — quản lý tab Ribbon

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
4. Chọn **Ghim toàn bộ lệnh vào tab add-in** để hiện 18 nút quản lý thư viện.

Library không tự tạo tab riêng nữa. Các nút trong bảng Library cũng chạy qua Manager. Tắt/xóa add-in trong Manager dừng bảng Library và grip quản lý; dữ liệu trong DWG vẫn được giữ. Khi cập nhật DLL hoặc dependency, mở lại AutoCAD nếu Manager yêu cầu.

## Kiểm tra

`tests/ToolManager.Tests` kiểm tra dữ liệu tab, phân tab, đổi tên, xóa và tương thích danh mục cũ. `AICADRIBBONMANAGERTEST` dùng thư mục cách ly có marker `.aicad-tools-test` để kiểm tra manifest Library, bộ điều phối lệnh, tab Ribbon và vòng đời add-in trong AutoCAD.
