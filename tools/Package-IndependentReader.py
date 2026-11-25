"""Seal the detached Reader and Manager without changing installed selections."""
from pathlib import Path
import datetime, hashlib, json, shutil, subprocess
repo=Path(__file__).resolve().parent.parent
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def pointer(name):return Path((repo/'.tools'/name).read_text(encoding='utf-8-sig').strip())
warehouse=Path(read(repo/'warehouse.local.json')['path'])
stages={'Manager':pointer('cad-manager-stage.txt'),'Reader':pointer('separate-reader-stage.txt')}
axis=pointer('axis-reader-test-run.txt');ribbon=next((pointer('cad-manager-native-run.txt')/'validation').glob('ribbon-*'));qa=next((pointer('qa-native-run.txt')/'validation').glob('native-*'));unit=pointer('shared-components-test-run.txt')
assert read(axis/'axis-upgrade-results.json')['passed']
assert read(axis/'axis-upgrade-results.json')['managerWithoutReader']
assert read(ribbon/'ribbon-edit-results.json')['passed']
assert read(qa/'ribbon-manager-results.json')['passed']
assert read(unit/'results.json')['passed']
for p in (stages['Manager']/'package/AutoCAD').iterdir():assert sha(p)==sha(axis/'AutoCAD'/p.name)==sha(ribbon/'AutoCAD'/p.name)==sha(qa/'AutoCAD'/p.name)
assert sha(stages['Reader']/'package/Rainmaker.Library.Reader.dll')==sha(axis/'reader-package/Rainmaker.Library.Reader.dll')
patch=subprocess.check_output(['git','diff','--binary','HEAD'],cwd=repo)
for name in filter(None,subprocess.check_output(['git','ls-files','--others','--exclude-standard','-z'],cwd=repo).split(b'\0')):
 result=subprocess.run(['git','diff','--no-index','--binary','--','/dev/null',name.decode()],cwd=repo,capture_output=True);assert result.returncode in (0,1);patch+=result.stdout
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo).decode().strip();outputs={}
for key,component,version in [('Manager','CAD.Manager','0.10.0'),('Reader','Rainmaker.Library.Reader','1.0.1')]:
 stage=stages[key];development=warehouse/'Tools/dotnet/_Development'/component
 assert stage.resolve().is_relative_to((development/'working').resolve())
 payload=stage/('package/AutoCAD' if key=='Manager' else 'package')
 files=[{'name':p.name,'hash':sha(p),'bytes':p.stat().st_size} for p in sorted(payload.iterdir(),key=lambda p:p.name.lower()) if p.is_file()]
 assert len(files)==(7 if key=='Manager' else 2)
 fingerprint=hashlib.sha256(''.join(f"{f['name']}\t{f['hash']}\n" for f in files).encode()).hexdigest();out=development/'versions'/version/('build-'+fingerprint[:12]);assert not out.exists(),out;out.mkdir(parents=True)
 shutil.copytree(stage/'package',out/'package');(out/'source.patch').write_bytes(patch)
 for doc in ['LIBRARY-READER-INDEPENDENT.md','LIBRARY-READER-PROTOCOL.md','ADDIN-MANAGER-STANDARD.md','LIBRARY-CONSUMER-STANDARD.md']:shutil.copy2(repo/'docs'/doc,out/doc)
 shutil.copy2(repo/'tools'/('Update-CadManagerRibbon.ps1' if key=='Manager' else 'Install-LibraryReader.ps1'),out/('CAP_NHAT_MAY_NAY.ps1' if key=='Manager' else 'Install-LibraryReader.ps1'))
 validation=out/'validation';validation.mkdir()
 for source,name in [(axis/'axis-upgrade-results.json','existing-axis.json'),(unit/'results.json','component-store.json'),(ribbon/'ribbon-edit-results.json','ribbon.json'),(qa/'ribbon-manager-results.json','qa-compat.json')]:shutil.copy2(source,validation/name)
 if key=='Manager':shutil.copy2(axis/'shared-components.png',validation/'shared-components.png')
 manifest={'component':component,'version':version,'fingerprint':fingerprint,'readerDeployment':'separate','readerProtocol':1,'files':files,'sourceCommit':commit,'sourcePatchHash':sha(out/'source.patch'),'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'selectedAtSeal':False,'validation':{p.name:sha(p) for p in validation.iterdir()},'guiMouseAfterInstall':False,'versionRetainedByUserRequest':key=='Manager'}
 (out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
 (out/'NOTES.md').write_text(f'# {component} {version} — independent Reader\n\nSource {commit} + source.patch. Build {fingerprint}.\nManager version 0.10.0 is retained at the user request; distinguish the separate Reader build by hash and readerDeployment, not version alone. Manager contains no Reader DLL. Reader is installed manually in Shared components, without a Ribbon or shared tool-catalog entry. Existing Axis 1.9.0 binary and QA native compatibility passed. See LIBRARY-READER-INDEPENDENT.md. Not installed at seal time; GUI interaction after deployment remains manual.\n',encoding='utf-8')
 outputs[key]=str(out)
(repo/'.tools/independent-reader-releases.json').write_text(json.dumps(outputs,indent=2),encoding='utf-8');print(json.dumps(outputs,indent=2))
