# Quy trình chuẩn đầu tiên: Quang Anh → PDF/ảnh sang CAD

Trạng thái: đang xây dựng đặc tả và chuẩn hóa dữ liệu; chưa nghiệm thu quy trình hoàn chỉnh.

## Bộ nền duy nhất trong danh sách sử dụng

| Thành phần | Mã | Tên hiển thị | Phiên bản hiện tại |
|---|---|---|---|
| Template | ISO_QUANGANH | Template Quang Anh | 1.1.9 |
| Skill | SOURCE_ISO_QUANGANH | PDF / ảnh sang CAD — nhà ở dân dụng | 1.0.0 |
| Bộ layer | QUANGANH_LAYERS | Layer chung Quang Anh | 1.0.0 |

Template và Skill được xếp vào nhóm Nhà ở dân dụng. Phiên bản hiện tại là dữ liệu nền, không đồng nghĩa đã đạt tiêu chuẩn nghiệm thu.

## Phạm vi xây dựng

Chỉ hoàn thiện một quy trình: nguồn PDF/ảnh mặt bằng nhà ở dân dụng → thông tin bổ sung có cấu trúc → bản dự thảo → kiểm tra → preview và duyệt tại AutoCAD.

Các quy trình chat tự do, chèn cửa/trục demo, mặt cắt và bộ thư viện mẫu đã được đưa khỏi danh sách sử dụng. Không phát triển thêm Skill chuyên môn khác trước khi quy trình này đạt nghiệm thu.

## Thứ tự hoàn thiện

1. Chuẩn hóa Template Quang Anh: bảng layer và vai trò, block theo nhóm, trạng thái visibility, gốc chèn, đơn vị, tham số động và quy tắc giữ tiêu chuẩn nội bộ.
2. Chốt bảng loại cửa: mã cửa, loại, rộng/cao ô chờ xây, quy đổi sang tham số block, hướng mở, tường áp dụng; tách bảng loại khỏi bảng vị trí sử dụng.
3. Chốt đặc tả Skill: phạm vi, biểu mẫu nhập thêm thông tin, quy tắc ưu tiên/mâu thuẫn/ước lượng, mô hình tường và lỗ mở, các bước xử lý, đầu ra và điều kiện đạt.
4. Hoàn thiện bộ thực thi và giao diện theo đặc tả đã chốt; trường chưa được bộ thực thi hỗ trợ phải được chỉ rõ.
5. Chạy ca Quang Anh từ app tới AutoCAD: kiểm tra kích thước bao, tường 220/110, cửa, layer, block và chiều mở; preview, áp dụng, Undo và mở lại DWG.
6. Chỉ sau nghiệm thu mới sao chép cấu trúc chuẩn để phát triển Skill tiếp theo.

## Thông tin cần chốt tiếp

- Danh mục block cho cửa đi, cửa sổ, nội thất, vệ sinh và ký hiệu; xác định loại nào được phép sử dụng.
- Danh sách kích thước cửa do người dùng cung cấp và vị trí gán trên mặt bằng.
- Cách xử lý kích thước thiếu hoặc mâu thuẫn; phạm vi cho phép ước lượng trong bản thử.
- Tường, giao góc và lỗ mở; trục chỉ đưa vào khi có vị trí/tên được xác nhận.
- PDF vector, PDF scan và ảnh: công bố rõ chức năng hỗ trợ và tiêu chí kiểm tra từng loại.

Đặc tả chi tiết nền: SKILL-SPECIFICATION-REVIEW.md.

## Dọn dữ liệu thử nghiệm

Đã chuyển 4 template và 10 Skill đang hiển thị trước đó vào thùng rác. Các phiên bản và lịch sử cũ vẫn được giữ để đọc/khôi phục; không xóa DWG, ảnh nguồn, thư viện DWG Quang Anh hoặc dữ liệu tài khoản.

Các bộ chia nhỏ Quang Anh cũ vốn đã bị ẩn khỏi danh sách vẫn là bản lưu lịch sử, không phải quy trình đang sử dụng.

Bằng chứng trước/sau: tests/results/library-cleanup-before.json và library-cleanup-after.json. Bản nháp AI 91 thao tác chưa được nghiệm thu và chưa được ghi vào CAD.
