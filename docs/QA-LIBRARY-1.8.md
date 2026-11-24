# QA Library 1.8.2 — xóa, gộp và cập nhật DWG/kho

## Bản sửa 1.8.2: đổi tên layer và nhãn trình bày

Sửa luồng **Sửa thông số trong DWG**: sau khi cập nhật định nghĩa hiện có, cập nhật ngay nhãn mẫu được QA quản lý thay vì kết thúc sớm. Tên layer, tiêu đề mẫu, màu và độ dày nét trên nhãn cùng nhận thông tin mới; không phải xếp gọn lại để đổi chữ. Giữ ID/mã, vị trí mẫu, nhóm, công dụng, ghi chú và dòng ghi thêm trên nhãn. Không dựng lại mẫu đã xóa.

Tên hiển thị mặc định theo tên layer được cập nhật khi đổi tên thật; tên hiển thị tùy chỉnh riêng vẫn được giữ. Lỗi thao tác sửa được báo bằng hộp thoại để tránh bỏ sót thông báo ở chân bảng, kể cả trường hợp nguồn chưa lưu. Nguồn vẫn phải lưu Ctrl+S trước khi bắt đầu; QA không tự lưu đè thay đổi đang làm dở.

Kiểm thử hồi quy tái hiện được nhãn cũ trên bản trước; bản sửa kiểm tra đổi tên lặp lại, nhãn và thông số, giữ vị trí/alias riêng, lưu/mở lại DWG, không tái tạo mẫu đã xóa và không sinh layer trùng trong danh mục.

## Thao tác

- Trong **Bố trí trên CAD**, chọn một hoặc nhiều mục rồi **Xóa khỏi CAD và kho**. Dùng được trong bộ lọc **Chỉ còn định nghĩa** và **Đã mất trong CAD**; không phải chọn lại mẫu đã xóa trên CAD.
- Xóa kiểm tra mẫu trong Model, layout và định nghĩa block lồng. Xóa định nghĩa bằng kiểm tra purge; tài nguyên hệ thống, xref và định nghĩa còn phụ thuộc không bị ép xóa. Các hồ sơ dùng chung định nghĩa phải được xem là cùng một lần xóa. Add-in/skill đang sử dụng sẽ chặn xóa và báo liên kết cần đổi.
- Hatch: chọn các cấu hình trong danh sách rồi **Gộp hatch từ danh sách**, hoặc **Chọn hatch trên CAD để gộp**. Chọn mã đích đang có danh mục. Bảng xác nhận cho biết số đối tượng, owner/handle và cấu hình đích. Áp dụng trên toàn DWG nguồn, gồm cả các cấu hình tương ứng ngoài vùng đã chọn; đây là gộp tài nguyên, không phải chỉ sửa vài entity.
- Hatch mới chỉ có trong CAD cũng gộp trực tiếp được; chỉ chuyển tham chiếu của các ID nguồn đã thực sự xuất vào kho. Bản 1.8.1 bổ sung xử lý trường hợp này, không sửa đè gói 1.8.0 đã niêm phong.
- Gộp giữ mã/hồ sơ đích, thay pattern, scale, góc, màu ACI/TrueColor, layer và thông số user-defined; giữ loops và associativity. Các cấu hình add-in lưu ID hatch nguồn được chuyển sang ID đích, có bản sao cấu hình. Skill dùng pattern trực tiếp không bị sửa ngầm; nếu thay thế làm mất pattern còn cần, thao tác bị chặn.
- Thêm/sửa tài nguyên, sửa công dụng/ghi chú/tên hiển thị, chuyển nhóm và sửa cây nhóm tại QA đi qua luồng ghi DWG, lưu nguồn và cập nhật kho. Xếp gọn chỉ di chuyển mẫu hiện có, không dựng lại mẫu đã xóa.
- Nguồn phải được lưu trước khi bắt đầu thao tác. Khi sửa trực tiếp bằng lệnh CAD, lưu Ctrl+S rồi **Lưu danh mục vào kho / Hoàn tất đồng bộ**. Bảng sẽ xác nhận toàn bộ thay đổi chờ; không duyệt riêng vài mục rồi báo cả thư viện đã khớp.

## Giao dịch và phục hồi

Mỗi thao tác có bản sao DWG trong `Versions/qa-mutations/<id>/before.dwg`, kết quả và bản sao cấu hình tiêu thụ nếu có. Không chỉnh các DWG công trình đã chèn tài nguyên trước đây.

`Catalog/qa-transactions/ISO_QUANGANH.pending.json` ghi thao tác chưa hoàn tất. Nếu lưu DWG/ghi kho thất bại, QA giữ trạng thái chưa đồng bộ và chặn thao tác mới; **Hoàn tất đồng bộ** kiểm tra nội dung nguồn trước khi tiếp tục. Hai nơi không thể ghi nguyên tử như một file: bản này dùng journal và kiểm tra hash để phát hiện, tiếp tục giao dịch bị ngắt, không báo thành công một nửa. Nếu nguồn bị thay đổi ngoài kế hoạch, không tự ghi đè để phục hồi.

Journal dùng ID nguồn và đường dẫn bản sao tương đối để dùng trên hai máy. Chỉ chỉnh kho từ một máy tại một thời điểm, đợi phần mềm đồng bộ sao chép đủ trước khi đổi máy. File lock cục bộ không phải cơ chế khóa phân tán qua phần mềm đồng bộ.

Thùng rác cũ vẫn đọc được. Xóa mới loại khỏi danh mục đang dùng và giữ lịch sử/bản sao; không tự khôi phục nguồn chỉ vì còn hồ sơ cũ. Khôi phục mẫu từ bản sao DWG trước khi khôi phục hồ sơ cũ thiếu mẫu.

## Kiểm thử và giới hạn

Kiểm thử dữ liệu: chặn xóa khi công cụ đang dùng, chuyển ID hatch và chạy lại idempotent, giữ cấu hình không liên quan. Core 2021: nạp qua Manager 0.9, UI render, gộp hatch trong block lồng, giữ vùng biên/mã đích, purge định nghĩa, xóa tham chiếu lồng và rollback khi không xóa được tài nguyên phụ thuộc. Giữ cả kiểm thử QA trước đó về 5 loại, ID, nhóm, xuất danh mục và chèn block.

Core không thay kiểm thử đầy đủ chuột trong AutoCAD GUI. Bản sao người dùng thuộc lịch sử phục hồi; không nằm trong bước dọn runtime/profile kiểm thử. Đọc manifest build để biết kiểm thử thực tế và hash gói. Cài bằng Manager **Cập nhật file** trên mục Rainmaker.Library hiện có; không sửa `_Installed` hoặc catalog thủ công.

Thao tác lưu nguồn đang mở dùng overload có `bBakAndRename=true`, theo [hướng dẫn Save and Close a Drawing của Autodesk](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-DevGuide-Managed/files/GUID-CBB74093-BB22-49CC-B54A-D33D7E92694C.htm); có kiểm thử Core lưu thành công và kiểm tra DBMOD.
