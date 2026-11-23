"""Seal a tested Manager update without touching the installed loader or catalog."""
from pathlib import Path
import datetime
import hashlib
import json
import shutil
import subprocess
import xml.etree.ElementTree as ET

repo=Path(__file__).resolve().parent.parent
stage=Path((repo/'.tools/cad-manager-stage.txt').read_text(encoding='utf-8-sig').strip())
warehouse=Path(json.loads((repo/'warehouse.local.json').read_text(encoding='utf-8-sig'))['path'])
development=warehouse/'Tools/dotnet/_Development/CAD.Manager'
assert stage.resolve().is_relative_to((development/'working').resolve())
version=ET.parse(repo/'src/CAD.Bridge.AutoCAD/CAD.Bridge.AutoCAD.csproj').findtext('.//Version')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
package=stage/'package/AutoCAD'
files=[{'name':p.name,'hash':sha(p),'bytes':p.stat().st_size} for p in sorted(package.iterdir(),key=lambda p:p.name.lower()) if p.is_file()]
assert len(files)==7
fingerprint=hashlib.sha256(''.join(f"{f['name']}\t{f['hash']}\n" for f in files).encode()).hexdigest()
native=sorted((stage/'validation').glob('ribbon-*'))[-1]
result=json.loads((native/'ribbon-edit-results.json').read_text(encoding='utf-8-sig'));assert result['passed']
compat=sorted((stage/'qa-compat/validation').glob('native-*'))[-1]
assert json.loads((compat/'ribbon-manager-results.json').read_text(encoding='utf-8-sig'))['passed']
out=development/'versions'/version/('build-'+fingerprint[:12])
if out.exists():raise SystemExit('Immutable build exists: '+str(out))
out.mkdir(parents=True)
shutil.copytree(stage/'package',out/'package')
shutil.copy2(repo/'tools/Update-CadManagerRibbon.ps1',out/'CAP_NHAT_MAY_NAY.ps1')
shutil.copy2(repo/'docs/CAD-MANAGER-0.9.md',out/'HUONG_DAN.md')
validation=out/'validation';validation.mkdir()
for source,name in [(native/'ribbon-edit-results.json','ribbon-edit.json'),(native/'manager.png','manager.png'),(compat/'ribbon-manager-results.json','qa-compat.json'),(compat/'library-data/standalone.json','qa-standalone.json')]:shutil.copy2(source,validation/name)
patch=subprocess.check_output(['git','diff','--binary','HEAD'],cwd=repo)
for name in filter(None,subprocess.check_output(['git','ls-files','--others','--exclude-standard','-z'],cwd=repo).split(b'\0')):
    r=subprocess.run(['git','diff','--no-index','--binary','--','/dev/null',name.decode('utf-8')],cwd=repo,capture_output=True)
    if r.returncode not in (0,1):raise RuntimeError(r.stderr.decode(errors='replace'))
    patch+=r.stdout
(out/'source.patch').write_bytes(patch)
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo).decode().strip()
manifest={'component':'CAD.Manager','version':version,'fingerprint':fingerprint,'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sourceCommit':commit,'sourcePatch':{'name':'source.patch','hash':sha(out/'source.patch')},'files':files,'validation':result,'qa16Compatible':True,'installed':False,'guiMouseAfterInstall':False}
(out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'NOTES.md').write_text(f'''# CAD Tool Manager {version} — {fingerprint[:12]}

Source `{commit}` + source.patch; chưa commit/push. DLL SHA256: `{next(f['hash'] for f in files if f['name']=='CAD.Bridge.AutoCAD.dll')}`.

Thêm cột tab thực tế, chuyển tất cả nút kể cả tab riêng trong một revision, danh sách checkbox và thao tác hàng loạt, bỏ ghim tất cả, hoàn tác được bảo vệ khi có chỉnh sửa khác, ghim một nút chính, đưa ghim tất cả vào nâng cao. Giữ nguyên các cấu hình cũ khi cập nhật; không tự đặt lại tab hoặc ghim.

Đã chạy hồi quy catalog, Core 2021 với các nút WPF thật và đối chiếu RibbonControl, render giao diện, QA 1.6 chạy dưới Manager mới. Không kiểm thử chuột đầy đủ trong GUI sau cài. Không đổi catalog, DWG hoặc gói đang dùng trong kiểm thử.

Gói chưa cài tại lúc chốt. Đọc HUONG_DAN.md. Lưu và đóng AutoCAD trước khi chạy CAP_NHAT_MAY_NAY.ps1 -Apply. Bộ cập nhật chỉ thay Manager sau khi kiểm tra hash và sao lưu; giữ Host, Registry loader, install.json, catalog và các add-in đã cài. Cập nhật cùng Manager trên máy còn lại.
''',encoding='utf-8')
(repo/'.tools/cad-manager-release.txt').write_text(str(out),encoding='utf-8')
print(out)
print('DLL SHA256: '+next(f['hash'] for f in files if f['name']=='CAD.Bridge.AutoCAD.dll'))
