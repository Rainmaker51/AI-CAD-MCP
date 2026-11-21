# Rainmaker.Axis 1.5.0 — chọn lưới nhanh, neo cột và nối tường

Triển khai phạm vi đã duyệt ngày 21/11/2026, trên nền 1.4.0. Tăng minor do có thêm lựa chọn neo cột; không ghi đè build cũ, không tự kích hoạt DLL trong Manager.

## Nạp lưới và preview

- Danh sách đọc metadata để mở nhanh. Mục chưa kiểm tra hiện **Chọn để đối chiếu**; chọn một lưới mới kiểm tra thành viên và hiện trạng của lưới đó. Không báo Khớp khi chưa kiểm tra. Kiểm tra thay đổi tay trước khi ghi vẫn giữ nguyên.
- Đọc hình hiện trạng trực tiếp qua định nghĩa block và ma trận chèn, bỏ qua hatch/attribute definition, không Explode block chỉ để xem hình. Polyline thẳng đọc trực tiếp các đỉnh, cung được lấy mẫu. Preview không thay thế kiểm tra hình học CAD chính xác.
- Hình hợp/trừ tường được chuyển thành đường biên phẳng và giữ lại khi thông số hình học chưa đổi. Pan, zoom, khung chọn hoặc đổi cột đang chọn không tính lại cây Boolean lồng nhau. Các lựa chọn tài nguyên được phân giải một lần cho cùng cấu hình trong lượt mở bảng; trước khi ghi vẫn đọc kho mới và kiểm tra snapshot.
- Khi cập nhật hình CAD, xóa hatch trước đường biên liên kết. Dim và phụ thuộc đã khớp kho được giữ nguyên, không clone/Replace lại chỉ vì gọi cập nhật hoặc đang trong nhóm Undo; tài nguyên thiếu hoặc xung đột vẫn theo cơ chế nhập/xác nhận cũ.
- Chuột trái kéo **trái → phải**: chọn cột/đoạn tường nằm trọn trong khung. Kéo **phải → trái**: chọn đối tượng giao khung. Xét hình bao cột xoay/tròn, vùng đoạn tường thực; vị trí tường chưa tạo dùng đường tim với vùng bắt nhỏ. Khung bao xanh dương nét liền; khung giao xanh lá nét đứt. Ctrl giữ thêm lựa chọn; Ctrl+click đảo chọn.
- Giữ chuột giữa rồi kéo để pan, vẫn giữ chuột phải kéo như bản cũ. Lăn chuột zoom tại con trỏ; Vừa khung khôi phục toàn cảnh. Mất capture chuột sẽ hủy trạng thái kéo.

## Bố cục

- Khi máy chưa có cấu hình: Thông số 30%, Preview 70%. Giữ thanh kéo giữa hai bên; chiều rộng tối thiểu giới hạn tỷ lệ khi cửa sổ quá nhỏ.
- Tỷ lệ gần nhất lưu theo máy tại `%LOCALAPPDATA%/Rainmaker/Axis/ui.json`; đóng bảng/AutoCAD rồi mở lại vẫn dùng tỷ lệ đã lưu, kể cả tỷ lệ từ 1.4.0. Máy khác có cấu hình riêng. Bản nâng cấp không tự ghi đè tỷ lệ người dùng bằng 30/70.
- Ô tên lưới, Đổi tên và Đến lưới trên CAD được căn cùng đáy. Danh sách neo có một hàng đủ rộng để đọc lựa chọn.

## Cột

- Neo **Center** giữ tim. Ngay sau Center là **Tim tường · Biên tự động** rồi các hướng trái/phải/trên/dưới/bốn góc. Các neo theo mép cột cũ vẫn có để đọc và sửa dữ liệu cũ.
- Với lưới mới, lần đầu bật Chèn cột dùng **Biên tự động**, lệch 110 mm vào trong ở cạnh biên, lệch cả X/Y ở góc; cột giữa đúng giao trục. Lưới cũ giữ nguyên neo/thông số đã lưu, không tự dịch cột chỉ vì mở bảng.
- Ô **Lệch tim tường** mặc định 110 mm, cho chỉnh theo từng cột hoặc nhiều cột. Trái = X âm, phải = X dương, trên = Y dương, dưới = Y âm trong hệ lưới. Lệch X/Y cộng thêm; hướng neo này không xoay theo tiết diện.
- Đây là dịch tim cột so với giao trục, **chưa có liên kết bám vào một đoạn tường** khi đoạn đó được dịch hoặc đổi chiều dày. Người dùng chủ động chỉnh khoảng lệch.
- Bật **Hiện hướng cột** sẽ thấy Xc (chiều B) và Yc (chiều H) tại các cột đang chọn; mũi tên quay theo góc tiết diện. Chỉ là hỗ trợ preview, không ghi mũi tên vào DWG. Tắt để giảm rối. Cột tròn vẫn có trục cục bộ để biết góc lưu.

## Tường

- Đầu tường không còn kéo thừa nửa bề dày một cách cố định. Giao L/T/X nối theo mặt đoạn tường thực, hợp vùng kín rồi trừ hình cột và tạo hatch. Đoạn thẳng hàng dùng dung sai nối 0,001 mm để Boolean CAD loại cạnh nối trong.
- Với đoạn biên **Center**, mặt ngoài theo đường bao tham chiếu lấy từ bề dày mặc định của lưới; khi đổi chiều dày riêng từng đoạn, phần chênh nằm phía trong. Giao T kết thúc tại mặt tường chính, không vượt mặt bên kia. Đổi căn Left/Right hoặc Lệch thêm vẫn là lựa chọn chủ động của người dùng; không tự xóa offset để ép các đoạn rời nhau phải nối.
- Khi nạp lưới cũ, lớp cam giữ hình thật; bản nháp xanh áp dụng quy tắc nối mới. Chỉ **Ghi cập nhật lưới** mới thay hình CAD. Xem lại vị trí/căn tường trước khi xác nhận; hatch và trừ cột vẫn theo tài nguyên kho.

## Dữ liệu, kiểm thử và cập nhật

Neo mới, khoảng lệch và quy tắc nối lưu cùng lưới trong DWG; vẫn đọc metadata cũ. DLL trước 1.5.0 chưa hiểu các tên neo mới: lưu bản sao DWG trước cập nhật nếu cần quay lại DLL cũ. Không chuyển dim RMAX, không đổi tên Dim kho và không sửa thư viện gốc trong đợt này.

Kiểm thử qua Manager trong AutoCAD Core 2021 trên kho/DWG biệt lập: hồi quy dim/annotative, Undo/rollback, sửa tay, save/reopen; khung bao/giao cho cột và tường, neo 110 và các hướng sau xoay, Xc/Yc bật tắt, pan, tỷ lệ 30/70 và đọc lại cấu hình, căn ô tên, nối L/T/đoạn thẳng khác bề dày, hiệu năng lưới 6×4. Kết quả thời gian trong `validation/axis150-results.json` là đo trên fixture/máy này, không phải cam kết mọi DWG.

Đã kiểm tra ảnh WPF. Chưa thao tác trực tiếp bản DLL mới trên cửa sổ AutoCAD đang mở bản cũ; không coi Core hoặc kiểm thử bộ xử lý sự kiện là nghiệm thu chuột/Ribbon thật. Xem manifest để biết hash gói, ma trận tương thích Contracts và tình trạng catalog.

Cài qua **Manager → đúng mục Tạo trục → Cập nhật file → package/Rainmaker.Axis.dll**. Không thêm mục trùng, không sửa `_Installed`/catalog; mở lại AutoCAD khi bản cũ đã nạp. Gói không thay Manager. Nguồn và patch được bàn giao cùng build; chưa commit/push nếu người dùng chưa yêu cầu.
