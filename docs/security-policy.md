# Chính sách 0.1

- HTTP chỉ bind IPv4 loopback, xác thực bearer ngẫu nhiên; không CORS cho nguồn khác. Request Origin không đúng bị từ chối. CSP hạn chế tài nguyên cùng origin.
- Plugin pipe ACL theo Windows user, tên session không đoán trước, secret riêng; client PID/path phải là Host executable đã cấu hình. Host kiểm tra server PID và process start ticks.
- Không có MCP approve/apply, shell, C#, LISP hoặc SendCommand tùy ý. SendStringToExecute trong plugin chỉ dùng tên command cố định do sản phẩm đăng ký.
- Native review giữ approval private, gắn plan/hash/context, hết hạn sau 5 phút. Đổi tab/file/revision hoặc đóng palette làm stale. Duyệt không gọi model.
- Shared validator giới hạn kích thước payload, số entity/vertex, tọa độ hữu hạn, tool whitelist, scope, nguồn template. Native bounds được kiểm tra trong transaction trước commit.
- DWG read-only hoặc UndoRecording=false bị chặn. Physical identity dùng Windows file identity cho DWG đã lưu; DWG mới dùng session/document ID riêng.
- Durable fence không tự giải phóng sau timeout/restart. Plan retry không vượt qua state/receipt. Phục hồi UNKNOWN cần đối soát; không có API “force retry”.
- Tệp nguồn và Skill chỉ nhận dạng giới hạn. Import Skill không tải executable. PDF/ảnh chưa được parse hoặc OCR trong 0.1.
- Token không xuất qua API/state, stdout MCP, Git hoặc báo cáo kiểm thử. Quyền cùng Windows user là ranh giới tin cậy của bản hiện tại.
- Không tự tắt SECURELOAD, sửa TRUSTEDPATHS, chấp nhận giấy phép hoặc sửa tài khoản Autodesk.

Chưa phải đánh giá bảo mật production: cần threat model đầy đủ, installer ký số, lock ACL riêng cho content, audit actor và resource revocation/recovery đa tiến trình hoàn chỉnh.
