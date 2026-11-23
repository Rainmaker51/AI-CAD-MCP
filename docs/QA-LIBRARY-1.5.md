# Thư viện QA 1.5.1

## Sửa lỗi 1.5.1

Bản 1.5.0 có lỗi hiển thị rỗng khi mã kiểm tra bản chụp được tính từ số thực trên .NET Framework nhưng kiểm lại trên .NET 10: hai runtime có cách ghi số khác nhau. Danh mục kho không bị xóa. Bản 1.5.1 dùng định dạng kiểm tra thống nhất, giữ nguyên dữ liệu số thực gốc và vẫn xác minh bản chụp 1.5.0 theo đúng định dạng cũ.

Host 1.5.1 tương thích DLL 1.5.0: cập nhật Host rồi đóng/mở bảng QA để khôi phục đọc danh mục. DLL 1.5.1 bổ sung hiển thị danh mục kho ở chế độ chỉ xem khi đọc CAD thất bại; báo rõ lỗi và chặn chỉnh sửa cho tới khi đọc thành công. Không biến lỗi đọc thành danh sách trống hoặc hành động xóa.

QA là nơi chỉnh sửa và duyệt thư viện; app đọc danh mục đã lưu trong kho. Nút Ribbon vẫn là **Thư viện QA**, do Manager quản lý. Bố trí CAD nằm trong phần thu gọn của cùng bảng.

## Cập nhật

1. Cập nhật dịch vụ nền đi kèm build bằng `Update-QaLibraryEngine.ps1 -RepositoryRoot <repo trên máy này> -Apply`. Có thể chạy trước không có `-Apply` để kiểm tra đường dẫn và hash. Script đọc bản cài Manager trên máy, sao lưu dịch vụ cũ; không đổi catalog hoặc DLL Manager.
2. Trong CAD Tool Manager, chọn đúng mục Rainmaker.Library đang có → **Cập nhật file** → `package/Rainmaker.Library.dll` của build này. Giữ cả năm file trong `package` cạnh nhau. Không thêm mục trùng, không sao chép tay vào `_Installed`.
3. Mở lại AutoCAD nếu phiên hiện tại đã nạp QA/dependency cũ. Trên hai máy dùng cùng catalog, cập nhật dịch vụ nền và Manager tương thích trên từng máy trước khi dùng QA mới.

## Đọc CAD và lưu kho

- Mở bảng hoặc nhấn **Làm mới CAD**: lấy hiện trạng từ bộ nhớ nếu DWG thư viện đang mở, kể cả thay đổi chưa lưu; nếu đóng, đọc file đã lưu. Đường dẫn nguồn và cách đọc hiển thị ở đầu bảng. Không quét lại khi chọn từng dòng hoặc di chuột.
- Đọc hiện trạng không sửa hoặc lưu DWG gốc. Bản chụp tạm ở `DataRoot/library-sync/<id>` của máy, dùng cho kiểm tra và chèn; bản chụp cũ chưa duyệt được dọn khi thay bản mới.
- Mục mới/chỉnh sửa chưa tự trở thành tài nguyên phát hành cho Skill/add-in. Lưu nguồn bằng **Ctrl+S** trên đúng DWG thư viện, rồi nhấn **Lưu danh mục vào kho**; xem thông số hai phía và chỉ chọn thay đổi muốn nhận hoặc loại bỏ.
- Phải lưu DWG nguồn trước khi xác nhận duyệt. Nếu đang ở DWG khác, chuyển về nguồn và lưu trước. QA so lại nội dung đã xem với bản đã lưu; nếu khác sẽ yêu cầu làm mới và xem lại. Việc duyệt không ghi đè nguồn CAD.
- Mã/ID, ghi chú và cấu hình sử dụng đã có được giữ lại; không tự duyệt hàng loạt. “Có trong danh mục” khác “Đã duyệt sử dụng”. Với block, trạng thái sử dụng lấy từ hồ sơ Usage đã có, không lấy nhầm cờ kiểm tra hồ sơ quản lý.

## Mẫu bị xóa

**Đang dùng** không hiển thị block đã mất mẫu. Bộ lọc **Chỉ còn định nghĩa** dành cho block đã xóa mẫu nhưng định nghĩa vẫn nằm trong bảng BLOCK của DWG; **Đã mất trong CAD** dành cho định nghĩa/tài nguyên đã mất hẳn. Duyệt **Loại khỏi danh mục** để loại liên kết ở kho. Không tự chèn lại mẫu, không tự PURGE nguồn.

Việc loại bỏ bị chặn nếu còn Skill, tài nguyên khác hoặc lựa chọn trong cấu hình add-in đang bật tham chiếu. Với cấu hình add-in, kiểm tra ID chính xác trong `Catalog/addin-settings/<tool-id>.json` và trạng thái Manager. Đổi lựa chọn ở công cụ trước khi loại bỏ. Đây không phải công cụ dò mọi DWG công trình bên ngoài kho.

## Chèn block vào bản vẽ khác

Chuyển sang bản vẽ đích → mở QA → chọn block còn mẫu → **Chèn block vào bản vẽ hiện tại**. Nhập tỷ lệ, góc (độ), tham số động và nội dung ATT, sau đó chọn điểm chèn trên CAD. Esc hủy trước khi nhập định nghĩa. Điểm chèn theo UCS hiện tại; góc xoay quanh trục Z của WCS.

Khi trùng tên: mặc định **Giữ định nghĩa hiện có**; hoặc chọn **Cập nhật từ thư viện**, thao tác này đổi các tham chiếu cùng tên trong bản vẽ đích. Giữ giá trị ATT của các tham chiếu cũ; không tự ATTSYNC các ATT cũ theo cấu trúc mới. Các layer/style phụ thuộc trùng tên giữ cấu hình bản vẽ đích. Đơn vị nguồn/đích phải được khai báo; tự đổi hệ số đơn vị. Bản vẽ đích không tự thành DWG thư viện.

## Hatch và ảnh

Hatch mặc định **Theo loại hatch**, nhóm thu gọn; có chọn tên Pattern, Thu gọn/Mở rộng và chuyển về danh sách. Cấu hình cùng Pattern khác scale/góc/màu vẫn là từng mục riêng, không gộp ID.

**Tạo ảnh block từ CAD** đọc bản chụp hiện tại, tạo ảnh nhỏ 240 × 180 trên máy để xem hình dạng; không tạo thêm version DWG trong kho và không gọi AI. Block mới/đổi hình học không lấy nhầm ảnh cũ từ danh mục. Ảnh chỉ minh họa, có thể đơn giản hóa chi tiết; không dùng để đo kích thước.

## Kiểm thử và giới hạn

Build .NET Framework 4.8 x64, AutoCAD 2021. Kiểm thử dữ liệu ứng dụng, HTTP có xác thực và native qua runtime Manager đang cài; có fixture lưu/mở lại, clone hiện trạng, mẫu bị xóa, đổi tên/ID, hatch đổi scale, chèn/ATT/trùng tên và render WPF. Không dùng DWG công trình thật làm fixture.

Core và ảnh render không thay cho kiểm thử chuột, Ribbon và chọn điểm trong AutoCAD GUI. Xem manifest/validation của build để biết kiểm thử thực tế. Gói bàn giao chưa tự được chọn trong catalog; không suy ra bản đang cài từ version lớn nhất.
