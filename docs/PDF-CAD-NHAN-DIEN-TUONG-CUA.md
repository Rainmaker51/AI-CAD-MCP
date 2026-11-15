# Đặc tả nhận diện tường, cửa và trục từ PDF / ảnh

Áp dụng cho Skill `SOURCE_ISO_QUANGANH`. Kích thước và vị trí đọc từ nguồn đã hiệu chuẩn. Template cung cấp dạng thể hiện, layer và block.

## 1. Đọc ảnh trước khi sinh CAD

1. Xác định vùng công trình và các mốc đọc được. Lưu điểm ảnh gốc và tọa độ CAD theo mm. Khi kích thước ghi trên bản vẽ khác số đo ước lượng theo ảnh, giữ số ghi và đánh dấu chỗ cần kiểm tra.
2. Theo dõi cặp mép tường qua các giao góc, cột và vách. Từ hai mép dựng tim và phân loại tường 220/110. Cột, đường trục, đường dim, mép bậc, mép sàn và đường song song ngoài nhà là các nhóm riêng.
3. Duyệt từng dãy tường từ đầu đến cuối, theo cả hai mặt nhà và các vách ngang/dọc/xiên. Ghi từng ô mở với hai mép jamb và dấu hiệu nhận dạng trước khi chọn block.
4. **Cửa sổ.** Dùng cặp mép jamb cùng cụm nét khung nằm giữa hai mặt tường. Một cụm khung dài tương ứng một ô chờ. Các đường dọc của cánh, đố và ray bên trong giữ vai trò chi tiết cửa.
5. **Cửa đi.** Dùng mép ô mở và nét cánh hoặc cung mở. Hai cánh trong cùng cặp jamb là một ô cửa hai cánh. Ghi phía bản lề và phía mở nếu nhìn rõ. Khi chỉ thấy khoảng trống, ghi vị trí chưa rõ vào mục cần xác nhận.

## 2. Dữ liệu và kiểm tra trước khi lập bản nháp

| Trường | Quy ước |
|---|---|
| `operationIndex` | Vị trí block trong `operations`, bắt đầu từ 0 |
| `mark` | `frame_between_jambs` cho cửa sổ hoặc `leaf_or_swing` cho cửa đi |
| `pixelA`, `pixelB` | Hai mép đọc từ nguồn, gốc trên trái của ảnh |
| `jambA`, `jambB` | Hai đầu khoảng hở trên tim tường, WCS mm |
| `OpeningWidth` | Khoảng cách giữa hai đầu ô chờ xây |
| Tường chủ | Đoạn tim, hai mặt tường tại ô mở, chiều dày và chiều dương dọc tim |
| Cấu hình đặt block | Mốc jamb tại hệ trục block, mặt đặt khung, bản lề, hướng mở, góc xoay và trạng thái đảo theo từng visibility |

- Dùng cùng cặp jamb để cắt đoạn tim tường và dựng block. Xác định tường chủ từ các đoạn tường nối tới jamb. Nếu vị trí có nhiều tường ứng viên tại giao góc, chọn theo phương của hai jamb và dấu hiệu cánh trên ảnh. Trường hợp chưa phân biệt được thì đưa vị trí đó vào mục cần xác nhận.
- Bộ kiểm tra yêu cầu dấu hiệu ảnh phù hợp loại block, hai vị trí pixel cách nhau ít nhất 2 px, rộng ô chờ khớp trong 1 mm, đầu block khớp jamb trong 35 mm và hướng block khớp phương tường trong 5°. Tim tường qua ô cửa kiểm tra với dung sai 5 mm. Các dung sai này kiểm tra tính nhất quán của bản nháp.
- Giữ block ở tỷ lệ 1:1. Quy đổi kích thước động sau khi chốt ô chờ. Cỡ phủ bì khác ô chờ phải ghi rõ trên kế hoạch duyệt và giữ nguyên các điểm cắt tường.
- Vị trí chưa chắc chắn phải được mô tả theo phòng, dãy tường hoặc mép ảnh trong ghi chú. Thiếu dấu hiệu đủ rõ cho việc xác định cửa thì chờ đối chiếu ở bản nháp. Việc đọc dấu hiệu do AI đề xuất. Bộ kiểm tra CAD đối chiếu tính nhất quán giữa dấu hiệu, đoạn tường và block.

### Đặt khung cửa theo hai mặt tường

1. Dùng tim tường và chiều dày để tính hai mặt tường ở ô mở. Cặp jamb trên tim xác định vị trí theo chiều dọc. Mặt đặt khung xác định độ lệch vuông góc với tim.
2. Với mỗi block và visibility, đo trong AutoCAD vị trí hai đầu khung so với gốc chèn và phía khung theo hệ tọa độ block sau khi gán tham số động. Lưu cấu hình riêng cho các trạng thái đảo. Bộ sinh tính điểm chèn từ mốc jamb thực của block. Khi quay 90°, −90° hoặc 180°, biến đổi cả góc lẫn véc-tơ lệch từ gốc đến jamb. Với cửa ở hai tường đối diện, xác định mặt đặt khung cho từng tường theo hướng mở trên ảnh.
3. Trước preview, đo lại **phần khung** đã chèn. Kiểm tra hai đầu khung dọc theo ô chờ và phần khung nằm giữa hai mặt tường. Dung sai lắp hình học đề xuất là **2 mm** ở các mép, cộng phần sai khác phủ bì đã khai báo trong Template. Phần cánh và cung mở được kiểm tra riêng theo phía bản lề và chiều mở. Nếu sai, tính lại điểm chèn từ mốc của block. Nếu vẫn sai, báo vị trí cửa, tường chủ và độ lệch để đối chiếu.

## 3. Chọn và gộp trục định vị

1. Thu thập trục có ký hiệu hoặc chuỗi kích thước đọc được từ nguồn. Tim dãy cột là chứng cứ tiếp theo. Tim tường dài chỉ dùng để đề xuất khi thiếu trục hoặc cột. Mép tường, mép cột và tim của các đoạn vách lệch cục bộ cần được đối chiếu với cùng dãy cột trước khi chọn trục.
2. So sánh các ứng viên cùng phương theo khoảng cách vuông góc. Dùng **50 mm** làm ngưỡng rà trùng ban đầu, tính cả đúng 50 mm. Khi hai ứng viên cùng đại diện một dãy, giữ tọa độ từ trục ghi trên nguồn hoặc tim dãy cột có bằng chứng rõ nhất. Với hai trục riêng được ghi rõ trên nguồn, giữ cả hai. Nếu chưa xác định được quan hệ, đưa đúng cặp đó vào mục cần xác nhận trước khi sinh dim.
3. Chốt danh sách trục trước khi đánh số hoặc gán chữ. Đánh số trục ngang từ dưới lên, gán chữ trục đứng từ trái sang phải. Sau khi chốt mới sinh đường trục, block ATT hai đầu và dim từng nhịp, rồi dim tổng giữa hai trục ngoài cùng. Kiểm tra đầu trục và chữ dim ở tỷ lệ chú thích đã chọn. Việc dời ký hiệu để tránh đè chữ phải giữ nguyên tọa độ tim trục và chân dim.

## 4. Ca nghiệm thu mặt bằng tầng 1

Nguồn `screenshot_1794697482.png`, bao công trình 42.000 × 17.000 mm. Dùng vùng khoanh của người duyệt để kiểm đếm và đối chiếu mép jamb.

- Dãy trên có 7 cửa sổ dài, 2 cửa sổ nhỏ khu WC và 2 cửa đi cạnh cụm thang giữa.
- Dãy dưới có 4 cửa sổ bên trái và 4 cửa sổ bên phải. Hai cửa đi ở phần lùi của sảnh được kiểm tra theo đường tường riêng.
- Một ô cửa có hai jamb và một block tương ứng. Kiểm tra các đoạn đặc ở giữa, nơi tiếp giáp cột và đầu vách. Đường mép ngoài phía sau dãy cửa thuộc nhóm mép sàn hoặc bậc, không dùng để cắt ô chờ.
- Thử lỗi bằng cách cho một đoạn tim chạy qua ô cửa, dịch block khỏi jamb, bỏ bằng chứng một ô, hoặc gán cùng bằng chứng cho hai block. App phải báo số thứ tự thao tác và nguyên nhân trước preview.
- Trên bản dựng hiện tại, hai cặp trục đứng **C–D cách 40 mm** và **H–I cách 50 mm**. Đối chiếu chuỗi trục gốc, kể cả nhịp 8.400 mm ghi trên nguồn, để chốt vị trí đại diện cho từng dãy. Sau khi gộp, kiểm tra mỗi dãy chỉ có một trục, một ký hiệu ở mỗi đầu và chuỗi dim không còn nhịp giả 40/50 mm. Tên trục được tính lại theo danh sách đã chốt.
- Kiểm tra hai cửa đi hai cánh ở **vách biên trái và phải giữa trục 3–4 của bản dựng hiện tại**. So sánh ô chờ, mặt trong/ngoài tường, góc quay và hướng mở ở từng bên. Trên preview, hai đầu khung phải chạm đúng jamb, phần khung nằm trong bề dày tường và cung mở khớp phía thể hiện trên bản gốc.

## 5. Cấu hình app đọc khi lập kế hoạch

- Skill `SOURCE_ISO_QUANGANH` lưu `Parameters.axisMergeToleranceMm`. Giá trị ban đầu là 50 mm, có thể chỉnh trong **Thư viện Skill → Cập nhật**. App áp dụng với trục đề xuất trước khi đánh tên và sinh dim. Trục do người dùng khai báo trong Skill giữ nguyên. Nếu không có tim dãy cột được ít nhất hai cột hỗ trợ, app ưu tiên ứng viên có tổng chiều dài tường lớn nhất trong cụm.
- Template `ISO_QUANGANH` lưu `Presets.doorFramePlacement` theo mã block. Cấu hình đầu tiên áp dụng cho `QA_CUA_1_CANH` ở ba visibility đã đo. App lấy phía khung theo Flip, đầu jamb theo Flip thứ hai và giữ hướng mở khi tính lại điểm chèn. Căn khung khi có một ô hở phù hợp với dung sai rộng 2 mm và giới hạn dịch 260 mm. Vị trí chưa đủ căn cứ được báo trong ghi chú kế hoạch.
