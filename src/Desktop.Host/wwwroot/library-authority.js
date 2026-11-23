// Activated by QA only after the replacement browser is available on this warehouse.
let qaLibraryReadOnly = false;
const qaWriteSelectors = '[data-sync="open"],[data-block-edit],[data-block-move],[data-block-move-selected],[data-layer-move],[data-layer-move-selected],[data-hatch-edit],[data-hatch-delete],[data-hatch-restore],[data-hatch-import],[data-style-edit],[data-style-delete],[data-style-restore],[data-style-import],[data-manage-action="groups"],[data-lib-action="edit"],[data-lib-action="new"],[data-lib-action="upload"],[data-lib-action="activate"],[data-lib-action="delete"],[data-lib-action="restore"],[data-draft-open]';
const qaOriginalLibraryView = libraryView;
libraryView = function(kind) {
 const html = qaOriginalLibraryView(kind);
 if (!qaLibraryReadOnly || kind === 'skills') return html;
 const fragment = document.createElement('div'); fragment.innerHTML = html;
 fragment.querySelectorAll(qaWriteSelectors).forEach(e => e.remove());
 fragment.querySelectorAll('.block-library-admin').forEach(e => e.remove());
 return '<div class="notice" role="status">Thư viện được chỉnh sửa và duyệt tại <strong>Thư viện QA</strong> trong AutoCAD. Bạn vẫn có thể xem, tìm kiếm và sử dụng dữ liệu ở đây.</div>' + fragment.innerHTML;
};
document.addEventListener('click', e => {
 if (!qaLibraryReadOnly || page !== 'template' || !e.target.closest(qaWriteSelectors)) return;
 e.preventDefault(); e.stopImmediatePropagation(); toast('Mở Thư viện QA trong AutoCAD để chỉnh sửa và duyệt.');
}, true);
async function refreshLibraryAuthority() {
 try { const authority = await api('qa-library/authority'); const next = authority.editor === 'QA'; if (next !== qaLibraryReadOnly) { qaLibraryReadOnly = next; if (page === 'template') render(); } }
 catch (_) { /* Older Host keeps the existing editor until the paired update is installed. */ }
}
refreshLibraryAuthority();
setInterval(refreshLibraryAuthority, 30000);
