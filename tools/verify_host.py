"""Local Host/API and official MCP SDK smoke checks; no CAD mutation."""
import json, os, pathlib, subprocess, threading, queue, urllib.request, urllib.error
root=pathlib.Path(__file__).resolve().parent.parent
build_root=root/'storage/04-builds/artifacts' if (root/'storage/04-builds/artifacts').is_dir() else root/'artifacts'
info=json.loads((root/'data/host.json').read_text(encoding='utf-8-sig'))
results=[]
def test(name,fn):
 try: fn();results.append({'name':name,'passed':True});print('PASS',name)
 except Exception as e:results.append({'name':name,'passed':False,'message':str(e)});print('FAIL',name,str(e))
def request(path,body=None,token=None,origin=None):
 headers={'Authorization':'Bearer '+(token or info['token'])}
 if body is not None:headers['Content-Type']='application/json'
 if origin:headers['Origin']=origin
 req=urllib.request.Request(info['url']+'/api/'+path,data=json.dumps(body).encode() if body is not None else None,headers=headers)
 try:
  with urllib.request.urlopen(req,timeout=15) as r:return r.status,json.load(r)
 except urllib.error.HTTPError as e:return e.code,json.loads(e.read() or '{}')
def expect(value,message):
 if not value:raise AssertionError(message)
test('Host health authenticated',lambda:expect(request('health')[1]['status']=='ok','Health failed'))
test('Missing or wrong bearer rejected',lambda:expect(request('health',token='invalid')[0]==401,'Bearer not enforced'))
test('Cross-origin request rejected',lambda:expect(request('health',origin='https://untrusted.invalid')[0]==403,'Origin not enforced'))
state=request('state')[1]
test('Host reports LIVE state',lambda:expect(state['mode']=='LIVE','Unexpected mode'))
test('Session secret never returned to UI',lambda:expect(not any('Secret' in s or 'secret' in s for s in state['sessions']),'Secret leaked'))
test('Actual central templates loaded',lambda:expect(any(t['Id']=='STARTER' for t in state['templates']),'No starter template'))
test('Actual declarative Skills loaded',lambda:expect(any(s['Id']=='INSERT_DOOR_AXIS_DEMO' for s in state['skills']),'No starter skill'))
test('Arbitrary CAD command API rejected',lambda:expect(request('query',{'method':'execute_lisp'})[1].get('code')=='UNSUPPORTED_METHOD','Arbitrary dispatch accepted'))
test('Plan traversal rejected',lambda:expect(request('cancel',{'planId':'../secret'})[1].get('code')=='INVALID_ID','Traversal accepted'))
env=dict(os.environ);env['DOTNET_ROOT']=str(root/'.tools/dotnet')
process=subprocess.Popen([str(build_root/'app/MCP/AICad.Mcp.exe')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8',env=env)
incoming=queue.Queue()
def read():
 for line in process.stdout:
  try:incoming.put(json.loads(line))
  except json.JSONDecodeError:incoming.put({'invalidStdout':line[:100]})
threading.Thread(target=read,daemon=True).start()
def rpc(message):process.stdin.write(json.dumps(message)+'\n');process.stdin.flush()
def receive(id):
 for _ in range(20):
  response=incoming.get(timeout=15)
  if 'invalidStdout' in response:raise AssertionError('Non-protocol stdout')
  if response.get('id')==id:return response
 raise AssertionError('Missing RPC result')
try:
 rpc({'jsonrpc':'2.0','id':1,'method':'initialize','params':{'protocolVersion':'2025-03-26','capabilities':{},'clientInfo':{'name':'AICad.Tests','version':'1.0'}}})
 init=receive(1)
 test('Official MCP SDK initialize over stdio',lambda:expect('result' in init,'MCP initialize failed'))
 rpc({'jsonrpc':'2.0','method':'notifications/initialized'})
 rpc({'jsonrpc':'2.0','id':2,'method':'tools/list','params':{}})
 tools=receive(2).get('result',{}).get('tools',[]);names=[t['name'] for t in tools]
 test('MCP discovers 13 typed tools',lambda:expect(len(names)==13,'Tools: '+str(names)))
 test('MCP has no remote approve/apply tool',lambda:expect(not any('apply' in n or 'approve' in n for n in names),'Remote approval exported'))
 rpc({'jsonrpc':'2.0','id':3,'method':'tools/call','params':{'name':'list_templates','arguments':{}}})
 reply=receive(3).get('result',{})
 test('MCP reads actual shared template through Host',lambda:expect(not reply.get('isError') and 'STARTER' in str(reply),'MCP/Host query failed'))
finally:
 process.terminate()
 try:process.wait(timeout=5)
 except subprocess.TimeoutExpired:process.kill()
out=root/'tests/results/host.json';out.parent.mkdir(exist_ok=True,parents=True)
report={'total':len(results),'passed':sum(r['passed'] for r in results),'failed':sum(not r['passed'] for r in results),'cases':results}
out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
raise SystemExit(0 if report['failed']==0 else 1)
