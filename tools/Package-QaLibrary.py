"""Seal a verified QA staging build. Does not install or edit the tool catalog."""
from pathlib import Path
import datetime
import hashlib
import json
import shutil
import subprocess
import xml.etree.ElementTree as ET

repo = Path(__file__).resolve().parent.parent
stage = Path((repo / '.tools/qa-library-stage.txt').read_text(encoding='utf-8-sig').strip())
version = ET.parse(repo / 'src/Rainmaker.Library/Rainmaker.Library.csproj').findtext('.//Version')
warehouse = Path(json.loads((repo / 'warehouse.local.json').read_text(encoding='utf-8-sig'))['path'])
development = warehouse / 'Tools/dotnet/_Development/Rainmaker.Library'
assert stage.resolve().is_relative_to((development / 'working').resolve())

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def files(folder):
    return [{'name': p.relative_to(folder).as_posix(), 'hash': sha(p), 'bytes': p.stat().st_size}
            for p in sorted(folder.rglob('*'), key=lambda p: p.as_posix().lower()) if p.is_file()]

package_files = files(stage / 'package')
assert {f['name'] for f in package_files} == {'Rainmaker.Library.dll', 'Rainmaker.Library.pdb', 'Rainmaker.Library.Core.dll', 'Rainmaker.Library.Core.pdb', 'CAD.LibraryScanner.dll', 'CAD.LibraryScanner.pdb', 'Newtonsoft.Json.dll'}
fingerprint = hashlib.sha256(''.join(f"{f['name']}\t{f['hash']}\n" for f in package_files).encode()).hexdigest()
native = sorted((stage / 'validation').glob('native-*'))[-1]
native_result = json.loads((native / 'ribbon-manager-results.json').read_text(encoding='utf-8-sig'))
data_result = json.loads((native / 'library-data/result.json').read_text(encoding='utf-8-sig'))
http = sorted((stage / 'validation').glob('http-*'))[-1]
http_result = json.loads((http / 'result.json').read_text(encoding='utf-8-sig'))
assert native_result['passed'] and data_result['passed'] and http_result['passed']
assert http_result.get('crossRuntimeNativeSignature'), 'Verify the actual Framework-produced snapshot in Host runtime'
for test_file in ['live-insert.json', 'hatch-view.json', 'standalone.json', 'real-catalog.json']:
    assert json.loads((native / 'library-data' / test_file).read_text(encoding='utf-8-sig'))['passed']
assert (native / 'library-data/browser.png').is_file()
out = development / 'versions' / version / ('build-' + fingerprint[:12])
if out.exists():
    raise SystemExit('Immutable build already exists: ' + str(out))
out.mkdir(parents=True)
shutil.copytree(stage / 'package', out / 'package')
shutil.copy2(repo / 'docs/QA-LIBRARY-1.6.md', out / 'HUONG_DAN.md')
shutil.copy2(repo / 'tools/Install-QaLibrary.ps1', out)
validation = out / 'validation'
validation.mkdir()
for src, name in [(native / 'ribbon-manager-results.json', 'manager.json'),
                  (native / 'library-data/result.json', 'native-data.json'),
                  (native / 'library-data/browser.png', 'browser.png'),
                  (native / 'library-data/live-insert.json', 'live-insert.json'),
                  (native / 'library-data/hatch-view.json', 'hatch-view.json'),
                  (native / 'library-data/standalone.json', 'standalone.json'),
                  (native / 'library-data/real-catalog.json', 'real-catalog.json'),
                  (http / 'result.json', 'http.json')]:
    shutil.copy2(src, validation / name)
# No host.json, process token, CAD profile, runtime cache or test install config is released.
patch = subprocess.check_output(['git', 'diff', '--binary', 'HEAD'], cwd=repo)
untracked = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', '-z'], cwd=repo).split(b'\0')
for name in filter(None, untracked):
    result = subprocess.run(['git', 'diff', '--no-index', '--binary', '--', '/dev/null', name.decode('utf-8')], cwd=repo, capture_output=True)
    if result.returncode not in (0, 1):
        raise RuntimeError(result.stderr.decode('utf-8', errors='replace'))
    patch += result.stdout
(out / 'source.patch').write_bytes(patch)
canonical = [{'name': f['name'], 'hash': f['hash']} for f in package_files]
package_hash = hashlib.sha256(json.dumps(canonical, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
catalog = json.loads((warehouse / 'Tools/catalog.json').read_text(encoding='utf-8-sig'))
selected = next(t for t in catalog['Items'] if t['Id'] == 'c6b4bffecd704cbcb56292579dcb6610')
manifest = {
    'version': version, 'fingerprint': fingerprint, 'packageHash': package_hash,
    'entryPoint': 'package/Rainmaker.Library.dll', 'command': 'QALIBRARY', 'protocol': 2,
    'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repo).decode().strip(),
    'sourcePatch': {'name': 'source.patch', 'hash': sha(out / 'source.patch')},
    'createdUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'files': package_files, 'engineFiles': [], 'requiresHost': False, 'backend': 'in-process',
    'validation': {'applicationFixture': True, 'http': True, 'nativeManager': True,
                   'nativeProfiles': True, 'wpfRendered': True, 'singleWindowLayout': True,
                   'capturedLayoutContext': True, 'liveCad': True, 'nativeInsertion': True,
                   'hatchPatternGroups': True, 'hostExecutableMissing': True,
                   'nativeDirectPublication': True, 'realCatalogReadOnly': True,
                   'guiCrudOnInstalledBuild': False},
    'catalogAtHandoff': {'revision': catalog['Revision'], 'selected': False,
                         'currentPath': selected['RelativePath'], 'currentFileHash': selected['FileHash']}
}
(out / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
(out / 'NOTES.md').write_text(f'''# Thư viện QA {version} — build {fingerprint[:12]}

Source commit: `{manifest['sourceCommit']}` + `source.patch` (bao gồm tài liệu giao diện đã sửa trước lượt này). Chưa commit/push.

DLL SHA256: `{next(f['hash'] for f in package_files if f['name']=='Rainmaker.Library.dll')}`.
PackageHash: `{package_hash}`. Fingerprint đầy đủ trong manifest.

Bản 1.6.0 quản lý DWG và danh mục kho trực tiếp trong AutoCAD bằng Rainmaker.Library.Core.dll. Không dùng Host, HTTP, token hoặc .NET 10 để chạy QA. App/Skill giữ định dạng và đọc cùng danh mục đã duyệt. Gói bàn giao chỉ chứa add-in, không có engine hoặc bước cài dịch vụ nền.

Sửa lỗi bảng trống của 1.5: khác cách ghi số thực giữa hai runtime; nhiều hồ sơ dùng cùng block gốc bị truy vấn như một bản ghi; hồ sơ thiếu Usage. Giữ riêng nhóm, ghi chú, mã, duyệt và tham số từng hồ sơ. Lỗi đọc CAD hiện danh mục kho chỉ xem và chặn sửa, không biến lỗi đọc thành xóa dữ liệu.

Đọc hiện trạng DWG đang mở hoặc file đã lưu thay cho việc chỉ hiển thị catalog cũ. Lưu danh mục là hành động rõ ràng, chọn từng thay đổi, yêu cầu DWG đã lưu và kiểm tra lại chữ ký nội dung. Không ghi đè nguồn; không tự khôi phục mẫu đã xóa. Mục chỉ còn định nghĩa và mục đã mất có bộ lọc riêng. Giữ cờ duyệt sử dụng cũ của block trong Usage, không nhầm với cờ hồ sơ quản lý.

Thêm chèn block sang bản vẽ khác qua Manager: điểm chèn, góc, tỷ lệ, tham số động, ATT và xử lý trùng tên có lựa chọn. Không tự tạo nhóm thư viện trong bản vẽ đích. Hatch xem theo Pattern thu gọn mặc định, giữ các cấu hình scale/góc/màu riêng. Ảnh block 240×180 được tạo từ bản chụp CAD và chỉ lưu đệm trên máy; không cần Core profile phụ cho mỗi lần tạo ảnh. Cấu hình add-in đang bật cũng được kiểm tra theo ID trước khi loại tài nguyên khỏi kho.

Kiểm thử: runtime Manager 0.8.3 đang cài trên Core 2021; dữ liệu mẫu/ATT/nhóm và bố trí; bản chụp mở/đã lưu tương đương, nguồn không đổi, giữ định nghĩa/layer chưa dùng; đổi tên block và đổi scale hatch giữ ID; ảnh CAD; chèn/góc/tỷ lệ/ATT/giữ hoặc cập nhật trùng tên/lưu-mở lại; hatch nhóm Pattern thu gọn và lọc vẫn giữ từng biến thể; dữ liệu ứng dụng và HTTP 5 loại với endpoint live, duyệt có kiểm tra bản đã lưu và xác thực. Hai cảnh báo nullable có sẵn trong scanner không thuộc thay đổi này.

Chưa kiểm tra đầy đủ thao tác chuột trên QA mới trong AutoCAD GUI. Chưa đổi lựa chọn trong catalog, gói hiện tại là `{selected['RelativePath']}` ở revision {catalog['Revision']}.

Đọc HUONG_DAN.md. Chỉ chọn Cập nhật file ở đúng mục Rainmaker.Library trong Manager với cả 7 file package; mở lại AutoCAD nếu dependency cũ đã nạp. Không cần cập nhật/mở dịch vụ nền. Không chép tay sang Installed. Giữ nguyên versions và gói đang dùng; staging riêng của phiên này có thể dọn sau khi kiểm tra hash bản bàn giao.
''', encoding='utf-8')
(repo / '.tools/qa-library-release.txt').write_text(str(out), encoding='utf-8')
print(out)
print('DLL SHA256: ' + next(f['hash'] for f in package_files if f['name'] == 'Rainmaker.Library.dll'))
