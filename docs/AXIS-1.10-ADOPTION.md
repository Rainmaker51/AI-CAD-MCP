# Tạo trục 1.10 — nhận Line/Polyline và khoảng chân dim

## Nhận lưới cũ

Trong bảng Tạo trục, chọn **Nhận lưới cũ** rồi quét chọn Line, Polyline hoặc Polyline 2D thẳng. Không yêu cầu chúng đã nằm trên layer trục. Add-in tách các đoạn, ghép đoạn thẳng hàng có đầu nối/chồng nhau, bỏ tọa độ trùng và lấy nhịp từ vị trí các trục.

Phạm vi hiện tại: lưới chữ nhật ngang/đứng theo WCS XY, ít nhất 2 trục mỗi phương, tối đa 50 trục mỗi phương và 10.000 đoạn. Dung sai nối 0,01 mm để xử lý sai số số học; không gộp hai trục thật cách nhau 50 mm, không kéo dài qua khe hở. Lưới xoay/chéo, polyline cong/fit/spline, có cao độ hoặc giao không đủ bị từ chối kèm hướng dẫn. Chọn rõ các nét trục: không thể phân biệt một đường tường thẳng với một đường trục chỉ từ hình học.

Có thể chọn thêm block ATT đầu trục và dim cũ. Chỉ nhận block có tag đã chọn và tọa độ gắn với lưới; dim Aligned phải đúng layer dim đang chọn và gắn với các tọa độ trục. Các đối tượng khác bị bỏ qua và không thuộc danh sách được thay. Polyline được nhận nguyên đối tượng; không nhận một phần rồi xóa cả polyline.

Hộp xác nhận nêu số trục đứng/ngang, số đối tượng nhận và số bị bỏ qua. Nhận/hủy chọn/chọn lỗi đều quay lại bảng. Nhận chỉ tạo bản nháp và preview; không viết DWG. Có thể sửa ký hiệu/tỷ lệ, chọn tài nguyên, thêm đầu trục/dim và bật cột/tường khi cần. Chỉ **Ghi cập nhật lưới** mới thay các đối tượng đã nhận bằng lưới quản lý. Hủy bảng giữ nguyên nét nguồn. Không nhận chồng đối tượng đã thuộc lưới quản lý.

## Cắt chân dim

Ô **Trục → chân dim · mm** mặc định **500 mm**. Đây là khoảng cách từ trục đến điểm bắt đầu đường gióng, cùng giá trị cho dim nhịp và dim tổng; không phải chiều dài đường gióng tính từ đường dim trở về trục. Khoảng nhập phải dương và nhỏ hơn khoảng trục → dim nhịp.

Ghi override `Dimexo`, tắt chiều dài đường gióng cố định trên đối tượng dim; không sửa Dim style trong kho. Chia theo tỷ lệ chú thích để khoảng thực trong Model vẫn là 500 mm ở 1:60, 1:100… Preview dùng cùng ý nghĩa khoảng cách. Khi không bật Cắt chân dim, tiếp tục dùng thông số style đã chọn.

Giữ tên trường dữ liệu cũ để đọc lưới đã lưu: giá trị 300 cũ vẫn hiện 300, nhưng khi ghi cập nhật bằng bản này nó có ý nghĩa khoảng **trục → chân dim** đã sửa. Lưới đã ghi trước đó không tự đổi hình; kiểm tra preview rồi ghi để cập nhật. Fingerprint phiên bản 3 theo dõi cả Dimexo, vẫn đọc được baseline phiên bản 1/2.

## Kiểm thử và triển khai

Kiểm thử thuật toán nhận các đoạn nối/chồng/ngược chiều, không gộp trục 50 mm, từ chối đoạn chéo/hở/tọa độ sai. Native test dùng Manager 0.10, Reader V1 cài riêng, DWG fixture; đo trực tiếp nét đường gióng của hai lớp ở tỷ lệ 60/100, nhận Line+Polyline, giữ đối tượng bỏ qua và lưu/mở lại. Ảnh WPF kiểm tra bảng nạp từ kết quả nhận.

Chưa nghiệm thu đầy đủ thao tác quét/hủy bằng chuột trong AutoCAD GUI. Nạp bản mới qua **Cập nhật file** của đúng mục Tạo trục trong Manager; giữ ID, thiết lập và Ribbon, không nhập mục mới. Không thay Manager/Reader hoặc tự kích hoạt gói khi yêu cầu chỉ là phát triển.
