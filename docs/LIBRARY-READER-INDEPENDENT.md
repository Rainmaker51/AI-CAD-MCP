# Reader cài riêng — Manager giữ 0.10.0

Theo yêu cầu ngày 25/11/2026, Reader được tách khỏi gói Manager. Manager vẫn mang số **0.10.0**, tiêu đề lấy đúng version DLL. Đây là build mới có `readerDeployment: separate` trong manifest; gói 0.10.0 cũ được giữ bất biến để đối chiếu.

## Tự cài Reader

1. Dùng Manager 0.10.0 bản tách Reader.
2. Mở **CAD Tool Manager → Thành phần dùng chung**.
3. Bấm **Cài / cập nhật Reader**, chọn `Rainmaker.Library.Reader.dll` trong `package` của build Reader 1.0.1 đã bàn giao.
4. Trạng thái hiện phiên bản Reader đã cài. Nếu Reader cũ đã nạp thì mở lại AutoCAD; lần cài đầu có thể dùng ngay.

Không dùng Thêm file tại tab .NET Add-in hoặc NETLOAD Reader. Reader không có nút Ribbon. Nếu muốn dùng PowerShell, chạy `Install-LibraryReader.ps1 -BuildDirectory '<build-Reader>' -Apply` trong gói Reader. Mỗi máy tự cài Reader một lần, từ cùng một gói đã đồng bộ.

Manager không còn phụ thuộc bắt buộc vào Reader để khởi động. Khi chưa cài Reader, Tạo trục sẽ báo cài thành phần còn thiếu; sau khi cài, chạy lại. **Giữ nguyên DLL Tạo trục 1.9.0 hiện có**, không cần cài lại Axis cho thay đổi này.

Bản cài Reader nằm trong dữ liệu cục bộ của Manager, tách khỏi `Tools/catalog.json` và `_Installed` của add-in. Con trỏ `current.json` chọn gói và lưu hash/revision; cập nhật có lịch sử, không ghi đè DLL đang nạp. Manager nạp một Reader chung cho các consumer. Thay đổi dữ liệu kho không cần cài lại Reader; thay đổi cấu trúc phải giữ giao thức V1.

Gói Manager có bộ cập nhật riêng để thay Manager và chuyển DLL Reader từng nằm cạnh Manager vào bản sao lưu. Bộ cập nhật **không tự cài Reader**, không đổi catalog, tài nguyên kho, cấu hình Axis hoặc gói Axis. Chọn Reader thủ công như trên.

## Bàn giao 25/11/2026

- Manager **0.10.0**, build `14ac870f2b74`: **đã cài** trên máy này, không kèm Reader. DLL SHA256 `90a9d8d0eff48403b494a968a0b3c7aa5e150eed3c8940647be0ae5139b64491`.
- Reader **1.0.1**, build `d1ebb94cc5a6`: **chưa cài**, người dùng tự chọn. DLL SHA256 `5aaf0d27de5388c2b717ddb75b48e740783083afe7deb7321f9dedf2ca0da744`.
- Mỗi gói nằm tại `Tools/dotnet/_Development/<Tên>/versions/<version>/build-<mã>/`; chọn DLL Reader từ `package/Rainmaker.Library.Reader.dll`.
- Source commit `7de2051278487424d290f7375e9da1445b674a20` cộng `source.patch` trong mỗi build, SHA256 `6faefc1f7ef421abbf1ca27de9a6a45aa41054f8c925a10a443b9faadda8b5a1`. Chưa commit/push thay đổi workspace.

Kiểm thử đạt: Manager không có/chưa cài Reader vẫn mở; thử chạy Axis khi thiếu Reader rồi cài và chạy lại cùng phiên; dùng đúng DLL Axis 1.9.0 hiện có vẽ 5 đường trục, 10 đầu trục và 10 Dim; kiểm tra hash/ID/revision, một bản Reader chung, hồi quy Ribbon, QA 1.11.1 và giao thức Reader V1. Đã render và xem ảnh tab Thành phần dùng chung. Chưa thử chuột thủ công trong AutoCAD GUI sau cài.

Catalog, cấu hình Axis, byte DWG thư viện và gói Axis được xác minh giữ nguyên. Đã dọn 1.208,4 MiB working/profile thử của phiên; giữ bằng chứng trong build và hồ sơ `_Development/CAD.Manager/cleanup-records/separate-reader-20261125-1642/`.
