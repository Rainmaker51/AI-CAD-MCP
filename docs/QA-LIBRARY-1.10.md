# QA Library 1.10 — quét theo khung

Nguồn CAD tiếp tục là nơi thêm/sửa thông số gốc. Lưu DWG → QA quét → chọn thay đổi → xác nhận xuất kho và cập nhật trình bày.

## Phạm vi năm loại

| Khung | Mẫu được nhận |
|---|---|
| Block | BlockReference trong Model; đọc định nghĩa, ATT, dynamic và hình học của block đó |
| Layer | Line, Polyline, Arc hoặc Circle dùng làm mẫu; lấy định nghĩa layer của mẫu |
| Hatch | Hatch đặt trực tiếp trong khung; không lấy hatch lồng trong block hoặc trong layout khác |
| Text | DBText hoặc MText mẫu; lấy text style |
| Dim | Dimension mẫu; lấy dim style |

Các định nghĩa phụ thuộc vẫn theo block khi nhập vào bản vẽ đích, nhưng không tự thành mục mới của loại thư viện khác. Khung, thuộc tính tiêu đề, MText mô tả và Polyline ô trình bày có dấu QA không phải mẫu.

Khung được nhận bằng Xrecord `AICAD_LIB_FRAME`, có ID, Kind và đường dẫn nhóm; không dựa trên chữ tiêu đề, màu hoặc tọa độ cố định. Dùng hình chữ nhật đơn vị biến đổi bởi BlockTransform, không dùng biên ATT. Mẫu phải nằm trọn trong khung gốc đúng loại; nhóm con sâu nhất chứa mẫu nhận quyền quản lý. Một định nghĩa có mẫu ở nhiều nhóm khác nhau hoặc khung đồng cấp chồng nhau sẽ được chẩn đoán và không tự chọn nhóm.

Mẫu cắt mép khung không được tự nhập. Trong bảng đối chiếu, **Kiểm tra phạm vi quét** hiển thị handle/kind và lý do cần rà soát. Thiếu một trong năm khung gốc thì dừng và báo khôi phục/khởi tạo bố trí; không quay về quét toàn bản vẽ.

## Danh mục đã có

- Tài nguyên còn định nghĩa/đối tượng nhưng không có mẫu hợp lệ trong khung: **Ngoài phạm vi quét**, không chọn cập nhật/xóa mặc định. Có bộ lọc riêng trong QA.
- Di chuyển mẫu về đúng khung rồi quét lại giữ ID và mã đã cấp. Chuyển sang khung nhóm con khác lấy đường dẫn nhóm của khung.
- Thực sự mất định nghĩa/đối tượng nguồn: vẫn là **Đã mất trong CAD**, cần xác nhận loại hồ sơ riêng như 1.9.
- Không tự xóa kho, tạo lại block mẫu hoặc sửa nguồn khi mở bảng.

Không thay danh mục đang xuất khi chỉ cài bản DLL này. Dữ liệu cũ ngoài khung được giữ để người dùng rà soát. Add-in tiêu thụ tiếp tục đọc phiên bản đã xuất; không tự quét nguồn hoặc đổi ID.

## Bàn giao và kiểm thử

Gói độc lập gồm Rainmaker.Library, Library.Core, CAD.LibraryScanner và Newtonsoft.Json; không cần dịch vụ nền. Đổi dependency cần khởi động lại AutoCAD. Catalog của Manager quyết định bản đang dùng, không phải version lớn nhất trong `versions`.

Kiểm thử Core 2021 có fixture riêng: năm loại, loại hatch lồng trong block, mẫu ngoài khung/đưa lại, khung di chuyển, mẫu cắt biên, khung thiếu, giữ ID và chặn xác nhận ngoài phạm vi; đồng thời chạy lại kiểm thử xuất kho/hàng chờ/Manager. Xem báo cáo thực tế của build để biết trạng thái đạt, hash và giới hạn. Kiểm thử chuột trong AutoCAD GUI sau cài không được suy ra từ Core.
