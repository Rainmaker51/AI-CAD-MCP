# Bàn giao Reader V1 và Axis 1.9.0

**Yêu cầu mới:** tách Reader để người dùng tự cài, Manager giữ **0.10.0**. Các lần 0.10.0 đóng gói Reader và 0.10.1 dưới đây là lịch sử; đối chiếu [cơ chế cài độc lập](LIBRARY-READER-INDEPENDENT.md) và manifest `readerDeployment: separate` của build mới.

Cập nhật tiếp ngày 25/11/2026: máy này đã lên **Manager 0.10.1** để sửa tiêu đề ghi cứng 0.9.0. Reader 1.0.0, Axis 1.9.0 và catalog giữ nguyên. Xem [bàn giao bản sửa tiêu đề](CAD-MANAGER-0.10.1.md). Phần dưới ghi nhận lần bàn giao Reader ban đầu.

Ngày 25/11/2026. Đã cài trên máy này: Reader 1.0.0 trong Manager 0.10.0; Axis 1.9.0 đã được Manager chọn ở revision catalog 146. Reader không là mục Ribbon riêng. QA vẫn dùng gói hiện có, không đổi.

Nguồn build: commit `7de2051278487424d290f7375e9da1445b674a20` cộng `source.patch` trong từng build. SHA256 của patch: `681232525b2196c9d9008c638953742cc179be672dad58f0e8f5b14dd79b6187`. Patch chứa cả các chỉnh sửa chưa commit có sẵn trong workspace tại lúc build; không bỏ các sửa QA trước đó. Chưa tạo commit hoặc push trong đợt này.

| Thành phần | Version | Build | SHA256 DLL chính |
|---|---|---|---|
| Rainmaker.Library.Reader | 1.0.0 | `085aa8cbc2c0` | `abe7e02461b93a0d52977ccbc6ecb041b05825571c8486fc043309f50a508263` |
| CAD.Manager | 0.10.0 | `d424b6ab434e` | `b8f01bd870f2fd5ad253879534bc3f3c84e7844fe6e3027503134ee0fa44a988` |
| Rainmaker.Axis | 1.9.0 | `f5970b5c78ea` | `d3163a8ea6c747e59759a487785cb6f0a9c07e70c41103dcc83016632125a9c5` |

Các build bất biến nằm dưới kho đang kết nối, tính từ `Tools/dotnet/_Development/`:

- `Rainmaker.Library.Reader/versions/1.0.0/build-085aa8cbc2c0/`
- `CAD.Manager/versions/0.10.0/build-d424b6ab434e/`
- `Rainmaker.Axis/versions/1.9.0/build-f5970b5c78ea/`

Mỗi build có manifest/hash, patch nguồn, hướng dẫn và bằng chứng chọn lọc. Bản Axis đang dùng: `Tools/dotnet/_Installed/Rainmaker.Axis/1.9.0-f5970b5c78ea/`; chỉ có một gói Axis tại `_Installed`. Manager đã tự lưu lịch sử bản cũ. Bản sao lưu Manager cục bộ: `artifacts/manager-install-backups/20261125-155748-1da701/`.

Đã xác minh hash toàn bộ DLL bàn giao và DLL cài, Reader được nạp đúng một bản cạnh Manager trong test Core. Cấu hình `rainmaker-axis.json`, Ribbon/tab, các công cụ khác và byte của DWG thư viện không đổi sau cập nhật.

Kiểm thử đạt: Reader đủ 5 loại và dữ liệu cũ; loại bỏ nháp/thùng rác/ID đã loại; checksum/snapshot/hàng chờ; thay storage với cùng DLL consumer; hồi quy catalog Manager; toàn bộ Axis native trên Core 2021 (Dim, annotative, cột/tường/Hatch, chỉnh lưới, preview, phủ bì, lưu/mở lại); QA 1.11.1 chạy dưới Manager mới. Đọc kho thật xác minh được 36 Block, 33 Layer, 24 Hatch, 15 Text và 8 Dim đang hoạt động tại thời điểm kiểm tra.

Chưa thử chuột thủ công trong AutoCAD GUI sau cài. Mở lại AutoCAD để dùng DLL mới. Trên máy thứ hai phải cài Manager 0.10.0 trước khi nạp Axis 1.9.0; chờ đồng bộ catalog/gói kho đầy đủ.

Đã dọn 1.214,8 MiB dữ liệu tạm của phiên (Core profile/runtime/fixture và working build). Giữ các release, bằng chứng chọn lọc, bản sao lưu Manager và hồ sơ tại `Rainmaker.Library.Reader/cleanup-records/reader-v1-20261125-1600/`. Không dọn lịch sử/version của phiên khác.

Xem [hướng dẫn cập nhật](LIBRARY-READER-1.0.md) và [giao thức V1 cho add-in mới](LIBRARY-READER-PROTOCOL.md).
