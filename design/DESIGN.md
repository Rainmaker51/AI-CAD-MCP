# AI CAD MCP — Đặc tả giao diện v0.1

Ngày: 10/11/2026. Cơ sở sản phẩm: `../../PLAN MCP NEW.md`.

## Phạm vi bản thiết kế

Thiết kế app mới theo PLAN MCP NEW. Không kế thừa bố cục hoặc hệ thống điều hướng của app cũ. Mã cũ chỉ được đánh giá riêng khi triển khai backend.

Prototype HTML/CSS/JavaScript nằm tại `prototype/index.html`, chạy được trực tiếp bằng trình duyệt, không dùng CDN và không cài dependency. Đây là tài liệu tương tác để đánh giá UX của desktop app, chưa chốt framework giao diện sản phẩm và chưa kết nối MCP/AutoCAD/OCR thật. Dữ liệu ví dụ không phải hồ sơ thiết kế thực.

## Áp dụng UI/UX Pro Max

Đã chạy công cụ tra cứu bằng Python 3.14 cài trên máy:

- Design system: `CAD engineering desktop workspace`; sau đó thu hẹp `engineering operations dashboard`.
- UX: `error prevention approval`; `keyboard focus dialog`.
- Stack: `desktop navigation accessibility --stack wpf`, làm tham khảo khả năng triển khai Windows; không coi là quyết định dùng WPF.

Kết quả phù hợp: Minimalism & Swiss Style, lưới rõ ràng, độ tương phản cao, màu xanh cho thao tác chính; bàn phím, focus, thông báo lỗi gắn trường nhập và automation name cho bản native.

Kết quả không phù hợp: cả hai lượt design-system trả pattern landing page “Hero + Features + CTA”. Loại bỏ pattern này; không lưu output đó thành tiêu chuẩn dự án. Bố cục workspace dưới đây được thiết kế riêng theo nghiệp vụ CAD. Không dùng scroll reveal/GSAP trong công cụ làm việc.

## Mô hình tương tác

Hai bề mặt phối hợp:

1. **Workspace quản lý:** phiên/DWG, danh sách task, nạp nguồn, quản trị Skill và Template, lịch sử, QA.
2. **Palette trong AutoCAD:** preview thực, thông số tác động, thông tin đúng file/phiên/phạm vi, duyệt hoặc hủy. Palette hẹp không phải bản thu nhỏ của toàn bộ workspace.

Khung canvas trong prototype là hình minh họa. Không coi browser canvas là CAD engine hoặc bản xem trước đã xác thực. Hộp thoại duyệt mô phỏng palette; sản phẩm phải dùng kết quả preview và quyền duyệt cục bộ thật.

Luồng cốt lõi: chọn phiên và DWG → quét `AI_TASK` → kiểm tra dữ liệu/phụ thuộc → chọn Skill và template version → tạo DrawingPlan → xem trước trong AutoCAD → duyệt/hủy tại plugin → kiểm tra lại revision → xếp hàng ghi → Apply → QA → lịch sử.

## Kiến trúc thông tin

| Màn hình | Nội dung và thao tác chính | PLAN |
|---|---|---|
| Triển khai | Khung task, trạng thái, phạm vi, chi tiết task, preview, bổ sung dữ liệu | F03, F05, F08 |
| Phiên & bản vẽ | Phiên kết nối, DWG mở, bản vẽ đích, trạng thái lưu, Model/Layout | F04 |
| Thư viện Skill | Tìm, nhập, validate, phụ thuộc, enable/disable, version pin/rollback | F07 |
| Thư viện Template | Block/layer/style/preset/DWT, metadata, checksum, publish, dependency impact | F06 |
| Nguồn PDF / ảnh | Chọn nguồn, trang/vùng, hiệu chuẩn, confidence, xác nhận dữ liệu | F01 |
| Lịch sử & chất lượng | Plan/execution, entity thay đổi, QA, checkpoint/restore | E01, E03 |
| Cài đặt | Preview policy, kết nối, cấu hình theo dự án | F04, F08 |

Các chức năng chưa thực hiện trong prototype phải được ghi rõ giới hạn. Chưa có GUI đầy đủ cho import/validate/publish/rollback, chọn vùng ảnh, checkpoint/restore hoặc so sánh revision. Tương tác “Nhập” chỉ chọn tệp và thông báo bước xử lý tiếp theo; không giả vờ validate/publish thành công.

## Màn hình Triển khai

- Sidebar giữ điều hướng nhất quán; task là nội dung chính, không lấy biểu đồ/thống kê làm trung tâm.
- Thanh ngữ cảnh luôn nêu tên DWG, phiên, không gian và revision; không dùng tab CAD hiện tại làm đích ghi ngầm định.
- Cột chính: hình minh họa bản vẽ và danh sách khung. Cột phải: nhiệm vụ đã chọn, Skill/version, template/version, thông số, phụ thuộc và kiểm tra.
- Một thao tác chính theo trạng thái: bổ sung dữ liệu, lập kế hoạch, xem/duyệt hoặc xem kết quả.
- Trạng thái có nhãn và màu; không chỉ dùng màu để biểu đạt.
- Nếu có task phụ thuộc, hiển thị task nguồn và lý do chưa chạy. Phân tích có thể song song; hàng đợi ghi theo từng DWG.

## Duyệt và áp dụng

Nội dung duyệt: phiên + DWG đích + không gian + task/scope + số tạo/sửa/xóa + Skill/template version + revision + plan identity.

| Tình huống | Hành vi sản phẩm cần có |
|---|---|
| Chưa có preview đã xác thực | Chưa cho Apply; hướng dẫn tạo preview |
| DWG đóng / mất kết nối | Chặn Apply; thông báo lỗi và kết nối lại |
| Đổi bản vẽ đích | Không chuyển approval sang file mới; phải tạo kế hoạch/preview mới |
| Revision, quyền hoặc tài nguyên thay đổi | Preview hết hiệu lực; chỉ rõ nguyên nhân; tạo lại |
| Hủy preview | Xóa transient/staging; không đổi DWG, không tạo Undo marker |
| Duyệt | Thực thi cùng plan đã xác thực, không gọi model để duyệt lại |
| Đang Apply | Nút disabled + tiến trình; không cho nhấn lặp |
| Hoàn tất | Kết quả, QA và execution/receipt rõ ràng |
| Lỗi trước commit | Báo chưa ghi; cho kiểm tra và thử lại theo policy |
| Không biết kết quả sau dispatch | Trạng thái cần đối soát; không tự retry Apply |

Không dùng một popup xác nhận chung để thay thế kiểm tra plan/scope/revision. Trong giai đoạn đầu mặc định ALWAYS. OFF/SMART không bỏ qua quyền ghi, kiểm tra nguồn, hoặc yêu cầu duyệt của policy.

## Skill và Template

- Skill: mã/tên/version, mô tả, tool bắt buộc, asset bắt buộc, trạng thái, lý do bị vô hiệu hóa.
- Template: ID/version/checksum/nguồn/phụ thuộc. Phân biệt bản nháp và bản công bố.
- Nút không khả dụng phải có nguyên nhân, ví dụ thiếu `create_hatch`.
- Task dùng snapshot đã chốt. Disable/upgrade không âm thầm đổi cấu hình task đang chạy; policy có thể thu hồi quyền thực thi và phải thể hiện lý do.
- Worker không sửa thư viện gốc. Import/validate/publish là luồng quản trị riêng.

## Nguồn và nghiệp vụ kiến trúc

- Tách vector PDF và raster/scan; hiển thị loại được phát hiện và phương pháp trích xuất.
- Hiệu chuẩn cần hai điểm chuẩn cùng khoảng cách có căn cứ; chỉ nhập một số chưa đủ xác lập scale.
- Hiển thị phần chưa chắc chắn theo vùng/đối tượng; người dùng xác nhận hoặc hiệu chỉnh trước khi tạo hình học chính thức.
- Mặt cắt cầu thang/WC/nhà cần biểu mẫu dữ liệu bắt buộc theo Skill. Thiếu cao độ/cấu tạo thì NEEDS_INPUT.
- Không dùng thông báo “thành công” khi mới nhận tệp hoặc khi chưa có kết quả backend.

## Hệ thống thị giác

| Token | Light | Dark |
|---|---|---|
| Background | `#F4F6F9` | `#111824` |
| Surface | `#FFFFFF` | `#192333` |
| Primary text | `#19253A` | `#E3EAF5` |
| Secondary text | `#59677C` | `#A5B4CA` |
| Action | `#285DDD` / chữ trắng | `#95B5FF` / chữ `#10203A` |
| Selected | `#EDF2FF` | `#263653` |
| Border | `#DCE2EB` | `#39485D` |
| Success | `#18754F` trên `#E9F6EF` | `#8BDBB5` trên `#203E35` |
| Needs input | `#935400` trên `#FFF3DF` | `#FFD089` trên `#443720` |
| Error | `#B52E3E` trên `#FFF0F2` | `#FFA1AF` trên `#472737` |

Typeface prototype: Segoe UI/Arial để đọc tiếng Việt và chạy offline; Consolas cho ID, tọa độ, version. Font heading 23–28 px, body 14 px, thông tin phụ 11–12 px. Nhãn eyebrow 9–10 px chỉ mang tính phụ trợ; thông tin cần để quyết định thao tác phải đọc được trong body/field. Bản native hỗ trợ text scaling và Windows high contrast.

Spacing theo nhịp 4/8 px. Radius 6 px cho control, 10 px cho panel. Ít bóng đổ, không gradient trang trí. Icon SVG cùng nét. Motion chỉ phục vụ phản hồi trạng thái; tắt chuyển động khi reduced motion.

## Responsive và khả năng truy cập

- Desktop: sidebar + vùng làm việc + inspector; tối ưu 1440 px trở lên.
- Dưới 900 px: sidebar thu gọn thành icon có tên truy cập; inspector chuyển xuống dưới.
- Dưới 600 px: thẻ và form xếp một cột; giữ nội dung file/phạm vi/preview. Đây là khả năng đọc và review bản mẫu, không thay thế trải nghiệm thao tác CAD trên desktop.
- Button/input/select dùng HTML semantic; focus ring 3 px; dialog native hỗ trợ focus và Escape.
- Task chọn được bằng Enter/Space; lỗi nhập liệu có `role=alert`, `aria-describedby`, focus trường lỗi.
- Trạng thái động thông báo qua `role=status`; văn bản/file người dùng nhập được escape trước khi render HTML.
- Màu semantic được kiểm tra độc lập cho hai theme; ưu tiên độ tương phản chữ 4.5:1.

## Tiêu chí nghiệm thu giao diện sản phẩm

1. Người dùng luôn xác định được phiên, DWG và vùng tác động trước khi duyệt.
2. Từ NEEDS_INPUT tìm được trường thiếu và sửa được bằng bàn phím.
3. Cancel không thay đổi DWG; Apply dùng đúng plan và retry không tạo trùng.
4. Mất kết nối hoặc revision stale chặn Apply và có bước phục hồi rõ ràng.
5. Ba task cùng DWG, một task phụ thuộc; trạng thái và lý do chờ dễ thấy.
6. Thêm Skill từ package có tool sẵn không thay MCP Core; thiếu dependency có thông báo cụ thể.
7. Template dùng chung có version pin; task cũ không đổi phiên bản âm thầm.
8. Giao diện không đưa PID/pipe/token/schema nội bộ vào thao tác người dùng thông thường.
9. QA thể hiện lỗi sau commit khác với lỗi chưa ghi; kết quả unknown cần đối soát.
10. Kiểm tra bàn phím, high contrast, DPI và text scaling trên framework native đã chọn trước phát hành.
