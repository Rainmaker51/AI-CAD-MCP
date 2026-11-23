"""Seal tested QA/Axis resource workflow builds; never activates catalog entries."""
from pathlib import Path
import hashlib, json, shutil, subprocess, datetime, xml.etree.ElementTree as ET

repo=Path(__file__).resolve().parent.parent
warehouse=Path(json.loads((repo/'warehouse.local.json').read_text(encoding='utf-8-sig'))['path'])
qa=Path((repo/'.tools/qa-library-stage.txt').read_text(encoding='utf-8-sig').strip())
axis=Path((repo/'.tools/axis-resource-stage.txt').read_text(encoding='utf-8-sig').strip())
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo).decode().strip()
patch=subprocess.check_output(['git','diff','--binary','HEAD'],cwd=repo)
for name in filter(None,subprocess.check_output(['git','ls-files','--others','--exclude-standard','-z'],cwd=repo).split(b'\0')):
    result=subprocess.run(['git','diff','--no-index','--binary','--','/dev/null',name.decode('utf-8')],cwd=repo,capture_output=True)
    if result.returncode not in [0,1]:raise RuntimeError(result.stderr.decode(errors='replace'))
    patch+=result.stdout
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def entries(folder):return [{'name':p.relative_to(folder).as_posix(),'hash':sha(p),'bytes':p.stat().st_size} for p in sorted(folder.rglob('*'),key=lambda p:p.as_posix().lower()) if p.is_file()]
def seal(name,stage,evidence):
    assert stage.resolve().is_relative_to((warehouse/'Tools/dotnet/_Development'/name/'working').resolve())
    version=ET.parse(repo/'src'/name/(name+'.csproj')).findtext('.//Version')
    ff=entries(stage/'package');assert len(ff)==(7 if name=='Rainmaker.Library' else 2)
    for p in evidence:assert json.loads(p.read_text(encoding='utf-8-sig'))['passed'],p
    fingerprint=hashlib.sha256(''.join(f"{f['name']}\t{f['hash']}\n" for f in ff).encode()).hexdigest()
    out=warehouse/'Tools/dotnet/_Development'/name/'versions'/version/('build-'+fingerprint[:12])
    if out.exists():raise RuntimeError('Immutable build already exists: '+str(out))
    out.mkdir(parents=True);shutil.copytree(stage/'package',out/'package');(out/'validation').mkdir()
    for p in evidence:shutil.copy2(p,out/'validation'/p.name)
    (out/'source.patch').write_bytes(patch)
    manifest=dict(component=name,version=version,fingerprint=fingerprint,sourceCommit=commit,sourcePatch={'name':'source.patch','hash':sha(out/'source.patch')},createdUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),files=ff,requiresHost=False,guiMouseAfterInstall=False,status='verified-candidate')
    (out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    shutil.copy2(repo/'docs/QA-RESOURCE-WORKFLOW.md',out/'HUONG_DAN.md')
    (out/'NOTES.md').write_text(f'''# {name} {version}

Source {commit} + source.patch. SHA256/fingerprint in manifest. Tested on AutoCAD Core 2021 with Manager 0.9.0; scope and results in validation. Full mouse testing in graphical AutoCAD after installation has not been performed.

QA shows native resources without usage approval/Role, retains old profiles on disk, and inserts from native DWG defaults. Axis uses its own selected block/layer/ATT; legacy Role/Reviewed do not govern its resource choice. PDF-to-CAD is archived for future development. No new DLL project or background service was introduced.

Candidate at packaging time. Use Manager Update on the existing tool ID; do not copy into _Installed. Preserve settings/Ribbon and close AutoCAD on all updating machines. This is an add-in package, not a complete app runtime.
''',encoding='utf-8')
    return out
native=sorted((qa/'validation').glob('native-*'))[-1]
http=sorted((qa/'validation').glob('http-*'))[-1]
qout=seal('Rainmaker.Library',qa,[native/'ribbon-manager-results.json',native/'library-data/result.json',native/'library-data/live-insert.json',native/'library-data/hatch-view.json',native/'library-data/standalone.json',http/'result.json'])
shutil.copy2(native/'library-data/browser.png',qout/'validation/browser.png')
shutil.copy2(repo/'tools/Install-QaLibrary.ps1',qout/'Install-QaLibrary.ps1')
aout=seal('Rainmaker.Axis',axis,[axis/'axis-upgrade-results.json'])
print('QA:',qout);print('Axis:',aout)
(repo/'.tools/qa-resource-release.txt').write_text(str(qout),encoding='utf-8')
(repo/'.tools/axis-resource-release.txt').write_text(str(aout),encoding='utf-8')
# Runtime updates are delivered separately from add-in packages.
runtime=axis/'app-runtime';runtime.mkdir(exist_ok=False)
shutil.copytree(qa/'engine/Host',runtime/'Host')
shutil.copytree(axis/'runtime-extras/MCP',runtime/'MCP')
shutil.copytree(repo/'artifacts/launcher',runtime/'Launcher')
loader=Path(subprocess.check_output(['powershell','-NoProfile','-Command',"(Get-ItemProperty 'HKCU:/SOFTWARE/Autodesk/AutoCAD/R24.0/ACAD-4101:409/Applications/AICadToolManager').LOADER"],text=True).strip())
shutil.copytree(loader.parent,runtime/'AutoCAD')
shutil.copytree(repo/'artifacts/p0-p6/Host/HashCompat',runtime/'Host/HashCompat')
shutil.copy2(repo/'artifacts/p0-p6/install.json',runtime/'install.json')
ff=entries(runtime);fingerprint=hashlib.sha256(''.join(f"{f['name']}\t{f['hash']}\n" for f in ff).encode()).hexdigest()
out=warehouse/'Tools/dotnet/_Development/CAD.Manager/versions/0.9.0'/('build-'+fingerprint[:12]+'-resource-runtime')
out.mkdir(parents=True,exist_ok=False);shutil.copytree(runtime,out/'runtime')
(out/'source.patch').write_bytes(patch)
(out/'runtime-manifest.json').write_text(json.dumps({'sourceCommit':commit,'sourcePatchHash':sha(out/'source.patch'),'files':[{'path':f['name'],'sha256':f['hash']} for f in ff]},indent=2),encoding='utf-8')
(out/'NOTES.md').write_text('App runtime for archived PDF workflow; Manager core remains 0.9.0. Source and changes in source.patch. Use repository Install-CadRibbonManager.ps1 -ReleasePath runtime after closing AutoCAD. Machine-specific configuration is preserved by the installer. QA and Axis must be installed through Manager separately.\n',encoding='utf-8')
(repo/'.tools/resource-runtime-release.txt').write_text(str(out/'runtime'),encoding='utf-8')
print('Runtime:',out/'runtime')
