# QA Library 1.9.0 — CAD là nơi thêm và sửa thư viện

## Luồng sử dụng

Thêm/sửa/xóa hoặc thay cấu hình Block, Layer, Hatch, Text, Dim trực tiếp trong **DWG thư viện nguồn**. Lưu **Ctrl+S**, mở QA → **Quét thay đổi / đối chiếu** → chọn mục, nhóm và xem hai phía → **Xác nhận cập nhật**. QA tiếp nhận thông số CAD, bổ sung/cập nhật mẫu trình bày và lưu DWG, rồi xuất các mục đã chọn vào kho.

- Bỏ các nút Thêm mẫu / kiểu, Sửa thông số trong DWG, Xóa khỏi CAD và kho và Gộp hatch thay thông số từ danh sách. QA không là nơi thứ hai chỉnh định nghĩa/hình học.
- QA vẫn quản lý nhóm/nhóm con, tên hiển thị, công dụng, ghi chú và duyệt. Đổi nhóm không đổi ID. Có thay đổi CAD chưa duyệt thì đối chiếu trước khi thay thông tin quản lý hoặc xếp gọn.
- Bảng đối chiếu phân biệt mới, đổi tên, sửa, đã mất, chỉ còn định nghĩa/thiếu mẫu và không thay đổi. Mở thông số hai phía để xem. Bỏ chọn mục cần rà sau; QA không báo cả thư viện khớp khi vẫn còn mục chưa nhận.
- Layer/Text/Dim mới chưa có mẫu được tạo mẫu trình bày từ định nghĩa CAD. Block phải có tham chiếu thật; chỉ còn định nghĩa thì chèn mẫu hoặc PURGE trên CAD, không tự sinh mẫu từ định nghĩa sót.
- Mẫu đã quản lý bị xóa không tự được dựng lại. Mục đã mất có thể chọn loại hồ sơ khỏi kho sau kiểm tra phụ thuộc; QA không xóa tài nguyên CAD. Nút vị trí sử dụng/hướng dẫn giúp tra owner/handle và hướng dẫn xử lý trên CAD.
- Khi mất thẻ ID và CAD nhận thành mục mới, người dùng có thể xác nhận **liên kết ID cũ** thuộc cùng loại đã mất. Không suy đoán chỉ dựa tên; không dùng một ID cũ cho nhiều mục. Hồ sơ thiếu được liên kết không đồng thời bị loại.
- Muốn gộp hatch: thay các hatch trên CAD về đúng cấu hình đích trước, lưu nguồn rồi chọn **gộp danh mục về** trong bảng đối chiếu. QA chỉ hợp nhất ID khi pattern, type, scale, góc, màu ACI/TrueColor, layer và user-defined settings đã khớp. Không dùng gộp danh mục để sửa thông số/hình học CAD. Kiểm tra công cụ/skill bị ảnh hưởng và chuyển ID cấu hình add-in được hỗ trợ.

## Trình bày trên CAD

Chỉ cập nhật các mục được xác nhận. Giữ vị trí mẫu cũ, giá trị ATT/tham số/rotation của block và ghi chú bổ sung. Mẫu mới được nối vào nhóm; không xếp lại toàn bộ thư viện. Các nhãn mô tả được cập nhật cho đủ 5 loại. Mẫu chữ/dim phản ánh style CAD; hatch trong block/layout có thể có bản sao chỉ dùng trình bày ở Model, không chuyển đổi hatch nguồn hay vùng biên gốc.

Hình học do người dùng tạo, giá trị biến thể block và các đối tượng công trình đã sử dụng thư viện không tự bị cập nhật theo danh mục. Các chỉnh sửa trình bày là đối tượng QA quản lý, không phải sửa lại định nghĩa từ bản kho cũ.

## Xử lý cập nhật chưa hoàn tất

Nhấn **Xử lý cập nhật** để xem thao tác, thời điểm, bước đã ghi và nơi lưu bản sao. Hai lựa chọn:

1. **Tiếp tục cập nhật:** chỉ bật khi DWG đã lưu còn đúng hash của bước đã ghi. Kiểm tra nguồn/kho trước khi nhận tiếp; nếu phát hiện thay đổi ngoài kế hoạch thì yêu cầu đối chiếu lại. Không dùng chữ ký của bản Wblock clone để khẳng định nguồn đã được lưu.
2. **Giữ CAD hiện tại, hủy kế hoạch cũ và quét lại:** sao lưu DWG hiện tại, chụp trạng thái kho/con trỏ và cấu hình sử dụng, lưu trữ nguyên journal cũ rồi mở lại đối chiếu. Không ghi đè DWG bằng bản cũ. Nếu kho/cấu hình đã ghi một phần thì giữ phần đã ghi và đối chiếu kế hoạch mới; không âm thầm hoàn tác hoặc chỉ xóa journal.

Bản sao, kế hoạch và kết quả nằm dưới `Versions/qa-mutations/<id>/`. Journal hiện hành ở `Catalog/qa-transactions/ISO_QUANGANH.pending.json`. Có giao dịch ghi kho chưa phục hồi thì QA không bỏ qua để hủy kế hoạch; kiểm tra/phục hồi trạng thái kho trước. Journal 1.8 cũ vẫn được xem/hủy giữ CAD; chỉ cho tiếp tục khi có bằng chứng DWG đã lưu khớp.

DWG nguồn phải được lưu trước lúc xác nhận/quét lại. QA không tự lưu đè các thay đổi chưa lưu mà người dùng đang làm dở. Hai máy phải chờ đồng bộ thư mục hoàn tất; file lock không phải khóa phân tán.

## Cài đặt và kiểm thử

Cài qua Manager → mục Rainmaker.Library → Cập nhật file; giữ ID/tab/nút và không sửa `_Installed` thủ công. Đóng/mở AutoCAD để nạp dependency mới nếu phiên cũ đang giữ DLL. Không cần Host để quản lý QA.

Build yêu cầu bằng chứng Core 2021 cho đủ 5 loại, bảo toàn thông số CAD, hatch lồng, biến thể block, không dựng lại mẫu đã xóa, lựa chọn/ID trong bảng đối chiếu và chạy phương thức xác nhận xuất kho thật trên fixture. Kiểm thử dữ liệu phải gồm tiếp tục khi hash khớp, từ chối nguồn đổi, hủy sau xuất một phần và giữ CAD/kho/cấu hình. Xem manifest/validation để biết kết quả thực tế. Core và ảnh render không thay kiểm thử chuột đầy đủ trong AutoCAD GUI.
