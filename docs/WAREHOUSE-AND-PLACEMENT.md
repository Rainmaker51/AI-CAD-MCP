# Kho chung và điểm đặt kết quả — app/plugin 0.5

## Nơi lưu dữ liệu

Kho tài nguyên hiện ở `G:\Du An MCP\KHO-AI-CAD`, tách khỏi thư mục `AI-CAD-MCP`.

| Thư mục trong kho | Nội dung |
|---|---|
| `CAD/Originals` | DWG gốc có tên dễ nhận biết, gồm thư viện Quang Anh |
| `CAD/Assets` | DWG dùng cho chèn block, định danh bằng SHA256 |
| `Templates` | Định nghĩa Template tương thích dữ liệu cũ |
| `Skills` | Định nghĩa Skill tương thích dữ liệu cũ |
| `Versions` | Nội dung và mã kiểm tra của từng phiên bản Template/Skill |
| `Sources/Files` | PDF/ảnh gốc, dùng chung theo mã kiểm tra để tránh lưu lặp |
| `Sources/Catalog` | Tên nguồn, kích thước file, hiệu chuẩn đã xác nhận |
| `Approved-Samples` | Nơi đặt bản sao bản vẽ mẫu đã được kiểm tra và lưu; chưa tự thu thập DWG đang mở |
| `Catalog` | Danh mục tài nguyên và danh sách các gói cũ được lưu trữ |

File `warehouse.json` nhận diện cấu trúc kho. Hãy giữ nguyên cấu trúc khi sao chép toàn bộ kho. Kho không chứa tài khoản đăng nhập, khóa phiên CAD, kế hoạch chờ duyệt hay dữ liệu vận hành của máy.

Trong thư mục app, `warehouse.local.json` chỉ lưu đường dẫn kho của máy đó. File này không đưa vào Git hoặc kho chung. `storage/02-app-data/data-p0-p6` tiếp tục giữ phiên, lịch sử, DWG làm việc, phân tích nguồn và cache của máy. Các thư viện cũ đã chuyển khỏi gốc app; bản dự phòng được giữ trong thư mục `warehouse-before-move-*` bên trong dữ liệu app.

## Dùng kho trên SSD hoặc Google Drive

1. Copy toàn bộ `KHO-AI-CAD` đến nơi muốn dùng, hoặc đặt nó trong thư mục Google Drive được đồng bộ.
2. Trong app chọn **Cài đặt → Đường dẫn thư mục kho**.
3. Nhập đường dẫn thư mục trên máy, bấm **Kiểm tra kho**, rồi **Kết nối kho**.
4. **Quét lại danh mục** sau khi cập nhật tài nguyên.

Mỗi máy được dùng đường dẫn khác nhau đến cùng một kho. Dùng đường dẫn thư mục, không dán URL chia sẻ `https://drive.google.com/...`. File CAD cần tải sẵn trên máy; chưa có cơ chế đăng nhập Google Drive hay tải file trực tiếp từ link web trong app. Khi kho mất kết nối, app báo lỗi để chọn lại đường dẫn, không tự đổi sang thư viện khác.

Nhiều máy có thể đọc kho. Chỉ nên cập nhật cùng một tài nguyên từ một máy tại một thời điểm, chờ Drive đồng bộ xong trước khi dùng máy khác. Phiên bản đã xuất bản không ghi đè. Đổi sang kho khác hủy các kế hoạch chưa duyệt để tạo preview mới; bản vẽ đã ghi không bị xóa.

## Một thư viện lớn

Template hỗ trợ tối đa **1.000 block và 100 layer**. Bộ quét DWG đọc đến 2.000 block có tên để người dùng chọn phần tương thích; giới hạn dung lượng tệp DWG/DWT vẫn là 20 MB. Block chứa proxy, tài nguyên ngoài hoặc hình học 3D chưa phù hợp luồng 2D vẫn được báo riêng.

`ISO_QUANGANH` chứa **288 block và 63 layer** trong một thư viện. Skill sử dụng là `CAD_QUANGANH`. Ba gói `ISO_QUANGANH_01–03` và Skill cũ được giữ cho lịch sử, ẩn khỏi danh sách sử dụng; không xóa các phiên bản của chúng. `QUANGANH_FLOORPLAN` là bộ chọn block dành cho dựng mặt bằng ảnh, không phải một phần thư viện bị cắt do giới hạn số block.

## PDF/ảnh vào đúng DWG đang kết nối

1. Mở app bằng `Mo AI CAD.cmd`, chọn DWG cần làm.
2. Vào **Nguồn PDF / ảnh**, chọn **Bản vẽ nhận kết quả**. Chọn nguồn cục bộ hoặc **Lấy PDF / ảnh từ kho chung**.
3. Hiệu chuẩn nguồn, chọn thư viện và kiểm tra bản dự thảo.
4. Bấm **Chuẩn bị khung trong DWG đã chọn**. App tạo khung nhiệm vụ và dấu **AI_OUTPUT_POINT** trong chính DWG này; không chạy thêm AutoCAD hoặc tạo DWG khác.
5. Nếu cần đổi vị trí, dùng **MOVE** trong AutoCAD để di chuyển block AI_OUTPUT_POINT màu cyan. Không phải nhập lại tọa độ chèn trong app.
6. Bấm **Tạo kế hoạch để duyệt tại CAD**, rồi **Xem & duyệt tại AutoCAD**. Duyệt bằng palette AutoCAD và lưu DWG.

Điểm đặt là mốc góc phạm vi đầu ra; nguồn được bố trí bên trong phạm vi với lề. Khung nguồn và dấu đặt độc lập: MOVE dấu đặt không kéo theo khung. Di chuyển dấu sau khi đã tạo kế hoạch làm revision thay đổi; quét lại và lập kế hoạch mới để dùng mốc mới. Di chuyển dấu không tự di chuyển những đối tượng đã được ghi trước đó.

Khung mới tạo bằng `AICADTASK` cũng có dấu đặt. Với khung cũ chưa có, dùng `AICADOUTPUT`, chọn khung và MOVE dấu nếu cần. App không hỏi vị trí chèn mỗi lần triển khai. Preview cố định **ALWAYS**, giao diện mặc định tối; quyền ghi vẫn chỉ có sau khi duyệt tại AutoCAD.

## Kiểm tra đợt cập nhật

- 52 kiểm thử managed đạt, gồm giới hạn 1.000 block, tương thích hash cũ và tách nguồn chung khỏi dữ liệu máy.
- 20 kiểm thử native đạt, gồm MOVE điểm đặt, giữ nguyên khung nguồn/DWG, phát hiện revision cũ, Undo và chèn block gốc.
- Thử qua giao diện: đường dẫn kho, kiểm tra/kết nối, thư viện Quang Anh 288 block, lấy nguồn chung và lập kế hoạch vào DWG đã chọn.
- Thử thực tế giữ nguyên PID AutoCAD và DocumentId. Người dùng duyệt; 81 đối tượng CAD được ghi, receipt PRESENT và hai kích thước thực 16.000 × 5.000 mm.

Chi tiết kiểm tra được lưu trong `tests/results/warehouse-placement-acceptance.json`.
