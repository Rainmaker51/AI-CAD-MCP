let librarySync = { revision: 0, items: [], differences: [], review: '', busy: false, kind: 'layers', error: '', source: '' };
const syncKindNames = { blocks: 'Block', layers: 'Layer', hatches: 'Hatch', texts: 'Text', dims: 'Dim' };
const syncStatusNames = { MATCH: 'Đã khớp', CAD_GROUP_CHANGED: 'Đổi nhóm trong CAD', UNCHECKED: 'Chưa đối chiếu DWG', CAD_NEW: 'Mới trong CAD', APP_NEW: 'Mới trong app', CAD_CHANGED: 'CAD đã sửa', APP_CHANGED: 'App đã sửa', CONFLICT: 'Cần chọn phía sử dụng', CAD_MISSING: 'Thiếu trong CAD', APP_MISSING: 'Thiếu trong app' };
const syncFields = {
    layers: [['color','Màu ACI','number',7],['rgb','Màu RGB dạng số, để trống khi dùng ACI','number',null],['lineType','Kiểu nét','text','Continuous'],['lineWeight','Độ dày nét × 100 (9 = 0,09 mm)','number',9],['plottable','Cho phép in','checkbox',true]],
    hatches: [['pattern','Tên pattern','text','ANSI31'],['type','Loại pattern','select','PreDefined',['PreDefined','UserDefined','CustomDefined']],['scale','Scale','number',100],['angle','Góc (độ)','angle',0],['layer','Layer','text','QA_KT_Hatch'],['color','Màu ACI (256 = ByLayer)','number',256],['rgb','Màu RGB dạng số, để trống khi dùng ACI','number',null],['spacing','Khoảng cách user-defined','number',1],['doublePattern','Hatch kép','checkbox',false]],
    texts: [['font','Tên file font','text','tahoma.ttf'],['bigFont','Bigfont (nếu có)','text',''],['height','Chiều cao style (0 = tùy đối tượng)','number',0],['widthFactor','Hệ số rộng','number',1],['obliqueAngle','Góc nghiêng (độ)','angle',0],['vertical','Chữ đứng','checkbox',false]],
    dims: [['textStyle','Text Style','text','QA_Tahoma'],['textHeight','Chiều cao chữ','number',2],['arrowSize','Kích thước mũi tên','number',0.8],['scale','DIMSCALE','number',100],['linearFactor','Hệ số đo DIMLFAC','number',1],['precision','Số lẻ kích thước','number',0],['extensionOffset','Khoảng hở đường dóng','number',0.5],['extensionBeyond','Đường dóng vượt đường dim','number',0.8],['baselineSpacing','Khoảng cách dim nền','number',7],['textGap','Khoảng hở chữ','number',0.6],['rounding','Làm tròn','number',0],['unitFormat','Định dạng đơn vị (2 = Decimal)','number',2],['angularPrecision','Số lẻ góc','number',0],['zeroSuppression','DIMZIN','number',8],['annotative','Annotative','checkbox',false]],
    blocks: []
};

function syncDialog(content) {
    let dialog = document.getElementById('sync-dialog');
    if (!dialog) {
        dialog = document.createElement('dialog'); dialog.id = 'sync-dialog'; dialog.className = 'sync-dialog';
        dialog.setAttribute('aria-label','Đồng bộ thư viện CAD'); document.body.appendChild(dialog);
    }
    dialog.innerHTML = content;
    if (!dialog.open) dialog.showModal();
}

function syncRender() {
    const s = librarySync;
    const rows = s.differences.filter(d => (d.App || d.Cad).Kind === s.kind);
    const drafts = s.items.filter(i => i.Kind === s.kind);
    const current = s.review ? rows : drafts.map(item => ({Id:item.Id, App:item, Status:item.Draft?'APP_NEW':'UNCHECKED'}));
    const pending = current.filter(d => d.Status !== 'MATCH');
    const matched = current.filter(d => d.Status === 'MATCH');
    function group(key, title, items, description, expanded) {
        const open = s.groupOpen?.[s.kind + ':' + key] ?? expanded;
        return `<details class="sync-status-group" data-sync-group="${s.kind}:${key}" ${open?'open':''}><summary><strong>${title}</strong><span class="sync-group-count">${items.length}</span><span class="hint">${description}</span></summary>${items.length?`<div class="sync-table-scroll"><table class="sync-table"><thead><tr><th>Tài nguyên</th><th>Trạng thái</th><th>Thông số hai phía</th><th>Chiều cập nhật</th></tr></thead><tbody>${items.map(d=>{
            const item=d.App||d.Cad;
            return `<tr><td><strong>${esc(item.Name)}</strong><div class="hint">${esc(item.Group||'Chưa phân nhóm')}</div>${d.App?`<button class="button" data-sync-edit="${esc(d.Id)}" ${s.busy?'disabled':''}>Chỉnh bản nháp</button>`:''}</td><td>${esc(syncStatusNames[d.Status]||d.Status)}${d.GroupDifferent?`<div class="hint">${d.CadGroupChanged?'Nhóm đã đổi trực tiếp trong CAD':'Nhóm quản lý theo app'}</div>`:''}</td><td><details><summary>Xem thông số</summary><div class="sync-compare">${['App','Cad'].map(side=>`<section><strong>${side==='Cad'?'DWG nguồn':'App'}</strong><pre>${esc(d[side]?JSON.stringify({Tên:d[side].Name,Nhóm:d[side].Group,Công_dụng:d[side].Purpose,...d[side].Values},null,2):'Chưa có')}</pre></section>`).join('')}</div></details></td><td>${s.review?`<select aria-label="Chiều cập nhật ${esc(item.Name)}" data-sync-choice="${esc(d.Id)}" ${s.busy?'disabled':''}><option value="skip">Giữ để rà sau</option>${d.Cad?'<option value="cad">CAD → App</option>':''}${d.App?'<option value="app">App → CAD</option>':''}${d.App&&!d.Cad?'<option value="remove">Xóa khỏi app</option>':''}${d.App&&!d.Cad&&item.Kind==='blocks'?'<option value="replace">Thay bằng block khác…</option>':''}</select>`:'Đọc và so sánh DWG trước'}</td></tr>`;
        }).join('')}</tbody></table></div>`:`<p class="sync-group-empty hint">${s.review?(key==='pending'?'Không có mục cần đồng bộ trong tab này.':'Chưa có mục đã khớp trong tab này.'):'Nhấn Đọc và so sánh DWG để xác nhận trạng thái.'}</p>`}</details>`;
    }
    syncDialog(`<header class="sync-header"><div><h2>Đồng bộ thư viện CAD</h2><p class="hint sync-source">${esc(s.source)}</p></div><button class="button" data-sync="close" ${s.busy?'disabled':''}>Đóng</button></header>
        <div class="sync-actions"><button class="button primary" data-sync="scan" ${s.busy?'disabled':''}>${s.busy?'Đang xử lý…':'Đọc và so sánh DWG'}</button><button class="button" data-sync="new" ${s.busy?'disabled':''}>Thêm ${syncKindNames[s.kind]}</button><button class="button" data-sync="arrange" title="Chỉ bố trí mẫu hiện có trong CAD; không tự khôi phục mẫu đã xóa." ${s.busy?'disabled':''}>Cập nhật bố trí CAD · ${syncKindNames[s.kind]}</button><span class="hint" role="status">${s.review?'Chọn chiều cập nhật cho từng mục, sau đó áp dụng.':'Lưu DWG thư viện trước khi đọc. Bản nháp trong app được ghi vào CAD khi áp dụng.'}</span></div>
        <nav class="block-tabs" aria-label="Loại tài nguyên">${Object.entries(syncKindNames).map(([kind,name])=>`<button class="button ${s.kind===kind?'primary':''}" data-sync-kind="${kind}" ${s.busy?'disabled':''}>${name}</button>`).join('')}</nav>
        <p class="sync-error" role="alert">${esc(s.error)}</p>
        <p class="hint" role="status">${syncKindNames[s.kind]}: ${s.review?`${pending.length} chưa đồng bộ · ${matched.length} đã đồng bộ`:'Chưa đối chiếu DWG — đọc và so sánh để phân nhóm chính xác.'}</p>
        <div class="sync-status-groups">${group('pending',s.review?'Chưa đồng bộ':'Chưa đối chiếu',pending,s.review?'Mục mới, thay đổi, xung đột hoặc thiếu ở một phía.':'Chưa xác nhận trạng thái với DWG hiện tại.',true)}${group('matched','Đã đồng bộ',matched,'Dữ liệu tài nguyên đã khớp; nhóm quản lý theo app.',false)}</div>
        <footer class="sync-footer"><p class="hint">Áp dụng sẽ sao lưu nguồn, cập nhật tài nguyên đã chọn và trình bày mẫu theo nhóm cho cả 5 hạng mục. Chọn Xóa khỏi app để loại mục đã thiếu trong DWG; không xóa thêm đối tượng CAD. Có bản sao lưu trước khi áp dụng.</p><button class="button primary" data-sync="apply" ${!s.review||s.busy?'disabled':''}>Áp dụng và cập nhật mẫu DWG</button></footer>`);
}

document.addEventListener('toggle', event => {
    const group = event.target;
    if (!(group instanceof HTMLDetailsElement) || !group.matches('[data-sync-group]') || !group.isConnected) return;
    librarySync.groupOpen ??= {};
    librarySync.groupOpen[group.dataset.syncGroup] = group.open;
}, true);

async function syncScan() {
    librarySync.busy=true; librarySync.error=''; syncRender();
    try {
        const result=await api('library-sync/scan',{});
        librarySync.revision=result.revision; librarySync.items=result.items; librarySync.differences=result.differences; librarySync.review=result.id; librarySync.source=result.source;
        librarySync.choices={};
    } catch(error) { librarySync.error=error.message; }
    finally { librarySync.busy=false; syncRender(); }
}

async function syncOpen() {
    librarySync.error='';
    try { const result=await api('library-sync');librarySync.revision=result.revision;librarySync.items=result.items;librarySync.source=result.source;librarySync.review=''; }
    catch(error) {librarySync.error=error.message;}
    syncRender();
    if (!librarySync.items.length&&!librarySync.error) await syncScan();
}

function syncEdit(item) {
    const kind=item?.Kind||librarySync.kind;
    const value=item||{Id:'',Kind:kind,Name:'',Group:'',Purpose:'',Notes:'',Values:{}};
    librarySync.editing=structuredClone(value); librarySync.donor=null;
    syncDialog(`<header class="sync-header"><h2>${item?'Chỉnh':'Thêm'} ${syncKindNames[kind]}</h2><button class="button" data-sync="back">Quay lại</button></header><form id="sync-item-form" class="sync-form">
        ${kind==='blocks'?`<label>DWG chứa block<input id="sync-donor-file" type="file" accept=".dwg"></label><label>Block nguồn<select id="sync-donor-name"><option value="">${item?'Giữ định nghĩa đang dùng':'Chọn DWG trước'}</option></select></label><p id="sync-upload-status" class="hint" role="status">Nhập DWG để thêm hoặc thay định nghĩa block. Thuộc tính ATT và tham số động được đọc từ nguồn.</p>`:''}
        <div class="sync-fields"><label>Tên<input name="Name" value="${esc(value.Name)}" required maxlength="120"></label><label>Nhóm<input name="Group" value="${esc(value.Group)}" maxlength="120"></label><label class="sync-wide">Công dụng<input name="Purpose" value="${esc(value.Purpose)}" maxlength="500"></label><label class="sync-wide">Ghi chú<textarea name="Notes" maxlength="2000">${esc(value.Notes)}</textarea></label>
        ${syncFields[kind].map(([key,label,type,fallback,options])=>{
            let field=value.Values[key]??fallback;
            if(type==='angle')field=Number(field)*180/Math.PI;
            return `<label>${esc(label)}${type==='checkbox'?`<input type="checkbox" name="v-${key}" ${field?'checked':''}>`:type==='select'?`<select name="v-${key}">${options.map(o=>`<option ${o===field?'selected':''}>${esc(o)}</option>`).join('')}</select>`:`<input name="v-${key}" type="${['number','angle'].includes(type)?'number':'text'}" ${['number','angle'].includes(type)?'step="any"':''} value="${esc(field??'')}" ${fallback===null||key==='bigFont'?'':'required'}>`}</label>`;
        }).join('')}</div><p id="sync-form-error" class="sync-error" role="alert"></p><footer class="sync-footer"><p class="hint">Lưu bản nháp rồi đọc so sánh để áp dụng vào DWG.</p><button class="button primary" type="submit">Lưu bản nháp</button></footer></form>`);
}

document.addEventListener('click',async event=>{
    const button=event.target.closest('[data-sync],[data-sync-kind],[data-sync-edit]'); if(!button||librarySync.busy)return;
    if(button.dataset.syncKind){librarySync.kind=button.dataset.syncKind;syncRender();return;}
    if(button.dataset.syncEdit){syncEdit(librarySync.items.find(i=>i.Id===button.dataset.syncEdit));return;}
    switch(button.dataset.sync){
        case 'open': await syncOpen();break;
        case 'close': document.getElementById('sync-dialog').close();break;
        case 'back': syncRender();break;
        case 'scan': await syncScan();break;
        case 'new': syncEdit(null);break;
        case 'arrange': {
            const kind=librarySync.kind;
            await syncScan();
            if(librarySync.error||!librarySync.review)break;
            librarySync.busy=true;librarySync.error='';syncRender();
            let arranged=false;
            try{
                const result=await api('library-sync/apply',{id:librarySync.review,layoutKind:kind,choices:{}});
                blockLibrary=null;await refresh(false);toast(`Đã cập nhật bố trí ${syncKindNames[kind]} cho các mẫu còn trong CAD.${result.missingSamples?` Bỏ qua ${result.missingSamples} mẫu không còn trong bố trí CAD; không tự tạo lại.`:''}`);arranged=true;
            }catch(error){librarySync.error=error.message;}
            finally{librarySync.busy=false;if(arranged)await syncScan();else syncRender();}
            break;
        }
        case 'apply': {
            const choices=Object.fromEntries([...document.querySelectorAll('[data-sync-choice]')].map(el=>[el.dataset.syncChoice,el.value]));
            // Choices from other tabs are retained when navigating the comparison.
            Object.assign(librarySync.choices||={},choices);
            librarySync.busy=true;librarySync.error='';syncRender();
            let success=false;
            try{
                const result=await api('library-sync/apply',{id:librarySync.review,choices:librarySync.choices});
                librarySync.review='';librarySync.choices={};blockLibrary=null;
                await refresh(false);toast(result.removed?`Đã xóa ${result.removed} mục khỏi app; ${result.sourceUpdated?'đã cập nhật mẫu DWG':'DWG giữ nguyên'}.`:`Đã đồng bộ ${result.items} tài nguyên và mẫu DWG.`);
                success=true;
            }catch(error){librarySync.error=error.message;}
            finally{librarySync.busy=false;if(success)await syncScan();else syncRender();}
            break;
        }
    }
});

document.addEventListener('change',async event=>{
    if(event.target.matches('[data-sync-choice]')){if(event.target.value==='replace'){(librarySync.choices||={})[event.target.dataset.syncChoice]='skip';await syncReplacementOpen(event.target.dataset.syncChoice);return;}(librarySync.choices||={})[event.target.dataset.syncChoice]=event.target.value;return;}
    if(event.target.id!=='sync-donor-file')return;
    const file=event.target.files[0];if(!file)return;
    const status=document.getElementById('sync-upload-status');status.textContent='Đang đọc định nghĩa block…';
    const form=document.getElementById('sync-item-form');form.querySelector('[type=submit]').disabled=true;
    try{
        const data=new FormData();data.append('file',file);librarySync.donor=await api('library-sync/block-file',data);
        document.getElementById('sync-donor-name').innerHTML=librarySync.donor.blocks.map(i=>`<option value="${esc(i.Name)}">${esc(i.Name)}</option>`).join('');
        status.textContent=`Đọc được ${librarySync.donor.blocks.length} block.`;
    }catch(error){status.textContent=error.message;librarySync.donor=null;}
    finally{form.querySelector('[type=submit]').disabled=false;}
});

document.addEventListener('submit',async event=>{
    if(event.target.id!=='sync-item-form')return;event.preventDefault();
    const form=event.target,button=form.querySelector('[type=submit]'),data=new FormData(form),item=structuredClone(librarySync.editing);
    item.Name=data.get('Name').trim();item.Group=data.get('Group').trim();item.Purpose=data.get('Purpose').trim();item.Notes=data.get('Notes').trim();
    for(const [key,,type,fallback] of syncFields[item.Kind]){
        const raw=data.get('v-'+key);
        item.Values[key]=type==='checkbox'?raw!==null:raw===''&&fallback===null?null:['number','angle'].includes(type)?Number(raw)*(type==='angle'?Math.PI/180:1):raw;
    }
    if(item.Kind==='blocks'&&librarySync.donor){
        const name=document.getElementById('sync-donor-name').value;
        const source=librarySync.donor.blocks.find(b=>b.Name===name);if(source)item.Values={...source.Values,donorHash:librarySync.donor.hash,donorName:name};
    }
    button.disabled=true;
    try{const result=await api('library-sync/item',{revision:librarySync.revision,item});librarySync.revision=result.revision;librarySync.review='';librarySync.choices={};const latest=await api('library-sync');librarySync.items=latest.items;syncRender();toast(result.message);}
    catch(error){document.getElementById('sync-form-error').textContent=error.message;}
    finally{button.disabled=false;}
});

// Restore choices after a tab or progress render.
const syncRenderBase=syncRender;
syncRender=function(){syncRenderBase();document.querySelectorAll('[data-sync-choice]').forEach(el=>{el.value=librarySync.choices?.[el.dataset.syncChoice]||'skip';});};
