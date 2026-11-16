# Nguồn thư viện Quang Anh

Nguồn chỉnh sửa chính: `CAD/Originals/ISO_QuangAnh_ThuVien.dwg` trong kho được chọn bằng `warehouse.local.json`. Không đặt đường dẫn tuyệt đối của một máy vào mã dùng chung.

Block, Layer, Hatch, Text và Dim đang dùng đều tham chiếu bản chụp của file này. `CAD/Assets/<sha256>.dwg` là bản chụp bất biến để dựng CAD; không sửa trực tiếp file trong Assets. Các phiên bản cũ và DWG nguồn cũ được giữ để đối chiếu và quay lại khi cần.

Không theo dõi hoặc tự quét khi lưu CAD. Sau khi sửa và lưu DWG:

- Block/Layer: mở đúng Template, chọn **Nhập thêm DWG/DWT**, chọn nguồn chính, chọn tài nguyên cần cập nhật và giữ chế độ gộp. Kiểm tra trước khi xuất bản. Cùng mã và cùng tên định nghĩa block sẽ giữ cấu hình Usage; khi thay đổi tên hoặc tham số phải kiểm tra lại cấu hình.
- Hatch: chọn **Nhập / cập nhật từ DWG nguồn**. Mã hatch dựa trên cấu hình mẫu, scale, góc, layer và màu; thay cấu hình tạo mã khác, cần phân nhóm/ghi công dụng cho mã mới. Trạng thái thùng rác của mã cũ được giữ.
- Text/Dim: nhập/cập nhật từ DWG nguồn. Ghi chú và nhóm dùng mã style theo tên; đổi tên sẽ tạo mục khác.
- Tạo/cập nhật ảnh block sau khi xuất bản Template mới.

Đặt block sang khu thùng rác trong bố cục DWG không thay đổi trạng thái quản lí. Không chọn các block đó khi nhập lại. Nguồn chung cũng chứa đối tượng gốc để đối chiếu và block phụ thuộc; chúng không tự được thêm vào danh mục đang dùng.

Chuyển nguồn ngày 16/11/2026: Template `ISO_QUANGANH` v1.2.3 (39 block, 33 layer), bộ layer chung `QUANGANH_LAYERS` v1.0.1, 24 hatch đang dùng và 33 hatch trong thùng rác, 16 Text style, 8 Dim style. Giữ mã tài nguyên, cấu hình Usage, nhóm, ghi chú và trạng thái duyệt. Bản sao lưu và báo cáo chuyển nguồn nằm trong `outputs/unified-library-*` trên máy thực hiện.
