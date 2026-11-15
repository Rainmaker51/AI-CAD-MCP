function workspaceView(){
 const d=doc(),a=state.account||{},t=task(),s=state.skills.find(s=>s.Id===t?.SkillId),session=d?.SessionId||state.sessions[0]?.SessionId||'',policy=state.previewPolicy||'ALWAYS';
 return heading('Không gian làm việc','Chọn bản vẽ · trao đổi với AI · kiểm tra và triển khai trong AutoCAD.')+`
 <section class="panel workspace-controls" aria-label="Phiên, bản vẽ và nhiệm vụ">
  <label for="workspace-session">PHIÊN AUTOCAD<select id="workspace-session" ${chatBusy?'disabled':''}><option value="">Chọn phiên</option>${state.sessions.map(x=>`<option value="${esc(x.SessionId)}" ${x.SessionId===session?'selected':''}>AutoCAD ${esc(x.CadVersion)} · ${esc(x.SessionId.slice(0,8))}</option>`).join('')}</select></label>
  <label for="chat-document">BẢN VẼ ĐÍCH<select id="chat-document" ${chatBusy?'disabled':''}><option value="">Chọn DWG đang mở</option>${state.documents.filter(x=>x.SessionId===session).map(x=>`<option value="${esc(x.DocumentId)}" ${x.DocumentId===target?'selected':''}>${esc(x.Name)}${x.IsActive?' · Đang mở':''}</option>`).join('')}</select></label>
  <label for="chat-task">KHUNG NHIỆM VỤ<select id="chat-task" ${chatBusy?'disabled':''}><option value="">Chọn khung nhiệm vụ</option>${tasks.map(x=>`<option value="${esc(x.TaskId)}" ${x.TaskId===taskId?'selected':''}>${esc(x.TaskId)} · ${esc(x.SkillId)}</option>`).join('')}</select></label>
  ${button('Quét khung','chat-scan',false,!d||chatBusy)}
 </section>
 <div class="unified-grid">
  <section class="panel workspace-chat" aria-labelledby="workspace-chat-title">
   <div class="panel-header"><h2 id="workspace-chat-title">Chat & giao việc</h2>${button('Chat mới','new-chat',false,chatBusy)}</div>
   <div class="chat-target">${badge(a.connected?'AI đã kết nối':'Chưa đăng nhập AI',a.connected?'green':'amber')}<span>${t?esc(t.TaskId)+' · '+esc(t.SkillId):'Chưa chọn khung đích'}</span></div>
   ${t&&s?.Builder!=='custom'?`<div class="workspace-chat-help">Khung này dùng Skill ${esc(t.SkillId)}. Để vẽ tự do, chọn khung CAD_TOOLS; tạo khung bằng AICADTASK trong AutoCAD nếu cần.</div>`:''}
   <div class="chat-log" role="log" aria-live="polite">${chatMessages()}${chatBusy?'<p class="chat-thinking" role="status">AI đang lập kế hoạch…</p>':''}</div>
   <form id="chat-form" class="chat-composer"><label for="chat-prompt" class="field-label">YÊU CẦU TRIỂN KHAI</label><textarea id="chat-prompt" rows="3" maxlength="10000" placeholder="Mô tả công việc cho khung đang chọn…" ${chatBusy?'disabled':''}>${esc(chatDraft)}</textarea><div class="chat-composer-foot"><small>${chatData.ModelCalls} lượt AI · Duyệt không gọi AI lại</small><div class="actions">${chatBusy?button('Dừng','stop-chat'):''}<button class="button primary" type="submit" ${chatBusy||!a.connected||!t?'disabled':''}>Gửi yêu cầu</button></div></div>${!a.connected?button('Kết nối tài khoản AI','open-account'):''}</form>
  </section>
  <section class="deployment-pane" aria-labelledby="deployment-title">
   <div class="deployment-heading"><div><span class="eyebrow">BẢN VẼ ĐANG TRIỂN KHAI</span><h2 id="deployment-title">${d?esc(d.Name):'Chưa chọn bản vẽ'}</h2></div><div class="actions">${d?badge(d.IsActive?'Đang mở tại AutoCAD':'Chưa kích hoạt',d.IsActive?'green':'amber'):''}${badge('Preview · '+policy,'blue')}</div></div>
   <div class="deployment-metrics"><span><strong>${tasks.length}</strong> khung nhiệm vụ</span><span><strong>${plans().filter(r=>r.Status==='PREVIEW').length}</strong> chờ duyệt</span><span><strong>${plans().filter(r=>r.Status==='DONE').length}</strong> hoàn tất</span></div>
   ${d?`<div class="deployment-grid"><div class="deployment-main"><section class="panel"><div class="panel-header"><h2>Bản vẽ & phạm vi</h2><small>Model / mm</small></div>${drawing()}</section><section class="panel task-list"><div class="panel-header"><h2>Nhiệm vụ trong DWG</h2><small>${tasks.length} nhiệm vụ</small></div>${tasks.map(x=>{const r=planForTask(x.TaskId);return `<button class="task-row ${x.TaskId===taskId?'selected':''}" data-task="${esc(x.TaskId)}"><span class="task-symbol">◇</span><div><strong>${esc(x.TaskId)} · ${esc(x.SkillId)}</strong><small>${esc(x.Dependencies.length?'Chờ '+x.Dependencies.join(', '):'Độc lập')}</small></div>${badge(x.Warnings.length?'Cần kiểm tra':labels[r?.Status]||x.Status,x.Warnings.length?'amber':colors(r?.Status))}</button>`}).join('')||'<p class="empty">Chưa có khung nhiệm vụ. Gõ AICADTASK trong AutoCAD để tạo, rồi quét lại.</p>'}</section></div>${inspector()}</div>`:setup()}
  </section>
 </div>`;
}
document.addEventListener('change',async e=>{if(e.target.id!=='workspace-session')return;try{const d=state.documents.find(d=>d.SessionId===e.target.value&&d.IsActive)||state.documents.find(d=>d.SessionId===e.target.value);target=d?.DocumentId||'';tasks=[];taskId='';if(d)await scan();else render();}catch(err){toast(err.message)}});
document.addEventListener('click',async e=>{const b=e.target.closest('[data-work-recreate]');if(!b)return;b.disabled=true;try{await api('plan/recreate',{planId:b.dataset.workRecreate});await refresh();toast('Đã tạo lại hình học, không gọi AI.');}catch(err){toast(err.message)}finally{if(b.isConnected)b.disabled=false;}});
