'use strict';
const assert = require('node:assert/strict'), fs = require('node:fs'), vm = require('node:vm'), path = require('node:path');
const assets = path.join(__dirname, '../source/assets/anitsu');
function fixture(origin = 'https://nuvem.anitsu.moe') {
  const sent = [], nodes = [], storage = new Map(); let receive;
  const window = {chrome: {webview: {postMessage: x => sent.push(x), addEventListener: (_, handler) => receive = handler}}}; window.top = window;
  const document = {head: {appendChild: x => nodes.push(x)}, body: {appendChild: x => nodes.push(x)}, getElementById: () => null,
    createElement: tag => ({tag, remove() {this.removed = true;}})};
  const ctx = vm.createContext({window, document, location: {origin}, crypto: {randomUUID: () => 'd59c32e8-f042-4b61-aef6-4603cd2a6f87'}, localStorage: {getItem: key => storage.has(key) ? storage.get(key) : null, setItem: (key, value) => storage.set(key, value)}});
  vm.runInContext(fs.readFileSync(path.join(assets, 'compat.js'), 'utf8'), ctx);
  return {window, document, sent, nodes, ctx, reply: x => receive({data: {...sent.at(-1), ...x}})};
}
async function main() {
  const f = fixture(); let response;
  f.window.GM_xmlhttpRequest({url: 'https://graphql.anilist.co/', method: 'POST', data: '{}', onload: x => response = x});
  f.reply({event: 'load', page: 'unrelated', body: 'wrong'}); assert.equal(response, undefined);
  f.reply({event: 'load', status: 401, body: 'login', headers: 'x-test: 1'}); assert.equal(response.status, 401); assert.equal(response.responseText, 'login');
  let done = 0, failed;
  f.window.GM_download({url: 'https://nuvem.anitsu.moe/api/download?path=Lain.mkv', name: 'Lain.mkv', onload: () => done++, onerror: x => failed = x.error});
  f.reply({event: 'start'}); const frame = f.nodes.at(-1); assert.equal(frame.tag, 'iframe'); assert.equal(done, 0);
  f.reply({event: 'error', status: 403}); assert.equal(failed, 403); assert.equal(frame.removed, true);
  const aborted = f.window.GM_download({url: 'x', onload: () => done++}); aborted.abort(); f.reply({event: 'load'}); assert.equal(done, 0);
  f.window.GM_setValue('key', {port: 1234}); assert.equal(f.window.GM_getValue('key').port, 1234); assert.equal(f.window.GM_getValue('missing', 'fallback'), 'fallback');
  assert.equal(fixture('https://discord.com').window.GM_download, undefined);
  let changed = false; const idm = {checked: true, onchange: () => changed = true}, label = {style: {}};
  f.document.getElementById = id => id === 'anu-idm' ? idm : label; f.window.aniListaEmbeddedReady(); assert.equal(idm.checked, false); assert.equal(idm.disabled, true); assert.equal(label.hidden, true); assert.equal(label.style.display, 'none'); assert.equal(changed, true);
  for (const outcome of ['success', 'noop', 'cancel']) {
    let now = 0, clicks = 0, messages = []; const input = {dispatchEvent() {}};
    const w = {chrome: {webview: {postMessage: x => messages.push(x)}}, __aniListaNavCancel: {n: outcome === 'cancel'}};
    const button = {disabled: false, querySelectorAll: () => [{children: [], textContent: 'Anime/Lain'}], click() {clicks++;}};
    const doc = {querySelector: () => input, querySelectorAll: tag => tag === 'button' ? [button] : outcome === 'success' && clicks ? [{children: [{textContent: 'Home'}, {textContent: 'Anime'}, {textContent: 'Lain'}]}] : []};
    function Input() {} Object.defineProperty(Input.prototype, 'value', {set(value) {this.value = value;}});
    const context = vm.createContext({window: w, document: doc, HTMLInputElement: Input, Event: function() {}, Date: {now: () => now}, setTimeout: (fn, ms) => {now += ms; fn();}});
    vm.runInContext(fs.readFileSync(path.join(assets, 'navigate.js'), 'utf8'), context); await w.aniListaNavigate('Lain', 'Anime/Lain', 'n');
    if (outcome === 'cancel') {assert.equal(clicks, 0); assert.equal(messages.length, 0);} else {assert.equal(clicks, 1); assert.equal(messages[0].ok, outcome === 'success');}
  }
  console.log('PASS: GM adapter, callback isolation, download lifecycle, storage, navigation confirmation and cancellation');
}
main().catch(error => {console.error(error); process.exitCode = 1;});
