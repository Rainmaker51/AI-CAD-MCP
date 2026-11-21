# Bổ sung V1.3.4 — bảng thông số và preview tương tác

Tiến độ mới nhất: đã triển khai phạm vi được duyệt thành **1.4.0** để giữ nguyên gói 1.3.4 đã bàn giao. Xem [hướng dẫn 1.4.0](RAINMAKER-AXIS.md) và manifest/validation của gói. Nội dung “chưa sửa code” bên dưới giữ lịch sử trao đổi; các gợi ý chưa duyệt không tự được coi là đã triển khai. Phần kiểm thử trực tiếp cửa sổ/Ribbon còn giới hạn vì kết nối GUI không khả dụng.

Ghi nhận ngày 21/11/2026. **Chỉ trao đổi/ghi nhận, chưa sửa code, build hoặc cập nhật gói cài cho nội dung này.** V1.3.4 đã bàn giao trước đó là bản sửa tương thích `get_Values`, chưa có các tính năng mới trong tài liệu này. Giữ nguyên build đã bàn giao; khi được yêu cầu triển khai phải tạo build riêng theo quy trình, không ghi đè gói cũ.

Đọc cùng `RAINMAKER-AXIS.md`, `RAINMAKER-AXIS-1.3.3-REQUIREMENTS.md`, `ADDIN-MANAGER-STANDARD.md` và `ADDIN-DEVELOPMENT-WORKFLOW.md`. Các quyết định dưới mục “Yêu cầu” do người dùng đưa ra; chi tiết trong mục “Đề xuất” vẫn cần duyệt.

## 1. Yêu cầu mới

- Bảng điều khiển rộng hơn preview; bố trí dày/gọn để xem bao quát chức năng.
- Cột tự tạo thành block phải có thêm hatch bên trong block.
- Khi bật chèn cột, preview hiển thị toàn bộ cột tại các giao trục; có thể chọn một/nhiều cột để đổi xoay, tiết diện và điểm chèn.
- Khi bật tường, preview ban đầu vẽ toàn bộ đường bao; có thể tương tác chọn thêm vị trí xây tường trên một/nhiều trục, căn tim/lệch tim và điều chỉnh một/nhiều đoạn.
- Người dùng xác nhận “chiều dàu tường” là **chiều dày tường**, không phải chiều dài. Phạm vi bắt buộc là điều chỉnh bề dày cho một/nhiều đoạn được chọn.
- Giữ tường kín, trừ đúng cột, hatch chỉ nằm trong phần tường còn lại; giữ dim đúng tỷ lệ/tên thư viện và các cơ chế sửa lưới đã duyệt.
- Bỏ phần **Chuyển dim RMAX** trong bản nâng cấp tiếp theo: bỏ nút và luồng chuyển/dọn RMAX cũ. Giữ việc tạo dim mới đúng tên thư viện, dùng lại style khớp và xử lý xung đột; không tự xóa các dim hoặc định nghĩa RMAX đã có trong DWG. Đây là thay đổi phạm vi so với kế hoạch 1.3.3, chưa sửa code/gói 1.3.4 hiện hành.

## 2. Đề xuất bố cục

- Vẫn chỉ hai tab **Thông số / Thư viện**. Bảng thông số khoảng **60–65%**, preview **35–40%**; có thanh chia kéo và nhớ tỷ lệ theo máy, giới hạn theo vùng màn hình/DPI thực tế.
- Trong Thông số: nhóm Trục, Dim, Cột, Tường luôn thấy tiêu đề và tóm tắt trạng thái. Dùng hàng hai/ba ô cho B/H/góc, tỷ lệ/khoảng cách, bề dày/độ lệch thay vì mỗi ô một hàng dài.
- Rút gọn khoảng trắng và đưa hướng dẫn dài vào tooltip/phần mở rộng; giữ nhãn, đơn vị, chữ và vùng bấm đủ rõ. Không thu nhỏ font để ép mọi trường lên một màn hình.
- Vùng sửa đối tượng được chọn nằm cố định: “Đang chọn 4 cột” hoặc “Đang chọn 3 đoạn tường”. Khi chưa chọn, hiển thị thông số mặc định cho đối tượng mới.
- Giữ thanh hành động cuối bảng luôn thấy. Phân biệt **Cập nhật xem trước** (chỉ bản nháp) với **Ghi/cập nhật vào CAD** (ghi DWG sau kiểm tra/xác nhận).
- Preview có chọn chế độ **Cột / Tường**, zoom/pan/vừa khung và bật/tắt lớp xem; đây là công cụ trong preview, không thêm tab chính.

## 3. Đề xuất tương tác chọn một/nhiều đối tượng

- Click chọn một; Ctrl+click thêm/bỏ một; kéo khung chọn nhiều; Ctrl+A chọn tất cả trong chế độ hiện tại. Escape bỏ chọn trước, khi không còn lựa chọn mới hủy bảng theo hành vi thông thường.
- Chế độ chọn cột chỉ bắt cột; chế độ chọn tường chỉ bắt đoạn tim của tường. Có tô sáng khi rê chuột, trạng thái chọn khác rõ ràng với chưa chọn; không chỉ dựa vào màu để báo số lượng.
- Bổ sung vùng bắt theo pixel quanh cột/tim đoạn để chọn được đối tượng nhỏ khi nhìn toàn lưới, không làm thay đổi hình học thật.
- Danh sách/bảng song song để chọn bằng bàn phím và kiểm tra ký hiệu giao điểm, số đoạn. Không bắt người dùng thao tác toàn bộ bằng chuột trên hình nhỏ.
- Chọn nhiều đối tượng khác thông số: hiển thị **Nhiều giá trị** cho các trường khác nhau. Chỉ trường được người dùng sửa mới áp dụng cho nhóm đã chọn; không lấy giá trị của đối tượng đầu rồi ghi đè mọi trường.
- Mọi thao tác chọn/đổi thông số chỉ cập nhật bản nháp và preview. Không ghi DWG khi chỉ click chọn, đổi tab hoặc zoom.

## 4. Cột tự tạo, hatch trong block

- Lần đầu bật chèn cột cho lưới mới: khởi tạo cột dự kiến ở **mọi giao điểm** và hiển thị trên preview; đây là thay đổi so với bản cũ phải chọn danh sách trước. Các lần bật lại giữ lựa chọn/thiết lập đã chỉnh, trừ khi bấm Đặt lại.
- Chọn một/nhiều cột để sửa: vuông/chữ nhật/tròn, B/H/đường kính, góc xoay, neo tim/mép/góc, lệch X/Y. Có thao tác bỏ cột ở vị trí được chọn và khôi phục vị trí bị bỏ.
- “Điểm chèn” đề xuất hiểu là **điểm neo cột so với giao trục**, không sửa base point của định nghĩa block dùng chung. Base point chuẩn vẫn ở tim; thay neo/offset chỉ tác động các instance được chọn.
- Có thông số mặc định chung và override riêng cho mỗi cột. Đổi tiết diện một cột sẽ dùng/tạo định nghĩa phù hợp rồi đổi reference của cột đó; không sửa định nghĩa chung làm tất cả cột cùng loại thay đổi theo.
- Block chứa đường bao kín và hatch liên kết với biên **bên trong block**, hatch nằm sau nét. Cùng hình/kích thước/cấu hình hatch dùng lại định nghĩa; khác hatch cần định nghĩa riêng phù hợp.
- Đã xác nhận bộ chọn độc lập trong Thư viện: **Hatch cột + Layer hatch cột**, tách **Hatch tường + Layer hatch tường**. Giữ scale, góc, màu ACI/TrueColor/ByLayer/ByBlock, công dụng và dependency đã chọn từ kho. Không tự dùng SOLID hoặc lấy hatch tường thay cho hatch cột khi thiếu lựa chọn.
- Cột và hatch theo kích thước thật; không nhân hình học cột hoặc hatch trong block với mẫu số tỷ lệ in.
- Khi đổi góc/tiết diện/neo cột, tính lại phần tường bị trừ và hatch tường trong preview và lần cập nhật CAD tương ứng.

## 5. Tường theo đường bao, thêm/sửa từng đoạn

- Người dùng xác nhận “toàn bộ đường bao” là **chu vi ngoài của lưới đang chọn**, không phải mọi trục bên trong hoặc tìm biên toàn bộ công trình/DWG.
- Lần đầu bật tường cho lưới mới: tạo bản nháp các đoạn chu vi ngoài. Bật lại giữ các đoạn đã chỉnh; có nút Đặt lại theo đường bao.
- Chế độ chọn tường: click/khung chọn các đoạn giữa giao trục để thêm tường bên trong, chọn tường đã có để sửa hoặc bỏ một/nhiều đoạn. Có lựa chọn cả trục để thao tác nhanh nhưng phải hiển thị rõ các đoạn nằm trong phạm vi.
- Đề xuất căn **Tim / Mép trái / Mép phải**, kèm độ lệch số. Hiển thị mũi hướng đoạn; trái/phải được tính theo hướng đang thấy, không âm thầm đổi bên khi đảo thứ tự đoạn.
- Cho nhập **bề dày riêng từng đoạn/nhóm** (ví dụ bao 220, ngăn 110). Có mặc định chung và override riêng như cột. Không đưa chỉnh chiều dài/lùi đầu/lùi cuối vào phạm vi bắt buộc của đợt này.
- Khi bề dày/neo/chiều dài khác nhau gặp nhau, ghép kín L/T/X theo vùng hình học, trừ cột rồi mới hatch. Tường bị cột tách thành nhiều vùng vẫn phải đóng kín từng vùng.
- Đề xuất hatch mặc định theo lưới, cho override theo nhóm đoạn nếu thực tế cần phân biệt vật liệu. Chưa mặc định đưa override hatch từng đoạn vào phạm vi bắt buộc.

## 6. Dữ liệu và sửa lưới

- Cần nâng cấu trúc dữ liệu từ thông số cột/tường chung hiện tại sang **mặc định + override từng đối tượng**; không chỉ thêm điều khiển UI rồi vẫn áp cùng B/H/bề dày cho cả lưới.
- Mỗi cột gắn ID giao điểm; đoạn tường gắn ID trục và giới hạn đầu/cuối. Nhãn chỉ để hiển thị, không làm khóa; đổi tên trục không mất liên kết.
- Khi nhận dữ liệu cũ: chuyển thông số chung thành mặc định và giữ nguyên các vị trí đã chọn. Không tự bổ sung toàn bộ cột/tường vào lưới cũ khi chỉ mở bản mới.
- Khi thêm/bớt trục, báo cột/đoạn mất liên kết và cho gán lại/bỏ trước khi áp dụng. Giữ phát hiện sửa tay, preview khác biệt, rollback và một Undo.
- Chỉ các tài nguyên được chọn/lưu mới được xem là đang sử dụng; vẫn phân giải kho theo máy, không ghi đường dẫn tuyệt đối hoặc sửa catalog Manager thủ công.

## 7. Gợi ý bổ sung để duyệt

1. **Sao chép thông số** từ một cột/đoạn tường sang nhóm đã chọn, chọn những trường cần sao chép.
2. **Chọn theo điều kiện**: cột góc/biên/giữa, cùng tiết diện; tường bao/ngăn, cùng bề dày. Có lựa chọn đảo chọn và bỏ chọn.
3. **Cảnh báo trước khi ghi**: cột trùng, cột chồng nhau, tường quá ngắn, mất liên kết, biên lỗi và tài nguyên hatch thiếu; highlight đúng vị trí trên preview.
4. **Thống kê bản nháp**: số cột theo tiết diện, tổng chiều dài tường theo bề dày, diện tích hatch. Ghi rõ đây là thống kê hình học, chưa phải khối lượng xây dựng hoàn chỉnh.
5. **Hoàn tác trong preview** cho thao tác chọn/sửa, tách khỏi Undo của DWG; nút Đặt lại chỉ tác động bản nháp/phạm vi được nêu rõ.

## 8. Các điểm cần chốt

- Đã chốt: chỉnh **chiều dày** tường.
- Đã chốt: đường bao mặc định là **chu vi ngoài của lưới**.
- Đã chốt: hatch cột và hatch tường chọn **độc lập từ kho**.

Các chi tiết còn lại là đề xuất có thể điều chỉnh khi duyệt. Không tự triển khai code hoặc thay gói đang cài chỉ vì tài liệu này đã được ghi.

## 9. Cập nhật hatch khi tài nguyên kho thay đổi

Đã chốt: cùng tài nguyên hatch được sửa scale (ví dụ 500 → 300), add-in đọc thông số mới ở lần nạp hoặc bắt đầu triển khai tiếp theo. **Không thêm nút đồng bộ/làm mới thư viện trong add-in.** Việc lưu/đồng bộ tài nguyên kho vẫn thực hiện theo quy trình thư viện hiện có.

- Liên kết bằng mã/nguồn/ID ổn định, không giữ một bản sao thông số bất biến hoặc chỉ lấy tên pattern. Khi cùng ID được sửa, đọc lại phiên bản/hash và scale/góc/màu mới sau khi tài nguyên đã lưu/đồng bộ đầy đủ.
- Mỗi lần nạp bảng hoặc bắt đầu một lượt triển khai mới, phân giải lại tài nguyên đã lưu theo ID và dùng thông số kho mới nhất đã đồng bộ đầy đủ. Không yêu cầu bấm nút đồng bộ riêng. Cố định snapshot cho một lượt thao tác; nếu kho đổi giữa preview và ghi thì dừng và yêu cầu mở lại bảng, không ghi lẫn phiên bản hoặc tự thay thông số sau khi người dùng đã duyệt preview.
- Tạo mới với cấu hình kho scale 300 thì dùng đúng 300, không nhân thêm mẫu số tỷ lệ in.
- Với cột/tường đã vẽ: thay đổi kho hoặc chỉ mở lại DWG không tự thay hatch đang lưu trong bản vẽ. Khi người dùng chủ động chọn và cập nhật lưới, lượt nạp đó đọc lại tài nguyên từ kho, xem preview rồi áp dụng trong phạm vi đã chọn; không cần nút đồng bộ riêng và không tự cập nhật tất cả DWG.
- Hatch nằm trong block cột dùng chung: phải tạo/phân giải định nghĩa đúng cấu hình hatch mới và đổi reference thuộc phạm vi cập nhật. Không sửa trực tiếp một định nghĩa chung rồi làm thay đổi cả các lưới/cột khác ngoài phạm vi. Dấu vân tay định nghĩa phải gồm cấu hình hatch thực tế.
- Nếu xóa hatch cũ rồi tạo tài nguyên mới có ID khác, liên kết cũ bị báo thiếu; không tự coi đó là cùng hatch hoặc thay thế chỉ vì giống tên.
- Hatch associative chỉ liên kết với biên hình học trong DWG, không tạo liên kết sống tới catalog kho. Lần nạp/triển khai sau đọc cấu hình mới; các đối tượng đã vẽ không tự đổi chỉ vì kho thay đổi. Các nội dung bổ sung này vẫn ở trạng thái tài liệu, chưa sửa code trong lần chốt yêu cầu.

## 10. Phản hồi preview khi chọn lưới và giao diện quản lý lưới

- Người dùng báo **Chọn lưới để sửa chưa preview đúng theo lưới vừa chọn**. Ghi nhận cần tái hiện và sửa, chưa khẳng định đã xác định được nguyên nhân duy nhất chỉ từ đọc code.
- Đối chiếu hiện tại: thao tác chọn đọc GridId/metadata rồi nạp các trường đã lưu vào bảng. Preview dựng lại từ thông số trong bảng, không phải ảnh/quét đầy đủ hình học hiện trạng DWG. Kiểm tra sửa tay đang diễn ra trước khi áp dụng; chưa có phần đối chiếu hiện trạng ngay sau chọn. Thay đổi tay hoặc MOVE ngoài add-in có thể làm cấu hình lưu không còn khớp hình thật.
- Hành vi cần có khi sửa: nhận đúng lưới, thay toàn bộ trạng thái chọn/bản nháp trước đó, hiển thị GridId/tên và thông số lưới vừa chọn, tự vừa khung. Kiểm tra khác biệt ngay khi nạp; nếu không thể khôi phục thì báo rõ thay vì hiện lưới mặc định hoặc hình xem trước cũ.
- Đề xuất phân biệt **Hiện trạng trong DWG** và **Sau chỉnh sửa** bằng bật/tắt lớp so sánh trong preview, không thêm tab chính. Những thành viên đã sửa/xóa hoặc không khớp metadata phải được đánh dấu, không được trình bày cấu hình cũ như thể đó là hình đang có trong CAD.
- Dữ liệu quản lý nền đã có trong DWG (`QA_AXIS_GRIDS`, GridId, revision, thông số, liên kết thành viên). **Chưa có giao diện danh sách/quản lý các lưới**; không nhầm việc có metadata với có màn hình quản lý.
- Điều chỉnh phạm vi mới nhất của người dùng: **thêm phần Các lưới đã nhận dạng ngay trong Axis ở đợt sửa này**, đặt trong tab Thông số, không thêm tab chính thứ ba. Quyết định này thay cho việc để toàn bộ danh sách lưới sang add-in riêng.
- Add-in quản lý bản vẽ riêng để mở rộng quản lý tường/cửa/cột vẫn triển khai sau. Phần hiện tại chỉ quản lý các lưới đã có liên kết nhận dạng trong DWG đang thao tác; không biến Axis thành bộ quản lý toàn bộ đối tượng CAD.

## 11. Tổng hợp phạm vi đợt sửa hiện tại

**Trạng thái: tổng hợp để duyệt; chưa sửa code/build các bổ sung này.**

1. **Bố cục**: bảng thông số rộng hơn preview, gợi ý 60–65% / 35–40%, có thanh chia; bố trí ô nhập theo hàng gọn, giữ nhãn/đơn vị và thanh hành động. Chỉ hai tab Thông số/Thư viện.
2. **Preview tương tác**: chọn một/nhiều cột hoặc đoạn tường, có chế độ chọn rõ ràng và số lượng đã chọn. Thông số chung khác nhau hiện Nhiều giá trị; chỉ những trường được sửa áp dụng cho nhóm đã chọn. Chỉ thay bản nháp trước khi ghi CAD.
3. **Cột**: lần đầu bật cho lưới mới hiển thị cột ở tất cả giao điểm; sửa tiết diện/góc/neo/offset cho một hoặc nhiều cột; cho bỏ/khôi phục vị trí. Thêm hatch bên trong block, chọn hatch/layer hatch cột độc lập từ kho. Đổi riêng một cột không thay mọi cột dùng chung định nghĩa.
4. **Tường**: lần đầu bật cho lưới mới tạo tường chu vi ngoài; chọn thêm/bỏ đoạn bên trong trên preview, hỗ trợ chọn nhiều. Sửa bề dày và căn tim/lệch tim theo đoạn/nhóm; không thêm chỉnh chiều dài trong đợt này. Giữ biên kín L/T/X, trừ cột thật rồi hatch đúng vùng còn lại. Hatch tường độc lập hatch cột.
5. **Chọn lưới để sửa**: sửa lỗi/thiếu sót nạp preview theo lưới vừa chọn; nạp đúng thông số và trạng thái chọn, tự vừa khung, kiểm tra ngay khác biệt với DWG và báo nếu không khôi phục được. Không coi hình dựng từ dữ liệu cũ là hiện trạng CAD khi có sai khác.
6. **Tài nguyên kho**: đọc lại cấu hình theo ID ở lần nạp/triển khai tiếp theo sau khi kho lưu/đồng bộ; không thêm nút đồng bộ thư viện. Không tự đổi hatch đã lưu trong DWG chỉ vì mở lại file hoặc kho thay đổi.
7. **Bỏ Chuyển dim RMAX**: bỏ nút/luồng chuyển và dọn RMAX trong bản nâng cấp. Giữ dim mới dùng tên thư viện, tỷ lệ đúng và xử lý xung đột; không xóa dim/RMAX cũ tự động.
8. **Giữ dữ liệu và thao tác hiện có**: sửa lưới cập nhật cột/tường/hatch/dim liên kết sau xác nhận; giữ nhận lưới cũ có kiểm tra phạm vi, phát hiện sửa tay, rollback/Undo và cấu hình hai máy. Chuyển dữ liệu cũ phải giữ thiết lập, không tự bật tất cả cột/tường cho lưới cũ.
9. **Các lưới đã nhận dạng**: danh sách gọn ngay trong tab Thông số, gồm lưới tạo mới bằng add-in và lưới cũ đã nhận quản lý/lưu thành công. Chọn một mục để nạp đúng thông số/preview, có thao tác sửa, đổi tên và tô sáng/zoom tới lưới. Xem chi tiết tại mục 12.

**Để sau**: add-in quản lý bản vẽ riêng mở rộng quản lý tường/cửa/cột; preset công trình, xoay toàn lưới, chừa cửa, chỉnh chiều dài tường. Danh sách lưới ngay trong Axis đã được bổ sung vào đợt hiện tại. Các gợi ý sao chép thông số, bộ lọc nâng cao, thống kê và Undo riêng của bản nháp ở mục 7 chưa tự trở thành tính năng bắt buộc nếu chưa được duyệt riêng.

## 12. Các lưới đã nhận dạng trong Axis — bổ sung theo yêu cầu

- Vị trí: nhóm gọn ở đầu tab **Thông số**, có thể thu gọn để giữ chỗ cho các thông số và preview. Giữ đúng hai tab chính.
- Phạm vi: lưới đã tạo bằng add-in hoặc đã nhận quản lý thành công trong **DWG hiện tại**, dựa trên GridId/metadata. Không tự coi mọi line cùng layer là lưới đã nhận dạng. Lưới cũ đang nhận ở bước xem trước nhưng chưa áp dụng phải ghi rõ **Chưa lưu**, không báo đã quản lý bền vững.
- Thông tin tối thiểu: tên dễ nhớ/mã ngắn, GridId trong chi tiết, số trục X/Y, kích thước tổng, số cột/đoạn tường và trạng thái **Khớp / Đã sửa tay / Thiếu thành viên / Dữ liệu lỗi**. Danh sách ngắn gọn, các thông tin dài mở khi chọn.
- Chọn một dòng: nạp đúng lưới vào bảng và preview, tự vừa khung, hiển thị tên/mã đang sửa. Nếu đang có bản nháp chưa ghi, hỏi giữ/hủy trước khi chuyển lưới; không trộn trạng thái của hai lưới.
- Thao tác trong phạm vi đề xuất: **Sửa lưới**, **Đổi tên**, **Tô sáng / Đến lưới trên CAD**. Đổi tên giữ GridId và các liên kết. Không tự thêm chức năng xóa lưới hoặc xóa đối tượng chỉ vì người dùng bỏ chọn dòng.
- Nạp lại danh sách khi mở bảng, đổi DWG, tạo/nhận/cập nhật lưới thành công. Đây là làm mới dữ liệu lưới của DWG, không phải thêm nút đồng bộ thư viện kho.
- Metadata hỏng hoặc thiếu thành viên vẫn được liệt kê với trạng thái lỗi và hướng dẫn xử lý, không âm thầm bỏ khỏi danh sách hoặc tự tái tạo hình học.
- Ghi nhận bổ sung vào kế hoạch; chưa sửa code/build phần quản lý danh sách này.
