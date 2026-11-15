"""Read-only verification of the last committed live execution."""
import json,pathlib,urllib.request
root=pathlib.Path(__file__).resolve().parent.parent
config=json.loads((root/'data/host.json').read_text(encoding='utf-8-sig'))
def call(path,body=None):
 request=urllib.request.Request(config['url']+'/api/'+path,data=json.dumps(body).encode() if body is not None else None,headers={'Authorization':'Bearer '+config['token'],'Content-Type':'application/json'})
 with urllib.request.urlopen(request,timeout=20) as response:return json.load(response)
state=call('state')
done=next(p for p in state['plans'] if p['Status']=='DONE')
plan=done['Plan'];document=next(d for d in state['documents'] if d['SessionId']==plan['SessionId'] and d['DocumentId']==plan['DocumentId'])
handles=set(done['EntityHandles']);found=[];offset=0
while True:
 data=call('query',{'method':'get_entities','sessionId':plan['SessionId'],'documentId':plan['DocumentId'],'revision':document['Revision'],'parameters':{'offset':offset,'limit':200}})
 found.extend(e for e in data['entities'] if e['handle'] in handles)
 if data['nextOffset'] is None:break
 offset=data['nextOffset']
assert set(e['handle'] for e in found)==handles,'Committed handles missing in the current DWG'
assert len(handles)==len(plan['Operations']),'Unexpected output count'
report={'planId':plan['PlanId'],'executionId':done['ExecutionId'],'document':document['Name'],'status':done['Status'],'handlesConfirmed':len(found),'entities':found,'revisionBefore':plan['DocumentRevision'],'revisionAfter':document['Revision'],'passed':True,'scope':'Read-only verification after actual local human approval'}
(root/'tests/results/live-apply.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='entities'},ensure_ascii=False,indent=2))
