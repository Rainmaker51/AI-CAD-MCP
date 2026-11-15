# Rà soát và đề xuất đặc tả Skill AI CAD

Ngày rà soát: 11/11/2026. Trạng thái: bản đề xuất để chốt nghiệp vụ; chưa áp dụng cấu trúc mới vào code hoặc thay phiên bản Skill.

## 1. Kết quả kiểm tra hiện trạng

Đã đọc 11 cấu hình Skill đang xuất hiện trong app và đối chiếu với CAD.Contracts/PlanFiles.cs, Application/WorkspaceService.cs, Application/ImagePlanService.cs, Application/DrawingIntelligence.cs và CAD.Geometry/ArchitectureBuilder.cs.

| Skill | Phiên bản | Bộ thực thi | Số đầu vào bắt buộc | Recipe nghiệp vụ trong Skill |
|---|---|---|---:|---|
| BUILDING_SECTION | 1.0.0 | BUILDING_SECTION | 5 | Chưa có |
| CAD_QUANGANH | 1.0.0 | custom | 0 | Chưa có |
| CAD_TOOLS | 1.0.0 | custom | 0 | Chưa có |
| CHAT_THU_VIEN_MAU | 1.0.0 | custom | 0 | Chưa có |
| DRAW_PLAN | 1.0.0 | workflow | 1 | Chưa có |
| INSERT_DOOR_AXIS_DEMO | 1.0.0 | workflow | 0 | Chưa có |
| SOURCE_FLOORPLAN | 1.1.0 | source | 2 | Chưa có |
| SOURCE_FLOORPLAN_QUANGANH | 1.0.1 | source | 2 | Chưa có |
| SOURCE_ISO_QUANGANH | 1.0.0 | source | 2 | Có; chưa có block được phép |
| STAIR_SECTION | 1.0.0 | STAIR_SECTION | 9 | Chưa có |
| WC_SECTION | 1.0.0 | WC_SECTION | 6 | Chưa có |

Các số lượng phản ánh cấu hình hiện tại. Workflow rỗng ở Skill dùng builder chuyên biệt không có nghĩa là không có thuật toán: thuật toán mặt cắt hiện nằm trong ArchitectureBuilder. Tuy nhiên người dùng không thể xem và sửa đầy đủ quy tắc đó từ trình biên soạn Skill.

### Những thiếu hụt cụ thể

1. RequiredInputs đang là danh sách mã trường. Chưa mô tả nhãn tiếng Việt, kiểu dữ liệu, đơn vị, danh sách lựa chọn, điều kiện xuất hiện, gợi ý hay cách báo lỗi cho từng trường.
2. Phần lớn Skill chỉ có mã, tên, công cụ, Template và tên bộ thực thi. Parameters đều rỗng trong 11 cấu hình được rà soát. Chưa có bộ đặc tả nghiệp vụ tự quản lý đủ chi tiết.
3. SOURCE_ISO_QUANGANH có quy ước layer, tường 220/110 và cơ sở kích thước cửa, nhưng AllowedBlocks còn rỗng. Đây là cấu hình chưa hoàn chỉnh cho quy trình dùng block.
4. Quy tắc ảnh đang tập trung trong chuỗi chỉ dẫn của ImagePlanService. Chuỗi đó còn nhắc hai phòng ngủ, phòng thờ, WC, ban công và chú thích 15.000/16.000 của bản mẫu Quang Anh. Các chi tiết này phải chuyển thành dữ liệu công trình hoặc ca kiểm thử, không áp đặt cho mọi nhà ở.
5. Các Skill chat dùng chung bộ lập kế hoạch. Chưa có danh mục hành vi, đầu vào và tiêu chí nghiệm thu riêng theo nghiệp vụ của từng Skill.
6. Các Skill mặt cắt đã có kiểm tra số liệu trong code, nhưng giao diện Skill chưa giải thích đủ phạm vi, giới hạn, nguồn số liệu và trường hợp ngoài khả năng của bộ thực thi.
7. PDF vector, PDF scan và ảnh cần quy trình nhận dạng/kiểm tra riêng. Hiện nhãn PDF/ảnh chưa biểu đạt rõ mức độ hỗ trợ thực tế của từng đường xử lý.
8. Chưa có bảng cửa theo mã, phạm vi áp dụng theo tầng/phòng, xử lý ngoại lệ, mức tin cậy và liên kết từ thông tin người dùng tới thao tác CAD.
9. Kiểm tra phần mềm và kiểm tra hình học cơ bản đã có, nhưng chưa đủ để nghiệm thu chất lượng kiến trúc của từng Skill. Số test đạt không thay thế việc đối chiếu bản vẽ thực tế.

## 2. Cấu trúc bắt buộc của một Skill hoàn chỉnh

| Phần | Nội dung cần lưu và cho phép quản lý |
|---|---|
| Thông tin và phạm vi | Mã, tên, nhóm, phiên bản, mục tiêu, loại công trình, đầu ra, việc có hỗ trợ/chưa hỗ trợ |
| Biểu mẫu đầu vào | Trường nhập có kiểu, đơn vị, lựa chọn, mặc định, điều kiện bắt buộc, trợ giúp và kiểm tra |
| Quy tắc nghiệp vụ | Tường, cửa, cầu thang, trục, kích thước, nội thất; quy tắc ưu tiên và ngoại lệ |
| Liên kết tài nguyên | Bộ layer, vai trò layer, danh mục block, visibility, tham số động và phiên bản |
| Các bước xử lý | Đọc nguồn, lập mô hình đối tượng, giải quyết mâu thuẫn, sinh hình học và kiểm tra |
| Nghiệm thu và ví dụ | Điều kiện đạt, lỗi phải chặn, cảnh báo, ca mẫu đúng/sai, kết quả mong đợi |

Prompt được sinh từ đặc tả, dữ liệu công trình và tài nguyên đã liên kết. Quy tắc đo đạc, sinh hình học và quyền ghi được kiểm tra bằng CAD Engine.

## 3. Skill mẫu: PDF/ảnh → mặt bằng kiến trúc nhà ở dân dụng

### 3.1. Mục tiêu và giới hạn

Đầu ra: mặt bằng kiến trúc 2D có đối tượng CAD chỉnh sửa được, layer theo bộ chuẩn, block đúng danh mục, bảng kích thước đã dùng và danh sách vấn đề cần kiểm tra.

Nguồn: PDF vector, PDF scan hoặc ảnh. Người dùng chọn trang, vùng mặt bằng và phạm vi cần dựng; app phải công bố đường xử lý nào đang được hỗ trợ.

Không suy ra khả năng chịu lực, cấu tạo kết cấu hoặc chiều cao tầng từ mặt bằng không đủ dữ liệu. Mặt đứng, mặt cắt và chi tiết cấu tạo có Skill riêng, tham chiếu mô hình công trình đã xác nhận.

Không tự áp bố cục của bản mẫu Quang Anh lên công trình khác.

### 3.2. Biểu mẫu đầu vào

| Nhóm | Trường | Quy tắc |
|---|---|---|
| Nguồn | Tệp, trang, vùng cần dựng, hướng bản vẽ | Bắt buộc chọn đúng phạm vi |
| Hiệu chuẩn | Hai điểm và khoảng cách; kích thước bao nếu có | Có ít nhất một cơ sở tỷ lệ xác nhận; kiểm tra tỷ lệ hai phương nếu có đủ dữ liệu |
| Công trình | Tên, tầng, loại mặt bằng, đơn vị | Đơn vị làm việc mm; chuyển đổi phải minh bạch |
| Phạm vi triển khai | Tường, cửa, nội thất, thiết bị, cầu thang, trục, kích thước, chữ | Chọn phần cần dựng; phần bỏ qua được ghi lại |
| Tường | Tường bao, tường ngăn, loại khác | Bộ mặc định người dùng đã chốt là 220/110 mm; cho phép ngoại lệ có vị trí áp dụng |
| Cửa | Bảng mã cửa và bảng gán vị trí | Rộng/cao theo ô chờ xây, hướng mở và liên kết block |
| Cầu thang | Rộng vế, số bậc, mặt bậc, chiếu nghỉ nếu biết | Không suy đoán chiều cao tầng thành số liệu xác nhận |
| Trục | Vị trí và tên trục | Tự bố trí nếu nguồn thiếu trục. Trục ngang 1, 2, 3… từ dưới lên, trục đứng A–Z từ trái sang phải. Hiển thị trên bản xem trước để duyệt cùng mặt bằng. |
| Tỷ lệ chú thích | Tỷ lệ annotative chính, tỷ lệ bổ sung nếu cần | Chọn lúc triển khai và gán cho text/DIM. Hình học Model theo mm thực. |
| Tài nguyên | Bộ layer, danh mục block, phiên bản | Kiểm tra đủ liên kết cho các thành phần được chọn |
| Nơi đặt | DWG đang kết nối, khung nhiệm vụ, AI_OUTPUT_POINT | Ghi đúng bản vẽ và điểm đặt; không tự mở CAD khác khi đã chọn DWG |
| Yêu cầu bổ sung | Ghi chú và trường tự tạo | Hiển thị phần nào đã được áp dụng, phần nào chưa hỗ trợ hoặc còn mâu thuẫn |

Tổ chức giao diện thành các nhóm mở rộng, lưu nháp khi đổi thư viện/Skill hoặc mở hộp cấu hình. Không làm mất kích thước và thông tin vừa nhập.

### 3.3. Danh sách cửa và vị trí áp dụng

Bảng loại cửa gồm: mã, tên loại, số cánh, rộng ô chờ, cao ô chờ, loại tường áp dụng, danh mục block, visibility, hướng mở/đảo hướng và ghi chú.

Bảng vị trí gồm: vị trí cửa trong nguồn, tầng/phòng, mã cửa được gán, phương đặt, phía bản lề, chiều mở, kích thước ngoại lệ, nguồn số liệu và trạng thái xác nhận.

Bảng khởi tạo đề xuất cho nhà ở dân dụng. Mỗi số đo dưới đây là rộng × cao **ô chờ xây**, tính bằng mm. Đây là lựa chọn cấu hình ban đầu cần đối chiếu nguồn và block từng công trình. Tham khảo nhóm công năng và dải kích thước sản phẩm từ [Eurowindow](https://eurowindow.biz/cua-vach-nhom/cua-di-nhom/cua-di-nhom-1-canh-mo-quay-trong.html) và [dòng cửa đi uPVC](https://eurowindow.biz/cua-nhua-upvc/cua-di-upvc). Kích thước ô chờ trong bảng là đề xuất cho template.

| Mã gợi ý | Công năng | Số cánh | Ô chờ gợi ý | Cỡ thêm có thể khai báo |
|---|---|---:|---|---|
| D01 | WC | 1 | Rộng 600, 650, 750 hoặc 810. Cao 2.100 (gợi ý) | Bốn cỡ rộng riêng |
| D02 | Phòng ngủ hoặc cửa thông phòng | 1 | Rộng 810 hoặc 930. Cao 2.200 (gợi ý) | Hai cỡ rộng riêng |
| D03 | Ban công hoặc cửa phụ | 1 | 900 × 2.200 | 800 × 2.200 |
| D04 | Cửa chính một cánh | 1 | 1.000 × 2.200 | 1.100 × 2.400 |
| D05 | Cửa chính hai cánh | 2 | 1.600 × 2.400 | 1.400 hoặc 1.800 × 2.400 |
| D06 | Cửa chính bốn cánh | 4 | 2.800 × 2.400 | 2.400 hoặc 3.200 × 2.400 |

Với cửa WC một cánh, 600, 650, 750 và 810 mm là bốn cỡ rộng ô chờ riêng để đối chiếu nguồn. Với cửa thông phòng một cánh, 810 và 930 mm là hai cỡ ứng viên riêng. Nếu chưa xác định được cỡ, hiển thị đề xuất để chọn trên bản nháp. Chiều cao của hai nhóm vẫn là gợi ý.

Cho phép thêm, sửa, tắt loại cửa và khai báo nhiều cỡ theo công năng. Khi mỗi cỡ cần được thống kê riêng, tạo mã riêng. Khi nguồn thiếu số đo, engine tự chọn nếu quy tắc công năng, số cánh và dung sai trong Template cho đúng một kết quả. Trường hợp còn nhiều cỡ phù hợp, đưa vào bản nháp để chọn. Quan hệ từ ô chờ sang tham số động phải được đo kiểm theo block và visibility trước khi ghi CAD. Kích thước riêng của một vị trí lưu theo công trình.

Mã loại chỉ biểu thị loại cửa; một loại có thể xuất hiện ở nhiều vị trí. Nếu chưa biết vị trí dùng loại nào, app phải cho phép gán trên lớp đối chiếu ảnh hoặc hỏi đúng vị trí đó.

Mỗi loại block phải khai báo quan hệ giữa rộng ô chờ và tham số thực. Công thức có thể cần nhiều tham số tùy block. Block nhiều cánh không được mặc nhiên dùng một tham số rộng cho mọi trường hợp. Không đổi kích thước bằng scale toàn block. Chiều cao cửa được lưu để dùng tiếp cho mặt đứng/mặt cắt; không gán vào tham số bề rộng chỉ vì block mặt bằng không có tham số cao.

### 3.4. Nguồn số liệu và xử lý thiếu thông tin

Mỗi giá trị lưu: giá trị, đơn vị, nguồn, vị trí trên tài liệu, trạng thái, người/lần xác nhận và phạm vi áp dụng.

Các trạng thái: đã xác nhận; đọc từ nguồn; ước lượng; còn thiếu; mâu thuẫn.

Thông tin người dùng khai báo được ưu tiên khi đã xác nhận. Nếu khác kích thước ghi trên nguồn, phải hiển thị mâu thuẫn và quyết định sử dụng; không âm thầm thay nguồn hoặc tự coi mọi số nhập là đúng.

Khi thiếu kích thước cửa, cho phép chọn rõ: nhập bảng cửa; ước lượng theo tỷ lệ ảnh để duyệt; dùng kích thước mặc định cho bản thử; hoặc bỏ qua thành phần đó. Tên chế độ và giới hạn phải xuất hiện trong kết quả.

Chỉ hỏi phần thiếu ảnh hưởng đến phần đang triển khai. Thông tin chưa cần cho mặt bằng được lưu mà không chặn công việc.

### 3.5. Mô hình trung gian và sinh CAD

Đọc nguồn → nhận dạng không gian, tường, lỗ mở, cửa, đồ đạc và chữ → gắn kích thước/loại/tài nguyên → kiểm tra mâu thuẫn → sinh hình học CAD.

Lưu tường theo tim hoặc biên với quy ước rõ ràng, chiều dày và quan hệ góc giao. Lưu cửa như lỗ mở thuộc một tường với kích thước và hướng mở. Bộ sinh hình học xử lý giao tường và cắt lỗ cửa; không để AI tự đoán các đoạn line rời thành một công trình hoàn chỉnh.

AI hỗ trợ nhận dạng, giải thích và đề xuất. Bộ sinh hình học áp dụng quy tắc và trả kết quả có thể kiểm tra lại.

### 3.6. Kiểm tra trước khi áp dụng

- Kích thước bao và tỷ lệ đúng thông tin đã chốt.
- Tường đúng chiều dày từng loại; giao góc không thừa hoặc thiếu nét; lỗ cửa thông đúng.
- Cửa đúng mã, rộng ô chờ, block, visibility, hướng mở và vị trí; đọc lại tham số thật sau gán.
- Reference block đúng layer, nội bộ block giữ tiêu chuẩn thư viện; không tự đổi màu hoặc scale sai.
- Trục ngang mang số nguyên từ 1, trục đứng mang chữ A–Z. Kiểm tra vị trí và tên từ bản xem trước trước khi áp dụng CAD.
- Kích thước đo từ hình học thật, không sửa số DIM để che sai lệch.
- Không trùng hình học, không đặt chồng nội thất với block đã chèn, không vượt phạm vi nhiệm vụ.
- Thống kê số đối tượng theo nhóm, tài nguyên đã dùng, dữ liệu ước lượng và thành phần bỏ qua.
- Preview và áp dụng dùng cùng kế hoạch đã ghim; bản vẽ/nguồn/tài nguyên đổi phải kiểm tra lại.
- Chỉ ghi vào DWG qua duyệt cục bộ trong AutoCAD; xác nhận lại kết quả và khả năng Undo.

Các ngưỡng sai số phải là trường của Skill hoặc bộ chuẩn, có đơn vị và lý do; không để AI tự đặt ngưỡng để cho qua kiểm tra.

### 3.7. Ca nghiệm thu bắt buộc

1. PDF vector rõ kích thước, đối chiếu với bản CAD chuẩn.
2. Ảnh scan có nhiễu, có ít nhất một mốc hiệu chuẩn.
3. Thiếu kích thước cửa nhưng đã cung cấp bảng cửa.
4. Nguồn và bảng cửa mâu thuẫn.
5. Block cửa nhiều visibility, đổi rộng và đảo hướng ở tỷ lệ 1:1.
6. Tường 220/110, góc giao và lỗ mở sát góc.
7. Thư viện thiếu loại block; người dùng chọn nhập bổ sung, bỏ qua hoặc dùng bản thử.
8. Di chuyển điểm đặt, đổi DWG và nguồn/thư viện thay đổi trong lúc triển khai.
9. Xem trước, hủy, áp dụng, Undo và mở lại DWG.

Mỗi ca cần dữ liệu nguồn, cấu hình Skill, dữ liệu bổ sung, kết quả mong đợi và kết quả đo thực. Không chỉ kiểm tra app trả được JSON hay tạo được entity.

## 4. Tự biên soạn Skill trong app

Bố cục đề xuất: Tổng quan → Dữ liệu đầu vào → Quy tắc → Layer và block → Các bước → Kiểm tra và chạy thử.

Cho phép Thêm trường với các kiểu: số có đơn vị; văn bản; lựa chọn; có/không; bảng nhiều dòng; liên kết tài nguyên; điểm/đối tượng chọn trong CAD. Mỗi trường có giải thích, mặc định, điều kiện bắt buộc, phạm vi áp dụng và quy tắc kiểm tra.

Trường mới cần chọn mục đích: chỉ ghi chú; cung cấp ngữ cảnh; hoặc tham số có bộ thực thi hỗ trợ. App phải báo rõ trường chưa được bộ thực thi dùng, không hứa mọi ghi chú đều biến thành chức năng CAD.

Cho phép sao chép Skill mẫu, sửa, chạy thử trên dữ liệu mẫu, xem lỗi theo từng phần, lưu nháp rồi xuất bản phiên bản. Giữ lịch sử thay đổi và khả năng quay lại.

## 5. Thứ tự hoàn thiện đề xuất

1. Chốt đặc tả một Skill nhà ở dân dụng, bao gồm bảng cửa và tiêu chí đạt.
2. Dùng ảnh Quang Anh làm ca nghiệm thu cụ thể; chốt những số đo được xác nhận và những phần chỉ ước lượng.
3. Hoàn thiện mô hình tường/lỗ mở/cửa và bộ sinh hình học.
4. Làm trình nhập thông tin bổ sung và trình biên soạn Skill theo cùng mô hình dữ liệu.
5. Nghiệm thu từ app tới AutoCAD theo từng tiêu chí; sau đó mở rộng các Skill mặt cắt.

Bản dự thảo Quang Anh 91 thao tác hiện là phản hồi AI chưa được app chấp nhận hoàn tất. Còn lỗi kiểm tra kích thước đang xử lý; chưa ghi bản này vào CAD. Không coi đây là ca đã nghiệm thu.
