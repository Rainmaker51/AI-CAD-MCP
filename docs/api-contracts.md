# API/contract 0.1

Model dùng trong code tại `src/CAD.Contracts/Models.cs` là nguồn hợp đồng thực tế. JSON truyền kiểu camelCase ở các tham số HTTP/MCP; DTO snapshot dùng tên thuộc tính C#.

| Nhóm | Tool MCP thật |
|---|---|
| Session / Document | list_sessions, list_documents, get_document_info |
| CAD read | get_entities, get_layers, get_blocks |
| Content | list_skills, list_templates |
| Task / Plan | scan_task_blocks, run_skill_plan |
| Preview / History | create_preview, cancel_preview, get_execution_log |

`SessionId + DocumentId + Revision` bắt buộc cho các truy vấn document cụ thể và tạo kế hoạch. Entity read trả metadata và bounds, offset/limit tối đa 200. Chưa có đầy đủ truy vấn hình học scope chuyên sâu.

`DrawingPlan`: SchemaVersion, PlanId, session/document/revision, task/handle, skill ID/version/hash, template snapshot/hash, CreatedUtc, Units, Space, Scope, Operations. Plan TTL tối đa 24 giờ, preview/approval private TTL 5 phút. Phải khớp revision tại Apply.

`PlanRecord`: Plan, Hash, Status, Message, UpdatedUtc, EntityHandles, ExecutionId. Trạng thái READY/PREVIEW/CANCELLED/STALE/DISPATCHED/DONE/FAILED/UNKNOWN. DISPATCHED/UNKNOWN không tự chuyển sang FAILED vì timeout.

Lỗi nghiệp vụ điển hình: SESSION_OFFLINE, CAD_TIMEOUT, DOCUMENT_NOT_FOUND, STALE_REVISION, NEEDS_INPUT, DEPENDENCY_PENDING, DEPENDENCY_MISSING, OUTSIDE_SCOPE, RESOURCE_CHANGED, UNDO_DISABLED, APPROVAL_REQUIRED, EXECUTION_UNKNOWN.

Không cung cấp API approval hoặc Apply. AI chỉ tạo/kiểm tra plan và yêu cầu hiển thị preview. Người dùng duyệt ở AutoCAD.
