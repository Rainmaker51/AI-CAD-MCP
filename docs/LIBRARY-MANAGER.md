# Rainmaker Library — AutoCAD 2021

DLL chính: `Rainmaker.Library.dll`. Gói cần giữ cùng `CAD.LibraryScanner.dll`, `CAD.Contracts.dll` và `Newtonsoft.Json.dll`; không chỉ chép riêng DLL chính.

## Cài trên máy hiện tại hoặc máy khác

Trong thư mục repo, chạy PowerShell:

```powershell
./tools/Install-LibraryManager.ps1 -Build
```

Lưu các DWG đang làm việc và mở lại AutoCAD. Trình cài chỉ đăng ký CAD Manager, không tự nạp Library. Làm theo [hướng dẫn CAD Ribbon Manager](CAD-RIBBON-MANAGER.md) để tự thêm `Rainmaker.Library.dll`, chọn tab và ghim các lệnh. Library không tạo tab riêng; các nút được quản lý trong Manager. Khi cập nhật DLL, mở lại AutoCAD nếu được yêu cầu.

## Quy trình dùng trong CAD

1. Mở đúng DWG thư viện. Chọn **Khởi tạo DWG thư viện** để tạo 5 khu Block, Layer, Hatch, Text, Dim. File khác phải được khởi tạo rõ ràng trước khi dùng các lệnh quản lý; mẫu chưa nhận vào nhóm vẫn được giữ nguyên.
2. Chọn loại trên ribbon hoặc bảng quản lý. Tạo nhóm cha/con nếu cần.
3. Chèn/tạo mẫu trong Model Space, chọn **Nhận vào nhóm** rồi chọn nhóm đích.
4. Chọn **Chuyển nhóm** để đổi nhóm tài nguyên đã quản lý. Chọn **Xếp gọn nhóm** rồi chọn khung để dồn mẫu theo chiều rộng khung.
5. Kéo các grip của khung hoặc dùng **Chỉnh khung** để đổi phạm vi; các mẫu không bị scale hoặc kéo méo.
6. Dùng **Di chuyển cả nhóm** để chuyển khung và toàn bộ mẫu/nhóm con. MOVE thông thường của AutoCAD chỉ di chuyển đối tượng được chọn.
7. **Giữ bố trí** giữ các vị trí khi nhận thêm mẫu. Tắt chế độ này trước khi xếp gọn nhóm hoặc dựng lại toàn bộ thư viện từ app.
8. Ctrl+S để lưu DWG. Trong app, đọc/so sánh rồi chọn **CAD → App** để nhận thay đổi nhóm đã làm bằng add-in. Nếu hai phía cùng đổi nhóm, cần chọn phía sử dụng.

Khung là block ATT có TITLE, PATH, COUNT và ID/KIND ẩn. Tay kéo do add-in cung cấp, thay cho việc lồng nhiều dynamic action vào các mẫu. Không có add-in, khung và ATT vẫn hiển thị; các lệnh/grip quản lý cần DLL được nạp. Cây nhóm được lưu trong DWG và các mẫu nằm độc lập trong Model Space. Khung quản lý được loại khỏi danh mục block khi app quét.

## Cách nhận từng loại

| Loại | Đối tượng chọn trong CAD |
|---|---|
| Block | Block reference có tên; nhận định nghĩa gốc của block động. Các nét rời cần tạo BLOCK trước. |
| Layer | Một đối tượng trên layer cần quản lý hoặc nét mẫu layer. |
| Hatch | Hatch thật; giữ pattern, góc, scale và màu. |
| Text | DBText hoặc MText sử dụng Text Style cần quản lý. |
| Dim | Dimension sử dụng Dim Style cần quản lý. |

Một tài nguyên/định nghĩa có một nhóm. Chọn nhiều lần cùng định nghĩa không tạo thêm mục thư viện. Các block mẫu và ATT được giữ khi xếp gọn; frame chỉ chứa đường bao và nhãn. Xóa nhóm chỉ cho phép khi không còn mẫu hoặc nhóm con; không xóa 5 khu gốc.

## Các lệnh ribbon

| Lệnh | Chức năng |
|---|---|
| `QALIBRARY` | Bảng quản lý 5 loại |
| `QALIBBLOCK`, `QALIBLAYER`, `QALIBHATCH`, `QALIBTEXT`, `QALIBDIM` | Chọn loại |
| `QALIBINIT` | Khởi tạo khu quản lý trên DWG hiện tại |
| `QALIBADOPT` | Nhận mẫu mới vào nhóm |
| `QALIBASSIGN` | Chuyển mẫu đã quản lý sang nhóm khác |
| `QALIBPACK` | Xếp gọn nhóm/khu được chọn |
| `QALIBMOVEGROUP` | Di chuyển cả nhóm và nhóm con |
| `QALIBRESIZE` | Điều chỉnh phạm vi khung |
| `QALIBNEWGROUP`, `QALIBRENAME`, `QALIBDELETEGROUP` | Tạo/đổi tên/xóa nhóm rỗng |
| `QALIBKEEP` | Bật/tắt giữ bố trí thủ công |
| `QALIBREFRESH` | Đọc lại bảng quản lý từ DWG |
| `QALIBZOOM` | Zoom đến nhóm đang chọn; cũng có thể nhấp đúp tên nhóm trong cây |

## Kiểm thử

`QALIBMANAGERTEST` chỉ chạy khi `QALIB_TEST_ROOT` trỏ tới thư mục có marker `.library-manager-test`. Lệnh tạo database cách ly, kiểm tra 5 loại, cây nhóm/ATT, giữ block reference và nội dung ATT, tay kéo không scale mẫu, di chuyển nhóm, bảo toàn định nghĩa, giữ bố trí và lưu/mở lại DWG. Không dùng bản vẽ công trình đang mở làm dữ liệu kiểm thử.
