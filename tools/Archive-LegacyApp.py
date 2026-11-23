"""Archive retired app/PDF data without touching native CAD resources or add-in settings.

Default is a dry run. --apply requires all AutoCAD windows and the local Host closed.
Original relative paths and SHA256 hashes are recorded for manual recovery.
"""
from pathlib import Path
import argparse, datetime, hashlib, json, shutil, subprocess

def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''): h.update(b)
    return h.hexdigest()

def plan(repo, warehouse, data):
    jobs=[]
    def add(base, rel, move):
        p=base/rel
        if p.exists(): jobs.append((base,p,move))
    # All Skill definitions belong to the old app. Keep non-PDF definitions in
    # place; move only source builders (including all historical versions).
    ids=set()
    for folder in (warehouse/'Versions/skills').glob('*'):
        pointer=folder/'active.json'
        if pointer.exists():
            content=folder/read(pointer)['version']/'content.json'
            if read(content).get('Builder')=='source': ids.add(folder.name)
    for folder in (warehouse/'Skills').glob('*'):
        p=folder/'manifest.json'
        if p.exists() and read(p).get('Builder')=='source': ids.add(folder.name)
    for name in sorted(ids):
        add(warehouse,Path('Versions/skills')/name,True)
        add(warehouse,Path('Skills')/name,True)
    for rel in ['Sources/Files','Sources/Catalog']: add(warehouse,Path(rel),True)
    # Shared resource catalogs stay active: Axis and QA still require them.
    for rel in ['Catalog','Templates','Versions/templates','Versions/standards']:
        add(warehouse,Path(rel),False)
    for rel in ['sources','source-queue']:
        add(data,Path(rel),True)
    # Existing plan/receipt history is copied, not moved or treated as reusable work.
    add(data,Path('plans'),False)
    for rel in ['src/Drawing.Intelligence','src/Desktop.Host/SourceQueue.cs','src/Desktop.Host/PdfSourcePages.cs','src/Desktop.Host/wwwroot/source.js','src/Application/ImagePlanService.cs','src/Application/ImageWorkspaceService.cs','content','schemas']:
        add(repo,Path(rel),False)
    return jobs

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');parser.add_argument('--repo',type=Path,default=Path(__file__).resolve().parent.parent)
    args=parser.parse_args();repo=args.repo.resolve()
    warehouse=Path(read(repo/'warehouse.local.json')['path']).resolve()
    if not (warehouse/'warehouse.json').is_file(): raise RuntimeError('Warehouse unavailable')
    config=read(repo/'artifacts/p0-p6/install.json');data=Path(config['DataRoot']).resolve()
    jobs=plan(repo,warehouse,data)
    if not args.apply:
        print(json.dumps([{'path':str(p),'action':'move' if move else 'snapshot'} for _,p,move in jobs],ensure_ascii=False,indent=2));return
    processes=subprocess.run(['powershell','-NoProfile','-Command',"@(Get-Process acad,AICad.Host -ErrorAction SilentlyContinue).Count"],capture_output=True,text=True,check=True)
    if processes.stdout.strip()!='0': raise RuntimeError('Save drawings and close AutoCAD and local Host before archival')
    archive=warehouse/'Dọn dẹp'/('App-cu-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
    archive.mkdir(parents=True,exist_ok=False);entries=[];moved=[]
    roots={str(warehouse):'Kho',str(data):'Du-lieu-may',str(repo):'Ma-nguon-tham-khao'}
    try:
        # Copy and verify every byte before moving anything out of active paths.
        for base,source,move in jobs:
            source=source.resolve()
            if not source.is_relative_to(base) or source==base: raise RuntimeError('Unsafe source path')
            target=archive/roots[str(base)]/source.relative_to(base)
            if target.exists(): raise RuntimeError('Archive collision')
            target.parent.mkdir(parents=True,exist_ok=True)
            if source.is_dir(): shutil.copytree(source,target)
            else: shutil.copy2(source,target)
            files=sorted(p for p in source.rglob('*') if p.is_file()) if source.is_dir() else [source]
            for p in files:
                dest=target/p.relative_to(source) if source.is_dir() else target
                h=digest(p)
                if digest(dest)!=h: raise RuntimeError('Archive hash differs: '+str(p))
                entries.append({'source':str(p),'archive':dest.relative_to(archive).as_posix(),'sha256':h,'bytes':p.stat().st_size,'action':'move' if move else 'snapshot'})
        (archive/'manifest.json').write_text(json.dumps({'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'files':entries,'sourceCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo).decode().strip()},ensure_ascii=False,indent=2),encoding='utf-8')
        # Move originals into a second safety area instead of deleting. The verified
        # snapshot remains a separate recovery copy in this archive.
        for base,source,move in jobs:
            if not move: continue
            source=source.resolve();target=archive/'Ban-goc-da-chuyen'/roots[str(base)]/source.relative_to(base)
            if not source.is_relative_to(base) or not target.resolve().is_relative_to(archive): raise RuntimeError('Unsafe move')
            target.parent.mkdir(parents=True,exist_ok=True);shutil.move(str(source),str(target));moved.append((source,target))
            if source in [warehouse/'Sources/Files',warehouse/'Sources/Catalog',data/'sources',data/'source-queue']:
                source.mkdir(parents=True,exist_ok=True)
        (archive/'README.md').write_text('''# Dọn dẹp — dữ liệu app / PDF sang CAD cũ

PDF sang CAD đã ngừng hoạt động. Các Skill nguồn, PDF/ảnh và hàng đợi cũ được chuyển vào đây.
`manifest.json` ghi đường dẫn gốc, SHA256 và phân biệt `move` với `snapshot`.
`Ban-goc-da-chuyen` chứa nguyên thư mục gốc đã chuyển; các thư mục Kho/Du-lieu-may là bản sao đã kiểm tra hash.
Catalog, template, tiêu chuẩn layer, block DWG, Tools và cấu hình add-in vẫn ở vị trí đang dùng.
Hồ sơ Usage/Role cũ trong snapshot được giữ làm dữ liệu tham khảo; không còn là quyền sử dụng của QA/Tạo trục.
Không chép đè snapshot Catalog/Template trở lại kho đang dùng. Nếu cần phục hồi, đối chiếu manifest và đóng CAD trên các máy trước.
Mã nguồn tham khảo chưa bao gồm toàn bộ dự án; source đầy đủ và thay đổi tiếp theo nằm trong Git.
''',encoding='utf-8')
        print(str(archive))
    except BaseException:
        for source,target in reversed(moved):
            if source.exists() and source.is_dir() and not any(source.iterdir()): source.rmdir()
            if not source.exists():shutil.move(str(target),str(source))
        raise

if __name__=='__main__':main()
