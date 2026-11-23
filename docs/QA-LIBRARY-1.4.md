# Thư viện QA 1.4.1

QA là nơi chỉnh sửa và duyệt thư viện. App vẫn đọc, tìm kiếm và cung cấp tài nguyên cho Skill. Dữ liệu không chuyển vào thư mục cài add-in.

## Cài trên mỗi máy

1. Dùng build được bàn giao trong `Tools/dotnet/_Development/Rainmaker.Library/versions/1.4.1/build-<fingerprint>/`. Giữ cả `package`, `engine` và manifest. Máy đã cài dịch vụ nền QA 1.4.0 có thể giữ dịch vụ đó: bản 1.4.1 chỉ gộp giao diện của DLL QA.
2. Tại repo của máy hiện tại, chạy script đi kèm build để xem trước đường dẫn:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File "<build>/Update-QaLibraryEngine.ps1" -RepositoryRoot "$PWD"
   ```

   Khi app không có kế hoạch CAD đang thực thi, thêm `-Apply` để cập nhật dịch vụ nền. Script lấy cấu hình từ Manager hiện tại, kiểm tra hash, sao lưu và giữ nguyên `install.json`. Không cần mở trang app để dùng QA. Dịch vụ sử dụng runtime .NET 10 sẵn trong `.tools/dotnet` hoặc runtime đã cài trên máy.
3. Trong **CAD Tool Manager**, chọn đúng **Rainmaker.Library → Cập nhật file**, chọn `package/Rainmaker.Library.dll`. Không chọn Thêm file và không chép vào `_Installed`.
4. Nếu DLL cũ đã nạp, lưu bản vẽ và mở lại AutoCAD. Giữ đúng một nút Ribbon **Thư viện QA / QALIBRARY**; các lệnh phụ nằm trong bảng. Manager giữ cấu hình nút đã lưu.
5. Mở QA mới thành công sẽ ghi nhận QA là trình chỉnh sửa của kho. App mới chuyển phần thư viện sang chỉ xem. Cập nhật gói dịch vụ và QA trên cả hai máy; catalog kho dùng chung, cấu hình đường dẫn vẫn riêng theo máy.

## Dùng hằng ngày

- **Block, Layer, Hatch, Text, Dim** có cây nhóm riêng, tìm kiếm theo mã/tên/công dụng/thông số, bộ lọc duyệt và thùng rác. Giữ Ctrl/Shift để chọn nhiều mục rồi chuyển nhóm.
- Chọn một mục để mở hồ sơ: công dụng, ghi chú, trạng thái duyệt, thông số, tên DWG nguồn và đường dẫn. Block có cấu hình sử dụng cho Skill, layer chèn, ánh xạ tham số động và quy đổi kích thước. Ảnh chỉ nằm trong danh sách, giải mã ở độ phân giải nhỏ.
- **Thêm mẫu / kiểu** tạo Layer, Text, Dim, Hatch trong DWG nguồn kèm mẫu trình bày. Với Block, chọn DWG mm và một định nghĩa block có tên để nhập. Định nghĩa trùng tên không bị ghi đè. Có thể tiếp tục tạo/sửa trực tiếp bằng các lệnh AutoCAD.
- **Sửa thông số trong DWG** kiểm tra đúng file nguồn đang mở. Đổi tên giữ ID tài nguyên. Sửa block bằng BEDIT khi cần chỉnh hình học, thuộc tính hoặc dynamic block.
- Sau khi sửa: **Ctrl+S → Đọc kiểm tra DWG → chọn từng thay đổi → Duyệt các mục đã chọn**. Không tự duyệt và không tự khôi phục mẫu đã bị xóa. Mục mất trong CAD có lựa chọn loại khỏi danh mục; các liên kết Skill được kiểm tra trước khi loại.
- Duyệt dùng bản DWG đã kiểm tra, xuất bản các danh mục và snapshot bất biến phục vụ công cụ. Không ghi đè DWG đang mở. Nếu CAD/hồ sơ đổi sau lúc đọc, phải đọc lại. Các lần nạp/triển khai tiếp theo của add-in dùng cấu hình kho mới; đối tượng đã vẽ giữ nguyên.
- Sửa nhóm trên QA đổi hồ sơ quản lý. **Áp dụng nhóm / xếp gọn CAD** chuyển các mẫu hiện có sang nhóm mới và bố trí lại khung, bao gồm nhóm rỗng. Nhấn Ctrl+S để lưu. Các mẫu bị xóa không được tự dựng lại.
- **Bố trí trên CAD** là phần thu gọn ngay trong bảng QA duy nhất: nhận mẫu, chuyển nhóm, xếp gọn, chỉnh khung, di chuyển cả nhóm và đến nhóm. Loại thư viện/nhóm lấy theo cây đang chọn, không cần mở thêm palette. Khi chọn Tất cả nhóm, lệnh có thể yêu cầu chọn khung/nhóm trên CAD. Bảng tạm ẩn trong lúc chọn đối tượng hoặc điểm rồi hiện lại khi kết thúc/hủy; giữ nguyên bộ lọc. Dữ liệu nhóm/ATT hiện có được giữ.
- Thùng rác hồ sơ không xóa hình học trong DWG. Mục đang được Skill hoặc thư viện khác tham chiếu sẽ bị chặn đưa vào thùng rác. Việc xóa vĩnh viễn và thay thế liên kết hàng loạt vẫn cần rà soát riêng.

## Hai máy và dung lượng

Không chỉnh kho đồng thời trên hai máy; chờ thư mục kho đồng bộ đủ trước khi chuyển máy. Mỗi máy cập nhật dịch vụ nền riêng. Chỉ một gói cho mỗi add-in nằm trong `_Installed`, do Manager quản lý.

`tools/Clear-AddinWorking.ps1` mặc định chỉ báo cáo. `-Apply` chỉ dọn gói tạm có DLL trùng hash bản đã bàn giao và hai profile kiểm thử cũ đã chỉ rõ. Không quét xóa tất cả `working`, không xóa `versions`, `_Installed`, DWG nguồn hoặc runtime cache của AutoCAD đang mở. Báo cáo nằm ở `.tools/addin-working-cleanup.json`.

## Kiểm thử và giới hạn bàn giao

Kiểm thử độc lập: metadata của đủ 5 loại, cây nhóm, revision, ID qua đổi tên, xuất bản có chọn lọc, tham chiếu Text/Dim, hatch scale 300, từ chối bản kiểm tra cũ, quyền chỉnh sửa QA và HTTP có xác thực. Kiểm thử native đi qua đúng Manager trên Core 2021, đọc/lưu lại DWG fixture, giữ ATT/nhóm, tạo mẫu và không khôi phục block đã xóa. Có ảnh render WPF để đối chiếu theme.

Kết quả kiểm thử riêng của build và tình trạng được chọn trong catalog nằm trong `manifest.json`/`NOTES.md`. Kiểm thử Core/ảnh render không thay cho việc thao tác chuột trên bản QA đã cài trong AutoCAD GUI.
