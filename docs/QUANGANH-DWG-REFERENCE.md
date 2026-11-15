# Chuẩn thể hiện từ File mau.dwg

Mẫu chính cho Skill PDF/ảnh sang CAD — nhà ở dân dụng. Đọc bản sao bằng AutoCAD 2021 Core Console, đơn vị mm, 600 đối tượng cấp Model. Mã SHA-256 của mẫu là 59E27DB4F3CBFBC5096055178AF0CBE0C87BB722A10B58DEE37916EED85324DA. Kích thước và bố trí công trình lấy từ ảnh/PDF và bảng dữ liệu của công trình đó.

| Vai trò | Quan sát từ mẫu | Quy tắc triển khai |
|---|---|---|
| Tường | QA_KT_Tuong, ACI 7, Continuous, 0,25 mm | Dùng layer này. Tường bao 220 mm, tường ngăn 110 mm theo quy ước đã chốt, có thể xử lý ngoại lệ theo công trình. |
| Cửa | 11 tham chiếu QA_GC_KT_MBCUA trên QA_KT_Cua (ACI 20, 0,15 mm). Cua di và Cua so & Vach xuất hiện trên k-kt-CUA (ACI 20, 0,09 mm) | Dùng danh mục Template Quang Anh và gốc chèn của block. Gán loại, visibility, lỗ mở và bản lề theo nguồn. Dùng QA_KT_Cua cho tham chiếu cửa mới theo xác nhận của người dùng. |
| Nét trục | QA_KT_Truc, kiểu CENTER, ACI 251, 0,09 mm | Dùng kiểu nét và layer này. App tự bố trí khi nguồn thiếu trục, dựa trên tim tường hoặc tim cột đọc được và bố cục mặt bằng. Trục ngang đánh số nguyên từ 1, trục đứng dùng A–Z. |
| Đầu trục | 12 block Ki hieu truc dinh vi trên k-kh-TRUC DINH VI, ACI 142 và 0,09 mm. Các mẫu đọc được dùng Visibility1 = Truc dinh vi và tỷ lệ chèn 60. Block có AttributeDefinition và Wipeout. | Kiểm tra bản định nghĩa từ DWG mẫu, khả năng clone và cách điền thuộc tính. Dùng QA_KH_Truc Dinh Vi cho đầu trục mới theo xác nhận của người dùng. Giữ tham chiếu tỷ lệ 60 như quan sát tại 1:60, kiểm tra cách quy đổi ở tỷ lệ khác trước khi áp dụng. |
| Hatch | QA_KT_Hatch, ACI 250, 0,05 mm. Có ANSI31 tỷ lệ 300 góc 0°, _USER 300/400 góc 0°, GRASS 50 góc 0°, AR-RROOF 200 góc 45°. Hatch thiết bị SOLID xuất hiện bên trong các block. | Hatch phần tường cắt trên mặt bằng dùng QA_KT_Hatch, ANSI31, tỷ lệ 300, góc 0° theo xác nhận của người dùng. Hatch các vùng khác theo preset vật liệu tương ứng khi có dữ liệu. |
| Text | 7 MText annotative trên QA_KT_Text dùng Tahoma ở 1:60, chiều cao Model 120 mm. Mẫu cũng có MText và DBText trên k-kt-TEXT. | Dùng QA_KT_Text và Tahoma annotative cho chữ ghi chú mới. Với nhóm MText cao 120 mm tại 1:60, mục tiêu tương đương 2 mm trên giấy. |
| DIM | 64 RotatedDimension annotative trên QA_KT_Dim, dùng DIM và annotation scale 1:60. Text 2, mũi tên 0,8; dữ liệu đọc từ đối tượng có Dimscale 0, còn bản ghi style DIM có Dimscale 75. | Ưu tiên DIM ngang/dọc theo mẫu, lấy số đo từ hình học. Khai báo các annotation scale đích và kiểm tra kết quả theo viewport. Cần thử riêng để tái tạo cấu hình annotative trước khi phát hành. |

## Cửa và tỷ lệ

- QA_GC_KT_MBCUA có KT CUA, KHUÔN, CUA (visibility), Flip state, Flip state1 và Xoay. Bản mẫu dùng nhiều visibility một, hai và bốn cánh, các tham chiếu QA_GC_KT_MBCUA đọc được đều có tỷ lệ 1:1.
- Các giá trị KT CUA trong mẫu là 550, 710, 1300 và 2900 mm. Đây là giá trị tham số quan sát được, chưa xác lập quan hệ với rộng ô chờ xây. Khi cấu hình danh mục, đo lỗ mở tương ứng hoặc lấy công thức người dùng đã xác nhận. Một số tham chiếu cửa/vách mẫu dùng scale âm theo X. Khi dùng bộ chuẩn mới cần kiểm tra trạng thái flip tương đương.
- Hai tên annotation scale 1:60 và 1.60 cùng có PaperUnits 1 và DrawingUnits 60. Nhận diện 1.60 và 1:60 là hai tên tương đương khi đọc mẫu. Người dùng chọn tỷ lệ annotative chính trong biểu mẫu triển khai và có thể thêm tỷ lệ bổ sung. 1:60 là tỷ lệ tham chiếu của mẫu.

## Nghiệm thu trên ảnh Quang Anh

- Dựng bao 16.000 × 5.000 mm và bố trí theo ảnh đã hiệu chuẩn.
- Kiểm tra bằng thuộc tính đối tượng CAD. Layer đúng vai trò, cửa đúng mã/visibility/bản lề/ô chờ, đầu trục đọc được và hatch đúng biên.
- Đối chiếu text, DIM và ký hiệu ở các tỷ lệ đã chọn lúc triển khai, với 1:60 là ca đối chiếu trực tiếp với mẫu. Kiểm tra DIM là số đo thực, text và DIM có annotation context đã chọn.
- Tiếp tục dùng DWG đích đang kết nối và điểm AI_OUTPUT_POINT, xem trước rồi duyệt tại AutoCAD.

Template ISO_QUANGANH 1.1.9 hiện có QA_GC_KT_MBCUA nhưng mục cửa catalog chưa duyệt quy đổi ô chờ. Danh mục hiện chưa có đầu trục Ki hieu truc dinh vi. Renderer tạo style thông thường, DIM aligned và hatch giới hạn ở ANSI31/SOLID. Các yêu cầu annotative, đầu trục và hatch bổ sung cần triển khai và kiểm thử trước khi xác nhận có thể tái tạo chuẩn mẫu.
## Cấu hình cho mỗi lần triển khai

- Chọn tỷ lệ annotative chính và các tỷ lệ bổ sung khi cần.
- Dùng bảng ô chờ xây gợi ý trong SKILL-SPECIFICATION-REVIEW.md. Cho phép thêm hoặc chỉnh loại cửa và đặt ngoại lệ theo vị trí.
- App tự bố trí lưới trục từ nguồn và hình học nhận diện được. Ở mặt bằng đã định hướng, mặc định đánh số trục ngang từ dưới lên và đánh chữ trục đứng từ trái sang phải. Dùng chữ hoa A đến Z. Nếu cần hơn 26 trục đứng, hiển thị yêu cầu đặt tên bổ sung. Vị trí suy ra từ ảnh xuất hiện trong bản xem trước để duyệt cùng mặt bằng.
