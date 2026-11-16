const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const ctx={librarySourceDetails:hash=>'File nguồn:'+(hash||''),libraryManager:{},libraryGroupPath:()=>'',libraryGroupContains:()=>true,esc:s=>String(s??'').replaceAll('<','&lt;'),librarySearchText:s=>String(s).toLowerCase(),blockNumber:String,blockInfo:(l,v)=>l+':'+v,document:{addEventListener(){}},libraryDialog:(title,body,footer)=>{ctx.detail={title,body,footer};}};
vm.createContext(ctx);vm.runInContext(fs.readFileSync('src/Desktop.Host/wwwroot/hatch-management.js','utf8'),ctx);vm.runInContext(fs.readFileSync('src/Desktop.Host/wwwroot/hatch-library.js','utf8'),ctx);
vm.runInContext(`hatchCatalog={version:'1.0.0',sourceName:'source.dwg',entityCount:4,items:[{id:'a',pattern:'ANSI31',scale:10,angle:0,layer:'WALL',locations:['ROOM'],count:3,type:'PreDefined',engineSupported:true},{id:'b',pattern:'CUSTOM',scale:1,angle:1,layer:'FLOOR',locations:['LOBBY'],count:1,type:'CustomDefined',engineSupported:false}]};`,ctx);
assert(ctx.hatchContent().includes('ANSI31'));assert(ctx.hatchContent().includes('CUSTOM'));
vm.runInContext("hatchQuery='lobby'",ctx);assert(!ctx.hatchContent().includes('ANSI31'));assert(ctx.hatchContent().includes('CUSTOM'));
ctx.hatchDetail('a');assert(ctx.detail.body.includes('ANSI31'));
ctx.hatchDetail('b');assert(ctx.detail.body.includes('CUSTOM'));assert(ctx.detail.body.includes('ANSI31/SOLID'));
assert(ctx.detail.footer.includes('data-hatch-edit'));
assert(ctx.detail.footer.includes('data-hatch-delete'));
vm.runInContext(`hatchCatalog.items[0].management={Purpose:'Concrete section',DeletedUtc:null};hatchQuery='concrete';`,ctx);
assert(ctx.hatchContent().includes('ANSI31'));assert(!ctx.hatchContent().includes('CUSTOM'));
vm.runInContext(`hatchCatalog.items[0].management.DeletedUtc='2026-11-16';hatchQuery='';`,ctx);
assert(!ctx.hatchContent().includes('data-hatch-detail="a"'));
vm.runInContext(`hatchTrash=true;`,ctx);assert(ctx.hatchContent().includes('data-hatch-detail="a"'));
ctx.hatchDetail('a');assert(ctx.detail.footer.includes('data-hatch-restore'));assert(!ctx.detail.footer.includes('data-hatch-copy'));
vm.runInContext(`hatchTrash=false;hatchQuery='';hatchCatalog.items=[
{id:'one',pattern:'ANSI31',scale:10,angle:0,locations:[],management:{Purpose:'Concrete'}},
{id:'two',pattern:'ANSI31',scale:20,angle:0,locations:[],management:{Purpose:'Earth; insulation'}},
{id:'three',pattern:'ANSI31',scale:10,angle:Math.PI/4,locations:[],management:{Purpose:'Steel'}},
{id:'four',pattern:'AR-CONC',scale:10,angle:0,locations:[],management:{Purpose:'Concrete'}}];hatchPatternQuery='ansi';`,ctx);
assert.equal(ctx.filteredHatches().length,3);
vm.runInContext(`hatchPatternQuery='ANSI31';hatchScaleFilter='10';`,ctx);
assert.equal(ctx.filteredHatches().length,2);
vm.runInContext(`hatchAngleFilter='0';`,ctx);
assert.equal(ctx.filteredHatches().length,1);assert.equal(ctx.filteredHatches()[0].id,'one');
vm.runInContext(`hatchAngleFilter=String(Math.PI/4);`,ctx);
assert.equal(ctx.filteredHatches()[0].id,'three');
vm.runInContext(`hatchScaleFilter='';hatchAngleFilter='';hatchQuery='insulation';`,ctx);
assert.equal(ctx.filteredHatches()[0].id,'two');
assert(ctx.hatchVariantFilters().includes('hatch-scale-filter'));
assert(ctx.hatchContent().includes('ANSI31'));
vm.runInContext(`hatchQuery='';hatchPatternQuery='';hatchCatalog.items.push({id:'missing',pattern:'ANSI31',scale:5,angle:0,locations:[],management:{Purpose:'   '}});hatchCatalog.items.push({id:'deletedMissing',pattern:'SOLID',scale:1,angle:0,locations:[],management:{DeletedUtc:'2026-11-16'}});hatchNeedsPurpose=true;`,ctx);
assert.equal(ctx.filteredHatches().length,1);
assert.equal(ctx.filteredHatches()[0].id,'missing');
assert(ctx.hatchTree().includes('Chưa có công dụng'));
vm.runInContext(`hatchCatalog.items.find(i=>i.id==='missing').management.Purpose='New purpose';`,ctx);
assert.equal(ctx.filteredHatches().length,0);
vm.runInContext(`hatchNeedsPurpose=false;hatchSetCollapsed(true);`,ctx);
assert(ctx.hatchContent().includes('data-hatch-pattern="ANSI31" ><summary>'));
vm.runInContext(`hatchSetCollapsed(false);`,ctx);
assert(ctx.hatchContent().includes('data-hatch-pattern="ANSI31" open><summary>'));
vm.runInContext(String.raw`hatchDisplayMode='purpose';hatchCatalog.items=[
{id:'v1',pattern:'ANSI31',scale:10,angle:0,locations:[],management:{Purpose:'Concrete; Insulation\nConcrete'}},
{id:'v2',pattern:'AR-CONC',scale:20,angle:1,locations:[],management:{Purpose:'concrete'}},
{id:'v3',pattern:'ANSI31',scale:20,angle:0,locations:[],management:{Purpose:''}}];`,ctx);
const purposeGroups=ctx.hatchDisplayGroups(ctx.filteredHatches());
assert.equal(purposeGroups.length,3);
assert.equal(purposeGroups.find(g=>g.key==='concrete').items.length,2);
assert.equal(purposeGroups.find(g=>g.key==='insulation').items.length,1);
assert(ctx.hatchContent().includes('data-hatch-detail="v1"'));
assert(ctx.hatchContent().includes('data-hatch-detail="v2"'));
assert(ctx.hatchContent().includes('Chưa có công dụng'));
vm.runInContext('hatchSetCollapsed(true)',ctx);
assert(ctx.hatchContent().includes('data-hatch-purpose="concrete" ><summary>'));
vm.runInContext('hatchSetCollapsed(false)',ctx);
assert(ctx.hatchContent().includes('data-hatch-purpose="concrete" open><summary>'));
vm.runInContext(`hatchScaleFilter='10';`,ctx);
assert.equal(ctx.filteredHatches().length,1);
assert.equal(ctx.hatchDisplayGroups(ctx.filteredHatches()).length,2);
console.log('PASS: hatch catalog, source/layer search, supported and unsupported detail states');
