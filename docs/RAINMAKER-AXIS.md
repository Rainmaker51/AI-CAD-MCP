# Rainmaker.Axis 1.7.0 — preview tương tác và danh sách lưới

AutoCAD 2021, .NET Framework 4.8 x64, CAD Tool Manager 0.8.3 trở lên. Một nút **Tạo trục**, hai tab **Thông số / Thư viện**. Bản này triển khai các bổ sung đã duyệt trên nền 1.3.4, tách version mới và giữ nguyên các build cũ.

Chi tiết mới: [1.7.0 — phủ bì và nhịp cuối](RAINMAKER-AXIS-1.7.0.md). Giữ [1.6.0](RAINMAKER-AXIS-1.6.0.md): neo theo mép cột, công thức nhịp, cắt chân dim và Undo/Redo. Khi bật phủ bì, điểm đặt mới là góc phủ bì; khi tắt vẫn chọn giao trục đầu như trước.

## Cài qua Manager

Chọn đúng mục **Tạo trục → Cập nhật file**, chọn `package/Rainmaker.Axis.dll` trong build 1.7.0 đã bàn giao ở kho. Giữ các DLL phụ cạnh DLL chính. Không thêm mục trùng, không sửa `_Installed`/catalog hoặc chép đè Contracts của Manager. Mở lại AutoCAD nếu bản cũ đã nạp. Kiểm tra version trên tiêu đề bảng và đường dẫn/hash trong catalog.

Giữ bản sửa tương thích `get_Values` của 1.3.4: Axis đọc dữ liệu đồng bộ bằng DTO riêng, không gọi getter Values thay đổi chữ ký của Contracts dùng chung. Không cài các DLL fixture kiểm thử vào máy thật.

## Bảng điều khiển

- Bảng thông số mặc định khoảng 30%, preview khoảng 70% khi máy chưa có cấu hình; kéo thanh chia để đổi, tỷ lệ gần nhất lưu theo máy kể cả sau khi tắt CAD. Chiều rộng/chiều cao giới hạn theo màn hình. Các nhóm Trục/Dim/Cột/Tường dùng ô nhập theo hàng, có thể thu gọn và cuộn.
- Chỉ hai tab. Không có nút đồng bộ thư viện và không còn nút/luồng **Chuyển dim RMAX**. Các dim/RMAX cũ trong DWG không bị tự xóa hay chuyển.
- **Thông số** chứa danh sách lưới, nhịp/ký hiệu, dim, cột và tường. **Thư viện** chứa tài nguyên theo vai trò, hiển thị mã · tên, tag, nguồn và ID.
- **Ghi cập nhật lưới / Chọn điểm đặt lưới** mới ghi hình học vào DWG. Click chọn, sửa ô, pan/zoom chỉ đổi bản nháp. Riêng **Đổi tên** là thao tác metadata rõ ràng, giữ nguyên đối tượng và không tái tạo hình học.

## Preview tương tác

1. Chọn chế độ **Chọn cột** hoặc **Chọn tường**. Click chọn một; Ctrl+click thêm/bỏ chọn. Kéo khung trái → phải chỉ chọn hình nằm trọn; phải → trái chọn cả hình giao khung. Ctrl+A chọn tất cả vị trí trong chế độ hiện tại khi preview có focus.
2. Ô/thanh màu vàng là phạm vi đang chọn. Vị trí chưa có đối tượng được vẽ mờ/nét đứt; chọn vị trí đó để thêm vào bản nháp. **Bỏ cột/Bỏ tường** hoặc Delete bỏ các vị trí đang chọn, **Khôi phục/Thêm lại** đưa chúng trở lại. **Bỏ chọn** chỉ bỏ lựa chọn, không xóa đối tượng.
3. Lăn chuột zoom tại con trỏ; giữ chuột giữa rồi kéo để pan (vẫn hỗ trợ chuột phải); **Vừa khung** đặt lại. Escape bỏ lựa chọn trước; khi hết lựa chọn dùng hành vi hủy bảng thông thường.
4. Danh sách vị trí hỗ trợ chọn bằng bàn phím/Ctrl/Shift. Dấu ● là đang có trong bản nháp, ○ là chưa có. Có nút chọn tất cả/biên để thao tác nhanh.
5. Khi chọn nhiều đối tượng có giá trị khác nhau, ô tương ứng để trống và ghi **Nhiều giá trị**. Chỉ ô bạn thay đổi được áp dụng; không ghi đè các giá trị khác bằng thông số của đối tượng đầu tiên.
6. Chưa chọn đối tượng thì bảng sửa **mặc định cho đối tượng mới**. Sửa mặc định không ghi đè các override riêng đã có.

Preview giản lược đầu trục, font/mũi tên và minh họa vùng hatch, không phải render chính xác texture hatch. Vùng chọn/hình học cột và tường dùng cùng thông số với kết quả CAD. Màu xanh là phương án; lớp cam là hình học hiện trạng đọc từ DWG khi chọn lưới, có thể bật/tắt.

## Trục và dim

- Nhịp theo mm: `4000 5000` hoặc `3*4000`. Ký hiệu có thể nhập điểm bắt đầu 1/A hoặc đủ danh sách `1 2 3a 4`. Tỷ lệ 1/… từ 1 đến 1000.
- Chọn phía đặt dim, khoảng cách trục → dim nhịp và dim nhịp → tổng, một/hai đầu ký hiệu. Khoảng cách là mm thật.
- Dim dùng đúng tên/định nghĩa kho, cùng tên/cùng nội dung thì dùng lại; khác nội dung hoặc dependency thì hỏi trước khi thay. Có thể ảnh hưởng các đối tượng khác đang tham chiếu cùng định nghĩa.
- Áp dụng đầy đủ dữ liệu style sau khởi tạo, loại override DIM hiện hành vô tình bị mang theo. Dim thường dùng Dimscale bằng mẫu số nhập trên đối tượng; annotative dùng Dimscale=0 và context đã chọn. Chữ nguồn 2 mm cho chữ model 120 mm ở 1:60, 200 mm ở 1:100; không nhân tỷ lệ hai lần, không đổi DIM/annotation scale hiện hành.
- Một trục vẫn dùng hai điểm CAD, chưa thuộc danh sách sửa lưới và không chèn cột/tường. Khi sửa lưới đang quản lý, dùng **Tạo mới** để chuyển sang Một trục.

## Cột tự tạo có hatch

- Lần đầu bật cho lưới mới: cột dự kiến tại mọi giao điểm, neo biên căn mép ngoài cách tim trục 110 mm; cột 300 dịch tim 40 mm, cột giữa đúng tim. Bật lại giữ lựa chọn đã chỉnh. Lưới cũ hiện lớp cam và phương án neo mới xanh, chỉ ghi cập nhật mới thay CAD.
- Chọn một/nhiều cột để sửa Rectangle/Square/Circle, B/H/đường kính, góc xoay, neo Center, neo tim tường tự động/tám hướng, các neo mép cũ và lệch X/Y. Neo tim tường dùng hướng lưới, không xoay theo tiết diện. Bật/tắt **Hiện hướng cột** để xem Xc/Yc tại cột đang chọn. Square/Circle dùng B, H chỉ có ý nghĩa với Rectangle.
- Kích thước cột là mm thật, không nhân tỷ lệ in. Đổi neo/offset thay vị trí instance so với giao trục; base point định nghĩa vẫn ở tim.
- Chọn riêng **Layer cột / Hatch trong cột / Layer hatch cột** từ kho. Block chứa đường bao kín và hatch associative bên trong, hatch đặt sau nét. Hatch quay cùng cột; pattern/scale/góc/màu lấy đúng cấu hình đã chọn.
- Cùng hình/kích thước/cấu hình hatch dùng chung định nghĩa. Đổi tiết diện một cột hoặc đọc hatch phiên bản mới sẽ phân giải định nghĩa phù hợp rồi đổi reference trong phạm vi cập nhật, không sửa định nghĩa dùng chung làm các lưới khác đổi theo. Định nghĩa bị sửa tay sẽ báo xung đột.

## Tường theo chu vi và từng đoạn

- Lần đầu bật cho lưới mới: tạo chu vi ngoài; chọn thêm các đoạn bên trong trên preview/danh sách. Bật lại giữ các đoạn đã chỉnh.
- Chọn một/nhiều đoạn để sửa **chiều dày**, căn Center/Left/Right và lệch thêm. Đoạn X có hướng sang phải, đoạn Y hướng lên trên; Left/Right theo mũi hướng, lệch dương về trái. Không thêm chỉnh chiều dài/lùi đầu/lùi cuối trong bản này.
- Mỗi đoạn giữ bề dày/độ lệch riêng, có thể dùng bao 220 và ngăn 110 trong cùng lưới.
- Tường hợp vùng tại L/T/X, trừ đường bao thật của cột thuộc lưới rồi xuất biên kín. Cột xoay/tròn được trừ theo hình thật; không dùng bounding box để cắt. Hatch chỉ phủ phần tường còn lại và không phủ các khoảng trống bên trong hệ tường.
- **Hatch tường / Layer hatch tường** độc lập với hatch cột. Tắt Hatch tường vẫn tạo nét. Pattern ngoài thiếu PAT sẽ báo lỗi và rollback, không tự thay bằng SOLID.
- Chưa nhận/trừ cột ngoài add-in, chưa tự chừa lỗ cửa. Khi xóa/bớt trục làm vị trí cột/đoạn mất liên kết, sửa danh sách ký hiệu cho khớp và dùng **Bỏ vị trí mất** để bỏ các vị trí không còn; đây chỉ là chỉnh bản nháp trước khi ghi.

## Các lưới đã nhận dạng

- Danh sách chỉ thuộc DWG đang thao tác: lưới tạo bằng add-in và lưới cũ đã nhận quản lý thành công. Hiển thị tên, số trục/kích thước, số cột/tường. Mục chưa chọn hiện **Chọn để đối chiếu**; chọn một lưới sẽ kiểm tra và báo Khớp/Đã sửa tay/Thiếu thành viên/Dữ liệu lỗi.
- Chọn một dòng để nạp đúng nhịp, ký hiệu, các override, lựa chọn tài nguyên và preview; tự vừa khung, xóa trạng thái chọn của lưới trước. Bản nháp chưa ghi được hỏi trước khi chuyển.
- **Đổi tên** lưu tên qua metadata, giữ GridId và toàn bộ đối tượng. Các chỉnh sửa hình học đang làm vẫn là bản nháp. **Đến lưới trên CAD** đóng bảng tạm, chọn/tô sáng các thành viên và zoom tới phạm vi rồi mở lại bảng.
- **Chọn lưới trên CAD** nhận một thành viên có GridId. Kiểm tra khác biệt ngay khi nạp; hiện trạng CAD màu cam khác phương án xanh khi có sửa tay. Preview không coi thông số lưu cũ là hình thật hiện tại. Thành viên không đọc được có thông báo; dữ liệu lỗi không được hiện như lưới mặc định/cũ hợp lệ.
- Khi ghi cập nhật, add-in xác nhận phạm vi và các thay đổi tay sẽ bị thay; tái tạo trục/dim/cột/tường/hatch theo phương án, giữ GridId. Lỗi rollback cả hình học và metadata; một lần Undo hoàn tác thao tác tạo/cập nhật.

**Nhận lưới cũ**: chọn đủ line tim, block đầu trục và dim muốn thay; không kèm tường/chữ/hatch ngoài phạm vi. Chỉ hỗ trợ lưới chữ nhật vuông góc WCS XY, tối thiểu hai trục đứng/hai ngang giao đủ; layer/tag phải khớp lựa chọn kho. Kiểm tra lại nhịp, ký hiệu và tỷ lệ trước khi áp dụng (dữ liệu suy ra mặc định 1:60). Chưa áp dụng thì chưa lưu nhận quản lý bền vững.

## Nguồn kho và dữ liệu hai máy

- Không có nút đồng bộ trong add-in. Mỗi lần mở/nạp bảng mới đọc tài nguyên kho đã lưu/đồng bộ theo ID. Ví dụ scale hatch đổi sang 300: lượt nạp/triển khai mới dùng 300, không nhân thêm tỷ lệ in.
- Nếu kho đổi sau khi preview đã được duyệt, chặn ghi và yêu cầu mở lại bảng để tránh dùng lẫn snapshot. Hatch đã vẽ trong DWG không tự đổi chỉ vì mở lại file hoặc sửa kho; cần chủ động cập nhật phạm vi lưới nếu muốn đổi.
- Cấu hình tài nguyên tại `Catalog/addin-settings/rainmaker-axis.json`, có revision/khóa ghi, không lưu đường dẫn máy phát triển. Lưới/override lưu trong DWG bằng metadata phiên bản 2, tương thích đọc dữ liệu phiên bản 1.
- Chuyển dữ liệu cũ giữ các vị trí đã chọn, kích thước và hướng lệch tường; không tự bật toàn bộ cột/tường khi chỉ mở bảng. Khi áp dụng tính năng hatch cột mới, chọn tài nguyên cột một lần nếu cấu hình cũ chưa có vai trò này.
- Lưu bản sao DWG trước khi nâng/cập nhật lưới quan trọng. Gói 1.3.4 cũ vẫn xem được hình học tiêu chuẩn CAD nhưng không có đủ khả năng sửa metadata phiên bản 2. Không xóa dữ liệu khi quay lại DLL cũ.
- Hai máy chỉ một máy ghi kho/DWG tại một thời điểm, chờ đồng bộ đủ catalog và DLL. Khóa file không thay cho khóa phân tán cloud. Định dạng số Việt/Anh không làm đổi fingerprint nhận diện chỉnh sửa.

## Kiểm thử và giới hạn

Đã kiểm thử qua Manager trong AutoCAD Core 2021 trên kho và DWG biệt lập: hồi quy dim/annotative/tỷ lệ, tường kín trừ cột, Undo/rollback; chọn click/Ctrl/khung qua cùng bộ xử lý preview, mixed values, override từng cột/đoạn, hatch trong block, danh sách/nạp đúng lưới/đổi tên, đọc hiện trạng CAD, chuyển dữ liệu cũ, bỏ vị trí mất, lưu/mở lại và lần nạp sau dùng scale 300 mà không đổi định nghĩa cũ. Chạy cả Contracts.Values kiểu JObject và fixture JToken để kiểm tra không tái phát `get_Values`.

Đã xem ảnh render WPF và kiểm tra chữ/tên lưới thực sự hiển thị. Chưa nghiệm thu chuột/Ribbon của DLL mới trên cửa sổ AutoCAD đang giữ DLL cũ; không coi kiểm thử Core/bộ xử lý sự kiện là kiểm thử GUI thật. Chi tiết kiểm thử 1.5.0 nằm trong tài liệu cùng phiên bản và validation của build.

Giới hạn: lưới chữ nhật WCS XY, chưa xoay toàn lưới/lưới cung, preset công trình, chừa cửa, add-in quản lý riêng hoặc reverse-engineer mọi chỉnh sửa EXPLODE/TRIM/COPY tùy ý. Preview hiện trạng là lớp so sánh; sửa hình học theo cấu hình phương án, không tự suy ra toàn bộ cấu hình mới từ đối tượng đã sửa tay. Xem manifest và validation của build để biết bằng chứng cụ thể, nguồn và trạng thái được Manager chọn.
