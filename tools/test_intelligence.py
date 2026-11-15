import hashlib,json,pathlib,subprocess,sys,concurrent.futures
ROOT=pathlib.Path(__file__).resolve().parent.parent;folder=ROOT/'tests/fixtures/intelligence';corpus=json.loads((folder/'corpus.json').read_text())
def run(kind):
 fixture=corpus[kind];source=folder/fixture['file'];req={'sourceId':('1' if kind=='vector' else '2')*32,'path':str(source),'hash':hashlib.sha256(source.read_bytes()).hexdigest(),'calibration':fixture['calibration'],'page':0}
 request=folder/(kind+'.request.json');request.write_text(json.dumps(req),encoding='utf-8');output=folder/(kind+'.analysis.json')
 p=subprocess.run([sys.executable,str(ROOT/'src/Drawing.Intelligence/worker.py'),'--request',str(request),'--output',str(output)],capture_output=True,text=True,encoding='utf-8',timeout=150)
 if p.returncode:raise RuntimeError(kind+': '+p.stderr[-2500:])
 model=json.loads(output.read_text(encoding='utf-8'));assert model['elements'];assert model['texts'];assert model['metrics']['referenceResidualMm']<.001
 if kind=='vector':assert sum(e['kind']=='wall' for e in model['elements'])==7
 else:assert model['uncertainties'];assert any('6000' in t['text'].replace(' ','') for t in model['texts'])
 print(kind,model['metrics']);return {'kind':kind,'passed':True,'metrics':model['metrics']}
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:results=list(executor.map(run,['vector','scan']))
(ROOT/'tests/results/intelligence.json').write_text(json.dumps({'cases':results,'synthetic':True},indent=2),encoding='utf-8')
