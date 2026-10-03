const assert = require('node:assert/strict');
const {Bridge, site} = require('./background.js');
const manifest = require('./manifest.json');
const hash = require('node:crypto').createHash('sha256').update(Buffer.from(manifest.key, 'base64')).digest('hex').slice(0, 32);
assert.equal([...hash].map(c => String.fromCharCode(97 + parseInt(c, 16))).join(''), 'peoanblcadbpgganeojampdfibjknnib');
assert.equal(site('https://nuvem.anitsu.moe/x'), true);
assert.equal(site('https://nuvem.anitsu.moe.evil.example/'), false);
function fake(existing, reply) {
  const records = {created: [], removed: [], focused: [], messages: [], scripts: []};
  const port = {onMessage: {addListener() {}}, onDisconnect: {addListener() {}}, postMessage: m => records.messages.push(m)};
  const api = {
    runtime: {connectNative: () => port}, action: {setBadgeText: async () => {}, setTitle: async () => {}},
    tabs: {query: async () => existing ? [{id: 4, url: 'https://nuvem.anitsu.moe/', status: 'complete'}] : [], create: async o => {records.created.push(o); return {id: 9};}, get: async id => ({id, url: 'https://nuvem.anitsu.moe/', status: 'complete'}), update: async (id, o) => records.focused.push(id), remove: async id => records.removed.push(id)},
    scripting: {executeScript: async options => {records.scripts.push(options); return [{result: reply}];}}
  }; const bridge = new Bridge(api); bridge.connect(); return {bridge, records};
}
const search = {type: 'search', id: 'a'.repeat(32), payload: {title: 'Lain'}};
async function actualNavigation(changesPath, abort) {
  const vm = require('node:vm'); let clock = 0, currentPath = '', clicks = 0;
  const input = {}, jobs = {};
  const button = {disabled: false, querySelectorAll: () => [{children: [], textContent: 'Anime/Lain'}], click() {clicks++; if (changesPath) currentPath = 'Anime/Lain'; if (abort) jobs['nav-test'].abort();}};
  const context = {module: {exports: {}}, location: {origin: 'https://nuvem.anitsu.moe'}, window: {__aniListaJobs: jobs}, Date: {now: () => clock}, Event: class {}, HTMLInputElement: function () {}, setTimeout: fn => {clock += 250; fn();}, document: {querySelector: () => input, querySelectorAll: selector => selector === 'button' ? [button] : [{children: [{textContent: 'Home'}, ...currentPath.split('/').filter(Boolean).map(textContent => ({textContent}))]}]}};
  context.HTMLInputElement.prototype = {}; Object.defineProperty(context.HTMLInputElement.prototype, 'value', {set() {}}); input.dispatchEvent = () => {};
  vm.runInNewContext(require('node:fs').readFileSync(require.resolve('./background.js'), 'utf8'), context);
  return {result: await context.module.exports.openFolder('Lain', 'Anime/Lain', 'nav-test'), clicks};
}
(async () => {
  assert.equal((await actualNavigation(false, false)).result.ok, false, 'A click alone must not report successful navigation');
  assert.equal((await actualNavigation(true, false)).result.ok, true, 'Successful breadcrumb transition confirms opening');
  assert.equal((await actualNavigation(false, true)).result.ok, false, 'Cancellation stops destination wait');
  let f = fake(true, {status: 200, body: '{"results":[]}'}); await f.bridge.receive(search); assert.deepEqual(f.records.removed, []); assert.equal(f.records.created.length, 0);
  f = fake(false, {status: 200, body: '{"results":[]}'}); await f.bridge.receive(search); assert.deepEqual(f.records.removed, [9]); assert.equal(f.records.created[0].active, false);
  f = fake(false, {status: 401, body: '{}'}); await f.bridge.receive(search); assert.deepEqual(f.records.focused, [9]); assert.deepEqual(f.records.removed, []);
  f = fake(true, {ok: true}); await f.bridge.receive({type: 'open', id: 'b'.repeat(32), payload: {name: 'Lain', path: 'Anime/Lain'}}); assert.deepEqual(f.records.focused, [4]); assert.equal(f.records.scripts[0].world, 'MAIN');
  f = fake(true, {ok: true}); await f.bridge.receive({type: 'open', id: 'b'.repeat(32), payload: {name: 'Lain', path: '../../x'}}); assert.equal(f.records.scripts.length, 0); assert.equal(f.records.messages.at(-1).ok, false);
  f = fake(true, {status: 200, body: '{"results":[{"name":"Lain","path":"Anime/Lain"}]}'});
  let finish; f.bridge.api.scripting.executeScript = () => new Promise(resolve => {finish = resolve;});
  const running = f.bridge.receive(search); await new Promise(resolve => setImmediate(resolve)); f.bridge.jobs.get(search.id).canceled = true; finish([{result: {status: 200, body: '{"results":[]}'}}]); await running;
  assert.equal(f.records.messages.length, 1); // only hello: late results are discarded
  console.log('PASS: extensão · identidade, abas existentes, sem resultado, login, navegação confirmada, caminhos e cancelamento');
})().catch(e => {console.error(e); process.exitCode = 1;});
