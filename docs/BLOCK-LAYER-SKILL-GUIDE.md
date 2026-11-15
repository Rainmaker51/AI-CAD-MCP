# Block động, bộ layer và Skill — bản 0.6

## Mở bản mới

Chạy `Mo AI CAD.cmd` trong thư mục dự án để mở app và AutoCAD 2021. Plugin hiện tại: `storage/04-builds/artifacts/p0-p6-warehouse/AutoCAD/CAD.Bridge.AutoCAD.dll`. Không tải lại DLL vào phiên đã nạp bản cũ; lưu và mở lại CAD khi đổi plugin.

## Dữ liệu đã chuẩn bị

Kho: `G:/Du An MCP/KHO-AI-CAD` (có thể chọn lại thư mục đồng bộ ở Cài đặt).

- `ISO_QUANGANH` 1.1.2: 288 block gốc được hỗ trợ và 2 mục danh mục cửa chờ duyệt, 63 layer giữ tên gốc, ACI/RGB, kiểu nét, độ dày nét, chế độ in.
- 37 block động được quét tham số; 20 block động nằm trong nhóm được phép nhập. 17 block động còn lại và các block khác chưa hỗ trợ vẫn bị loại nếu chứa proxy/đối tượng không phù hợp.
- `QUANGANH_LAYERS` 1.0.0 là bộ layer chung riêng tại `Versions/standards/QUANGANH_LAYERS/1.0.0`. Không chứa DWG đang làm, tài khoản hoặc lịch sử máy.
- `QA_CUA_1_CANH` và `QA_CUA_SO_1_CANH` là cấu hình nháp, chưa xác nhận quy đổi rộng ô chờ. Không tự cho AI dùng nháp như tài nguyên đã duyệt.

## 1. Quản lý bộ layer chung

Thư viện Template → Bộ layer chung → chọn Template nguồn và khai báo vai trò tường, cửa, nội thất, vệ sinh, trục, kích thước, chữ → xuất bản phiên bản.

Bộ chuẩn tách khỏi danh mục block. Skill ghim mã/phiên bản và bản sao vai trò layer; khi thay đổi bộ chuẩn, tạo phiên bản Template/Skill tương ứng. Việc đổi bộ chuẩn không âm thầm sửa kế hoạch cũ.

Khi chèn, layer bên trong block giữ tên, màu, kiểu nét, độ dày nét gốc. Layer đặt reference lấy từ danh mục. Nếu DWG đích có layer cùng tên nhưng khác chuẩn, app báo `LAYER_CONFLICT`, không tự đổi màu hoặc đổi tên layer để tiếp tục. Kiểm tra và thống nhất bộ chuẩn trong CAD trước khi thử lại. Tên các định nghĩa block phụ thuộc có thể được đổi để tránh ghi đè; hình học, thuộc tính và đồ thị động được giữ.

## 2. Tạo loại block / visibility

Thư viện Template → thẻ ISO_QUANGANH → Danh mục block động.

1. Chọn block gốc hoặc mục nháp. Chọn `QA_CUA_1_CANH` để xem cấu hình đã chuẩn bị.
2. Đặt mã loại dễ nhớ. Giữ mã để cập nhật; đổi mã để thêm một loại/visibility khác trong cùng DWG.
3. Chọn nhóm và layer đặt reference.
4. Chọn visibility và các tham số mặc định từ tên thật AutoCAD đã đọc. Không cần tách file hay tách block chỉ vì có nhiều visibility.
5. Với cửa: chọn tham số rộng, tham số bề dày tường. Giá trị nhập từ yêu cầu là **rộng ô chờ xây**. Công thức: `tham số = rộng ô chờ × hệ số + độ lệch`.
6. Đo thử block để xác nhận ý nghĩa tham số. Ví dụ `KT CUA=1100` có bbox rộng 1200 không đủ để kết luận ô chờ là 1100 hay 1200. Chỉ đánh dấu xác nhận quy đổi khi đã kiểm tra.
7. Xác nhận đúng loại, layer, visibility và gốc chèn rồi xuất bản phiên bản.

Bản hiện tại hỗ trợ một tham số rộng và một tham số bề dày cho mỗi loại. Block cần nhiều tham số chiều rộng liên hệ khác nhau phải tạo cấu hình phù hợp hoặc bổ sung bộ quy đổi; không tự suy đoán các tham số đó. Không có thao tác scale/mirror toàn block. Flip dùng tham số động nếu block có cung cấp.

AutoCAD kiểm tra lại giá trị sau gán, bao gồm trạng thái visibility, kiểu giá trị và danh sách cho phép. API không cung cấp đầy đủ min/max/step cho mọi tham số; giá trị bị AutoCAD từ chối hoặc không đọc lại đúng sẽ làm hủy thao tác. Phạm vi hình học động được kiểm tra bằng extents thực trong staging và trước commit.

## 3. Tự tạo Skill, không cần viết JSON

Thư viện Skill → Tạo Skill có hướng dẫn:

1. Chọn thư viện đã chuẩn hóa và bộ layer chung.
2. Nhập mã, tên, phiên bản; chọn dựng ảnh/PDF đã hiệu chuẩn hoặc chat CAD.
3. Chọn các loại block đã duyệt cho phép AI dùng. Mục cửa chưa xác nhận quy đổi sẽ không xuất hiện như loại sẵn sàng.
4. Quy ước cố định: tường bao 220, tường ngăn 110 mm; cửa theo rộng ô chờ.
5. Nếu cần trục: mở Trục định vị, nhập từng dòng `tên, x đầu, y đầu, x cuối, y cuối`, chọn triển khai trục và xác nhận vị trí/tên.
6. Kiểm tra và xuất bản. Chọn Skill này khi tạo khung nhiệm vụ / chuẩn bị nguồn trong app.

Skill là cấu hình của các quy trình app đã triển khai, không tự tạo một bộ máy CAD mới. Biểu mẫu nâng cao JSON vẫn dùng được cho quy trình đã biết. Các Skill cũ không có Recipe vẫn giữ hành vi và quyền công cụ cũ; để dùng chuẩn mới, tạo Skill theo biểu mẫu và chọn đúng Skill cho khung/nguồn. Đợt cập nhật không tự dựng lại hoặc thay thế bản vẽ đã nghiệm thu.

## Tường và trục

`draw_wall`: hai đầu tim tường, `Height` là chiều dày 220 hoặc 110 mm. Engine tạo biên tường đúng chiều dày; AI phải tách đoạn ở vị trí lỗ cửa. Công cụ hiện chưa tự nối/miter/giao tường hay tự cắt cửa; cần kiểm tra giao góc và lỗ mở trong preview.

Trục chỉ được tạo từ tọa độ và tên đã xác nhận. Với nguồn ảnh, tọa độ nhập cùng hệ tọa độ nguồn sau hiệu chuẩn, trước khi chuyển tới điểm đặt; với chat là WCS. Ký hiệu trục hiện dùng vòng tròn và chữ chuẩn. Block trục của DWG gốc còn bị proxy nên chưa đưa vào sử dụng. Không suy diễn trục từ ảnh hiện tại.

## Kiểm tra trước khi dùng

Xem trước luôn ALWAYS. App không tự duyệt ghi. Hình học mới chỉ được ghi sau nút duyệt ở palette AutoCAD. Cập nhật này đã qua kiểm tra ứng dụng và AutoCAD Core Console với DWG thật; nghiệm thu mặt bằng hoàn chỉnh cần cấu hình block được duyệt, xác nhận trục nếu dùng, rồi dựng preview mới trong app.
