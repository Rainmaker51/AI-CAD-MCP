# QA Library 1.11 — nhóm, khung và bảng đối chiếu

## Nhóm và khung CAD

Áp dụng cho Block, Layer, Hatch, Text và Dim. QA quản lý nhóm và bố trí; thông số native vẫn thêm/sửa trên DWG nguồn như quy trình 1.9–1.10.

- Thêm nhóm trong QA tạo cả nhóm trong kho và khung rỗng trong DWG. Khung con đặt gần vùng cha, khung cha được mở rộng khi cần; các khung khác được tách để không chồng lên nhau. Trong AutoCAD GUI, đưa màn hình đến nhóm vừa tạo.
- Khung có `CatalogGroupId` liên kết ID nhóm với frame ID ổn định. Với khung cũ chưa có liên kết, đối chiếu đường dẫn để nhận diện; không suy đoán một khung đã đổi cả tên/đường dẫn trước khi có liên kết ID.
- Xóa nhóm rỗng: bỏ nhóm và khung. Xóa nhóm có nội dung: chọn nhóm nhận mẫu hoặc khung gốc/Chưa phân nhóm; mẫu thật, ô và chú thích được chuyển sang vùng đích. Chọn đưa nhóm con lên cấp cha hoặc chuyển sang nhóm đích.
- Khung nhóm đã bị xóa trực tiếp trên CAD vẫn có thể xóa nhóm trong QA. Không yêu cầu tạo lại khung và không tự phục hồi khung đó. Không xóa định nghĩa layer, block, style hoặc hatch khi chỉ xóa nhóm.
- Giữ năm khung gốc. Chặn trùng tên cùng cấp, vòng cây, chuyển vào nhóm đang xóa/nhóm con, và nhóm cha không còn khung.
- Thay đổi nhóm chỉ cập nhật metadata nhóm và vị trí mẫu. Không tự duyệt các thay đổi thông số tài nguyên đang chờ, kể cả mẫu mới chưa xuất kho. Giữ ID/mã, trạng thái duyệt, tên, công dụng và ghi chú.

Thao tác sao lưu nguồn, ghi journal, lưu CAD rồi áp dụng cây nhóm và metadata kho. Có kiểm tra trước khi ghi, phát hiện cây nhóm/hồ sơ thay đổi ở phiên khác và tiếp tục idempotent sau khi ghi một phần. Hàng chờ dùng cơ chế Xử lý cập nhật hiện có. Đọc hash nguồn bằng chế độ chia sẻ tệp để xử lý được DWG còn mở trong AutoCAD.

## Quét thay đổi

Đối chiếu tài nguyên và cả nhóm/khung: Nhóm mới trên CAD, Khung nhóm đã bị xóa, Đổi tên/chuyển nhóm, Nhóm không thay đổi. Không tự nhận thao tác xóa/khôi phục nhóm khi chỉ mở bảng. Khi xác nhận nhóm mất khung, vẫn yêu cầu chọn đích cho mẫu và cách xử lý nhóm con.

Nhóm được xử lý trước tài nguyên đã tích chọn; sau đó đọc lại thông số, kiểm tra nội dung không đổi trước khi xuất tài nguyên. Nếu có thay đổi ngoài kế hoạch, dừng để quét lại. Mỗi bước hoàn tất có journal/bản sao riêng; không hoàn tác dữ liệu đã xác nhận một cách ngầm định.

## Bộ lọc và lựa chọn

- Loại: tất cả, Block, Layer, Hatch, Text, Dim.
- Trạng thái: các trạng thái có trong lần quét, gồm tài nguyên và nhóm; hiển thị số mục theo trạng thái.
- Tình trạng chọn: tất cả, đã tích chọn, chưa tích chọn.
- Tìm theo mã tài nguyên, tên, nhóm hoặc thông số hai phía.
- Kết hợp bộ lọc; thay bộ lọc không bỏ lựa chọn đang ẩn. Tổng số đã chọn và số đã chọn đang ẩn luôn được hiển thị.
- Chọn/bỏ chọn hàng loạt chỉ áp dụng các dòng đang hiển thị. Mục ngoài phạm vi và chỉ còn định nghĩa không thể tích nhận nhầm.
- Chuỗi “Ngoài khung quét” lưu Unicode đúng; ô liên kết/gộp hiển thị nhãn lựa chọn, không hiện tên lớp CLR. Trạng thái có tooltip đầy đủ.

## Kiểm thử và bàn giao

Xem `validation` trong gói phát hành: Core 2021/Manager, vòng đời nhóm đủ năm loại, API ghi cây nhóm và phục hồi từng phần, thao tác bảng QA thật trên nguồn thử, bộ lọc và ảnh WPF. Không dùng DWG thư viện thật làm fixture. Chạy Core/ảnh WPF không thay kiểm thử chuột trong AutoCAD GUI sau cài.

Gói và source.patch nằm trong `_Development/Rainmaker.Library/versions/1.11.0/build-<fingerprint>/`. Nhập bằng Manager để giữ ID/Ribbon; khởi động lại AutoCAD khi thay DLL/dependency. Không cần dịch vụ nền.
