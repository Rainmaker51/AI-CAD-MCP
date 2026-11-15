# Phát triển AI CAD trên nhiều máy

Ngày: 12/11/2026. Đây là hướng dẫn cấu hình; dự án hiện chưa có Git và chưa có bộ thiết lập máy mới tự động.

## 1. Bố trí khuyến nghị

- Mã nguồn: Git/GitHub repository riêng tư; mỗi máy có bản làm việc trên ổ cục bộ.
- Kho Quang Anh: KHO-AI-CAD qua Google Drive for desktop hoặc ổ cứng di động.
- .NET, NuGet, Python/OCR, bản build, dữ liệu chạy và phiên tài khoản: lưu riêng trên từng máy.

Không đồng bộ thư mục code đang build bằng Drive. Kho chia sẻ chỉ có một máy sửa/xuất bản tại một thời điểm; đợi đồng bộ hoàn tất trước khi đổi máy. Cơ chế khóa hiện tại không phải khóa phân tán giữa các máy qua Drive.

## 2. Chuyển lần đầu khi chưa dùng Git

Trước khi sao chép: lưu DWG, đóng AutoCAD và dừng app/Host nếu đang chạy.

Chép cây dự án AI-CAD-MCP sang máy mới, có thể bỏ các thư mục sau khỏi bản chép: storage/01-test-data, storage/02-app-data, artifacts cũ ở gốc và .tools/dotnet-home. Giữ storage/04-builds/artifacts là bản build đang dùng. Giữ storage/03-python-ocr nếu cần OCR và đọc ảnh. Giữ .tools/dotnet để có SDK/runtime đi kèm; .tools/nuget/packages có thể giữ để tránh tải lại hoặc để restore tải lại.

Phải sao chép dữ liệu vật lý một lần. Không đi theo/copy các junction tuyệt đối của máy cũ: tests/fixtures, tests/results, data-p0-p6 và .tools/python-packages. Nếu dùng robocopy để chép, dùng /XJ để bỏ junction. Giữ bản gốc cho tới khi máy mới được kiểm tra.

Chép riêng toàn bộ KHO-AI-CAD, hoặc đồng bộ kho đó bằng Drive. Không chỉ chép storage; mã nguồn, công cụ, content, schemas và các cấu hình gốc cũng cần được giữ.

Dữ liệu công trình cần mang theo: sao chép riêng các DWG và nguồn mong muốn. Tránh mang nguyên dữ liệu phiên/tài khoản từ storage/02-app-data sang dùng chung. Đăng nhập AI riêng ở máy mới.

## 3. Máy mới

Cần Windows phù hợp với môi trường dự án, AutoCAD 2021 cho plugin hiện tại, bộ .NET 10 đã mang theo (hoặc cài SDK/runtime tương ứng), và môi trường Python/OCR tương thích nếu dùng phân tích nguồn. Bộ packages OCR hiện dùng Python 3.12; không giả định các gói nhị phân sẽ hoạt động với Python phiên bản khác.

Nếu muốn dùng các Skill của Codex như grill-me/grilling, cài riêng trên máy mới. Đây là các Skill của trợ lý phát triển, khác với Skill PDF/ảnh sang CAD trong kho app. Không mang nguyên thư mục đăng nhập Codex để đồng bộ tài khoản.

Ví dụ đặt mã nguồn ở C:\AI-CAD\AI-CAD-MCP. Đường dẫn trên mỗi máy có thể khác nhau.

Tại thư mục dự án mới, tạo lại các junction theo đường dẫn của máy đó. Chỉ thực hiện khi các đường dẫn junction chưa tồn tại; nếu đã chép nhầm liên kết trỏ về ổ máy cũ, kiểm tra rồi bỏ liên kết đó trước, không xóa thư mục dữ liệu đích.

```powershell
$repo = (Get-Location).Path
$pairs = @{
  'tests/fixtures' = 'storage/01-test-data/fixtures'
  'tests/results' = 'storage/01-test-data/results'
  'data-p0-p6' = 'storage/02-app-data/data-p0-p6'
  '.tools/python-packages' = 'storage/03-python-ocr/python-packages'
}
foreach ($pair in $pairs.GetEnumerator()) {
  $targetPath = Join-Path $repo $pair.Value
  $linkPath = Join-Path $repo $pair.Key
  New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
  New-Item -ItemType Directory -Path (Split-Path $linkPath -Parent) -Force | Out-Null
  if (-not (Test-Path -LiteralPath $linkPath)) {
    New-Item -ItemType Junction -Path $linkPath -Target $targetPath | Out-Null
  }
}
```

## 4. Cấu hình riêng của máy

Cập nhật các tệp dưới đây theo đường dẫn thật ở máy mới:

- storage/04-builds/artifacts/p0-p6/install.json
- storage/04-builds/artifacts/p0-p6-warehouse/install.json

Ví dụ các giá trị cấu trúc, thay PythonExecutable và CodexExecutable bằng tệp thực tế của máy mới:

```json
{
  "DataRoot": "C:\\AI-CAD\\AI-CAD-MCP\\storage\\02-app-data\\data-p0-p6",
  "ContentRoot": "C:\\AI-CAD\\AI-CAD-MCP",
  "HostExecutable": "C:\\AI-CAD\\AI-CAD-MCP\\storage\\04-builds\\artifacts\\p0-p6\\Host\\AICad.Host.exe",
  "PythonExecutable": "DUONG_DAN_PYTHON_THUC_TE",
  "CodexExecutable": "DUONG_DAN_CODEX_THUC_TE"
}
```

Hai install.json phải thống nhất DataRoot, ContentRoot và HostExecutable. Chỉ chép install.json cũ mà không sửa đường dẫn sẽ không đủ.

Kết nối kho trong Cài đặt của app hoặc chỉnh warehouse.local.json để trỏ đến thư mục KHO-AI-CAD đã đồng bộ trên máy mới, không dùng link web Drive. Tệp này được bỏ qua bởi Git vì đường dẫn khác nhau theo máy.

## 5. Kiểm tra build

Chạy tại thư mục chứa AI-CAD-MCP.slnx:

```powershell
$env:DOTNET_ROOT = Join-Path (Get-Location) '.tools/dotnet'
$env:NUGET_PACKAGES = Join-Path (Get-Location) '.tools/nuget/packages'
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.tools/dotnet-home'
& ./.tools/dotnet/dotnet.exe --info
& ./.tools/dotnet/dotnet.exe restore AI-CAD-MCP.slnx --force
& ./.tools/dotnet/dotnet.exe build AI-CAD-MCP.slnx -c Release --no-restore
```

Nếu máy mới được clone chỉ có mã nguồn và chưa có bản publish, tạo các đầu ra mà launcher hiện sử dụng:

```powershell
& ./.tools/dotnet/dotnet.exe publish src/Desktop.Host/Desktop.Host.csproj -c Release -o storage/04-builds/artifacts/p0-p6/Host
& ./.tools/dotnet/dotnet.exe publish src/CAD.HashCompat/CAD.HashCompat.csproj -c Release -o storage/04-builds/artifacts/p0-p6/Host/HashCompat
& ./.tools/dotnet/dotnet.exe publish src/CAD.LibraryScanner/CAD.LibraryScanner.csproj -c Release -o storage/04-builds/artifacts/p0-p6/Host/LibraryScanner
& ./.tools/dotnet/dotnet.exe publish src/Desktop.Launcher/Desktop.Launcher.csproj -c Release -o storage/04-builds/artifacts/launcher
& ./.tools/dotnet/dotnet.exe publish src/CAD.Bridge.AutoCAD/CAD.Bridge.AutoCAD.csproj -c Release -o storage/04-builds/artifacts/p0-p6-warehouse/AutoCAD
```

Kiểm tra từng lệnh thành công trước khi tiếp tục. Cần hai install.json theo mục 4. Đường dẫn xuất bản trên tương ứng launcher hiện tại; không mặc định rằng build thư mục app bất kỳ sẽ được launcher chọn.

Mở Mo AI CAD.cmd, kiểm tra app kết nối AutoCAD, chỉ có Template Quang Anh và Skill PDF/ảnh sang CAD; đăng nhập AI và thử nguồn nhỏ, preview rồi hủy trước khi dùng công trình thật.

## 6. Làm việc nhiều máy bằng Git

Dự án hiện chưa được khởi tạo Git. Khi thiết lập, dùng repository riêng tư, rà danh sách tệp trước commit đầu. Không đẩy storage, thư viện DWG riêng, token/tài khoản hoặc warehouse.local.json. .gitignore hiện đã bỏ qua .tools, storage, bin/obj, dữ liệu chạy và tệp chỉ đường dẫn kho.

Bắt đầu làm trên máy B: cập nhật mã nguồn bằng pull, chờ kho Drive đồng bộ, rồi build cục bộ. Kết thúc trên máy A: commit và push thay đổi đã kiểm tra. Nếu sửa song song, dùng nhánh riêng và hợp nhất thay đổi; không lấy thư mục từ máy này chép đè lên bản sửa chưa lưu của máy kia.

Với ổ cứng di động, có thể dùng để chuyển mã nguồn/kho và bản sao lưu. Nên làm việc trên bản cục bộ ở mỗi máy; giữ lịch sử Git để đồng bộ thay đổi.

## Nguồn hướng dẫn

- Git distributed workflows: https://git-scm.com/book/en/v2/Distributed-Git-Distributed-Workflows
- Google Drive streaming/mirroring: https://support.google.com/drive/answer/13401938
- Google Drive offline: https://support.google.com/drive/answer/16631477
