# Kiểm tra bản mẫu giao diện — 10/11/2026

## Đã kiểm tra

- JavaScript qua `node --check`, không lỗi cú pháp.
- Đã mở giao diện bằng Chrome trên máy người dùng và quan sát ảnh chụp.
- Điều hướng đến đủ 7 màn hình, nội dung heading tương ứng.
- Tìm “cửa” trong Skill chỉ còn thẻ cửa/trục.
- Duyệt T-001 cập nhật Hoàn tất, T-003 sang Sẵn sàng, không còn nút Apply của task đã hoàn tất.
- T-002 không có cao độ: lỗi xuất hiện và focus về input; giá trị 3.600 mm tạo preview mẫu.
- Hủy preview cập nhật Đã hủy, thông báo không thay đổi bản vẽ.
- Bật revision mới: nút duyệt/áp dụng disabled; thông báo tạo lại preview.
- Tắt kết nối: nút duyệt/áp dụng disabled; thông báo kết nối lại AutoCAD.
- Có đường tạo lại preview sau stale; thao tác đổi DWG làm các plan đã preview/hoàn tất cần lập lại.
- Đã xem bố cục sáng và tối, lưu ảnh riêng.
- Kiểm tra viewport 375×812, 768×1024, 1440×1000: không có overflow ngang theo DOM. Đã xem ảnh màn Triển khai ở 375 và 1440 px; reset viewport sau kiểm tra.
- Các cặp chữ chính/phụ, nút chính, success/warning trên nền tương ứng của hai theme được tính tương phản theo luminance, đạt ít nhất 4.5:1.

## Giới hạn

- Đây là xác minh UX bằng dữ liệu mẫu; không chứng minh backend hoặc AutoCAD production Apply hoạt động.
- Chưa kiểm thử screen reader hoàn chỉnh, mọi tổ hợp tab/Shift-Tab, forced colors thật, Windows DPI hoặc native palette.
- Chưa kiểm thử visual mọi màn hình ở mọi breakpoint; thử overflow và ảnh không thay thế nghiệm thu layout native.
- Các trạng thái giả lập nằm trong bộ nhớ, refresh là reset. Không có lưu dự án, session thật, schema validator, checksum thật, OCR hoặc restore.
- QA 4/4 và receipt trên giao diện là kết quả mô phỏng; không có đọc/ghi DWG thật.
- Các hành vi đổi đích/tạo lại plan là minh họa. Backend phải bind session/document/revision/plan/snapshot/approval thật và xác minh trước dispatch; không dùng state JavaScript làm quyền ghi.
