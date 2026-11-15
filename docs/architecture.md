# Kiến trúc triển khai 0.1

Nguồn yêu cầu: `../../PLAN MCP NEW.md`. Thực hiện project mới; thuật toán và các bài học của app cũ chỉ được chọn lọc, ví dụ guard Undo recording và định danh physical DWG.

```text
AI Client --stdio--> MCP.Server --Bearer/loopback--> Desktop.Host
                                                  |
Browser UI --Bearer/loopback----------------------> Application
                                                  |
                                    ACL + verified Named Pipe
                                                  |
                                CAD.Bridge.AutoCAD (.NET 4.8)
                                main-thread queue / native palette
                                                  |
                                AutoCAD 2021 DocumentLock/Transaction
```

CAD.Contracts/CAD.Geometry là netstandard2.0 dùng chung giữa plugin và .NET10. Host không giữ tham chiếu Autodesk. Application quản lý session/content/plan; tool MCP chỉ gọi các service đã định kiểu. UI trình duyệt cục bộ là bề mặt quản lý; preview và approval nằm trong palette AutoCAD.

Plugin đăng ký session ngẫu nhiên và heartbeat. Mỗi Document được định danh trong vòng đời mở, revision tăng khi object thay đổi hoặc Undo/Redo/Save. Không dùng tên tab làm danh tính ghi. IPC chạy tác vụ đọc trên main thread khi CAD rảnh; hết hạn trước dispatch thì bỏ tác vụ.

Plan được lưu JSON theo GUID, hash canonical, chứa snapshot template và hash Skill. `create_preview` chỉ gửi tên lệnh cố định AICADREVIEW cho document đã bind. Bước duyệt là click trong palette, không có RPC giả lập click/approval. AICADAPPLY yêu cầu private approval state và chạy như một CAD command thường để tạo Undo boundary.

Trước mutation: đối chiếu session/document/revision/plan/hash/content/task boundary/units/space/Undo/read-only → physical drawing mutex → DocumentLock → durable execution fence và DISPATCHED → transaction tạo entity + native scope checks + receipt → commit → DONE. Lỗi trước commit rollback; lỗi sau commit không được báo thành chưa ghi. UNKNOWN giữ fence để không tự ghi lại.

Metadata v0.1 lưu JSON atomic trong thư mục riêng. Đây là quyết định tạm cho prototype thực; SQLite và scheduler/reconciliation nâng cao còn trong backlog. Không coi khóa metadata là thay thế DWG transaction.

Thư viện Skill workflow chỉ là danh sách tool đã hỗ trợ, không đọc/chạy script. Điểm placement chuẩn hóa theo cạnh task. Hình học cục bộ của block là mm; kích thước cửa Starter cố định 900 mm, không suy ra kích thước thiết kế thực.

ADRs cần chốt tiếp: UI native/WebView hoặc browser desktop; đóng gói self-contained; versioned install/update; chính sách nhận assets DWG/DWT; định dạng Skill v2 có workflow giàu hơn; quy tắc scheduler/recovery và QA nghiệp vụ.

Nguồn launcher: [Autodesk startup switches](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Startup-switches-for-AutoCAD.html), [Running scripts at startup](https://help.autodesk.com/cloudhelp/2022/ENU/AutoCAD-Customization/files/GUID-5510017F-4656-478F-BD4C-AB6B1998BF55.htm).
