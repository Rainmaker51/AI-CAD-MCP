const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

(async () => {
  const listeners = {}, calls = [], messages = [];
  const context = vm.createContext({
    document: { addEventListener: (type, callback) => { listeners[type] = callback; } },
    api: async (route, payload) => { calls.push({ route, payload }); return { missingSamples: 2 }; },
    refresh: async () => {}, toast: text => messages.push(text)
  });
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../src/Desktop.Host/wwwroot/library-sync.js'), 'utf8'), context);
  vm.runInContext(`syncRender=()=>{}; syncScan=async()=>{librarySync.review='fresh-review';}; librarySync.kind='blocks';`, context);
  await listeners.click({ target: { closest: () => ({ dataset: { sync: 'arrange' } }) } });
  assert.equal(calls.length, 1);
  assert.equal(calls[0].route, 'library-sync/apply');
  assert.equal(calls[0].payload.id, 'fresh-review');
  assert.equal(calls[0].payload.layoutKind, 'blocks');
  assert.equal(Object.keys(calls[0].payload.choices).length, 0, 'Layout must not silently choose App -> CAD');
  assert.match(messages[0], /Bỏ qua 2 mẫu/);
  assert.match(messages[0], /không tự tạo lại/);
  assert.equal(vm.runInContext('librarySync.busy', context), false);
  console.log('PASS: layout requests stay separate from data writes and report skipped CAD samples');
})().catch(error => { console.error(error); process.exitCode = 1; });
