# AI CAD MCP — không gian làm việc 0.4

App mới phát triển theo `../PLAN MCP NEW.md`. Giao diện tại `design/` là cơ sở UX; app thật nằm trong `src/`, không chạy bằng dữ liệu mô phỏng.

## Chat và tài khoản AI — bản mở rộng 0.2

Mở `Tai khoan AI.cmd` để vào thẳng **Tài khoản AI** trong bản `p0-p6`, hoặc chạy `tools/Start-App.ps1 -Release p0-p6 -Page account`. Bản `app` 0.1 vẫn giữ riêng dữ liệu và chưa có màn hình này.

- **Liên kết Codex đã đăng nhập** dùng phiên sẵn có trên tài khoản Windows hiện tại. Đăng xuất tại AI CAD chỉ ngắt liên kết app.
- **Đăng nhập tài khoản mới** mở luồng device login chính thức, với hồ sơ `CODEX_HOME` riêng trong dữ liệu AI CAD. App không nhận mật khẩu. Có thể **Hủy đăng nhập** trong lúc chờ.
- Khi đã kết nối, **Đăng xuất khỏi app** dừng phản hồi chat và đăng xuất phiên riêng (nếu đang dùng phiên riêng).
- Tài khoản AI dùng cho chat; duyệt ghi DWG vẫn ở palette AutoCAD.

Kiểm thử tài khoản dùng CLI giả lập trong tiến trình kiểm thử, không sử dụng thông tin đăng nhập thật: cách ly hồ sơ, đăng xuất phiên riêng, ngắt liên kết không đăng xuất Codex desktop, hủy đăng nhập không tự kết nối lại. Bộ managed tests hiện đạt 45/45. Đăng nhập với tài khoản thật do người dùng thực hiện trong giao diện.

### Sửa lỗi chat tiếng Việt

Tiến trình Codex nhận stdin/stdout/stderr bằng UTF-8. Bản trước dùng mã hóa mặc định Windows nên yêu cầu có dấu có thể thất bại trước khi gọi model (`input is not valid UTF-8`). Managed tests kiểm tra vòng truyền ký tự tiếng Việt và Unicode qua tiến trình con; kiểm thử live trong `tests/results/chat-live-utf8.json` xác nhận phản hồi từ tài khoản thật. Lỗi CLI được lưu có giới hạn và che token tại `data-p0-p6/last-ai-error.json`.

Chat chỉ lập kế hoạch trong khung đang chọn. Với yêu cầu vẽ tự do, dùng khung có Skill `CAD_TOOLS`; gõ `AICADTASK` trong AutoCAD để đặt khung mới nếu cần, rồi quét lại. Chọn nhầm Skill sẽ nhận hướng dẫn và không tự chạy một workflow khác.

## Thư viện 0.4

Thư viện Template nhận JSON/DWG/DWT, chọn block/layer/style, review rồi xuất bản. Có cập nhật metadata, phiên bản, rollback và xuất JSON. Skill có biểu mẫu tạo/cập nhật. Hướng dẫn đầy đủ tại [docs/LIBRARY-GUIDE.md](docs/LIBRARY-GUIDE.md). Các tệp nguồn được giữ theo checksum; block native cần plugin 0.4, do `Mo AI CAD.cmd` nạp từ `artifacts/p0-p6-library`.

## Không gian làm việc hợp nhất

Mục **Không gian làm việc** gộp chat, triển khai và phiên/bản vẽ. Thanh trên chọn phiên AutoCAD, DWG và khung nhiệm vụ. Chat nằm bên trái; phạm vi, danh sách task và chi tiết/kế hoạch nằm bên phải. Ba tab cũ không còn xuất hiện riêng. Trang thích ứng về một cột trên màn hình nhỏ. Giao diện tối là mặc định; nút đổi giao diện ghi nhớ lựa chọn trong trình duyệt.

**Cài đặt → Chế độ xem trước** có OFF, SMART, ALWAYS và nút lưu. OFF bỏ nét xanh; SMART hiển thị khi có nguồn/giả định, trên 5 thao tác, block/cung/dimension/hatch; ALWAYS luôn hiển thị. Các chế độ đều kiểm tra hình học bằng staging và yêu cầu xác nhận cục bộ trước khi ghi DWG. Cấu hình lưu tại `data-p0-p6/preview-preferences.json`; mặc định thử nghiệm vẫn ALWAYS. Plugin 0.3 ở `artifacts/p0-p6-native` hỗ trợ đủ ba chế độ. `Mo AI CAD.cmd` nạp đúng bản này. Phiên đã nạp plugin cũ giữ ALWAYS; Host chặn OFF/SMART và hướng dẫn nạp bản mới.

Mã kiểm tra plan được tạo bởi utility net48 `CAD.HashCompat`, cùng runtime/serializer với AutoCAD 2021. Cách này giữ nguyên toàn bộ tọa độ và kiểm tra tính toàn vẹn, tránh sai khác định dạng hoặc parse double giữa .NET 10 và .NET Framework. Đã đối chiếu 10.008 cặp tọa độ với bộ kiểm tra native nguyên bản. Kiểm thử Apply tương tác đã xác nhận một polyline thực trong DWG, QA PASSED và receipt PRESENT (`tests/results/chat-apply-live.json`). Không sửa hash/trạng thái của plan lỗi cũ. Nút **Tạo lại kế hoạch · không gọi AI** tạo ID mới sau khi kiểm tra plan chưa có execution/receipt và khung/Skill/Template không đổi; từ chối DONE, DISPATCHED, UNKNOWN hoặc dấu vết thực thi.

## Mở app bằng nhấp đúp

Mở `Mo AI CAD.cmd` trong thư mục dự án để chạy app và AutoCAD 2021. File này gọi `artifacts/launcher/AICad.Launcher.dll` bằng .NET có sẵn, không gọi PowerShell và không thay ExecutionPolicy. Trình khởi chạy kiểm tra Host qua health endpoint, mở URL có phiên đăng nhập cục bộ, dùng lại phiên AutoCAD đã nạp plugin 0.3 hoặc tạo một bản vẽ mới và nạp đúng DLL. Các bản vẽ đang mở được giữ nguyên. Lỗi khởi chạy được ghi vào `data-p0-p6/launcher-last.json`; cửa sổ CMD giữ lại thông báo nếu lỗi.

`Tai khoan AI.cmd` dùng cùng trình khởi chạy để mở trang tài khoản. `tools/Start-App.ps1` còn dành cho môi trường phát triển; Windows PowerShell trên máy hiện tại chặn script nên không dùng nó làm hướng dẫn nhấp đúp.

## Chạy trên máy hiện tại

1. Nhấp đúp `Mo AI CAD.cmd`: trình khởi chạy mở app, dùng lại phiên plugin 0.4 đã kết nối hoặc mở AutoCAD 2021 mới và nạp đúng plugin.
2. Trong cửa sổ AutoCAD mới, dùng Ctrl+O để mở DWG cần làm. Gõ `AICAD` nếu muốn hiện palette.
3. Trong app chọn **Không gian làm việc**, đúng phiên/DWG và **Quét khung**.
4. Gõ `AICADTASK` trong AutoCAD nếu chưa có khung: chọn điểm đặt, nhập mã Skill. Plugin lấy Template từ Skill. Quét lại rồi chọn khung.
5. Nhập yêu cầu ở chat hoặc chạy Skill có dữ liệu đã xác nhận. Xem và duyệt kế hoạch tại palette AutoCAD; lưu DWG theo cách thông thường.

Không mở trực tiếp `index.html`. Không cần NETLOAD thủ công khi dùng launcher. Plugin mới ở `artifacts/p0-p6-library/AutoCAD`; bộ `artifacts/app` là phiên bản 0.1 giữ lại từ giai đoạn trước.

## Thành phần đã triển khai

- Host C#/.NET 10 trên loopback, token ngẫu nhiên, kiểm tra Origin và phiên Windows.
- MCP Server dùng official C# SDK/stdio với 13 tool; không có tool cấp approval hoặc Apply từ xa.
- Plugin .NET Framework 4.8 cho AutoCAD 2021: session/document/revision, IPC Named Pipes có ACL và kiểm tra đường dẫn tiến trình Host.
- Đọc DWG mở, layer/block, metadata entity phân trang; quét AI_TASK, transform đường bao, duplicate ID và overlap.
- Template Starter dùng chung và Skill JSON declarative có kiểm tra tool/input/dependency, nạp từ thư mục, bật/tắt/nhập không build lại core.
- Các thao tác: Line, Polyline, Circle, Arc 90°, Text, Insert Block; preview transient và duyệt tại palette.
- Apply theo plan đã hash, kiểm tra lại file/revision/resource/scope, DocumentLock/transaction, guard Undo/read-only, receipt trong DWG.
- Physical drawing mutex và durable execution fence: trạng thái chưa rõ không tự hết hạn hoặc tự retry.
- Lưu kế hoạch/lịch sử trên máy; nhận PDF/ảnh có checksum và trạng thái cần hiệu chuẩn.

## Giới hạn rõ ràng của 0.1

Đây là bản đầu để kiểm chứng luồng tích hợp, chưa hoàn tất P0–P6 của kế hoạch. Chỉ ghi Model space, đơn vị mm; task có biên polyline thẳng, lồi. Workflow placement hiện yêu cầu biên bốn đỉnh. Không ghi đồng thời cùng một physical DWG.

Chưa có: OCR/PDF vector → mặt bằng; bộ mặt cắt cầu thang/WC/nhà hoàn chỉnh; text/dim/hatch/style nâng cao; transform/trim/offset; quản trị template publish/rollback đầy đủ; queue/DAG scheduler bền vững; checkpoint/restore tự động; installer và hỗ trợ nhiều phiên bản AutoCAD.

T001/T003 dùng Skill chèn cửa/trục thử nghiệm. T002 dùng DRAW_PLAN và cao độ xác nhận để vẽ khung/ghi chú, **không phải Skill dựng mặt cắt cầu thang hoàn chỉnh**. Starter không được xem là tiêu chuẩn công ty.

Plan DONE là receipt của một transaction đã commit; nếu người dùng Undo hoặc đóng mà không lưu, lịch sử không tự biến thành bằng chứng dữ liệu vẫn còn trong DWG. Plan DISPATCHED/UNKNOWN hoặc fence chưa giải quyết chặn ghi tiếp. Không tự xóa fence hay đổi trạng thái để chạy lại; cần đối soát receipt/bản vẽ trước khi phục hồi.

## Build và kiểm thử

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1 -Test
powershell -ExecutionPolicy Bypass -File .\tools\Run-NativeTests.ps1
```

Build và khởi chạy sử dụng SDK .NET trong `.tools/dotnet`; gói NuGet nằm tại `.tools/nuget/packages`. Dự án chạy độc lập với thư mục dự án trước đây. Plugin cần Autodesk SDK DLL tại thư mục cài AutoCAD 2021. Không phân phối DLL Autodesk cùng app.

Không publish đè DLL đã nạp trong AutoCAD; đóng phiên thử nghiệm trước khi build lại plugin. `-Component AutoCAD`, `-Component Host` hoặc `-Component MCP` cho phép build riêng.

Chạy Host trước khi dùng `tools/verify_host.py` để kiểm tra HTTP auth và MCP stdio. Kết quả managed/Host nằm trong `tests/results`; kết quả CAD nằm trong mỗi thư mục native fixture riêng.

## Dữ liệu và bảo mật

`data/` chứa token local, danh tính phiên, nguồn nhập và nhật ký; bị loại khỏi Git và được giới hạn quyền theo Windows user. Không gửi `data/host.json` hoặc tệp session cho người khác. Hệ thống hiện tin cậy cùng tài khoản Windows; không được thiết kế để chống mã độc chạy với quyền của chính user.

`artifacts/app/install.json` là cấu hình đường dẫn máy hiện tại. Khi chuyển máy cần build/configure lại; không chuyển token/PID/pipe/phiên cũ. Chi tiết tại `docs/architecture.md` và `docs/security-policy.md`.


Dữ liệu nặng đã được phân nhóm trong `storage/01-test-data`, `02-app-data`, `03-python-ocr`, `04-builds`. Xem [hướng dẫn bố cục và chuyển máy](storage/README.md). Trình mở app vẫn là `Mo AI CAD.cmd`.


Bản 0.5 đã tách kho chung ra ngoài app, thêm đường dẫn kho trong Cài đặt, gộp Quang Anh 288 block và hỗ trợ điểm AI_OUTPUT_POINT trong DWG đang kết nối. Xem [hướng dẫn kho và điểm đặt](docs/WAREHOUSE-AND-PLACEMENT.md).

Hướng dẫn block động, bộ layer chung và trình tạo Skill: [docs/BLOCK-LAYER-SKILL-GUIDE.md](docs/BLOCK-LAYER-SKILL-GUIDE.md).
