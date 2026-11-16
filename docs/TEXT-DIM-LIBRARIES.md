# Thư viện Text và Dim

Hai tab Text và Dim trong Thư viện CAD đọc các định nghĩa TextStyle/DimStyle từ `CAD/Originals/ISO_QuangAnh_ThuVien.dwg`. Nút nhập/cập nhật quét cả hai loại bằng AutoCAD Core Console, giữ nguyên bản vẽ đang mở. App đọc bản file đã lưu; thay đổi chưa lưu trong AutoCAD chưa thuộc lần nhập.

Text hiển thị font, bigfont, chiều cao, hệ số rộng, góc nghiêng và cờ chữ. Dim hiển thị text style tham chiếu, chiều cao chữ, mũi tên, tỷ lệ, hệ số đo, số lẻ, khoảng lùi/nhô đường gióng, khoảng cách dim nền/chữ, làm tròn và mã đơn vị. Một số mã là enum gốc AutoCAD, không phải kích thước mm.

Mỗi loại có cây nhóm riêng, hồ sơ tên hiển thị/công dụng/ghi chú, duyệt cho Skill tham khảo, thùng rác và khôi phục như Hatch. Metadata nằm trong `Catalog/management/texts` và `dims` của kho, tách khỏi các định nghĩa gốc. Chỉ các style đã duyệt, chưa xóa được đưa vào ngữ cảnh AI. App không tự gán toàn bộ font/style gốc vào DWG và không tự thay tiêu chuẩn của Template.

Danh mục phiên bản và dữ liệu chú giải nằm ở `Styles/ISO_QUANGANH`. Máy cùng kho đọc được danh mục và ghi chú; không commit dữ liệu kho vào Git.

## Chú giải hatch từ DWG

Lần nhập đọc Text/MText và phạm vi hatch trong Model Space. Với hatch có đúng một nhãn nằm bên trái và cùng hàng, app chuyển nhãn dùng font Vn/TCVN3 sang Unicode rồi điền tên/công dụng còn trống. File `Styles/ISO_QUANGANH/hatch-legend-evidence.json` giữ mã nguồn và handle của từng cặp nhãn/hatch để đối chiếu.

Nếu cùng cấu hình hatch có nhiều công dụng ở các hàng khác nhau, app giữ tất cả công dụng. Không suy đoán công dụng từ tên pattern và không tự điền cho mẫu không khớp dòng. Ghi chú người dùng đã sửa và mẫu đã xóa được giữ nguyên. Dữ liệu nhập từ chú giải vẫn chờ duyệt, không tự cho Skill sử dụng. Danh mục Hatch phải có cùng source hash; nếu nguồn khác, cập nhật Hatch rồi nhập Text/Dim lại để điền chú giải.

Kiểm thử: `node tests/style-library.test.cjs` và `dotnet run --project tests/Source.Regression/Source.Regression.csproj -c Release`.
