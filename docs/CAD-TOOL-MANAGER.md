# CAD Tool Manager — AutoCAD 2021

Gõ `AICADTOOLS` trong phiên AutoCAD đã nạp plugin AI CAD 0.7.0. `Mo AI CAD.cmd` dùng lại phiên 0.7.0 hoặc mở một DWG mới với đúng plugin; các bản vẽ đang mở vẫn được giữ.

```mermaid
flowchart TD
    A[CAD TOOL MANAGER — C# / WPF Palette]
    A --> L[LISP MANAGER]
    A --> N[.NET ADD-IN MANAGER]
    A --> M[MCP CONNECTOR]
    L --> F[LSP / FAS / VLX]
    N --> D[DLL — .NET Framework 4.8 x64]
    M --> C[Codex / Claude Desktop]
```

## Dùng trong AutoCAD

- `AICADLISP`: mở tab LISP.
- `AICADADDINS`: mở tab .NET.
- `AICADMCP`: mở tab MCP Connector.
- **Thêm file**: sao chép công cụ vào kho; chưa nạp hoặc chạy mã.
- **Cập nhật file**: tạo gói mới, giữ ID, tên, nhóm, ghi chú; giữ gói cũ để đối chiếu.
- **Lưu hồ sơ**: lưu tên, nhóm, công dụng/ghi chú, bật/tắt và lệnh LISP.
- **Nạp / nạp lại** và **Chạy lệnh**: chạy tại DWG đang chọn, khi AutoCAD không bận lệnh khác. Nếu đổi DWG hoặc gói công cụ trong lúc chờ, yêu cầu bị từ chối.
- **Xóa / khôi phục**: chuyển mục vào/ra thùng rác, không xóa payload. Không có tự nạp khi khởi động.

Danh mục ở `<Kho>/Tools/catalog.json`; payload được lưu theo ID và checksum bằng đường dẫn tương đối. Khi dùng trên hai máy, đồng bộ toàn bộ `Tools` cùng kho. Cache DLL và nhật ký chạy nằm trong DataRoot của từng máy.

## AutoLISP

Nhận `.lsp`, `.fas`, `.vlx`. LSP được dò tên `defun c:...`, bỏ qua comment dòng, comment `;| ... |;` và chuỗi. FAS/VLX là định dạng đã biên dịch, cần nhập tên lệnh rồi lưu. Lệnh nạp/chạy được chuyển cho AutoCAD, kết quả và lỗi xuất hiện ở command line.

Nhập hiện sao chép một file LISP chính. Với công cụ nhiều file/DCL hoặc VLX namespace riêng, chuẩn bị các phụ thuộc và support path theo yêu cầu của công cụ. Manager không thay đổi `SECURELOAD` hay `TRUSTEDPATHS`. Không tích hợp trình biên dịch FAS/VLX hoặc debugger VS Code vào palette.

Có file mẫu không sửa hình học: `content/cad-tools/samples/hello.lsp`, lệnh `QATOOLSHELLO`.

## .NET add-in

Chọn DLL .NET Framework 4.8 x64 tương thích AutoCAD 2021. Nhập DLL cùng các dependency DLL/PDB nằm cạnh nó; không sao chép các DLL API AutoCAD hoặc lõi AI CAD. Nên đặt riêng từng công cụ trong một thư mục trước khi nhập.

Manager nạp bản sao vào cache, dò các `CommandMethod` public trả về `void`, không có tham số và có thể khởi tạo class. Hỗ trợ method static/instance, phân biệt command document với `CommandFlags.Session`. Các implementation `IExtensionApplication` được khởi tạo khi nạp; bản cũ được gọi `Terminate` khi nạp lại.

Danh sách lệnh tìm được được lưu vào hồ sơ để MCP tra cứu. Không thực thi `.cs` trực tiếp: biên dịch thành DLL trước. Add-in phụ thuộc cơ chế đăng kí riêng của NETLOAD hoặc command flags đặc biệt cần kiểm tra riêng.

CLR .NET Framework trong AutoCAD 2021 không gỡ riêng từng assembly. Nạp lại DLL chính dùng shadow copy; đổi dependency đã nạp với cùng assembly identity sẽ yêu cầu mở lại CAD. Xóa/tắt hồ sơ chặn các lần gọi tiếp từ Manager, không tương đương unload assembly hay hoàn tác thay đổi do công cụ đã chạy.

## MCP Connector

Tab MCP cung cấp cấu hình Claude Desktop JSON và Codex TOML theo đường dẫn cài đặt hiện tại; chọn client rồi **Sao chép cấu hình** để gộp vào cấu hình của client. Không ghi đè cấu hình client hoặc xử lí tài khoản. Sau khi đổi cấu hình, kết nối lại MCP phía client.

Server stdio vẫn là `MCP.Server` hiện có. Tool mới `list_cad_utilities` chỉ đọc danh mục đang hoạt động, công dụng, trạng thái, lệnh và đường dẫn. Các tool lập kế hoạch/xem trước và việc duyệt ghi DWG hiện có vẫn giữ nguyên; MCP không có endpoint chạy mã LISP/DLL tùy ý.

Chạy server thủ công: `tools/Start-Mcp.ps1 -Release p0-p6`.

## Kiểm tra

- `tests/ToolManager.Tests`: parser LISP, metadata, đường dẫn tương đối, lịch sử gói, thùng rác, cạnh tranh cập nhật, kiểm tra toàn vẹn và cấu hình MCP.
- `tests/ToolManager.Fixture` + `AICADTOOLSTEST` trong Core Console: dò lệnh DLL, chạy V1 rồi nạp/chạy V2 không khởi động lại, kiểm tra DLL nguồn không bị khóa, sinh script nạp/chạy LISP thực tế. Chỉ chạy trong thư mục test có marker `.aicad-tools-test`.
- Nguồn tham khảo và giấy phép: [TOOL-MANAGER-SOURCES.md](../third_party/TOOL-MANAGER-SOURCES.md).

## Giao diện dark mode 0.7.2

Palette dùng theme tối riêng cho cả ba tab AutoLISP, .NET Add-in và MCP; không thay đổi theme của các cửa sổ AutoCAD khác. Danh sách và bộ lọc ở phía trên, hồ sơ chỉ hiện khi chọn công cụ, nút nạp/chạy giữ ở cuối pane. File nguồn/SHA256 và phần thiết lập nâng cao nằm trong mục có thể mở rộng. Khi palette hẹp, phần hồ sơ cuộn riêng để giữ các thao tác chạy luôn nhìn thấy.

Theme WPF áp dụng cho bảng, ô nhập, menu chọn, checkbox, thanh cuộn, tab, nút, trạng thái focus và disabled. Đã render thử 420×640, 620×860, 900×900; màu chữ thường đạt tương phản trên 4.5:1. Plugin 0.7.2 được nạp ở phiên AutoCAD mới khi mở `Mo AI CAD.cmd`; gõ `AICADTOOLS` để mở palette.
