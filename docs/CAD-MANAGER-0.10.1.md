# Manager 0.10.1 — sửa phiên bản trên tiêu đề

Ngày 25/11/2026. DLL 0.10.0 đã được cài đúng nhưng bảng Manager còn ghi cứng tiêu đề 0.9.0. Bản 0.10.1 lấy phiên bản từ assembly đang chạy, tránh lệch tiêu đề ở các lần cập nhật sau.

Đã cài và xác minh hash trên máy này. Catalog/Ribbon không đổi; Reader 1.0.0 và Axis 1.9.0 giữ nguyên. Mở lại AutoCAD để nạp bản sửa. Máy còn lại cần cập nhật Manager bằng `CAP_NHAT_MAY_NAY.ps1 -Apply` trong build sau khi lưu/đóng CAD.

- Build trong kho: `Tools/dotnet/_Development/CAD.Manager/versions/0.10.1/build-cca7722c1ef7/`.
- SHA256 DLL: `d1e21334f6290204e295af34f9a37e9947572cc63a47e6579b23690d83dcc3c6`.
- Source: commit `7de2051278487424d290f7375e9da1445b674a20` cộng `source.patch` SHA256 `61714ceabeff1a259cccd9a7292c3622130486c13e25bc174cbb8714800588ee`.
- Kiểm thử Ribbon trên Core 2021 đạt; đã render và xem ảnh tiêu đề đúng 0.10.1. Ảnh/kết quả giữ trong `validation` của build. Chưa thử chuột thủ công sau cài.
- Đã dọn 237,1 MiB working/runtime thử của phiên. Hồ sơ: `_Development/CAD.Manager/cleanup-records/manager-0.10.1-20261125-1624/`.

Chưa commit/push các thay đổi nguồn.
