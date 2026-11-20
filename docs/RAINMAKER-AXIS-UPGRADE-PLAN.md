# Yêu cầu nâng cấp Rainmaker.Axis — chọn thư viện kho và preview

Ngày ghi nhận/nghiên cứu: 20/11/2026. Cập nhật: **đã triển khai phần chọn kho, preview và bố trí ở bản 1.3.0**; xem `docs/RAINMAKER-AXIS.md` và manifest/validation của build để biết kết quả kiểm thử, tình trạng cài đặt. Các tính năng mở rộng ở mục 6 không mặc định được triển khai toàn bộ.

## 1. Phạm vi người dùng yêu cầu

1. Nâng cấp add-in Tạo trục hiện có, giữ một nút mở bảng trên Ribbon và quản lý gói qua CAD Tool Manager.
2. Trong bảng riêng của add-in có phần **Cài đặt thư viện** để chọn các đối tượng từ kho theo mã định danh ổn định.
3. Có **hình preview cập nhật khi nhập thông số** để kiểm tra bố trí trước khi vẽ.
4. Ghi nhận yêu cầu, trao đổi và tham khảo các công cụ tương tự trước; chưa sửa hoặc chạy mã nâng cấp.

Không khôi phục kế hoạch tab “Đang sử dụng kho” đã hủy. Phần lựa chọn tài nguyên nằm trong add-in. Những tính năng ở mục 6 là gợi ý, chưa tự coi là phạm vi được duyệt.

## 2. Hiện trạng cần giữ và vấn đề cần giải quyết

Nguồn hiện tại: `src/Rainmaker.Axis/`, phiên bản source 1.2.0. Khi triển khai phải đọc lại catalog để xác định gói thực tế đang chọn; không suy ra bản chạy từ số version source.

- Đã có nhập nhịp X/Y, nhịp lặp như `3*4000`, ký hiệu bắt đầu, tỷ lệ, chế độ lưới và một trục.
- Hiện bảng `AxisGridDialog` có tóm tắt bằng chữ, chưa có vùng hình preview của lưới.
- Layer đường trục/đầu trục/dim và việc chọn block đầu trục đang gắn với quy tắc cố định trong code.
- `WriteGrid` hiện tạo/tìm dim style thông qua `EntityFactory` rồi gán `Dimexo`, `Dimexe`, `Dimgap` theo tỷ lệ. Khi nâng cấp, cần thay bằng dim style thực tế người dùng chọn từ kho; không tiếp tục ghi đè các thông số này ngoài cấu hình được phép.
- Giữ khả năng làm việc trên AutoCAD 2021, .NET Framework 4.8, x64 và nạp/chạy đúng qua Manager. Lỗi tương thích Bridge 0.7.14 của Axis cũ đã được sửa ở Manager 0.8.3; bản nâng cấp phải có kiểm thử hồi quy này.

Đọc cùng `docs/ADDIN-MANAGER-STANDARD.md`, `docs/RESOURCE-IDENTIFIERS.md` và `docs/ADDIN-DEVELOPMENT-WORKFLOW.md`.

## 3. Cài đặt chọn tài nguyên từ kho

### 3.1. Các vai trò cần có

| Vai trò | Loại tài nguyên | Yêu cầu |
|---|---|---|
| Đường tim trục | Layer (`LAY-…`) | Chọn layer, giữ kiểu nét/màu/độ dày từ thư viện |
| Đầu ký hiệu trục | Block (`BLK-…`) | Có attribute để ghi ký hiệu; kiểm tra đơn vị và tham số động nếu có |
| Layer chèn đầu trục | Layer (`LAY-…`) | Tách khỏi layer đường tim nếu người dùng chọn khác |
| Dim nhịp | Dim (`DIM-…`) | Dùng đúng định nghĩa nguồn |
| Dim tổng | Dim (`DIM-…`) | Cho phép chọn cùng hoặc khác dim nhịp |
| Layer dim | Layer (`LAY-…`) | Mặc định dùng chung cho hai loại dim; tách riêng là tùy chọn đề xuất |
| Chữ độc lập, nếu cần | Text (`TXT-…`) | Không ép thay chữ bên trong block hoặc font đã được dim style tham chiếu |

Hatch không phải nhu cầu bắt buộc của Tạo trục hiện tại; không thêm bộ chọn hatch chỉ để đủ năm loại thư viện.

### 3.2. Cách chọn và lưu

- Cho nhập/dán mã hoặc mở danh sách đúng loại, tìm theo mã/tên/nhóm; hiển thị tên và hình mẫu khi có.
- Hiển thị mã ngắn, `SourceId`, ID ổn định, tên nguồn DWG và đường dẫn đã phân giải trên máy hiện tại.
- Lưu bộ khóa nguồn/loại/ID và chính sách phiên bản. Không chỉ lưu tên đối tượng, đường dẫn tuyệt đối hoặc số thứ tự trong danh sách.
- Các asset biến thể cùng dùng một block nguồn có thể chung mã nguồn; vẫn phải giữ cấu hình biến thể/attribute/tham số riêng, không chỉ lưu mã block rồi làm mất các thiết lập đó.
- Chọn block cần kiểm tra attribute ký hiệu và cho ánh xạ tag khi không có cấu hình hợp lệ; không mặc định mọi block đều có tag giống nhau.
- Đổi tên/chuyển nhóm không làm mất lựa chọn. Mã chưa xác nhận đồng bộ, nguồn thiếu hoặc tài nguyên đã xóa phải báo rõ và chặn thao tác liên quan.
- Việc lưu lựa chọn dùng chung theo add-in phải có định dạng/version và khóa ghi phù hợp với kho hai máy. Không tự ghi trường mới vào `Tools/catalog.json`.

### 3.3. Nhập tài nguyên vào DWG đích

- Phân biệt định nghĩa trùng tên và định nghĩa trùng nội dung. Có chính sách nhập với tên riêng hoặc cập nhật có chủ ý; không tự thay layer/style đang ảnh hưởng đối tượng khác trong DWG.
- Giữ font, mũi tên, độ chính xác, hậu tố/tiền tố đo, hệ số đo và các biến dim của nguồn. Nhập cả dependency cần thiết như text style hoặc block mũi tên nếu dim style tham chiếu.
- Phân biệt dim thường và annotative. Tỷ lệ bố trí lưới, chiều dài nhô đầu trục và khoảng cách đặt chuỗi dim là thông số bố trí; không nhân tỷ lệ chữ/mũi tên hai lần.
- Tham số nào thực sự cần override style phải được khai báo và hiển thị. Không mặc định thay `Dimexo/Dimexe/Dimgap` theo công thức cũ.

## 4. Preview khi nhập thông số

### 4.1. Nội dung hình xem trước

- Hiển thị đường trục, ký hiệu đầu trục, các khoảng cách, dim nhịp và dim tổng.
- Cập nhật khi thay nhịp, số/ký hiệu trục, tỷ lệ hoặc tài nguyên đã chọn.
- Tự vừa khung; có phóng to/thu nhỏ và trở về toàn lưới. Có ô xem chi tiết đầu trục/dim để đọc khi lưới lớn.
- Hiển thị tổng chiều dài X/Y và số trục, không nhầm số nhịp với số trục.
- Nhập sai thì báo ngay tại trường và không để hình cũ được hiểu là preview của dữ liệu mới. Chưa chọn đủ tài nguyên phải có chỉ dẫn rõ.

### 4.2. Cách làm đề xuất

Ưu tiên preview vector WPF cho hình học và khung bố trí; không tạo ảnh AI và không chạy AI mỗi lần gõ. Điều tiết cập nhật khoảng 200–300 ms sau lần nhập cuối, hủy yêu cầu render cũ khi có thay đổi mới.

Dữ liệu lưới đã kiểm tra phải được tính bằng cùng một mô hình dùng cho thao tác vẽ. Tách phần tính bố trí, phần hiển thị và phần ghi AutoCAD. Khi cần thể hiện đúng block động/font/mũi tên, cần có đường render CAD phù hợp; hình WPF giản lược phải ghi rõ giới hạn, không giả vờ giống hoàn toàn bản in.

Đề xuất hai mức:

1. **Trong bảng:** preview cập nhật nhanh khi nhập, chưa ghi đối tượng vào bản vẽ.
2. **Khi đặt vào CAD:** preview tạm theo con trỏ để chọn gốc/hướng nếu triển khai chế độ này; chỉ tạo đối tượng thật khi xác nhận. Hủy không để lại rác hoặc layer/style dở dang.

Mức 1 đáp ứng yêu cầu trực tiếp. Mức 2 là gợi ý cần chốt thêm, không mặc định đưa vào bản đầu.

## 5. Bố cục bảng đề xuất

- Một bảng riêng, dark mode, một nút Ribbon.
- Ba trang bên trong: **Thông số lưới**, **Thư viện**, **Bố trí đầu trục / Dim**.
- Vùng preview giữ được trên màn hình khi chuyển trang cấu hình. Ở cửa sổ hẹp có thể chuyển sang chế độ xem preview riêng, không ép các trường nhập quá nhỏ.
- Cuối bảng có **Lưu cài đặt**, **Chọn điểm đặt / Tạo lưới**, **Hủy**; chọn tài nguyên không tự tạo lưới.
- Tạo lưới phải vào đúng ngữ cảnh lệnh AutoCAD qua Manager. Nếu chuyển sang bảng modeless/PaletteSet, cần thiết kế điều phối rõ; không gọi sửa Database trực tiếp từ sự kiện WPF hoặc luồng render.

## 6. Gợi ý bổ sung, chưa tự triển khai

| Ưu tiên đề xuất | Tính năng | Giá trị |
|---|---|---|
| Cao | Chọn lại lưới đã vẽ để sửa nhịp/ký hiệu | Giảm phải xóa và tạo lại; lưu ID lưới, thông số và danh sách đối tượng thuộc lưới trong DWG |
| Cao | Chọn phía đặt dim: trên/dưới/trái/phải | Tránh dim đè nội dung mặt bằng, có thể bật riêng dim nhịp/tổng |
| Cao | Đầu trục ở đầu, cuối hoặc cả hai | Phù hợp mặt bằng sát khung; chỉnh độ nhô và khoảng hở |
| Cao | Bảng danh sách nhịp có thêm/xóa/sửa dòng | Dễ kiểm tra hơn chuỗi dài, vẫn giữ nhập nhanh `3*4000` |
| Vừa | Góc xoay toàn lưới và quy tắc WCS/UCS rõ ràng | Dùng cho công trình xoay; bản đầu có thể giữ WCS XY |
| Vừa | Đánh số ngược, tiền tố/hậu tố, nhãn riêng từng trục | Hỗ trợ trục phụ như 1a, A1; tùy chọn bỏ chữ dễ nhầm I/O cần người dùng chọn |
| Vừa | Lưu cấu hình mẫu theo bộ môn/công trình | Gọi lại nhịp và tài nguyên đã chọn; giữ cùng định danh khi chuyển máy |
| Vừa | Cảnh báo lưới trùng, nhãn trùng, dim/chữ quá dày | Giảm phát sinh đối tượng trùng và lỗi trình bày |
| Sau | Dim bám theo chỉnh sửa lưới | Cần kiểm thử liên kết hình học thật; không chỉ bật `DIMASSOC` rồi khẳng định đã associative |
| Sau | Nhận tim từ line hiện có, lưới xiên/phụ hoặc lưới cung | Phạm vi lớn hơn; tách khỏi bản chọn kho + preview đầu tiên |

Chọn lại lưới phải xác định đúng đối tượng của add-in, có một bước Undo hợp lý và không đụng các trục người dùng vẽ thủ công. Nếu trục thành viên đã bị sửa/xóa ngoài add-in, cần báo khác biệt trước khi tái tạo.

## 7. Nghiên cứu công cụ tương tự

Tra cứu ngày 20/11/2026; chỉ đọc tài liệu công khai, chưa tải/cài/chạy các sản phẩm. Tính năng bên dưới là mô tả của nhà cung cấp, không phải kết quả thử trên máy người dùng.

| Nguồn | Điểm đã xác minh từ mô tả | Ý tưởng có thể áp dụng |
|---|---|---|
| [Axis grid one click — sxCAD](https://apps.autodesk.com/ACD/en/Detail/HelpDoc?appId=350956700980131361&appLang=en&os=Win64), [trang sản phẩm](https://marketplace.autodesk.com/apps?appLang=en&id=350956700980131361&os=Win64) | Có nhập khoảng cách X/Y và quản lý thêm/sửa/xóa lưới; trang sản phẩm liệt kê AutoCAD 2021 | Thao tác nhịp rõ ràng và sửa lưới đã tạo |
| [APLUS — hướng dẫn chính chủ, trang in 66–67](https://cadaplus.com/download/aplus_users_guide.pdf#page=66) | AX1/AXA đánh ký hiệu số/chữ; AXP thiết lập tiền tố, hậu tố, layer và cỡ ký hiệu | Tùy chỉnh cách đánh nhãn và cỡ đầu trục. Đây là tài liệu phiên bản cũ, không dùng để kết luận tương thích bản APLUS hiện tại với AutoCAD 2021 |
| [AutoCAD Architecture — Automatic AEC Dimensions](https://help.autodesk.com/cloudhelp/2020/ENU/AutoCAD-Architecture/files/GUID-986C5312-D543-4D37-B118-88322BCF986B.htm) | Có chọn style và vị trí dim lưới, cập nhật dim theo lưới | Tham khảo hành vi dim khi sửa lưới; đây là chức năng của Architecture toolset, không giả định có cùng đối tượng AEC trong AutoCAD thường |
| [Smart Slope Generator](https://apps.autodesk.com/ACD/en/Detail/Index?id=3129029253557500596&mode=preview) | Nhà phát triển mô tả preview dùng cùng engine với kết quả ghi, có Undo và kiểm soát layer | Chỉ tham khảo kiến trúc preview nhất quán; sản phẩm này vẽ taluy, không phải add-in tạo trục |

Về tỷ lệ, [tài liệu Autodesk về annotative objects](https://help.autodesk.com/view/ACD/2026/ENU/index.html?guid=GUID-C33D5B68-5A3F-4AF6-9AFB-F74DAB8B6722) giải thích dim/style và annotation scale. Tài liệu này là bản mới; khi triển khai phải kiểm tra API và hành vi trên AutoCAD 2021 thực tế.

Các tính năng đề xuất của Rainmaker.Axis ở mục 6 là suy luận thiết kế từ nhu cầu và tham khảo, không khẳng định mọi sản phẩm trên đều có các tính năng đó.

## 8. Thứ tự triển khai dự kiến, chưa thực hiện

1. Chốt vai trò và tài nguyên kho, cơ chế lưu/phân giải ID; sửa đường tạo dim để dùng đúng style chọn.
2. Làm preview trong bảng từ cùng dữ liệu lưới và tài nguyên; giữ tính năng nhập nhịp/đánh số hiện có.
3. Thêm các tùy chỉnh bố trí được người dùng chấp thuận.
4. Kiểm thử GUI + DWG trắng trên AutoCAD 2021, qua Manager; kiểm tra kết quả dim, block, layer, tỷ lệ và Undo.
5. Tăng version, đóng gói theo quy trình `_Development/Rainmaker.Axis/versions/...`; không thay DLL đang cài trước khi kiểm thử và bàn giao.

## 9. Tiêu chí nghiệm thu cho yêu cầu chính

- [ ] Chọn/dán mã trong add-in và phân giải đúng tài nguyên kho; đổi tên/nhóm không mất liên kết.
- [ ] Dim nhịp/tổng dùng đúng style đã chọn và dependency, không tự ghi đè công thức tỷ lệ cũ.
- [ ] Preview phản hồi khi thay nhịp, nhãn, tài nguyên; báo rõ giới hạn hiển thị và dữ liệu nhập lỗi.
- [ ] Các vị trí trục, đầu trục, nhãn và điểm đo trên preview khớp dữ liệu dùng để vẽ.
- [ ] Chọn điểm đặt/hủy hoạt động đúng, không ghi rác vào DWG; Undo phục hồi được thao tác.
- [ ] Một nút Ribbon, một bảng riêng; cài/cập nhật qua Manager giữ cấu hình người dùng.
- [ ] Không suy đoán kho hoặc chọn thay tài nguyên hỏng; không cần AI để tạo preview hoặc dựng lưới xác định.

Các mục yêu cầu/nghiên cứu ở trên được giữ để đối chiếu. Chỉ những chức năng được ghi trong hướng dẫn bản 1.3 và validation của build mới được xem là đã triển khai/kiểm thử.
