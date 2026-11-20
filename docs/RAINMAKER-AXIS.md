# Rainmaker.Axis 1.3.1 — chọn thư viện và preview

## Sử dụng

1. Trong CAD Tool Manager, chọn đúng mục **Tạo trục → Cập nhật file**, chọn `Rainmaker.Axis.dll` trong `package/` của build 1.3.1 đã bàn giao. Giữ các DLL phụ cạnh DLL chính. Không nhập thành mục mới, không copy vào `_Installed`.
2. Nhấn nút **Tạo trục** trên Ribbon hoặc chạy `RAINMAKERAXIS` qua Manager.
3. Ở trang **Thư viện**, chọn/gõ mã layer tim trục, block đầu trục, layer đầu trục, dim nhịp, dim tổng và layer dim. Kiểm tra tag thuộc tính; bảng hiển thị các tag có sẵn của block.
4. Bấm **Lưu cài đặt thư viện** nếu muốn dùng lại và đồng bộ sang máy kia. Hai dim style không tự chọn mặc định để tránh dùng nhầm.
5. Nhập nhịp X/Y, ký hiệu bắt đầu và tỷ lệ ở trang **Thông số**. Giữ các cú pháp nhập nhanh như `3*4000`.
6. Trang **Bố trí** cho nhập **Trục → dim nhịp** (mặc định 1.000 mm) và **Dim nhịp → dim tổng** (mặc định 600 mm), chọn phía đặt dim và đầu ký hiệu: Both (cả hai), Start (dưới/trái), End (trên/phải). Khoảng cách tính bằng mm thật trong bản vẽ, không nhân thêm tỷ lệ; đầu trục tự kéo dài ra ngoài dim tổng. Bấm lưu cài đặt để dùng lại khoảng cách.
7. Kiểm tra preview, bấm **Chọn điểm đặt lưới** hoặc **Chọn hai đầu trục**. Hủy chọn điểm không tạo hình vào DWG.

## Dim và tỷ lệ

Dim nhịp và dim tổng được clone từ đúng DimStyleTableRecord trong DWG nguồn, gồm phụ thuộc font/mũi tên. Tên style trong DWG đích dùng tiền tố `RMAX_<mã>_<lần vẽ>` để không ghi đè style có sẵn. Thông số dim giữ theo nguồn, gồm chiều cao chữ, mũi tên, hệ số đo, tỷ lệ, độ chính xác và khoảng hở; không dùng lại công thức gán Dimexo/Dimexe/Dimgap cũ.

Với dim thường, mẫu số tỷ lệ bố trí không tự thay Dimscale của style nguồn. Hãy chọn style có tỷ lệ phù hợp. Với style annotative, dimension nhận annotation context đã chọn mà không nhân tỷ lệ chữ lần nữa; không đổi annotation scale hiện hành của DWG.

## Cấu hình và định danh

- Cấu hình lưu tại `<kho>/Catalog/addin-settings/rainmaker-axis.json`, phiên bản cấu trúc 1.
- Lưu ID ổn định theo vai trò và nguồn `ISO_QUANGANH`; không lưu đường dẫn kho cố định của máy.
- Mã ngắn đọc từ `Catalog/resource-codes.json`, tên hiện tại đối chiếu với trạng thái đồng bộ. Đổi tên/chuyển nhóm vẫn dùng cùng ID sau khi đồng bộ.
- Cấu hình có revision và khóa ghi để tránh ghi đè từ một phiên khác. Với cloud, vẫn phải chỉ cập nhật từ một máy và chờ đồng bộ đủ trước khi mở ở máy kia.
- Chưa đồng bộ, tài nguyên bị loại, tag không hợp lệ hoặc thư viện đổi giữa lúc chọn điểm: chặn vẽ và yêu cầu kiểm tra lại.

## Preview và phạm vi bản này

Preview vector phản hồi sau khoảng 220 ms khi sửa trường nhập, có zoom/vừa khung và dùng cùng mô hình tọa độ với thao tác vẽ. Đây là sơ đồ bố trí theo mm: đầu trục được biểu diễn giản lược, chữ/mũi tên không phải ảnh render CAD chính xác của style được chọn. Chế độ một trục hiển thị đoạn minh họa 4.000 mm; chiều dài thật lấy từ hai điểm trên CAD.

Bản này chưa có chỉnh lại lưới đã vẽ, preset nhiều công trình, xoay lưới hoặc dim associative khi kéo trục bằng tay. Lưới vẫn theo WCS XY; dim phản ánh hình học tại lần tạo. Các mục này giữ trong kế hoạch mở rộng riêng.

Yêu cầu: AutoCAD 2021, .NET Framework 4.8 x64, CAD Tool Manager 0.8.3 trở lên và kho đã có mã/nguồn block, layer, dim hợp lệ. Không tạo tab mới trong app.

## Kiểm thử

Hồi quy tính nhịp/đánh ký hiệu; nạp DLL qua Manager trong AutoCAD Core Console 2021; dựng lưới 5 trục/10 đầu/10 dim; đối chiếu các biến dim và font nguồn; kiểm tra dim annotative/context; lưu/mở lại DWG; lưu ID, từ chối ghi revision cũ; render bảng/preview và chặn đầu vào sai. Kết quả cụ thể và giới hạn kiểm tra GUI nằm trong `validation/` của build.
