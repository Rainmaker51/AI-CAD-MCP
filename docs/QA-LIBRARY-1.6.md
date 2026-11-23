# Thư viện QA 1.6.0 — chạy trực tiếp trong AutoCAD

## Cách hoạt động

QA đọc DWG và quản lý danh mục kho bằng `Rainmaker.Library.Core.dll` ngay trong AutoCAD 2021 (.NET Framework 4.8). Không dùng HTTP, token, `host.json`, tiến trình `AICad.Host.exe` hoặc runtime .NET 10 để mở/quản lý thư viện. Chỉ cần Manager, gói QA đầy đủ và kho có thể truy cập trên máy.

App/Skill tiếp tục đọc cùng danh mục đã được QA duyệt. Dịch vụ nền vẫn phục vụ app/AI; có thể chạy hoặc tắt độc lập với QA. Không thay định dạng kho, không tạo kho phụ và không chuyển dữ liệu vào thư mục cài add-in.

## Cài hoặc cập nhật

Trong CAD Tool Manager, chọn mục Rainmaker.Library đang có → **Cập nhật file** → `package/Rainmaker.Library.dll` của build này. Giữ cả **7 file** trong `package` cạnh nhau. Không thêm mục trùng, không chép tay sang `_Installed`, không thay DLL của Manager.

QA dùng `CAD.Contracts.dll` và `CAD.Geometry.dll` từ đúng bản cài Manager; gói có phần xử lý kho riêng để tương thích Manager 0.8.3. Nếu phiên AutoCAD đang giữ dependency khác, mở lại AutoCAD theo thông báo Manager. Không cần cập nhật hoặc mở dịch vụ nền để dùng QA 1.6.

Có thể dùng `Install-QaLibrary.ps1 -BuildDirectory <build>` để kiểm tra gói. Thêm `-Apply` để gọi chính API nhập/cập nhật của bản Manager đang cài trên máy. Script xác minh hash, cập nhật đúng mục hiện có, giữ ID/tab/nút và để Manager tự lưu lịch sử, đổi con trỏ, dọn gói cũ. Không chỉnh catalog hoặc `_Installed` thủ công. Cần đóng/mở bảng hoặc khởi động lại AutoCAD nếu Manager yêu cầu.

## Dữ liệu và sửa lỗi hiển thị

- Giữ nhóm/nhóm con, ghi chú, công dụng, mã/ID và trạng thái duyệt. Nhiều cấu hình sử dụng cùng một block gốc vẫn là các hồ sơ riêng; không gộp hoặc ghi đè tham số của nhau. Ảnh của các cấu hình cũng lưu theo từng hồ sơ riêng.
- Sửa lỗi 1.5.0 kiểm tra số thực khác nhau giữa .NET Framework và .NET 10. Tiếp tục đọc được bản chụp cũ. Sửa lỗi truy vấn một bản ghi khi nhiều hồ sơ dùng cùng định nghĩa block và xử lý hồ sơ chưa có Usage.
- Nếu đọc DWG thất bại, hiện danh mục đã lưu ở chế độ **chỉ xem**, kèm lỗi rõ ràng; chặn chỉnh sửa tới khi đọc thành công. Lỗi đọc không trở thành hành động xóa hoặc danh mục trống.
- Không xóa mẫu hoặc danh mục tự động. Không phục hồi mẫu đã xóa chỉ vì hồ sơ cũ còn trong kho.

## Luồng sử dụng

1. Mở QA hoặc nhấn **Làm mới CAD**. Nếu nguồn đang mở, đọc hiện trạng trong bộ nhớ; nếu đóng, đọc file đã lưu. Nút mở nguồn và đường dẫn nằm trên bảng.
2. Quản lý nhóm và hồ sơ ngay tại QA. Bố trí CAD nằm trong phần thu gọn của cùng bảng, dùng đúng nguồn và nhóm đã chọn.
3. Với thay đổi hình học/thông số: sửa trong DWG, lưu **Ctrl+S** trên nguồn, sau đó **Lưu danh mục vào kho**. Xem và chọn từng thay đổi trước khi duyệt. Kiểm tra lại nguồn, revision và nội dung để tránh ghi đè thay đổi ở máy khác.
4. App và add-in/Skill dùng danh mục đã duyệt ở lần nạp/triển khai tiếp theo. Không gọi AI, không tốn token cho các thao tác thư viện này.

Block mất mẫu nhưng còn định nghĩa nằm ở bộ lọc **Chỉ còn định nghĩa**; mất hẳn nằm ở **Đã mất trong CAD**. Chúng không xuất hiện trong **Đang dùng**. Muốn loại khỏi danh mục phải chọn rõ khi duyệt; tài nguyên còn được công cụ/Skill tham chiếu sẽ bị chặn xóa.

Có thể chèn block từ nguồn sang DWG khác, nhập tỷ lệ/góc/tham số động/ATT, chọn giữ hoặc cập nhật định nghĩa trùng tên. Hủy chọn điểm không nhập thêm block. Hatch xem theo Pattern thu gọn mặc định, giữ từng cấu hình scale/góc/màu riêng.

Ảnh block 240 × 180 tạo từ bản chụp CAD, chỉ lưu đệm trên máy; không tạo version DWG mới trong kho. Ảnh chỉ để xem hình dạng. Bản chụp chưa duyệt và ảnh đệm được dọn khi thay bản mới/đóng bảng.

## Bảo vệ và kiểm thử

Giữ khóa ghi, kiểm tra revision/hash, phiên bản bất biến, sao lưu con trỏ và phục hồi giao dịch dở. Hai máy dùng cùng cấu trúc kho; chỉ chỉnh/cập nhật từ một máy tại một thời điểm và chờ đồng bộ đủ trước khi đổi máy.

Kiểm thử native qua Manager 0.8.3 trên Core 2021 khi đường dẫn Host cố ý không tồn tại: đọc đủ 5 loại, quản lý nhóm/hồ sơ, giữ duyệt cũ, cấu hình block, duyệt thay đổi được chọn, giữ DWG nguồn và mở lại dữ liệu. Có kiểm thử lỗi đọc DWG vẫn giữ danh mục để xem, nhiều hồ sơ dùng chung block, chữ ký từ Framework sang .NET, API của app và kiểm tra chỉ đọc trên kho thực tế với hash danh mục trước/sau không đổi.

Core và ảnh render không thay kiểm thử đầy đủ chuột/Ribbon trong AutoCAD GUI. Xem manifest/validation để biết phạm vi thực tế và trạng thái gói trong catalog.
