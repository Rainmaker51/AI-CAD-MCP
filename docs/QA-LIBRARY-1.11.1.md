# QA Library 1.11.1 — loại hatch ngoài khung khỏi kho sử dụng

Giữ các chức năng nhóm/khung và bộ lọc của [QA 1.11](QA-LIBRARY-1.11.md).

Trong bảng Quét thay đổi, các hatch ngoài khung vẫn không được tự nhập hoặc tự xóa khi chỉ mở bảng. Có thể tích chọn hồ sơ hatch đã xuất và nhấn Xác nhận cập nhật để **loại khỏi danh mục sử dụng**. Nút “Chọn hatch ngoài khung để loại khỏi kho” chỉ chọn những dòng hatch ngoài khung đang hiển thị; bộ lọc không làm mất các lựa chọn đang ẩn.

- Thao tác `exclude` khác xóa đối tượng CAD: giữ nguyên hatch và nội dung DWG; với kế hoạch chỉ loại hồ sơ, không thực hiện SaveAs hoặc chỉnh trình bày trên nguồn.
- Xóa ID khỏi state đang dùng và xuất phiên bản danh mục Hatch mới không chứa các hồ sơ đó. Các add-in đọc lại kho sẽ không thấy chúng trong danh sách lựa chọn.
- Giữ lịch sử phiên bản, bản sao phục hồi và sổ mã. Không tái cấp mã/ID đã loại. Không sửa bản vẽ công trình đã triển khai trước đó.
- Kiểm tra liên kết add-in/skill trước khi loại; nếu đang được dùng, yêu cầu xử lý liên kết trước. Không tự chuyển sang hatch khác.
- Ngoài khung của các loại khác vẫn được giữ, chưa áp dụng hành động loại này cho chúng. Đưa hatch đã loại trở lại khung và duyệt từ DWG là thao tác chủ động, không phải phục hồi từ cache.

Bản quét phải là DWG đã lưu còn đúng hash, hồ sơ vẫn tồn tại và `inScope=false`. Trường hợp không khớp bị từ chối. Chỉ tiếp tục hàng chờ khi nguồn còn đúng bản đã ghi.

Kiểm thử: bảng WPF tạo đúng kế hoạch exclude; nghiệp vụ loại hồ sơ khỏi state/catalog và không đổi DWG; chạy lại Core 2021/Manager và nhóm/khung. Công cụ bảo trì trong bộ kiểm thử có chế độ kiểm tra trước và chỉ xử lý đúng 10 ID ngoài khung trong lần quét được chỉ định, sử dụng cùng API xuất kho; không sửa JSON catalog thủ công.

Gói có manifest SHA-256 và source.patch. Cài qua Manager; khởi động lại AutoCAD để nạp DLL/dependency mới. Không cần dịch vụ nền.
