# P0–P6: sổ nghiệm thu triển khai

Nguồn chuẩn: `../../PLAN MCP NEW.md`. Bản 0.1 đã xác minh Apply trực tiếp trong AutoCAD 2021. Đợt mở rộng bao gồm chat trong app theo yêu cầu bổ sung.

| Mốc | Điều kiện đóng | Bằng chứng cần có | Trạng thái đầu đợt |
|---|---|---|---|
| P0 | ADR, schema, môi trường, DWG/DWT/block/PDF/scan, MCP client | fixture manifest + ADR + build | Thiếu bộ corpus đầy đủ |
| P1 | Nhiều DWG, đọc theo vùng, ID/revision, lỗi khi đóng | native scope/document tests | Cần scope query và bổ sung negative cases |
| P2 | Dimension/layer, preview chuẩn, cancel, QA, checkpoint, idempotency | native + local human review | Đã có primitives/Apply; cần dim/hatch/checkpoint |
| P3 | Registry version, Skill loader, dependency và rollback | import/version tests không rebuild Core | Cần registry version và style/preset |
| P4 | DAG/scheduler bền vững, 3 task, một phụ thuộc, lỗi độc lập | batch tests + native run | Có scan/dependency ban đầu |
| P5 | PDF vector và scan, scale/confidence, CAD edit được | nguồn/overlay/report/DWG thực | Chưa triển khai parser/OCR |
| P6 | Mặt cắt từ DWG nguồn và tham số xác nhận | source binding + section output + QA | Chưa có builder nghiệp vụ |

Thông số cầu thang đã được người dùng xác nhận cho fixture P6: H=3600, 20 cổ bậc, hai vế, R=180, T=270, rộng vế 1000, chiếu nghỉ 1200, sàn 120 mm. Nguồn và đầu ra phải ghi nhãn dữ liệu mẫu. Không thay chúng bằng suy đoán từ ảnh.

Không đóng mốc chỉ từ code skeleton, số test hoặc file được tạo. Ghi riêng: triển khai, kiểm thử fixture, nghiệm thu tương tác. Các ca cần người dùng duyệt tại AutoCAD không được giả lập approval để gọi là UAT thật.
