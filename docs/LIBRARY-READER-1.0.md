# Bộ cập nhật Reader 1.0 / Axis 1.9 / Manager 0.10

**Đã thay cách cài:** tài liệu này lưu lịch sử lần đóng gói ban đầu. Theo yêu cầu mới, Reader được cài riêng trong Thành phần dùng chung và Manager giữ 0.10.0; dùng [hướng dẫn hiện hành](LIBRARY-READER-INDEPENDENT.md), không làm theo phần cài Reader kèm Manager bên dưới.

Ngày 25/11/2026. Giao thức đọc kho dùng chung nằm trong `Rainmaker.Library.Reader.dll`, độc lập QA. Manager triển khai một bản DLL này; Axis dùng giao thức V1 thay vì ghép nhiều file danh mục.

## Nội dung

- Tra cứu 5 loại theo mã/ID ổn định, tìm kiếm, thông tin nhóm/công dụng và phân giải dữ liệu đã xuất.
- Loại mục đã xóa, thùng rác, bản nháp hoặc chưa được xuất khỏi danh sách chọn. Sổ mã vẫn giữ dấu vết để nhận biết cấu hình cũ; không phục hồi ID cũ từ cache.
- Kiểm tra checksum danh mục, hash asset, hàng chờ chưa hoàn tất và snapshot thay đổi giữa lúc đọc/triển khai.
- Axis giữ file cấu hình, ID tài nguyên, giao diện, chức năng vẽ và tên Dim hiện tại. Không đổi dim sang tên RMAX, không sửa DWG nguồn hoặc tài nguyên kho.
- Nhận dữ liệu mới ở lần mở/triển khai tiếp theo; không phải biên dịch DLL cho mỗi lần sửa scale Hatch.
- Thay đổi storage phải do adapter Reader xử lý và giữ API/payload V1. Đã kiểm thử di chuyển toàn bộ vị trí danh mục/asset bằng cùng một consumer DLL không biên dịch lại.

Xem [giao thức V1](LIBRARY-READER-PROTOCOL.md) và [quy định consumer](LIBRARY-CONSUMER-STANDARD.md) khi viết add-in tiếp theo. Reader không có Ribbon hoặc bảng riêng; không nhập Reader vào danh sách công cụ.

## Cập nhật trên mỗi máy

1. Chờ kho đồng bộ đủ. Lưu bản vẽ và đóng AutoCAD trên các máy chuẩn bị cập nhật.
2. Trong build `CAD.Manager/versions/0.10.0/build-...`, chạy `CAP_NHAT_MAY_NAY.ps1 -Apply`. Trình cài xác minh hash, sao lưu DLL cũ và cập nhật Manager/Reader; giữ Registry loader, install.json, Host và catalog.
3. Cập nhật mục **Tạo trục hiện có** trong Manager bằng `Rainmaker.Axis.dll` từ build 1.9.0. Hoặc chạy script trong build: `Install-AxisReader.ps1 -BuildDirectory '<build-Axis>' -Apply` khi CAD đang đóng. Script gọi API nhập gói của Manager; không tự ghi `_Installed` hay JSON catalog.
4. Mở lại AutoCAD, mở bảng Tạo trục và kiểm tra các lựa chọn trước khi vẽ. Reader được nạp từ Manager, gói Axis chỉ có DLL/PDB của Axis.

Nếu hai máy dùng chung kho, chỉ cần chọn gói Axis một lần rồi đợi catalog và `_Installed` đồng bộ. Mỗi máy vẫn phải cập nhật Manager cục bộ lên cùng phiên bản trước khi dùng Axis 1.9.0. Không mở Axis mới với Manager cũ.

Gói Reader riêng trong `_Development` chứa DLL/PDB để tham chiếu phát triển và bằng chứng giao thức. Không sao chép nó thủ công vào các gói consumer. Khi cập nhật Reader V1 tương thích về sau, consumer hiện có không cần sửa chỉ vì JSON vật lý đổi; phải giữ đúng hợp đồng V1 và kiểm thử hồi quy.

## Kiểm thử và giới hạn

Đã kiểm thử Reader trên fixture năm loại, loại bỏ ID/thùng rác/nháp, sai checksum, hàng chờ, snapshot cũ, đổi storage và đường dẫn asset. Đã đọc/xác minh kho thực tế ở chế độ chỉ đọc. Đã chạy hồi quy catalog Manager và Axis trên Core 2021, gồm Dim annotative, lưu/mở DWG, lựa chọn theo ID, Hatch scale 300 ở lần nạp sau, cột/tường, preview tương tác, công thức nhịp và phủ bì. QA 1.11.1 đã chạy qua Manager 0.10.0 trong test cách ly.

Các kiểm thử Core render được bảng WPF nhưng không thay thế thử chuột thủ công trong AutoCAD GUI trên từng máy sau cập nhật. Không thêm API đăng ký vai trò chung, nguồn DWG mới hoặc tự chuyển các skill/add-in khác. Nguồn tích hợp hiện tại vẫn là `ISO_QUANGANH`.
