# Thư viện Hatch Quang Anh

Trong Thư viện CAD, chọn tab Hatch. App đọc các đối tượng Hatch trong mọi định nghĩa block và layout của `CAD/Originals/ISO_QuangAnh_ThuVien.dwg`, bỏ qua xref chưa bind. Đây là các định nghĩa hatch trong file, không phải số lần xuất hiện sau khi nhân block hoặc thống kê khối lượng công trình.

Nút **Nhập / cập nhật từ DWG nguồn** đọc bản sao bằng AutoCAD Core Console; không thay đổi DWG đang mở. Mỗi cấu hình mẫu/tỷ lệ/góc/layer/màu/khoảng cách/hatch kép có một mục. Mục giống nhau được gộp và ghi số lượng cùng block/layout nơi xuất hiện. Mẫu thu nhỏ PNG 240 × 180 chỉ thể hiện hình dáng của hatch đại diện; hatch SOLID dùng ô tô đặc. Một số hatch quá nhiều nét được giới hạn ảnh; hồ sơ ghi rõ trạng thái này.

Danh mục và ảnh lưu theo phiên bản tại `Hatches/ISO_QUANGANH` trong kho chung, tham chiếu checksum DWG và giữ DWG nguồn ở `CAD/Assets`. Máy thứ hai cùng kho sẽ đọc được thư viện hatch, không cần nhập lại. Git chỉ quản lý mã nguồn tính năng. Cập nhật khi file gốc thay đổi sẽ tạo phiên bản mới; không ghi đè danh mục cũ.

Bấm mẫu để xem chi tiết, dùng ô tìm kiếm để lọc theo tên pattern, layer hoặc block/layout nguồn. Có thể xuất danh mục JSON. Nút sao chép cấu hình chỉ xuất hiện với cấu hình được công cụ `create_hatch` hiện tại hỗ trợ (ANSI31/SOLID, scale 1–1000, góc 0). Mẫu tùy chỉnh và các cấu hình khác được lưu để tra cứu; chưa tự mở rộng quyền hay khả năng ghi CAD. Mọi ghi DWG vẫn qua kế hoạch, preview và duyệt tại AutoCAD.

Kiểm tra độc lập: `node tests/hatch-library.test.cjs` và bộ `tests/Source.Regression`.

## Quản lý và ghi chú cho Skill

Tìm tên loại hatch tại ô **Tên loại hatch (Pattern)**, chọn **Scale** và **Góc** để thu hẹp cấu hình. Ô tìm công dụng còn lọc được tên hiển thị, ghi chú, layer và block nguồn. Kết quả được trình bày theo pattern; các cấu hình khác scale/góc không gộp công dụng với nhau. Tên pattern gốc luôn hiện trên thẻ dù đã đặt tên hiển thị khác. Một cấu hình có nhiều công dụng có thể ghi nhiều dòng trong hồ sơ. Chọn nhóm hoặc Thùng rác sẽ xóa bộ lọc pattern/scale/góc để tránh giữ bộ lọc không thuộc nhánh mới.

Mỗi loại pattern có thể thu gọn/mở rộng bằng tiêu đề; các nút **Thu gọn tất cả / Mở rộng tất cả** áp dụng cho kết quả đang hiển thị. Trạng thái được giữ trong lần mở trang hiện tại khi tìm kiếm hoặc chỉnh hồ sơ. Mục **Chưa có công dụng** là bộ lọc tự động cho những cấu hình chưa điền `Purpose` (không tính thùng rác). Sau khi bổ sung công dụng và lưu, mục tự rời khỏi danh sách này; không làm thay đổi nhóm quản lý đã gán.

**Hiển thị theo → Công dụng** gom các cấu hình thuộc nhiều pattern vào nhóm công dụng, giữ nguyên pattern, scale và góc trên từng thẻ. Nhiều công dụng trong hồ sơ được phân tách bằng dấu chấm phẩy hoặc xuống dòng; dấu phẩy vẫn thuộc tên công dụng (ví dụ “Vữa, chất kết dính”). Cấu hình có nhiều công dụng xuất hiện trong mỗi nhóm tương ứng, nhưng tổng cấu hình vẫn đếm theo mục duy nhất. Nhóm công dụng tự tạo từ dữ liệu, không di chuyển hoặc thay đổi cây nhóm quản lý. Cả chế độ pattern và công dụng đều dùng được tìm kiếm, lọc scale/góc và thu gọn/mở rộng.

Bấm một mẫu → **Quản lý / ghi chú** để đặt tên dễ hiểu, chọn nhóm, ghi công dụng và các quy tắc sử dụng. Mỗi mẫu có thể được đánh dấu **Đã rà soát công dụng, cho phép Skill tham khảo**; cần có công dụng trước khi duyệt. Chat và bộ dựng ảnh nhận danh mục đã duyệt cùng cờ khả năng triển khai của CAD Engine. Ghi chú là dữ liệu tham khảo, không phải chỉ thị thực thi.

Hatch có cây nhóm riêng. Nhóm hỗ trợ nhóm con, đổi tên, chuyển nhánh và xóa nhóm. Xóa nhóm chỉ bỏ liên kết nhóm, giữ ghi chú/công dụng của mẫu. Xóa mẫu đưa vào thùng rác, loại khỏi danh sách đang dùng và dữ liệu Skill; có thể khôi phục trong Thùng rác Hatch.

Thông tin quản lý nằm tại `Catalog/management/hatches/` của kho chung, tách khỏi danh mục hatch và DWG nguồn. Nhập lại cùng cấu hình giữ thông tin quản lý theo mã cấu hình; cấu hình đổi tỷ lệ/góc/layer/màu sẽ có mã khác và cần rà soát lại. App kiểm tra số revision trước khi lưu để tránh ghi đè chỉnh sửa từ máy khác đã được đồng bộ.
