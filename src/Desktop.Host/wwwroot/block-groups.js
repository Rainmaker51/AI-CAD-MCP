libraryManager.blocks={query:'',group:'',data:null,loading:false,error:''};
let blockGrouping=null,blockGroupSelected='',blockGroupChecked=new Set();
async function loadBlockGrouping(id){
 const data=await api('block-management/'+encodeURIComponent(id));
 if(id!==blockTemplate)return;
 blockGrouping=data;libraryManager.blocks.data={groups:data.groups,items:[]};
 const names=new Set(data.items.map(l=>l.Name));blockGroupChecked=new Set([...blockGroupChecked].filter(n=>names.has(n)));
}
function blockGroupId(name){return blockGrouping?.items.find(l=>l.Name===name)?.GroupId||'';}
function blockGroupMatches(name){return !blockGroupSelected||(blockGroupSelected==='ungrouped'?!blockGroupId(name):libraryGroupContains('blocks',blockGroupSelected,blockGroupId(name)));}
function blockTreeNav(){
 const groups=libraryManager.blocks.data?.groups||[],items=blockGrouping?.templateId===blockLibrary?.Id?blockGrouping.items:[];
 const branch=(parent='',depth=0)=>depth>15?'':groups.filter(g=>(g.ParentId||'')===parent).map(g=>{
  const count=items.filter(l=>libraryGroupContains('blocks',g.Id,l.GroupId)).length;
  const button=`<button class="library-tree-item ${blockGroupSelected===g.Id?'active':''}" data-block-folder="${esc(g.Id)}"><span>${esc(g.Name)}</span><small>${count}</small></button>`;
  return groups.some(c=>c.ParentId===g.Id)?`<details open class="library-tree-branch"><summary>${button}</summary><div class="library-tree-children">${branch(g.Id,depth+1)}</div></details>`:button;
 }).join('');
 return `<nav class="library-folder-tree" aria-label="Cây nhóm block"><div class="library-tree-head"><h2>Nhóm block</h2><button class="button" data-manage-action="groups" data-kind="blocks">Quản lý nhóm block</button></div><button class="library-tree-item ${!blockGroupSelected?'active':''}" data-block-folder=""><span>Tất cả block</span><small>${items.length}</small></button>${branch()}<button class="library-tree-item ${blockGroupSelected==='ungrouped'?'active':''}" data-block-folder="ungrouped"><span>Chưa phân nhóm</span><small>${items.filter(l=>!l.GroupId).length}</small></button><p class="hint">Cây nhóm block riêng biệt với nhóm layer. Chọn nhóm cha để xem cả các nhóm con.</p></nav>`;
}
function blockAssignDialog(names){
 if(!names.length){toast('Chọn ít nhất một block.');return;}
 const current=names.length===1?blockGroupId(names[0]):'';
 libraryDialog('Chuyển nhóm · '+names.length+' block',`<form id="block-group-assign"><p class="description">${names.map(esc).join(', ')}</p><label>Nhóm block<select id="block-assign-group"><option value="">Chưa phân nhóm</option>${libraryTreeOptions('blocks',current)}</select></label><p class="hint">Chỉ thay đổi nhóm quản lý. Tên, màu, kiểu nét và các thuộc tính CAD giữ nguyên.</p><input type="hidden" id="block-assign-names" value="${esc(JSON.stringify(names))}"><div class="library-form-actions"><button class="button primary" type="submit">Lưu nhóm block</button></div></form>`);
}
document.addEventListener('click',e=>{
 const folder=e.target.closest('[data-block-folder]'),move=e.target.closest('[data-block-move]'),bulk=e.target.closest('[data-block-move-selected]');
 if(folder){e.preventDefault();blockGroupSelected=folder.dataset.blockFolder;blockGroupChecked.clear();render();}
 if(move)blockAssignDialog([move.dataset.blockMove]);
 if(bulk)blockAssignDialog([...blockGroupChecked]);
});
document.addEventListener('change',e=>{
 if(e.target.matches('[data-block-check]')){if(e.target.checked)blockGroupChecked.add(e.target.dataset.blockCheck);else blockGroupChecked.delete(e.target.dataset.blockCheck);}
 if(e.target.id==='block-check-all')document.querySelectorAll('[data-block-check]').forEach(n=>{n.checked=e.target.checked;if(n.checked)blockGroupChecked.add(n.dataset.blockCheck);else blockGroupChecked.delete(n.dataset.blockCheck);});
 const button=document.querySelector('[data-block-move-selected]');if(button)button.textContent='Chuyển nhóm block đã chọn ('+blockGroupChecked.size+')';
});
document.addEventListener('submit',async e=>{
 if(e.target.id!=='block-group-assign')return;e.preventDefault();const button=e.target.querySelector('[type=submit]');button.disabled=true;
 try{const id=blockLibrary.Id;await api('block-management/assign',{templateId:id,version:blockLibrary.Version,names:JSON.parse($('#block-assign-names').value),groupId:$('#block-assign-group').value});blockGroupChecked.clear();$('#library-dialog').close();await loadBlockGrouping(id);render();toast('Đã cập nhật nhóm block.');}
 catch(error){libraryError(error);}finally{if(button.isConnected)button.disabled=false;}
});
