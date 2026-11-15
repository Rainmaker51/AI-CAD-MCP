"""Run isolated AutoCAD 2021 regression tests; never open a user drawing."""
from pathlib import Path
import subprocess, os, uuid, json
repo=Path(__file__).resolve().parents[1]
run=repo/'tests/fixtures'/('drafting-'+uuid.uuid4().hex)
run.mkdir(parents=True)
(run/'.aicad-test-root').write_text('AI CAD isolated native test root')
(run/'analysis.json').write_bytes((repo/'tests/results/dynamic-library-live-analysis.json').read_bytes())
(run/'seed.dwg').write_bytes((repo/'content/launch/blank-mm.dwg').read_bytes())
dll=(repo/'storage/04-builds/artifacts/p0-p6-warehouse/AutoCAD/CAD.Bridge.AutoCAD.dll').as_posix()
script=run/'test.scr'
script.write_text('_NETLOAD\n"'+dll+'"\nAICADDRAFTINGTEST\nAICADNATIVETEST\nAICADPHASETEST\nAICADREVIEWFLOWTEST\n_QUIT\n_Y\n',encoding='ascii')
(repo/'tests/results/drafting-native-path.txt').write_text(str(run))
env={**os.environ,'AICAD_TEST_ROOT':str(run)}
result=subprocess.run(['C:/Program Files/Autodesk/AutoCAD 2021/accoreconsole.exe','/i',str(run/'seed.dwg'),'/s',str(script),'/isolate','AICadDrafting',str(run/'profile')],env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=120,creationflags=subprocess.CREATE_NO_WINDOW)
(run/'console.log').write_bytes(result.stdout)
failed=0
for name in ['drafting-native-results.json','native-results.json','phase-native-results.json','review-flow-results.json']:
 p=run/name
 if not p.exists():raise RuntimeError('Missing native results: '+str(p))
 r=json.loads(p.read_text(encoding='utf-8-sig'));failed+=r['failed'];print(name,r['passed'],r['total'],r['failed'])
 for case in r['cases']:
  if not case['passed']:print(json.dumps(case,ensure_ascii=False))
raise SystemExit(1 if failed else 0)
