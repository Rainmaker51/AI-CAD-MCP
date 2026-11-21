# Rainmaker.Axis 1.6.0

Phạm vi chốt ngày 21/11/2026: căn mép cột, công thức nhịp, cắt chân dim, giảm lag khi chỉnh cột/thêm bớt tường của lưới đã nạp và hoàn tác/làm lại bản nháp. Không thêm chia đều, dim tạm, sao chép thông số hoặc công cụ ưu tiên nối tường.

## Căn cột theo mép tường

**Tim → mép ngoài** thay cho cách hiểu cũ là dịch tim cột. Với tường 220, khoảng từ tim tường tới mặt ngoài là 110. Cột 300 có nửa bề rộng 150, vì vậy tim cột dịch **150 − 110 = 40 mm** vào trong; mặt ngoài cột trùng mặt ngoài tường. Cột góc căn theo cả hai phương. Cột xoay dùng nửa kích thước chiếu lên phương X/Y để căn điểm ngoài cùng; không coi mép của hình chữ nhật xoay là song song mặt tường. Circle dùng bán kính.

Neo biên tự động nhận hướng vào trong theo vị trí trên lưới. Các hướng thủ công dùng hệ lưới; ô Lệch X/Y cộng thêm. Giá trị 110 vẫn do người dùng chọn theo mặt tường mong muốn; chưa liên kết động cột với một đoạn tường bị dịch hoặc đổi bề dày. Khi lưới cũ dùng cách neo sai của 1.5.0, lớp cam giữ hình thực, phương án xanh áp dụng cách mới; chỉ ghi cập nhật mới thay CAD.

## Nhập nhịp bằng công thức

`5000 3000+2000 1000-500 2000` cho bốn nhịp `5000 5000 500 2000` mm. Dấu cách/chấm phẩy phân tách nhịp; không có dấu cách bên trong công thức. Hỗ trợ cộng/trừ, số thập phân dấu chấm hoặc phẩy; không chạy mã hoặc dùng bộ eval. Không có ngoặc/chia/lũy thừa. Mỗi kết quả phải lớn hơn 0 và không quá 1.000.000 mm, tối đa 99 nhịp/phương. Cú pháp `3*4000` vẫn là lặp ba nhịp; `2*3000+2000` là hai nhịp, mỗi nhịp 5000, không phải phép nhân số học.

Kết quả tính hiển thị dưới ô nhịp. Lưới lưu giá trị số trong DWG; lần mở lại hiển thị số đã tính. Lịch sử bản nháp trong lượt mở bảng giữ chuỗi công thức để hoàn tác/làm lại.

## Cắt chân dim

Bật **Cắt chân dim**, nhập **Dài chân dim · mm thật**. Ví dụ trục → dim 1000, chân 300: phần đường dóng về phía trục dài 300, phần còn lại để trống; không đổi khoảng cách đường dim. Phần vượt qua đường dim vẫn theo Dimexe của thư viện. Áp dụng cho dim nhịp và tổng do lượt tạo/cập nhật này sinh ra.

Dùng override `Dimension.DimfxlenOn`/`Dimfxlen` trên đối tượng, không thay Dim style nguồn hoặc DIM variables toàn bản vẽ. Chia giá trị nhập cho tỷ lệ trước khi gán: 300 mm ở 1:60 có Dimfxlen 5; 1:100 có Dimfxlen 3. Đồ họa dimension đã được kiểm tra trong fixture CAD, gồm chiều dài thực và phần vượt Dimexe. Tắt tùy chọn sẽ giữ cơ chế đường dóng của style kho, không ép tắt thiết lập cố định vốn có của style. [API Autodesk](https://help.autodesk.com/cloudhelp/2020/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_Dimension_DimfxlenOn.html).

## Hoàn tác/làm lại preview

- Hai nút **Hoàn tác / Làm lại**, phím **Ctrl+Z / Ctrl+Y** (Ctrl+Shift+Z cũng làm lại) khi bảng có focus.
- Một thay đổi trên nhiều cột/tường là một bước. Các ô liên tiếp trong một lượt cập nhật preview được gom thành một bước; các thao tác thêm/bớt đối tượng có mốc riêng. Giữ tối đa 100 bước.
- Chọn đối tượng đang có, pan/zoom, bật/tắt lớp xem không tạo bước. Chọn vị trí mờ để thêm cột/tường là thay đổi bản nháp nên có bước.
- Chỉnh sửa mới sau hoàn tác xóa nhánh làm lại. Nạp/chọn lưới khác xóa lịch sử để không đưa dữ liệu lưới này sang lưới kia. Lịch sử chỉ nằm trong lượt mở bảng, không lưu vào DWG.
- Undo này không ghi hình CAD, không hoàn tác việc **Đổi tên** đã lưu metadata hoặc cấu hình kho đã lưu bằng nút riêng. Sau khi ghi CAD dùng Undo của AutoCAD. TextBox trong bảng dùng cùng lịch sử bản nháp qua phím tắt, không lịch sử riêng từng ký tự.

## Hiệu năng và dữ liệu cũ

Lớp hiện trạng CAD được chuyển thành StreamGeometry bất biến một lần khi nạp, không phát lại từng đoạn thẳng ở mọi lần chỉnh cột/tường. Giữ đường biên hợp tường riêng để đổi cột không tính lại hợp tường. Danh sách vị trí giữ ItemsSource khi chỉ đổi thuộc tính, không dựng lại toàn bộ hàng. Vẫn đối chiếu sửa tay khi nạp và trước khi ghi; không bỏ kiểm tra để làm nhanh.

Fingerprint thành viên mới có version 2 để phát hiện chỉnh chân dim bằng tay. Dữ liệu cũ thiếu trường version được đọc theo fingerprint cũ; không tự báo toàn bộ đối tượng cũ đã sửa chỉ vì nâng add-in. DLL trước 1.6.0 chưa hiểu đầy đủ quy tắc neo và fingerprint mới: lưu bản sao DWG trước áp dụng nếu cần quay lại phiên bản cũ.

Kết quả Core/WPF nằm trong `validation`: công thức và dữ liệu sai, cột 300/tường 220 dịch 40, cột xoay, Undo/Redo nhóm và thêm/bớt tường, loại thao tác chọn khỏi lịch sử, reset khi nạp lưới, chân dim 300 ở 1:60 và 1:100, nguồn Dim không đổi, phát hiện sửa tay và lưu/mở lại. Đo hiệu năng trên chính lưới đã nạp; con số fixture không bảo đảm tốc độ mọi DWG. Đã xem ảnh WPF, chưa nghiệm thu DLL mới bằng thao tác trực tiếp trên cửa sổ/Ribbon AutoCAD thật.

## Cập nhật

Manager ≥0.8.3 → đúng mục **Tạo trục → Cập nhật file → package/Rainmaker.Axis.dll**. Giữ dependency cạnh DLL, mở lại CAD nếu bản cũ đã nạp. Không thêm mục trùng, không sửa `_Installed`/catalog, không thay Manager hoặc Contracts. Gói bàn giao là ứng viên đã kiểm thử, chưa tự được chọn trong catalog; xem manifest/hash. Có source patch và commit gốc, chưa commit/push khi chưa được yêu cầu.
