# Bằng chứng và giới hạn kiểm chứng

## Kết quả tự động

- Managed: 29/29 ca đạt, `tests/results/managed.json`.
- Host HTTP + MCP SDK: 13/13 ca đạt, `tests/results/host.json`. Có kiểm tra bearer, Origin, không lộ secret, từ chối lệnh CAD tùy ý, tool discovery và đọc template thật qua MCP/Host.
- Native CAD Engine: 5/5 ca đạt, `tests/fixtures/native-87f0b5c2ae774b4da917bfbef0b6d110/native-results.json`. Tạo 6 entity native, kiểm tra type, rollback vượt scope, guard khi tắt Undo, save/reopen DWG fixture.
- Compatibility probe: hash plan/template và validator đều qua .NET Framework 4.8 trên cùng plan do Host .NET10 tạo. Không coi serialization khác framework là nguyên nhân khi chưa có bằng chứng.

## Quan sát tích hợp trên máy

- Đã mở AutoCAD 2021 bằng launcher sửa sang `/b` cho graphical startup.
- Người dùng xác nhận palette AI CAD đã hiện.
- Host/UI đã đọc được 1 phiên và 2 DWG thật.
- Scan trên Drawing1.dwg đọc đúng 3 AI_TASK (T001/T002/T003), T003 phụ thuộc T001.
- Đã tạo plan T001 từ Skill/template thật; plan lưu vào data/plans.
- Yêu cầu preview được plugin nhận và trả QUEUED. Người dùng đã cung cấp ảnh AutoCAD 2021 hiển thị cửa/trục màu xanh trong khung T001 và palette đúng Drawing1.dwg, Template Starter 1.0.0. Graphical preview đã được xác nhận. Khi app ở nền, cần chuyển sang AutoCAD để quan sát và duyệt.
- Người dùng đã bấm duyệt thật; plan 33eef372c832497d937f523acc270db7 chuyển DONE, có execution 5b34961e458241e08364219a1a279362 và 3 entity handles. Kiểm tra đọc lại xác nhận đủ 3 handle trong Drawing1.dwg, revision từ 147 lên 148. Bằng chứng tại tests/results/live-apply.json. Chưa nghiệm thu Undo/Redo bằng thao tác người dùng trong graphical AutoCAD; fixture không thay thế bước này.

## Lỗi đã phát hiện và sửa

1. Graphical launcher dùng nhầm switch `/s` của Core Console. Đổi sang `/b`, mở DWG thử riêng và cho hiện cửa sổ CAD theo yêu cầu người dùng.
2. Side Database mặc định tắt Undo recording khiến rollback test không phục hồi số entity. Bật Undo chỉ trong test database; production không tự sửa cấu hình mà chặn ghi khi UndoRecording=false. Kế thừa nguyên tắc guard từ mã cũ.
3. Thêm đường ghi lỗi preview vào record/log để app thể hiện nguyên nhân, thay vì chỉ có thông báo ở command line. Cần nạp bản plugin mới trong phiên AutoCAD mới sau mỗi lần build native.

Không đánh dấu hoàn tất kế hoạch P0–P6 từ số test hoặc số module. Bản 0.1 vẫn có các giới hạn tại README.

## Ghi chú triển khai cuối phiên

- Source solution build Release: 0 warning/error; managed suite chạy lại 29/29. Không publish đè DLL đang nạp.
- Bản runtime đang chạy là bundle đã kiểm chứng; source có bổ sung log lỗi native và kiểm tra JSON Skill chặt hơn cho lần publish/restart kế tiếp.
- Sửa UI chọn mặc định DWG đang active, giữ nguyên đích đã chọn khi đọc CAD tạm timeout; không tự nhảy sang DWG khác.
- UI bổ sung thời điểm hết hạn preview để người dùng biết khi cần tạo lại.
