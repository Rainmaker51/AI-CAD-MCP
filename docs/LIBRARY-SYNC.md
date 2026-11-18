# Đồng bộ thư viện app và DWG

Mở **Thư viện → Đồng bộ app ↔ DWG**. Cả Block, Layer, Hatch, Text và Dim dùng file nguồn `ISO_QuangAnh_ThuVien.dwg` trong `CAD/Originals` của kho đang kết nối. Đồng bộ chạy theo thao tác người dùng.

## Quy trình

1. Lưu các thay đổi trong AutoCAD. Đóng file thư viện trước khi áp dụng cập nhật từ app để tránh khóa ghi; có thể giữ các bản vẽ khác mở.
2. Nhấn **Đọc và so sánh DWG**. Chuyển giữa năm tab để rà tài nguyên mới, thay đổi hoặc xung đột.
   Mỗi tab chia thành **Chưa đồng bộ** (mở sẵn) và **Đã đồng bộ** (thu gọn), có số lượng. Trước khi đọc DWG, hiển thị **Chưa đối chiếu**; sau khi áp dụng sẽ đọc lại để cập nhật hai nhóm.
3. Chọn **CAD → App**, **App → CAD**, hoặc **Giữ để rà sau** cho từng tài nguyên.
4. Nhấn **Áp dụng và cập nhật mẫu DWG**. Mở lại file thư viện sau khi hoàn tất.

Trong app, **Thêm** tạo bản nháp. Block được nhập từ một DWG và chọn đúng định nghĩa block; Layer, Hatch, Text, Dim nhập thông số trong biểu mẫu. Bản nháp chỉ được ghi vào DWG sau bước so sánh và áp dụng. Font và mẫu hatch tùy chỉnh phải sẵn có trên máy AutoCAD.

Trong CAD, tạo/sửa định nghĩa tương ứng rồi lưu: block definition, layer, hatch thực tế, text style hoặc dimension style. Hatch phân biệt theo cấu hình, bao gồm màu, tỷ lệ và góc. Trên bảng mẫu do đồng bộ sinh ra, có thể sửa các dòng `Nhóm | …`, `Công dụng | …`, `Ghi chú | …` rồi đồng bộ về app.

## Trình bày và định danh

Mỗi lần áp dụng sẽ cập nhật khu mẫu quản lý riêng, phân theo loại và nhóm, gồm mẫu block, nét layer, hatch, chữ và kích thước. Dùng named view bắt đầu bằng `SYNC_` để tìm nhóm. Mẫu cũ chưa có dấu quản lý vẫn được giữ; lần sau chỉ thay các mẫu do cơ chế đồng bộ tạo.

Tài nguyên được gắn ID bền vững trong DWG để theo dõi đổi tên. Các biến thể/alias block đang có trong app được giữ. Một định nghĩa CAD có thể tương ứng nhiều cấu hình block trên app.

Nhóm phân loại do app quản lý. Chỉ đổi nhóm không khiến tài nguyên chuyển sang Chưa đồng bộ nếu dữ liệu còn khớp. CAD → App giữ nhóm đã phân loại của mục đang có; mục mới lấy nhóm từ CAD nếu có. Chọn App → CAD nếu muốn đưa nhóm app vào nhãn trình bày DWG. Công dụng, ghi chú và các thông số tài nguyên vẫn được đối chiếu.

## Cập nhật bố trí theo nhóm

Sau khi phân nhóm, mở bảng đồng bộ, chọn loại thư viện rồi nhấn **Cập nhật bố trí CAD**. App đọc DWG hiện tại, đưa mẫu của loại đang chọn vào các nhóm theo app và dựng lại khu trình bày. Các thay đổi dữ liệu chưa duyệt vẫn được giữ để đối chiếu; thao tác bố trí không ghi đè thông số từ bản nháp app lên CAD.

Khu quản lý có năm phần từ trái qua phải: **01 Block, 02 Layer, 03 Hatch, 04 Text, 05 Dim**. Mỗi phần có các nhóm riêng, nhãn ghi đường dẫn nhóm cha/con. Các ô mẫu xếp theo hàng dựa trên kích thước thực và chữ thuộc tính, tự xuống hàng; mẫu lớn làm vùng nhóm và khoảng cách khu giãn theo. Khi thêm, bớt hoặc đổi nhóm, lần cập nhật bố trí tiếp theo sẽ tính lại các hàng và phạm vi. Khu này đặt bên phải nội dung cũ để tránh chồng lên vùng lưu đối chiếu. Named view `SYNC_00_ALL` xem tổng quan; `SYNC_01_BLOCK` đến `SYNC_05_DIM` mở từng phần; các view `SYNC_<kind>_<hash>` mở từng nhóm.

Thao tác chỉ sắp xếp mẫu thư viện do app quản lý trong file nguồn, không di chuyển block đã chèn trong bản vẽ công trình. Lưu và đóng DWG thư viện trước khi cập nhật bố trí. Thao tác chạy bằng mã trên máy, không gọi AI và không dùng token AI.

## Kiểm soát cập nhật

- Thiếu ở một phía không tự động xóa phía còn lại. Với mục chỉ còn trong app, chọn **Xóa khỏi app** trong cột chiều cập nhật rồi **Áp dụng**. App loại mục khỏi danh mục đang dùng và trạng thái đồng bộ, có kiểm tra liên kết và sao lưu; các phiên bản lịch sử vẫn giữ để phục hồi. Nếu chỉ chọn xóa, không ghi lại DWG và không tạo lại mẫu CAD. Nếu mục vẫn có trong DWG, lựa chọn này không khả dụng.
- Nếu app hoặc DWG đổi sau bước so sánh, yêu cầu đọc và so sánh lại.
- Kho có khóa ghi khi đồng bộ, trạng thái chung tại `Catalog/sync/state.json`, bản sao lưu và nhật ký phục hồi. Khi ghi nguồn lỗi, khôi phục các con trỏ danh mục và metadata của giao dịch.
- Các phiên bản danh mục được xuất bản mới; mẫu DWG được ghi ở định dạng tương thích AutoCAD 2021. Đổi tên layer cập nhật liên kết trong template và recipe liên quan.

Khi làm việc trên hai máy, dùng cùng phiên bản mã ứng dụng và đồng bộ cả kho thư viện theo quy trình kho hiện có. Đường dẫn kho trên mỗi máy được chọn riêng, không đưa `warehouse.local.json` lên Git.
