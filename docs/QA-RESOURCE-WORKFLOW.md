# Thư viện QA và cấu hình riêng của add-in

QA 1.7 quản lý tài nguyên gốc trong DWG: block, layer, hatch, text, dim, nhóm, tên và ghi chú. Một block gốc hiện một thẻ; các hồ sơ nghiệp vụ cũ vẫn còn trong dữ liệu để lưu trữ, không làm tăng số thẻ.

QA không có bước duyệt sử dụng, Role hoặc quy đổi kích thước cửa. Chèn thử dùng giá trị gốc của DWG và cho nhập tham số/ATT ngay lúc chèn. Lưu danh mục vào kho vẫn đối chiếu DWG đã lưu, xác nhận các thay đổi được chọn; không đồng nghĩa với duyệt quyền sử dụng.

Từng add-in chọn tài nguyên theo ID, tự lưu cấu hình nghiệp vụ tại `Catalog/addin-settings/<tool-id>.json`, kiểm tra trước khi chạy. Không dùng tên/mã hiển thị làm ID, không sửa định nghĩa block hoặc Usage chung để cấu hình cho một công cụ. Đổi máy dùng cấu hình đã lưu trong kho; thiết lập cửa sổ/cache và đường dẫn kho của máy vẫn lưu cục bộ.

Tạo trục 1.8 dùng block được chọn, tag ATT và layer riêng; bỏ phụ thuộc Role/Reviewed/giá trị ghi đè của Skill. Chọn sai tag hoặc tài nguyên thiếu vẫn phải sửa lựa chọn trước khi chạy. Bộ tạo hình học cũ còn có cấu trúc tương thích nội bộ, không lấy quyền duyệt từ kho.

PDF/ảnh sang CAD ngừng nhận công việc mới và không khởi động worker hàng đợi. Các endpoint ghi trả `PDF_CAD_ARCHIVED`; mã và dữ liệu giữ để phát triển sau. Skill nguồn đã ngừng không ngăn QA xóa tài nguyên hoặc bị tự cập nhật khi đổi tên layer/dim/text.

`tools/Archive-LegacyApp.py` mặc định chỉ liệt kê. `--apply` yêu cầu CAD và Host đã đóng, tạo `Dọn dẹp/App-cu-<thời điểm>`, sao lưu và kiểm tra hash trước khi chuyển Skill nguồn/PDF/ảnh/hàng đợi. Không chuyển Catalog dùng chung, template Quang Anh, CAD, Tools hoặc cấu hình add-in. Manifest phân biệt dữ liệu đã chuyển và snapshot giữ nguyên vị trí.

Không thêm DLL mới, server hoặc dịch vụ nền. QA sử dụng Library.Core đang có; add-in kiểm tra nghiệp vụ trong tiến trình AutoCAD.
