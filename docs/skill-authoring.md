# Viết Skill 0.1

Mỗi Skill là `skills/<ID>/manifest.json`. Có thể dùng nút Nhập Skill JSON trong app; metadata được kiểm tra trước khi lưu. Không sửa MCP Core nếu chỉ phối hợp tool hiện có.

Trường bắt buộc: Id, Name, Version, Enabled, TemplateId, RequiredTools, RequiredInputs, Workflow. Xem hai manifest thật trong `skills/`.

Workflow chứa các Operation khai báo: Kind, Points, Layer và tham số theo tool. Points có X/Y chuẩn hóa theo đường bao task bốn đỉnh: `scope[0] + X*(scope[1]-scope[0]) + Y*(scope[3]-scope[0])`. Rotation cộng góc cạnh đầu của task. Radius, Height và block geometry dùng mm. Không có eval hoặc công thức chạy code.

Tool hiện có: draw_line (2 điểm), draw_polyline (ít nhất 2 điểm, Closed), draw_circle (1 tâm/Radius), draw_arc (1 tâm/Radius, cung 90° từ Rotation), add_text (1 điểm/Text/Height), insert_block (1 điểm/AssetId/Rotation).

RequiredInputs là tên tham số số dương đã xác nhận. Trong Text có thể dùng `{{floorHeight}}` để chèn giá trị vào ghi chú. Input hiện chưa điều khiển hình học qua biểu thức; thuật toán nghiệp vụ mới cần tool/engine được kiểm chứng riêng.

Chỉ phối hợp các tool đã tồn tại; thiếu tool trả DEPENDENCY_MISSING. TemplateId phải khớp task. Import không ghi đè ID đang có. Task đã tạo plan giữ snapshot; thay manifest sau đó làm plan cũ không đủ điều kiện Apply.
