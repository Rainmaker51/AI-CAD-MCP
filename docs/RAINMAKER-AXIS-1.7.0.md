# Rainmaker.Axis 1.7.0 — phủ bì và nhịp cuối

Áp dụng phạm vi duyệt ngày 21/11/2026. Giữ các tính năng 1.6.0: công thức nhịp, neo mép cột, cắt chân dim, Undo/Redo và tối ưu chỉnh lưới đã nạp. Không sửa Manager hoặc tự đổi gói đang cài.

## Flow nhập

1. Trong **Thông số → Phủ bì và phần còn lại**, bật **Nhập theo phủ bì**. Nhập X ngang, Y dài theo mm, ví dụ **6000 × 15000**.
2. Mở **Mép ngoài → tim trục** nếu cần đổi bốn cạnh. Mặc định trái/phải/dưới/trên đều 110 mm. Đây là khoảng từ đường phủ bì tới trục biên, không phải độ dịch tim cột.
3. Nhập các nhịp bằng số thực hoặc công thức như trước. App giữ các nhịp đã nhập và hiển thị riêng X/Y: chiều dài tim trục, tổng đã nhập, **Còn cho nhịp cuối** hoặc **Vượt**.
4. Ví dụ Y: 15000 − 110 − 110 = **14780**. Nhập `5000 5000` thì còn **4780**; tự gõ thêm 4780 hoặc bấm **Điền cuối**. Kết quả `5000 5000 4780` báo Khớp phủ bì. Không trừ 220 lần nữa và không thay hai nhịp đầu.
5. Nếu nhập `5000 5000 5000`, app báo **Vượt 220 mm**, giữ nguyên số nhập để người dùng sửa. Không tự co nhịp cũ. Còn thiếu hoặc vượt thì chưa cho ghi CAD; preview thể hiện phương án đang nhập và đường phủ bì.

**Điền cuối** chỉ thêm phần còn lại thành một nhịp, không vượt 99 nhịp/phương. Nếu đã khớp thì không thêm hoặc giảm nhịp lần nữa. Nếu tổng vượt, cần sửa trước. Công thức có thể được chuẩn hóa thành số khi bấm nút; giá trị các nhịp trước giữ nguyên. Nếu số ký hiệu được nhập thành danh sách riêng, phải cập nhật danh sách khi số trục thay đổi; nút không tự xóa ký hiệu tùy chỉnh.

## Nút Chia đều

Mỗi phương X/Y có ô số nhịp và nút nhỏ **Chia đều**, độc lập với phương còn lại. Nhập 3 rồi bấm ở Y: chia **14780/3**, không chia 15000/3. Nút thay danh sách nhịp của phương đó; sau đó vẫn sửa từng nhịp và dùng cộng/trừ được.

Giữ giá trị double đầy đủ trong ô nhịp/DWG, hiệu chỉnh sai số số học vào nhịp cuối để tổng khớp; không dùng số hiển thị rút gọn để tính lại. Trạng thái khớp dùng dung sai 0,000001 mm. Số nhịp hợp lệ 1–99, mỗi nhịp theo giới hạn cũ. Chia đều/Điền cuối có Undo/Redo; Undo giữ cả cách nhập ký hiệu bắt đầu 1/A để có thể thêm nhịp tiếp.

## Preview và đặt vào CAD

- Preview có đường phủ bì tím nét đứt và nhãn kích thước. Đây là chỉ dẫn preview, không tạo thêm layer/khung DWG.
- Khi tạo mới trong chế độ phủ bì, chọn **góc dưới trái phủ bì**. App tự chuyển điểm đó sang giao trục đầu bằng mép trái/dưới; chỉ dịch một lần. Vẫn theo WCS XY như các bản trước.
- Khi sửa lưới đã có phủ bì, giữ góc phủ bì cũ; thay khoảng mép → tim sẽ điều chỉnh gốc trục tương ứng. Không cộng thêm offset lần nữa mỗi lần cập nhật.
- Với tường biên Center và neo cột Biên tự động, mặt ngoài theo bốn cạnh phủ bì, kể cả khoảng mép → tim không đối xứng. Giá trị neo hướng thủ công và offset riêng vẫn là lựa chọn chủ động; người dùng có thể đặt đối tượng vượt đường phủ bì bằng các override đó. Phần này không tự kiểm tra khoảng lùi pháp lý hoặc khả năng chịu lực.
- Tắt Nhập theo phủ bì để quay lại nhập nhịp thông thường; không tự sửa các nhịp còn trong ô. Chế độ Một trục không sử dụng phủ bì.

## Dữ liệu và kiểm thử

Lưu profile phủ bì và bốn khoảng mép/tim cùng tham số lưới trong DWG. Đọc lại khôi phục kích thước, trạng thái khớp và preview; lưới cũ không có profile vẫn dùng cách cũ. Chặn tổng chưa khớp cả ở UI và trước transaction ghi CAD. Nguồn thư viện vẫn đọc theo cơ chế 1.6.0, không thêm nút đồng bộ.

Kiểm thử thuần và AutoCAD Core trên kho/DWG biệt lập: 6000×15000 → tim 5780×14780, phần còn 4780, không trừ lặp, giữ nhịp trước, chia riêng X/Y và độ chính xác, báo vượt/thiếu, profile sai, Undo/Redo, offset đặt một lần, biên tường/cột thực, khoảng biên không đối xứng, giữ góc phủ bì khi sửa và save/reopen. Kết quả cùng ảnh WPF trong validation; xem manifest về ma trận Contracts và hash.

Chưa nghiệm thu DLL mới bằng thao tác trực tiếp trong cửa sổ/Ribbon AutoCAD thật. Lưu bản sao DWG trước cập nhật; DLL trước 1.7.0 chưa hiểu profile phủ bì khi sửa lại lưới. Không tự thay/xóa gói cũ.

## Cập nhật

Manager ≥0.8.3 → đúng mục **Tạo trục → Cập nhật file → package/Rainmaker.Axis.dll**. Giữ dependency cạnh DLL chính, mở lại AutoCAD nếu bản cũ đã nạp. Không thêm mục trùng, không NETLOAD riêng, không sửa `_Installed`/catalog. Gói được bàn giao vào `_Development/.../versions/1.7.0/build-<hash>/`, kèm source patch/commit gốc; chưa tự được chọn/cài và chưa commit/push nếu chưa được yêu cầu.
