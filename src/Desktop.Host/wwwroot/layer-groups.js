libraryManager.layers={query:'',group:'',data:null,loading:false,error:''};
let layerGrouping=null,layerGroupSelected='',layerChecked=new Set();
async function loadLayerGrouping(id){
 const data=await api('layer-management/'+encodeURIComponent(id));
 if(id!==blockTemplate)return;
 layerGrouping=data;libraryManager.layers.data={groups:data.groups,items:[]};
 const names=new Set(data.items.map(l=>l.Name));layerChecked=new Set([...layerChecked].filter(n=>names.has(n)));
}
function layerGroupId(name){return layerGrouping?.items.find(l=>l.Name===name)?.GroupId||'';}
function layerGroupMatches(name){return !layerGroupSelected||(layerGroupSelected==='ungrouped'?!layerGroupId(name):libraryGroupContains('layers',layerGroupSelected,layerGroupId(name)));}
function layerTreeNav(){
 const groups=libraryManager.layers.data?.groups||[],items=layerGrouping?.templateId===blockLibrary?.Id?layerGrouping.items:[];
 const branch=(parent='',depth=0)=>depth>15?'':groups.filter(g=>(g.ParentId||'')===parent).map(g=>{
  const count=items.filter(l=>libraryGroupContains('layers',g.Id,l.GroupId)).length;
  const button=`<button class="library-tree-item ${layerGroupSelected===g.Id?'active':''}" data-layer-folder="${esc(g.Id)}"><span>${esc(g.Name)}</span><small>${count}</small></button>`;
  return groups.some(c=>c.ParentId===g.Id)?`<details open class="library-tree-branch"><summary>${button}</summary><div class="library-tree-children">${branch(g.Id,depth+1)}</div></details>`:button;
 }).join('');
 return `<nav class="library-folder-tree" aria-label="Cây nhóm layer"><div class="library-tree-head"><h2>Nhóm layer</h2><button class="button" data-manage-action="groups" data-kind="layers">Quản lý nhóm layer</button></div><button class="library-tree-item ${!layerGroupSelected?'active':''}" data-layer-folder=""><span>Tất cả layer</span><small>${items.length}</small></button>${branch()}<button class="library-tree-item ${layerGroupSelected==='ungrouped'?'active':''}" data-layer-folder="ungrouped"><span>Chưa phân nhóm</span><small>${items.filter(l=>!l.GroupId).length}</small></button><p class="hint">Cây nhóm layer riêng biệt với nhóm block. Chọn nhóm cha để xem cả các nhóm con.</p></nav>`;
}
function layerAssignDialog(names){
 if(!names.length){toast('Chọn ít nhất một layer.');return;}
 const current=names.length===1?layerGroupId(names[0]):'';
 libraryDialog('Chuyển nhóm · '+names.length+' layer',`<form id="layer-group-assign"><p class="description">${names.map(esc).join(', ')}</p><label>Nhóm layer<select id="layer-assign-group"><option value="">Chưa phân nhóm</option>${libraryTreeOptions('layers',current)}</select></label><p class="hint">Chỉ thay đổi nhóm quản lý. Tên, màu, kiểu nét và các thuộc tính CAD giữ nguyên.</p><input type="hidden" id="layer-assign-names" value="${esc(JSON.stringify(names))}"><div class="library-form-actions"><button class="button primary" type="submit">Lưu nhóm layer</button></div></form>`);
}
document.addEventListener('click',e=>{
 const folder=e.target.closest('[data-layer-folder]'),move=e.target.closest('[data-layer-move]'),bulk=e.target.closest('[data-layer-move-selected]');
 if(folder){e.preventDefault();layerGroupSelected=folder.dataset.layerFolder;layerChecked.clear();render();}
 if(move)layerAssignDialog([move.dataset.layerMove]);
 if(bulk)layerAssignDialog([...layerChecked]);
});
document.addEventListener('change',e=>{
 if(e.target.matches('[data-layer-check]')){if(e.target.checked)layerChecked.add(e.target.dataset.layerCheck);else layerChecked.delete(e.target.dataset.layerCheck);}
 if(e.target.id==='layer-check-all')document.querySelectorAll('[data-layer-check]').forEach(n=>{n.checked=e.target.checked;if(n.checked)layerChecked.add(n.dataset.layerCheck);else layerChecked.delete(n.dataset.layerCheck);});
 const button=document.querySelector('[data-layer-move-selected]');if(button)button.textContent='Chuyển nhóm layer đã chọn ('+layerChecked.size+')';
});
document.addEventListener('submit',async e=>{
 if(e.target.id!=='layer-group-assign')return;e.preventDefault();const button=e.target.querySelector('[type=submit]');button.disabled=true;
 try{const id=blockLibrary.Id;await api('layer-management/assign',{templateId:id,version:blockLibrary.Version,names:JSON.parse($('#layer-assign-names').value),groupId:$('#layer-assign-group').value});layerChecked.clear();$('#library-dialog').close();await loadLayerGrouping(id);render();toast('Đã cập nhật nhóm layer.');}
 catch(error){libraryError(error);}finally{if(button.isConnected)button.disabled=false;}
});
