# Rainmaker.Axis 1.3.3 — yêu cầu và đề xuất nâng cấp

Tiến độ triển khai: mã nguồn và kiểm thử Core của phạm vi chính đã được thực hiện ở đợt 21/11/2026. Xem [hướng dẫn 1.3.3](RAINMAKER-AXIS.md) và manifest/validation của gói để đối chiếu phần đã kiểm thử và các giới hạn (gồm chưa kiểm thử GUI trực tiếp). Các đoạn “chưa triển khai” bên dưới là trạng thái lúc duyệt kế hoạch, không thay cho trạng thái bàn giao. Các đề xuất bổ sung không tự được coi là đã triển khai.

Ngày ghi nhận/nghiên cứu: 21/11/2026.

**Trạng thái: phạm vi đã được người dùng duyệt, gồm bốn quyết định tại mục 1.4–1.6 và thay đổi cột tại mục 1.2; chưa triển khai 1.3.3, chưa đổi version source, build hay cập nhật gói cài.** Các thay đổi sửa dim 1.3.2 đang có trong working tree thuộc công việc trước, không phải triển khai tài liệu này. Catalog tại lúc rà soát đang chọn Axis 1.3.2; ảnh người dùng gửi có tiêu đề 1.3.1, không dùng ảnh để kết luận DLL đang chạy trong RAM.

Đọc cùng `ADDIN-MANAGER-STANDARD.md`, `ADDIN-DEVELOPMENT-WORKFLOW.md`, `RESOURCE-IDENTIFIERS.md` và `RAINMAKER-AXIS.md`.

## 1. Yêu cầu người dùng đã đưa ra

### 1.1. Chỉ còn hai tab chính

- **Thông số**: gom toàn bộ nội dung nhập nhịp, ký hiệu, chế độ Lưới trục/Một trục và nội dung Bố trí hiện tại vào đây.
- **Thư viện**: chọn tài nguyên kho cho trục, dim, cột, tường và hatch; hiển thị mã/tên đã chọn, nguồn và liên kết hợp lệ.
- Bỏ tab **Bố trí** và hai tab con **Lưới trục/Một trục**. Giữ hai chế độ dưới dạng lựa chọn trong Thông số, không xóa tính năng Một trục.
- Giữ preview bên phải và một nút Ribbon do CAD Tool Manager quản lý.

### 1.2. Cột tham số do add-in tự tạo — đã duyệt, thay yêu cầu lấy cột từ kho

- Add-in tự dựng cột vuông, chữ nhật hoặc tròn theo B×H/đường kính, tạo thành block rồi chèn tại các giao điểm được chọn.
- Cùng hình dạng/kích thước dùng chung định nghĩa block; khác kích thước tạo loại khác, không sinh một định nghĩa riêng cho từng vị trí.
- Đường bao cột kín, base point tại tim; hỗ trợ căn tim/mép/góc, xoay và lệch vị trí. Lưu hình dạng, kích thước và liên kết giao điểm để sửa sau này.
- **Bỏ lựa chọn block cột từ kho trong phạm vi 1.3.3**, không giữ như phương án phụ. Layer nét cột và tài nguyên hatch nếu dùng vẫn chọn từ kho theo ID ổn định. Việc này không xóa block cột đang có trong kho hoặc bản vẽ.

### 1.3. Vẽ tường theo các trục đã vẽ, kèm hatch

- Có thao tác chọn lưới/trục đang có trong CAD, chọn đoạn cần vẽ tường rồi tạo nét tường và hatch.
- Layer tường, layer hatch và cấu hình hatch lấy từ kho; không chỉ lấy tên pattern rồi bỏ qua scale/góc/màu.
- Biên tường phải kín, xử lý các góc và giao nhau; không thay nét tường bằng polyline có width lớn hay SOLID che mặt bằng.
- Xác nhận bổ sung của người dùng: **tường phải vẽ kín và trừ phần cột**. Đây là yêu cầu bắt buộc, không phải tùy chọn đề xuất. Mỗi vùng tường còn lại sau khi trừ phải có đường bao kín; hatch chỉ phủ vùng tường, không phủ diện tích cột.

### 1.4. Sửa lại lưới — đã duyệt đưa vào 1.3.3

- Chọn lưới đã tạo để sửa nhịp và ký hiệu, xem preview rồi bấm cập nhật.
- Lưu GridId, phiên bản dữ liệu, thông số, ID trục/giao điểm và thành viên trong DWG; định danh không phụ thuộc nhãn trục đang hiển thị.
- Có bước nhận diện/nhận quản lý riêng đối với lưới cũ chưa có metadata; không tự nhận tất cả đối tượng cùng layer là một lưới.

### 1.5. Cập nhật cột/tường liên kết — đã duyệt

- Khi sửa nhịp, cột và tường liên kết được cập nhật theo sau khi người dùng xem preview và bấm áp dụng. Hatch và dim liên quan phải tính lại theo hình học mới.
- Hiển thị số lượng thêm/sửa/xóa; đối tượng đã chỉnh tay ngoài add-in phải báo khác biệt và cho xử lý trước, không âm thầm ghi đè.
- Không tự cập nhật liên tục bằng reactor. Giao điểm hoặc đoạn trục bị bỏ cần được đối chiếu theo ID để báo cột/tường mất liên kết trước khi áp dụng.

### 1.6. Giữ tên dim thư viện, bỏ RMAX — đã duyệt

- Dim nhập mới giữ đúng tên trong kho; không tiếp tục sinh tên `RMAX_...` hoặc đổi tên các dependency thành tên riêng không cần thiết.
- Chưa có tên trong DWG: nhập style cùng dependency; đã có cùng tên/cùng thông số: dùng lại. Phải đối chiếu nội dung và dependency, không chỉ so tên.
- Trùng tên nhưng khác thông số: hiển thị khác biệt và hỏi người dùng trước khi cập nhật; nêu rõ thay định nghĩa có thể ảnh hưởng dim khác đang tham chiếu. Không tự ghi đè hoặc âm thầm quay về tên RMAX.
- Với RMAX do bản cũ tạo: có thao tác đối chiếu/chuyển các dim được xác nhận về style thư viện tương ứng, giữ chính sách tỷ lệ đúng của 1.3.2. Chỉ dọn định nghĩa RMAX khi không còn tham chiếu và AutoCAD cho phép xóa; không xóa hàng loạt chỉ dựa vào tiền tố hoặc làm mất đối tượng dim.
- RMAX chứa override/cấu hình khác, không xác định được nguồn hoặc đang ảnh hưởng đối tượng ngoài phạm vi phải báo rõ trước khi chuyển. Kiểm thử lưu/mở lại và Undo cả bước chuyển/dọn.

## 2. Bố trí bảng đề xuất để thực hiện yêu cầu

Đây là cách tổ chức dự kiến, chưa phải giao diện đã xây dựng.

**Thông số** dùng các nhóm thu gọn, không thêm tab con:

1. **Trục**: chế độ Lưới trục/Một trục; tạo mới hoặc chọn trục đã có; nhịp X/Y, ký hiệu, tỷ lệ, điểm đặt.
2. **Dim và đầu trục**: các phía đặt dim, khoảng cách trục → dim nhịp, dim nhịp → dim tổng, đầu ký hiệu và độ nhô. Giữ các trường đang có trong Bố trí.
3. **Cột**: bật/tắt chèn; chọn giao điểm; hình vuông/chữ nhật/tròn, B/H hoặc đường kính; góc xoay, lệch X/Y, điểm neo tim/mép/góc; kiểm tra số lượng trước khi chèn.
4. **Tường và hatch**: bật/tắt; chọn đoạn trục; bề dày, tim/biên trái/biên phải, độ lệch, đóng đầu; bật/tắt hatch và xem cấu hình tài nguyên đang chọn.

**Thư viện** gom bộ chọn theo vai trò:

| Nhóm | Tài nguyên cần chọn |
|---|---|
| Trục | Layer tim, block ký hiệu, layer ký hiệu, tag ATT |
| Dim | Dim nhịp, Dim tổng, layer dim |
| Cột | Layer nét/chèn cột và hatch nếu dùng; không có bộ chọn block cột từ kho. Hình dạng/kích thước nằm ở Thông số |
| Tường | Layer nét tường; preset bề dày nếu kho có hợp đồng dữ liệu tương ứng |
| Hatch | Mẫu hatch theo mã HAT, layer hatch; hiển thị công dụng, pattern, scale, góc, màu ACI/TrueColor/ByLayer/ByBlock, loại mẫu và dependency PAT nếu cần |

Không giả định kho đã có tài nguyên loại “Tường”. Hiện tài nguyên có năm loại Block/Layer/Hatch/Text/Dim; bề dày và kiểu đặt tường là tham số nghiệp vụ của add-in, trừ khi sau này thống nhất một cấu hình tường riêng. Nếu tài nguyên bị xóa/chưa đồng bộ, báo rõ và chặn thao tác liên quan, không tự thay mẫu khác.

## 3. Chi tiết hành vi cột đề xuất

- Chọn tất cả giao điểm, các giao điểm biên hoặc chọn từng vị trí trong CAD/preview. Luôn xem số lượng và hình dạng trước khi áp dụng; không tự chèn toàn lưới khi chỉ bật tính năng.
- Giao điểm tính trên phạm vi đoạn trục đã chọn; không tự kéo dài line vô hạn để sinh vị trí ngoài lưới.
- B/H là kích thước model thực theo mm, **không nhân với tỷ lệ in**. Tỷ lệ trục/đầu ký hiệu/dim không được làm cột 220 mm biến thành 13.200 mm ở 1:60.
- Hình học do add-in dựng đúng kích thước, không dùng non-uniform scale để kéo méo một mẫu cột. Đổi B/H/đường kính sẽ phân giải/tạo đúng loại block và cập nhật các instance thuộc phạm vi lựa chọn.
- Có lựa chọn căn tim, căn mép hoặc căn góc và góc quay; base point của định nghĩa đặt tại tim, điểm neo được tính từ đường bao tham số.
- Phát hiện cột trùng tại cùng vị trí; cho biết cột do add-in quản lý và cột vẽ tay, không tự xóa đối tượng vẽ tay.

## 4. Chi tiết hành vi tường/hatch đề xuất

- Chọn trục/chuỗi đoạn rồi chọn khoảng giữa các giao điểm hoặc hai điểm giới hạn; có thể chỉ vẽ một phần trục. Không hiểu “theo trục” là vẽ tường trên tất cả tim trong bản vẽ.
- Bề dày nhập bằng mm, cho chọn theo đoạn/nhóm: 110, 220 hoặc giá trị khác. Các yêu cầu trước cho nhà hành chính có tường ngăn 220 được giữ làm ngữ cảnh/preset khi phù hợp, không biến thành quy tắc bắt buộc cho mọi công trình.
- Căn tim, lệch trái/phải; mũi hướng phải hiển thị để trái/phải không bị nhầm khi đảo chiều line.
- Ghép kín góc L, giao T, giao X, đóng đầu; loại nét nằm bên trong phần giao. Xử lý cả các bề dày khác nhau; báo biên tự cắt, đoạn quá ngắn hoặc khoảng hở vượt dung sai.
- Cột và tường gặp nhau: bắt buộc trừ phần giao với cột khỏi vùng tường, đóng kín các đầu/biên tường tại mặt cột và loại phần nét tường nằm trong cột. Hatch theo đường bao sau khi trừ, không đè cột. Giữ nguyên block cột. Lấy đường bao cột tham số theo đúng kích thước, góc xoay và vị trí chèn; không dùng bounding box thay cho cột xoay hoặc tròn. Không xác định được biên cột thì báo rõ và chặn áp dụng phần liên quan, không âm thầm bỏ qua việc trừ cột. Nhận diện cột vẽ tay/ngoài add-in là phạm vi riêng, không tự suy đoán.
- Chỉ hatch sau khi biên hợp lệ. Dùng pattern, scale, góc, màu và layer đã chọn từ kho; cùng pattern nhưng khác scale/góc/công dụng vẫn là các cấu hình khác nhau.
- Có xem trước vùng hatch; hatch đặt sau nét tường và cột. Mẫu ngoài thiếu PAT hoặc cấu hình không hỗ trợ phải báo rõ; không thay bằng SOLID để che lỗi.
- Ưu tiên hatch liên kết với biên do add-in quản lý; cần kiểm thử chỉnh biên, lưu/mở lại, xóa/rebuild. Chưa hứa liên kết tự động khi người dùng tùy ý EXPLODE/TRIM/MOVE ngoài add-in.
- Chừa lỗ cửa hoặc trừ vùng mở là mục mở rộng; không tự cắt cửa khi chưa có dữ liệu/vùng mở được chọn.

## 5. Đối chiếu đề xuất và phạm vi đã duyệt

| Ưu tiên | Đề xuất | Lý do và phạm vi |
|---|---|---|
| Đã duyệt | Chọn lại lưới để sửa nhịp/ký hiệu | Bao gồm cập nhật cột/tường liên kết sau preview, báo đối tượng chỉnh tay; xem mục 1.4–1.5 |
| Cao | Preview chung trục–cột–tường–hatch, bật/tắt từng lớp xem | Nhìn thấy chọn nhầm vị trí, bề dày và vùng hatch trước khi ghi CAD; preview vẫn phải ghi rõ phần giản lược |
| Cao | Dựng lại có kiểm soát, tránh chèn trùng | Cùng một lưới không sinh thêm cột/tường ở mỗi lần Áp dụng; hiển thị số thêm/sửa/xóa và ngoại lệ đã chỉnh tay |
| Cao | Kiểm tra hình học trước hatch + một lần Undo | Ngăn lặp lỗi góc tường đã gặp; hủy/lỗi không để lại một phần hình học |
| Vừa | Lưu preset theo công trình | Lưu nhịp, bề dày, bộ tài nguyên cột/tường/hatch và tùy chọn bố trí; giữ ID ổn định và revision khi chuyển máy |
| Vừa | Bảng nhịp/nhãn chỉnh từng dòng | Bổ sung nhập nhanh hiện tại, hỗ trợ trục phụ/tiền tố/hậu tố và chọn bỏ một số giao điểm |
| Sau | Xoay lưới, nhận line vẽ tay, chừa lỗ cửa | Tách khỏi vòng đầu để tránh mở rộng đồng thời quá nhiều trường hợp hình học |

Các thao tác sửa theo liên kết nên chạy khi người dùng bấm Cập nhật, chưa đề xuất reactor tự cập nhật liên tục. Lưới cũ 1.3.2 trở xuống chưa có metadata quản lý lưới: phải có bước nhận diện/nhận quản lý được xác nhận; không tự đoán chỉ bằng layer hoặc gần nhau.

## 6. Nguồn tham khảo công khai

Tra cứu ngày 21/11/2026; đọc mô tả chính chủ, không tải/cài/thử các sản phẩm này. Đây là tham khảo thiết kế, không phải cam kết những công cụ đó tương thích với AutoCAD 2021 trên máy người dùng.

1. [Axis grid one click — sxCAD, hướng dẫn trên Autodesk App Store](https://apps.autodesk.com/ACD/en/Detail/HelpDoc?appId=350956700980131361&appLang=en&os=Win64): mô tả thêm/sửa/xóa lưới và nhập khoảng cách X/Y. Gợi ý ưu tiên sửa lại lưới.
2. [Tansuozhe Smart Structural Design — hướng dẫn của nhà cung cấp](https://apps.autodesk.com/ACD/en/Detail/HelpDoc?appId=4839028259109355596&appLang=en&os=Win64): nhóm TSPlan có Grid/Column/Beam/Wall/Slab/Foundation. Gợi ý giữ luồng lưới–cột–tường trong cùng bảng, không sao chép bộ công cụ hoặc giả định sản phẩm có mọi cơ chế đề xuất ở đây.
3. [Autodesk — About Structural Members](https://help.autodesk.com/cloudhelp/2024/ENU/AutoCAD-Architecture/files/GUID-A90E26B3-13EB-4355-9838-F7E871C53D6F.htm): mô tả cột gắn nút lưới. Gợi ý lưu liên kết cột–giao điểm thay vì chỉ chèn rời.
4. [Autodesk — About Wall Cleanups and Priorities](https://help.autodesk.com/cloudhelp/2020/ENU/AutoCAD-Architecture/files/GUID-A5F21978-FC03-4CFA-95F1-C027177A7A25.htm): mô tả nối biên, xử lý giao và thứ tự hatch sau xử lý hình học. Gợi ý yêu cầu kiểm tra biên và quy tắc giao tường/cột.

Hai tài liệu cuối thuộc **Architecture toolset**. Rainmaker phải làm việc trong AutoCAD 2021 thường bằng block/polyline/hatch chuẩn; không giả định sẵn API/đối tượng AEC của Architecture. Các đề xuất trong tài liệu này là suy luận thiết kế cho Rainmaker, không phải tính năng sẵn có của bản hiện tại.

## 7. Thứ tự triển khai dự kiến khi được yêu cầu viết code

1. Rà tài nguyên layer/hatch thực tế; chốt bộ tham số cột tự tạo và chính sách xung đột tài nguyên. Giữ hồi quy tỷ lệ dim 1.3.2; triển khai giữ tên dim thư viện, đối chiếu trùng tên và chuyển/dọn RMAX theo mục 1.6. Không ghi là đã sửa trước khi có kiểm thử.
2. Gom giao diện thành hai tab; giữ lựa chọn và cấu hình cũ, preview và thao tác Một trục.
3. Làm metadata/version, chọn/sửa lưới và quy trình nhận lưới cũ; cơ chế chọn giao điểm, dựng block cột và cập nhật liên kết khi sửa nhịp.
4. Làm biên tường, nối góc/giao, trừ cột, sau đó hatch; tích hợp preview và kiểm tra lỗi.
5. Kiểm thử AutoCAD 2021 qua Manager, gồm hủy/Undo, lỗi giữa chừng, tránh tạo trùng, lưu/mở lại DWG, tỷ lệ 1:60/1:100, hai máy và cấu hình kho.
6. Khi đạt mới build/bàn giao vào `_Development/Rainmaker.Axis/versions/<version>/build-<fingerprint>/`; không sửa gói đang cài hay catalog thủ công.

Người dùng đang gọi mục tiêu là **1.3.3**. Đây là nhãn kế hoạch, chưa phải phiên bản đã phát hành. Khi thực sự phát triển, đối chiếu quy tắc minor/patch trong workflow vì có thêm chức năng, không tự đổi version trong lần ghi nhận này.

## 8. Tiêu chí nghiệm thu dự kiến

- [ ] Chỉ hai tab Thông số/Thư viện; không còn tab con, nhưng giữ đủ Lưới trục/Một trục và tùy chọn bố trí.
- [ ] Tự tạo block cột vuông/chữ nhật/tròn đúng kích thước và điểm neo, dùng lại định nghĩa cùng loại; không còn chọn block cột kho, không phóng hình học cột theo tỷ lệ in.
- [ ] Chọn lại lưới để sửa nhịp/ký hiệu; nhận lưới cũ qua bước riêng. Sau preview, cập nhật đúng cột/tường/hatch/dim liên kết; báo các đối tượng đã sửa tay hoặc mất liên kết.
- [ ] Chèn đúng các giao điểm được chọn, không chèn trùng khi áp dụng lại.
- [ ] Tường đúng đoạn, bề dày, hướng lệch; biên kín tại L/T/X, đầu tường và chỗ tiếp cột; bắt buộc trừ vùng cột, không còn nét tường hoặc hatch bên trong cột. Kiểm tra cột giữa đoạn, cột tại góc/giao, cột chữ nhật xoay và cột tròn; các vùng tường còn lại đều kín.
- [ ] Hatch đúng cấu hình kho, gồm scale/góc/màu/layer và công dụng đã chọn; không hatch vùng hỏng.
- [ ] Preview và kết quả CAD cùng hình học; đếm rõ đối tượng trước khi áp dụng.
- [ ] Hủy/Undo và lỗi không để lại hình vẽ dở; không đổi tài nguyên/đối tượng ngoài phạm vi.
- [ ] Hồi quy lỗi dim: kiểm tra giá trị trên dimension và chữ render thực, không chỉ kiểm tra DimStyleTableRecord.
- [ ] Giữ tên dim thư viện, dùng lại khi giống, hỏi trước khi cập nhật xung đột; không tạo RMAX mới. Chuyển/dọn RMAX cũ có đối chiếu, không làm mất dim hoặc xóa style còn tham chiếu.
- [ ] Một nút Ribbon; cập nhật qua Manager giữ cấu hình, mã tài nguyên và dữ liệu hai máy.

Danh sách này là yêu cầu kiểm tra tương lai, chưa đánh dấu đạt hay coi các đề xuất là đã triển khai.
