# CAD Tool Manager 0.9.0

## Nội dung cập nhật

- Danh sách .NET add-in có cột **Tab Ribbon**. Hiển thị những tab thực sự nhận nút theo cùng quy tắc với bộ vẽ Ribbon: tab riêng của lệnh, hoặc tab mặc định của add-in. Có trạng thái tab ẩn, add-in tắt, chưa ghim và nhiều tab.
- **Chọn tab đích → Chuyển toàn bộ nút** chuyển mọi nút đã ghim, kể cả nút từng có tab riêng, sang tab mới trong một revision. Giữ nhãn, thứ tự, biểu tượng, nhóm và hồ sơ. Không còn nút của add-in ở tab cũ. Không tự xóa tab cũ hoặc nút của add-in khác. Tab đích đang ẩn phải được bật trước.
- Danh sách checkbox để ghim, đổi tên nút, tab và nhóm; hỗ trợ chọn nhiều dòng rồi ghim/bỏ ghim. Nhấn **Lưu các nút** để áp dụng các ô đã sửa. Nút **Bỏ ghim tất cả** không xóa DLL, danh sách lệnh hoặc dữ liệu của add-in.
- **Chỉ ghim lệnh mở bảng** giữ đúng một nút. QA chọn `QALIBRARY`, Axis chọn `RAINMAKERAXIS`; add-in khác cần chọn đúng lệnh mở bảng trong danh sách nếu chưa có lựa chọn rõ ràng. Không đoán rằng tất cả lệnh khai báo đều là nút chính.
- **Ghim tất cả lệnh** nằm trong phần nâng cao. Có **Hoàn tác Ribbon gần nhất** cho cả chuyển tab, sửa danh sách, bỏ ghim tất cả và ghim tất cả.
- Hồ sơ và file nguồn nằm trong phần thu gọn để ưu tiên quản lý Ribbon. Lưu hồ sơ không ngầm chuyển tab nữa; dùng nút chuyển riêng.

## Hoàn tác và dữ liệu

Chỉ giữ một bước hoàn tác gần nhất cho từng add-in tại `Tools/ribbon-history/<tool-id>.json`. Giữ riêng với dữ liệu thư viện QA, DWG và cấu hình sử dụng của add-in. Không thêm trường vào cấu trúc catalog hiện có.

Hoàn tác được bảo vệ bằng package hash và hash cấu hình hiện tại: không ghi đè khi một phiên khác đã sửa hoặc gói DLL đã được cập nhật. Nếu tab cũ đã bị xóa, cần chọn tab mới thủ công. Không tự tạo lại tab hoặc lệnh đã mất. Mọi cập nhật danh mục vẫn dùng khóa ghi và kiểm tra revision của Manager.

Các ô trong bảng là bản nháp cho tới khi Lưu các nút. Chuyển add-in hoặc Làm mới sẽ nạp cấu hình đã lưu; hãy lưu các ô đang sửa trước. Các nút hàng loạt là thao tác áp dụng trực tiếp và có hoàn tác.

## Cập nhật máy

Manager là bộ lõi AutoCAD, không nhập như add-in trong chính Manager.

1. Lưu bản vẽ và đóng AutoCAD.
2. Chạy `CAP_NHAT_MAY_NAY.ps1` trong build bằng Windows PowerShell. Không có `-Apply` để kiểm tra gói/đường dẫn; thêm `-Apply` để cập nhật đúng thư mục Manager đang được Registry chọn.
3. Script kiểm tra hash, sao lưu DLL, thay bộ Manager và giữ nguyên Registry loader, install.json, Host, catalog và các gói `_Installed`. Có khôi phục các DLL đã thay nếu thao tác thất bại.
4. Mở lại AutoCAD → `AICADTOOLS`. Cập nhật Manager cùng phiên bản trên cả hai máy trước khi dùng chung kho.

Script dừng nếu AutoCAD còn mở để tránh thay DLL đang nạp. Không tự đóng AutoCAD hoặc lưu thay bản vẽ của người dùng.

## Kiểm thử

Kiểm thử catalog: chuyển tab có override, một revision cho toàn bộ thao tác, giữ metadata, bỏ ghim tất cả/hoàn tác, một nút chính, từ chối tab ẩn/lệnh sai, từ chối hoàn tác cũ khi phiên khác thay đổi, lưu/mở lại.

Kiểm thử Core 2021: gọi các nút thật trên WPF editor với dữ liệu biệt lập, đối chiếu RibbonControl trước/sau, bảo vệ tab bên ngoài Manager, render giao diện. Kiểm tra add-in QA 1.6 nạp và quản lý danh mục dưới Manager mới. Không dùng DWG công trình thật làm fixture và không thay cấu hình Ribbon đang dùng trong các kiểm thử này.

Core và render không thay cho kiểm thử đầy đủ chuột trong AutoCAD GUI sau cài. Xem manifest/validation và trạng thái cài thực tế khi bàn giao.
