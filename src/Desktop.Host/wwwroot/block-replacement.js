let blockReplacement = null;
async function syncReplacementOpen(sourceId) {
    librarySync.busy=true;librarySync.error='';syncRender();
    try {
        const options=await api('library-sync/replacement/options',{id:librarySync.review,sourceId});
        blockReplacement={...options,request:{id:librarySync.review,sourceId,targetId:'',parameterMap:{},offsetX:0,offsetY:0,rotation:0},preview:null,error:'',busy:false};
    } catch(error) {librarySync.error=error.message;blockReplacement=null;}
    finally {librarySync.busy=false;if(blockReplacement)replacementRender();else syncRender();}
}
function replacementRender() {
    const s=blockReplacement,p=s.preview;
    const info=block=>JSON.stringify({Mã:block.Id,Nguồn:block.NativeSource,Vai_trò_và_kích_thước:block.Usage},null,2);
    syncDialog(`<header class="sync-header"><div><h2>Thay thế block ${esc(s.source.Name)}</h2><p class="hint">Cập nhật liên kết trong Skill và Template rồi loại block cũ khỏi app.</p></div><button class="button" data-replacement="back" ${s.busy?'disabled':''}>Quay lại</button></header>
      <form id="block-replacement-form" class="sync-form">
      <label>Block thay thế đã đồng bộ<select name="targetId" required ${s.busy?'disabled':''}><option value="">Chọn block thay thế</option>${s.targets.map(t=>`<option value="${esc(t.id)}" ${t.id===s.request.targetId?'selected':''}>${esc(t.name)} · ${esc(t.id)} · ${esc(t.role||'Chưa cấu hình vai trò')}${t.reviewed?'':' · Chưa duyệt'}</option>`).join('')}</select></label>
      ${!s.targets.length?'<p class="notice amber">Chưa có block thay thế đã khớp với DWG. Quay lại, nhập và đồng bộ block mới trước.</p>':''}
      <p class="hint">Giữ nguyên vị trí và hướng chèn mặc định. Độ dịch dưới đây theo trục cục bộ của từng thao tác chèn trong Workflow; Recipe dùng cấu hình block mới khi lập bản vẽ tiếp theo.</p>
      <label>Dịch X (mm)<input name="offsetX" type="number" min="-20000" max="20000" step="any" value="${esc(s.request.offsetX)}"></label>
      <label>Dịch Y (mm)<input name="offsetY" type="number" min="-20000" max="20000" step="any" value="${esc(s.request.offsetY)}"></label>
      <label>Xoay thêm (độ)<input name="rotation" type="number" min="-360" max="360" step="any" value="${esc(s.request.rotation)}"></label>
      <label>Ánh xạ tên tham số động trong Workflow (JSON: tên cũ → tên mới)<textarea name="parameterMap" rows="3" spellcheck="false">${esc(JSON.stringify(s.request.parameterMap,null,2))}</textarea></label>
      <p class="hint">Ví dụ: {"Distance1":"Rộng"}. Tham số không khai báo giữ nguyên tên; không tự đổi visibility hoặc giá trị. Vai trò, layer, ATT và quy đổi kích thước dùng cấu hình đã duyệt của block thay thế. Chỉnh cấu hình block trước nếu còn lỗi.</p>
      <button class="button primary" type="submit" ${s.busy||!s.targets.length?'disabled':''}>${s.busy?'Đang kiểm tra…':'Xem bảng thay đổi'}</button></form>
      <p class="sync-error" role="alert">${esc(s.error)}</p>
      ${p?`<section><h3>Bảng thay đổi để duyệt</h3><p>Template → ${esc(p.templateVersion)} · ${p.skills.length} Skill cập nhật</p><ul>${p.skills.map(x=>`<li>${esc(x.Name)} (${esc(x.Id)}) → ${esc(x.Version)}</li>`).join('')}</ul>
      <div class="sync-compare"><section><strong>Block cũ</strong><pre>${esc(p.oldBlocks.map(info).join('\n'))}</pre></section><section><strong>Block thay thế</strong><pre>${esc(info(p.target))}</pre></section></div>
      <details open><summary>Nơi sẽ cập nhật liên kết (${p.references.length})</summary><pre>${esc(JSON.stringify(p.references,null,2))}</pre></details>
      <details><summary>Workflow sau thay thế: vị trí, hướng, tham số và ATT</summary><pre>${esc(JSON.stringify(p.workflow,null,2))}</pre></details>
      ${p.errors.length?`<div class="notice amber" role="alert"><strong>Cần hoàn tất cấu hình</strong><ul>${p.errors.map(x=>`<li>${esc(x)}</li>`).join('')}</ul></div>`:''}
      <p class="hint">Lưu phiên bản Skill và Template mới, giữ lịch sử và bản vẽ đã triển khai. Kế hoạch chưa áp dụng có dùng block cũ phải lập lại. Thao tác này không ghi DWG.</p>
      <label class="check"><input id="replacement-confirm" type="checkbox" ${p.errors.length||s.busy?'disabled':''}> Tôi đã kiểm tra loại block, điểm chèn, hướng, layer, ATT và thông số động.</label>
      <button class="button primary" data-replacement="apply" disabled>Thay thế liên kết và loại block cũ khỏi app</button></section>`:''}`);
    if(s.busy)document.querySelectorAll('#block-replacement-form input,#block-replacement-form select,#block-replacement-form textarea').forEach(el=>el.disabled=true);
}
document.addEventListener('submit',async event=>{
    if(event.target.id!=='block-replacement-form')return;
    event.preventDefault();const s=blockReplacement;if(!s||s.busy)return;
    try {
        const data=new FormData(event.target),map=JSON.parse(data.get('parameterMap')||'{}');
        if(!map||Array.isArray(map)||typeof map!=='object'||Object.values(map).some(v=>typeof v!=='string'))throw new Error('Ánh xạ phải là JSON tên cũ → tên mới.');
        Object.assign(s.request,{targetId:data.get('targetId'),parameterMap:map,offsetX:Number(data.get('offsetX')),offsetY:Number(data.get('offsetY')),rotation:Number(data.get('rotation'))});
        s.busy=true;s.error='';s.preview=null;replacementRender();
        s.preview=await api('library-sync/replacement/preview',s.request);
    }catch(error){s.error=error.message;}finally{s.busy=false;replacementRender();}
});
document.addEventListener('input',event=>{
    if(!blockReplacement||!event.target.closest('#block-replacement-form'))return;
    // Any edits require a fresh preview and approval; keep form contents intact.
    blockReplacement.preview=null;
    const apply=document.querySelector('[data-replacement="apply"]');if(apply)apply.disabled=true;
    const confirm=document.getElementById('replacement-confirm');if(confirm){confirm.checked=false;confirm.disabled=true;}
});
document.addEventListener('change',event=>{
    if(event.target.id==='replacement-confirm')document.querySelector('[data-replacement="apply"]').disabled=!event.target.checked||!blockReplacement.preview;
});
document.addEventListener('click',async event=>{
    const button=event.target.closest('[data-replacement]');if(!button||button.disabled||!blockReplacement)return;
    const s=blockReplacement;if(s.busy)return;
    if(button.dataset.replacement==='back'){blockReplacement=null;syncRender();return;}
    if(button.dataset.replacement!=='apply'||!s.preview||!document.getElementById('replacement-confirm')?.checked)return;
    s.busy=true;s.error='';replacementRender();
    try {
        const result=await api('library-sync/replacement/apply',{request:s.request,fingerprint:s.preview.fingerprint,confirmed:true});
        blockReplacement=null;librarySync.review='';librarySync.choices={};blockLibrary=null;
        await refresh(false);toast(`Đã thay block và lưu phiên bản mới cho ${result.skills} Skill. DWG giữ nguyên.`);await syncScan();
    }catch(error){s.error=error.message;s.busy=false;replacementRender();}
});
