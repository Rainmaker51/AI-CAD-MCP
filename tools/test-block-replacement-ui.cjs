const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const listeners={},calls=[];let html='',data={targetId:'NEW',parameterMap:'{"Width":"W"}',offsetX:'0',offsetY:'0',rotation:'0'};
const confirm={checked:false,disabled:false},apply={disabled:true};
const preview={fingerprint:'approved-preview',errors:[],references:[{owner:'SKILL',kind:'Recipe.AllowedBlocks'}],oldBlocks:[{Id:'OLD'}],target:{Id:'NEW'},skills:[{Id:'SKILL',Name:'Sample',Version:'1.0.1'}],templateVersion:'1.0.1',workflow:[]};
const context=vm.createContext({console,JSON,Object,Array,Number,FormData:class{get(key){return data[key];}},
 document:{addEventListener:(type,fn)=>listeners[type]=fn,querySelectorAll:()=>[],querySelector:()=>apply,getElementById:()=>confirm},
 librarySync:{review:'review',choices:{},busy:false},blockLibrary:{},esc:v=>String(v??'').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;'),
 syncDialog:v=>html=v,syncRender:()=>{},refresh:async()=>{},syncScan:async()=>{},toast:()=>{},
 api:async(path,payload)=>{calls.push({path,payload:JSON.parse(JSON.stringify(payload))});if(path.endsWith('options'))return{source:{Name:'<unsafe>',Id:'OLD'},targets:[{id:'NEW',name:'New window',role:'window',reviewed:true}]};if(path.endsWith('preview'))return structuredClone(preview);if(path.endsWith('apply'))return{skills:1};throw Error(path);}});
vm.runInContext(fs.readFileSync('src/Desktop.Host/wwwroot/block-replacement.js','utf8'),context);
async function submit(){await listeners.submit({target:{id:'block-replacement-form'},preventDefault(){}});}
(async()=>{
 await vm.runInContext('syncReplacementOpen("OLD")',context);
 assert(html.includes('&lt;unsafe&gt;')&&!html.includes('<unsafe>'));
 assert(!html.includes('replacement-confirm'),'Approval must not precede preview');
 await submit();assert(html.includes('Bảng thay đổi để duyệt')&&html.includes('1.0.1'));
 assert(!calls.some(c=>c.path.endsWith('/apply')),'Preview wrote data');
 listeners.input({target:{closest:()=>true}});assert.equal(vm.runInContext('blockReplacement.preview',context),null);assert(apply.disabled);
 await submit();confirm.checked=true;listeners.change({target:{id:'replacement-confirm',checked:true}});assert(!apply.disabled);
 await listeners.click({target:{closest:()=>({dataset:{replacement:'apply'},disabled:false})}});
 const sent=calls.find(c=>c.path.endsWith('/apply'));assert(sent&&sent.payload.confirmed&&sent.payload.fingerprint==='approved-preview');
 assert.equal(vm.runInContext('blockReplacement',context),null);
 console.log('PASS: safe rendering, preview before approval, edited preview invalidation and explicit apply payload');
})().catch(e=>{console.error(e);process.exitCode=1;});
