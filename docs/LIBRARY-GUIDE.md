# Quản lý thư viện AI CAD 0.4

## Nhập block, layer và style từ DWG/DWT

1. Mở app bằng `Mo AI CAD.cmd`, vào **Thư viện Template**.
2. Chọn **Nhập JSON / DWG / DWT**, chọn DWG hoặc DWT tối đa 20 MB.
3. App đọc một bản sao bằng AutoCAD Core Console; không sửa tệp nguồn hoặc DWG đang mở.
4. Nhập mã/tên Template và phiên bản. Chọn block, layer và các thông số style cần lấy. Có thể đổi mã tài nguyên ngay trên biểu mẫu. Với nguồn không khai báo đơn vị, chỉ xác nhận mm khi bản vẽ thực sự dùng mm; nguồn m/inch cần chuyển đơn vị trong CAD trước.
5. Nhấn **Kiểm tra & xem lại trước khi xuất bản**, kiểm tra số tài nguyên và Skill liên quan, đánh dấu xác nhận rồi **Xuất bản phiên bản**.

Block được sao chép native, giữ block lồng, cung/đường cong, màu, layer, style và thuộc tính mặc định; block động được chèn ở trạng thái mặc định. Có thể chọn toàn bộ Model thành một block chi tiết. Không nhập layout giấy, tài nguyên 3D, xref chưa bind, proxy entity, ảnh/underlay phụ thuộc file ngoài. Danh sách hiển thị rõ các mục bị loại. Hình thu nhỏ chỉ là sơ đồ; xem trước đầy đủ trong AutoCAD. Font cần có trên máy để hiển thị đúng.

Style cho chữ/kích thước mới lấy font, chiều cao chữ và cỡ mũi tên, dùng tỷ lệ mm/1:1 cùng các thiết lập engine hiện có. Không tuyên bố sao chép toàn bộ Dimstyle DWT thành tiêu chuẩn của app. Style nằm bên trong block gốc vẫn được giữ qua native cloning.

## Cập nhật và quay lại phiên bản

- **Cập nhật** mở biểu mẫu metadata/JSON; app đề xuất phiên bản mới, không ghi đè phiên bản đã xuất bản.
- **Nhập thêm DWG/DWT** trên thẻ Template đưa dữ liệu CAD vào Template đó. Mặc định giữ tài nguyên hiện có, thay những mã trùng được chọn.
- **Phiên bản** cho phép chuyển về phiên bản cũ sau khi kiểm tra phụ thuộc.
- **Xuất JSON** tải cấu hình của phiên bản đang dùng hoặc một phiên bản cụ thể. Với block native, JSON tham chiếu DWG theo SHA256 trong `CAD/Assets` của kho đã chọn; khi chuyển máy cần giữ cả dữ liệu nguồn. Đây không phải gói backup đầy đủ chỉ gồm một JSON.

## Dùng thư viện trong chat

1. Vào **Thư viện Skill → Tạo Skill**.
2. Đặt mã, tên, phiên bản; chọn Template vừa nhập và loại **Chat CAD tự do**, rồi xuất bản. Với workflow chuyên môn dùng cấu hình JSON.
3. Trong AutoCAD dùng `AICADTASK`, chọn điểm đặt khung và nhập đúng mã Skill vừa tạo. Plugin 0.4 tự lấy Template từ Skill.
4. Trong app chọn phiên/DWG, quét khung, chọn khung đó rồi yêu cầu chèn block bằng mã đã nhập.
5. Xác nhận tại palette AutoCAD trước khi ghi. Nhập thư viện và quản lý phiên bản không gọi model AI.

Thư viện `LIB_CAD_MAU` và Skill `CHAT_THU_VIEN_MAU` là dữ liệu mẫu kiểm thử, không phải tiêu chuẩn công ty.

## Kiểm thử

- Managed tests kiểm tra version immutability, rollback, checksum, giới hạn đường dẫn, metadata native và phụ thuộc.
- Native fixtures kiểm tra block lồng/cung/thuộc tính, Model thành block, layer trùng không bị ghi đè và DWG đầu ra mở lại được.
- API live kiểm tra DWT → phân tích → review → publish → update → rollback; nguồn không đổi hash; mục 3D bị từ chối.
- Browser kiểm tra biểu mẫu cập nhật và phiên bản. Bước chọn tệp và tải xuống tự động trong trình duyệt bị người dùng từ chối quyền; không dùng đường vòng để chọn/tải lại tệp sau đó. Người dùng thực hiện các bước này trực tiếp.


Từ bản 0.5, giới hạn là 1.000 block/Template và 100 layer. Quang Anh đã được gộp thành ISO_QUANGANH, 288 block. Kho được chọn trong Cài đặt và nằm ngoài app; xem [kho chung và điểm đặt](WAREHOUSE-AND-PLACEMENT.md).
