# Xem thiết kế AI CAD MCP

Mở `prototype/index.html` bằng Chrome hoặc Edge. Không cần cài thư viện, build hoặc kết nối Internet. Mở cả thư mục cùng nhau để CSS và JavaScript được nạp đúng.

Trong phiên thiết kế hiện tại có thể xem tại http://127.0.0.1:8766/ nếu máy chủ còn chạy. Cách mở file HTML trực tiếp vẫn dùng được khi máy chủ đã dừng.

## Thử tương tác

1. **Triển khai → T-001 → Xem & duyệt:** kiểm tra DWG/phạm vi/plan; thử duyệt hoặc hủy.
2. Duyệt T-001: T-003 chuyển từ chờ phụ thuộc sang sẵn sàng; xem lịch sử mẫu.
3. **T-002:** nhập cao độ 3.600 mm để thử chuyển từ thiếu dữ liệu sang preview. Thử để trống để xem lỗi gắn trường nhập.
4. **Cài đặt:** bật revision mới hoặc tắt kết nối, rồi trở về Triển khai; Apply bị chặn trong hộp thoại. Có nút tạo lại preview khi stale.
5. **Thư viện Skill:** tìm “cửa”, bật/tắt Skill. Skill lát gạch báo thiếu tool khi thử bật.
6. **Phiên & bản vẽ:** chọn DWG khác; kế hoạch trước phải lập lại.
7. **Đổi giao diện:** chuyển sáng/tối. Đặt lại mô phỏng hoặc reload để về trạng thái ban đầu.

Nhập gói Skill/Template chỉ mở hộp chọn tệp và mô tả bước kiểm tra tiếp theo. Nguồn PDF/ảnh chỉ mô phỏng tiếp nhận và bố cục hiệu chuẩn. Không có OCR, backend, bản vẽ hoặc thao tác lưu/ghi thật.

## Tài liệu

- `DESIGN.md`: luồng nghiệp vụ, bản đồ màn hình, design tokens, yêu cầu desktop/palette và tiêu chí nghiệm thu.
- `prototype/preview-light.jpg`, `prototype/preview-dark.jpg`: ảnh chụp giao diện trong Chrome.
- `QA.md`: phạm vi đã kiểm tra và phần chưa xác minh.
