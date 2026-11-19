# Rainmaker Library — AutoCAD 2021

## Bản sửa 1.3.1 — mở nhóm thư viện trên máy khác

Sửa lỗi `Cannot create and populate list type Newtonsoft.Json.Linq.JToken. Path 'nodes'` khi các add-in nạp Json.NET trong những ngữ cảnh DLL khác nhau. JSON cây nhóm được đọc trực tiếp trong DLL sử dụng nó, giữ kiểm tra kích thước, độ sâu và khóa trùng. Không cần xóa nhóm hay khởi tạo lại DWG.

Đóng AutoCAD, thay toàn bộ gói DLL bằng bản mới rồi mở lại. Với Library đã nhập vào CAD Manager, chọn **Cập nhật file** cho đúng mục Library, chọn `Rainmaker.Library.dll` 1.3.1 cùng các DLL đi kèm và nạp lại. Cập nhật file giữ tab, ghim lệnh và hồ sơ add-in; không sửa dữ liệu thư viện trong DWG.

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
7. Đồng bộ giữ nguyên vị trí các mẫu hiện có. Mẫu mới được xếp nối tiếp vào nhóm. Khi muốn sắp lại, chọn **Xếp gọn nhóm**. Library 1.3.0 bỏ chế độ bật/tắt giữ bố trí và bỏ qua cờ cũ đã lưu trong DWG.
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
| `QALIBREFRESH` | Đọc lại bảng quản lý từ DWG |
| `QALIBZOOM` | Zoom đến nhóm đang chọn; cũng có thể nhấp đúp tên nhóm trong cây |

## Kiểm thử

`QALIBMANAGERTEST` chỉ chạy khi `QALIB_TEST_ROOT` trỏ tới thư mục có marker `.library-manager-test`. Lệnh tạo database cách ly, kiểm tra 5 loại, cây nhóm/ATT, giữ block reference và nội dung ATT, tay kéo không scale mẫu, di chuyển nhóm, bảo toàn định nghĩa, đồng bộ không đổi tọa độ, thêm mẫu và lưu/mở lại DWG.

## Thay block đã xóa trong CAD nhưng Skill vẫn dùng

1. Lưu DWG thư viện, vào **Đồng bộ thư viện CAD → Đọc và so sánh DWG**.
2. Ở block **Thiếu trong CAD**, chọn **Thay bằng block khác…**.
3. Chọn block mới đã đồng bộ. Nếu danh sách trống, nhập và đồng bộ block mới trước.
4. Chọn **Xem bảng thay đổi**. Bảng liệt kê Recipe, Workflow và block chứa thao tác chèn, phiên bản Skill/Template mới, cấu hình hai block và các lỗi cần sửa. Block mới cần cấu hình đúng vai trò/layer, duyệt kích thước và visibility trong danh mục block. Tham số động trong Workflow có thể ánh xạ tên; giá trị không bị tự đổi hoặc bỏ. ATT dùng liên kết LabelAttribute đã khai báo ở block mới.
5. Nếu cần, nhập độ dịch X/Y theo trục cục bộ và góc xoay thêm cho thao tác Workflow. Mặc định giữ nguyên tọa độ/hướng; Recipe dùng cấu hình block mới khi lập bản vẽ sau.
6. Khi không còn lỗi, kiểm tra và tích xác nhận rồi **Thay thế liên kết và loại block cũ khỏi app**. Mọi sửa đổi trên form cần xem bảng thay đổi lại.

Thao tác chỉ cập nhật danh mục và xuất bản phiên bản Skill/Template mới. DWG đang mở, DWG đã vẽ và lịch sử phiên bản cũ giữ nguyên. Có sao lưu và nhật ký phục hồi khi xuất bản bị lỗi. Các kế hoạch dùng block cũ chưa áp dụng phải lập mới. Cần plugin AutoCAD 0.7.19 trở lên trên các máy sử dụng kho để kiểm tra chính sách thay block khi duyệt. App chặn áp dụng thay thế nếu phiên AutoCAD cục bộ đang kết nối còn dùng plugin cũ.

Kiểm thử: `tests/BlockReplacement.Tests` kiểm tra ánh xạ, phiên bản, duyệt đúng bảng thay đổi, chống áp dụng lặp, phục hồi khi khóa file và chặn kế hoạch cũ. `node tools/test-block-replacement-ui.cjs` kiểm tra luồng xem trước/xác nhận trên giao diện.
