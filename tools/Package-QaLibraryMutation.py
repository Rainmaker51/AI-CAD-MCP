"""Seal standalone QA 1.8, with local native test evidence and no runtime copies."""
from pathlib import Path
import datetime,hashlib,json,shutil,subprocess
import xml.etree.ElementTree as ET

repo=Path(__file__).resolve().parent.parent
version=ET.parse(repo/'src/Rainmaker.Library/Rainmaker.Library.csproj').findtext('.//Version')
stage=Path((repo/'.tools/qa-library-stage.txt').read_text(encoding='utf-8-sig').strip())
run=Path((repo/'.tools/qa-native-run.txt').read_text(encoding='utf-8-sig').strip())
warehouse=Path(json.loads((repo/'warehouse.local.json').read_text(encoding='utf-8-sig'))['path'])
development=warehouse/'Tools/dotnet/_Development/Rainmaker.Library'
assert stage.resolve().is_relative_to((development/'working').resolve())
assert run.resolve().is_relative_to((repo/'.tools/test-runs').resolve())
marker=json.loads((run/'test-run.json').read_text(encoding='utf-8-sig'))
assert Path(marker['package']).resolve()==(stage/'package').resolve()
native=sorted((run/'validation').glob('native-*'))[-1]
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
evidence={'manager.json':native/'ribbon-manager-results.json'}
for name in ['result.json','live-insert.json','hatch-view.json','standalone.json','mutations.json','save-source.json']:
    evidence['native-data.json' if name=='result.json' else name]=native/'library-data'/name
if tuple(map(int,version.split('.'))) >= (1,8,2):evidence['layer-rename.json']=native/'library-data/layer-rename.json'
if tuple(map(int,version.split('.'))) >= (1,9,0):
    for name in ['cad-first.json','cad-publication.json']:evidence[name]=native/'library-data'/name
if tuple(map(int,version.split('.'))) >= (1,10,0):evidence['frame-scope.json']=native/'library-data/frame-scope.json'
if tuple(map(int,version.split('.'))) >= (1,11,0):
    for name in ['groups.json','group-publication.json','review-filters.json']:evidence[name]=native/'library-data'/name
for path in evidence.values():assert read(path)['passed'],path
files=[{'name':p.name,'hash':sha(p),'bytes':p.stat().st_size} for p in sorted((stage/'package').iterdir(),key=lambda p:p.name.lower()) if p.is_file()]
assert len(files)==7
for entry in files:assert sha(native/'library'/entry['name'])==entry['hash'],entry['name']
# Evidence must have exercised the exact DLLs that will be delivered.
tested=next((native/'dotnet/_Installed').rglob('Rainmaker.Library.dll'),None)
if tested is None:tested=next(native.rglob('Rainmaker.Library.dll'))
assert sha(tested)==sha(stage/'package/Rainmaker.Library.dll')
fingerprint=hashlib.sha256(''.join(f"{f['name']}\t{f['hash']}\n" for f in files).encode()).hexdigest()
out=development/'versions'/version/('build-'+fingerprint[:12])
if out.exists():raise SystemExit('Immutable build exists: '+str(out))
out.mkdir(parents=True);shutil.copytree(stage/'package',out/'package')
guide='QA-LIBRARY-1.11.1.md' if tuple(map(int,version.split('.'))) >= (1,11,1) else 'QA-LIBRARY-1.11.md' if tuple(map(int,version.split('.'))) >= (1,11,0) else 'QA-LIBRARY-1.10.md' if tuple(map(int,version.split('.'))) >= (1,10,0) else 'QA-LIBRARY-1.9.md' if tuple(map(int,version.split('.'))) >= (1,9,0) else 'QA-LIBRARY-1.8.md'
shutil.copy2(repo/'docs'/guide,out/'HUONG_DAN.md');shutil.copy2(repo/'tools/Install-QaLibrary.ps1',out/'Install-QaLibrary.ps1')
validation=out/'validation';validation.mkdir()
for name,path in evidence.items():shutil.copy2(path,validation/name)
business=repo/'.tools/qa-business-results.log'
assert 'PASS: deletion dependency preflight' in business.read_text(encoding='utf-8-sig')
shutil.copy2(business,validation/'business-tests.log')
shutil.copy2(native/'library-data/browser.png',validation/'browser.png')
if tuple(map(int,version.split('.'))) >= (1,11,0):shutil.copy2(native/'library-data/review-filters.png',validation/'review-filters.png')
# Preserve the earlier regression failure that motivated explicit un-erase recovery.
failure=sorted(p for p in (repo/'.tools/test-runs').glob('qa-native-*/validation/native-*/ribbon-manager-results.json') if 'rollback' in p.read_text(encoding='utf-8-sig').lower() and not read(p)['passed'])
if failure:shutil.copy2(failure[-1],validation/'rollback-before-fix.json')
rename_failure=sorted(p for p in (repo/'.tools/test-runs').glob('qa-native-*/validation/native-*/ribbon-manager-results.json') if 'caption stayed old' in p.read_text(encoding='utf-8-sig').lower() and not read(p)['passed'])
if rename_failure:shutil.copy2(rename_failure[-1],validation/'layer-rename-before-fix.json')
patch=subprocess.check_output(['git','diff','--binary','HEAD'],cwd=repo)
for name in filter(None,subprocess.check_output(['git','ls-files','--others','--exclude-standard','-z'],cwd=repo).split(b'\0')):
    r=subprocess.run(['git','diff','--no-index','--binary','--','/dev/null',name.decode()],cwd=repo,capture_output=True)
    assert r.returncode in (0,1);patch+=r.stdout
(out/'source.patch').write_bytes(patch)
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo).decode().strip()
manifest={'component':'Rainmaker.Library','version':version,'fingerprint':fingerprint,'files':files,'requiresHost':False,'sourceCommit':commit,'sourcePatchHash':sha(out/'source.patch'),'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'installedAtSeal':False,'validation':{name:sha(validation/name) for name in evidence},'guiMouseAfterInstall':False}
(out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'NOTES.md').write_text('QA '+version+'\n\nSource '+commit+' + source.patch.\nBuild '+fingerprint+'\n\nSee HUONG_DAN.md for changes and recovery semantics. Native Core 2021 + Manager 0.9 regression, hatch merge/boundary/ID checks, deletion and explicit recovery after purge failure passed. Runtime GUI mouse testing after installation is pending. No real library DWG was edited by tests. Not selected in catalog at seal time.\n',encoding='utf-8')
if 'layer-rename.json' in evidence:
    with (out/'NOTES.md').open('a',encoding='utf-8') as note:note.write('\nLayer rename regression: old CAD caption reproduced before fix; fixed native name/caption/appearance, stable ID/code, repeated rename, custom alias, position, erased-sample guard and saved/reopened DWG validated.\n')
if 'cad-first.json' in evidence:
    with (out/'NOTES.md').open('a',encoding='utf-8') as note:note.write('\nCAD-first workflow: native editing controls removed; selected scan review, five-kind presentation, explicit ID binding, saved-byte recovery and actual window publication/replay validated. Earlier 1.8 native mutation tests are retained as regressions, not user-facing editing commands.\n')
(repo/'.tools/qa-library-release.txt').write_text(str(out),encoding='utf-8')
print(out);print('DLL SHA256: '+sha(out/'package/Rainmaker.Library.dll'))
