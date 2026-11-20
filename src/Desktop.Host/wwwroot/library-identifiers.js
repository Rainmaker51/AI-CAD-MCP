let resourceIdentifiers=null,resourceIdentifiersLoading=false,resourceIdentifiersError='',resourceIdentifiersStamp='';
function resourceIdentifierStamp(){return JSON.stringify([state.templates.map(t=>[t.Id,t.Version]),typeof hatchCatalog==='undefined'?null:hatchCatalog?.version,typeof styleCatalogs==='undefined'?null:[styleCatalogs.texts?.version,styleCatalogs.dims?.version]]);}
function ensureResourceIdentifiers(){
 const stamp=resourceIdentifierStamp();
 if(!resourceIdentifiersLoading&&!resourceIdentifiersError&&(!resourceIdentifiers||stamp!==resourceIdentifiersStamp))queueMicrotask(loadResourceIdentifiers);
}
async function loadResourceIdentifiers(){
 if(resourceIdentifiersLoading)return;
 resourceIdentifiersLoading=true;resourceIdentifiersError='';const stamp=resourceIdentifierStamp();
 try{resourceIdentifiers=await api('library-identifiers',{});resourceIdentifiersStamp=stamp;}
 catch(e){resourceIdentifiersError=e.message;toast('Chưa đọc được mã tài nguyên: '+e.message);}
 finally{resourceIdentifiersLoading=false;if(page==='template')render();}
}
function resourceIdentifier(kind,id,sourceId='ISO_QUANGANH'){
 const item=resourceIdentifiers?.items.find(r=>r.Kind===kind&&r.ResourceId===id&&r.SourceId===sourceId);
 return item?.Code?`<span class="badge resource-code" title="ID ổn định: ${esc(item.Id)}">${esc(item.Code)}</span>`:`<small class="hint">${resourceIdentifiersError?'Chưa đọc được mã':resourceIdentifiersLoading?'Đang đọc mã…':'Mã chờ đồng bộ'}</small>`;
}
function resourceIdentifierCode(kind,id,sourceId='ISO_QUANGANH'){return resourceIdentifiers?.items.find(r=>r.Kind===kind&&r.ResourceId===id&&r.SourceId===sourceId)?.Code||'';}
function resourceIdentifierDetail(kind,id,sourceId='ISO_QUANGANH'){
 const item=resourceIdentifiers?.items.find(r=>r.Kind===kind&&r.ResourceId===id&&r.SourceId===sourceId);
 return `<section class="resource-identity"><h3>Định danh tài nguyên</h3>${item?.Code?`<dl class="block-specs">${blockInfo('Mã tài nguyên',item.Code)}${blockInfo('ID ổn định',item.Id)}${blockInfo('Nguồn thư viện',item.SourceId)}</dl><button class="button" data-resource-copy="${esc(item.Code)}">Sao chép mã</button><p class="hint">Mã giữ nguyên khi đổi tên hoặc chuyển nhóm. ID gắn với đối tượng thư viện đã đồng bộ.</p>`:`<p class="hint">${item?.Status==='AMBIGUOUS'?'Có nhiều đối tượng khớp; cần đối chiếu đồng bộ trước khi xác định mã.':'Lưu DWG và đồng bộ tài nguyên để xác nhận ID. Không cấp mã dựa riêng vào tên.'}</p>`}${resourceIdentifiersError?`<p class="notice amber">${esc(resourceIdentifiersError)}</p>`:''}<button class="button" data-resource-refresh>Đọc lại mã</button></section>`;
}
document.addEventListener('click',async e=>{
 const copy=e.target.closest('[data-resource-copy]');if(copy){try{await navigator.clipboard.writeText(copy.dataset.resourceCopy);toast('Đã sao chép '+copy.dataset.resourceCopy);}catch{toast('Không sao chép được; bạn có thể chọn trực tiếp mã trong hồ sơ.');}}
 if(e.target.closest('[data-resource-refresh]')){await loadResourceIdentifiers();$('#library-dialog')?.close();toast('Đã đọc lại thông tin; mở hồ sơ tài nguyên để xem mã.');}
});
