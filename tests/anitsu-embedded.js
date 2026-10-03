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
  await readyTests();
  const session = fixture(); session.ctx.setTimeout = setTimeout;
  let busy = false, renewals = 0;
  const sessionButton = {onclick() {}, click() {renewals++; busy = true; setTimeout(() => {busy = false;}, 80);}, getAttribute: () => busy ? 'true' : 'false', classList: {contains: () => true}};
  session.document.getElementById = id => id === 'anu-session' ? sessionButton : null;
  const renewal = session.window.aniListaRenewSession('session-1');
  assert.equal(renewals, 1); assert.equal(session.sent.length, 0, 'Search waits for the session renewal');
  await renewal; assert.equal(session.sent.at(-1).id, 'session-1'); assert.equal(session.sent.at(-1).ok, true);
  await session.window.aniListaRenewSession('session-2'); assert.equal(renewals, 2, 'Each opening renews the session again');
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
  let changed = false; const idm = {checked: true, onchange: () => changed = true}, label = {style: {}}; let compactStyle;
  f.document.getElementById = id => id === 'anu-idm' ? idm : id === 'anu-idm-label' ? label : null; f.document.head.appendChild = node => {compactStyle = node;}; f.window.aniListaEmbeddedReady(); assert.equal(idm.checked, false); assert.equal(idm.disabled, true); assert.equal(label.hidden, true); assert.equal(label.style.display, 'none'); assert.equal(changed, true); assert.equal(compactStyle.id, 'anilista-compact-downloader'); assert.match(compactStyle.textContent, /anu-abdm-cfg/); assert.match(compactStyle.textContent, /anu-filter/);
  for (const outcome of ['success', 'noop', 'cancel', 'late']) {
    let now = 0, clicks = 0, messages = [], queries = 0; const input = {dispatchEvent() {}};
    const w = {chrome: {webview: {postMessage: x => messages.push(x)}}, __aniListaNavCancel: {n: outcome === 'cancel'}};
    const button = {disabled: false, querySelectorAll: () => [{children: [], textContent: 'Anime/Lain'}], click() {clicks++;}};
    const doc = {querySelector: () => outcome === 'late' && ++queries < 3 ? null : input, querySelectorAll: tag => tag === 'button' ? [button] : (outcome === 'success' || outcome === 'late') && clicks ? [{children: [{textContent: 'Home'}, {textContent: 'Anime'}, {textContent: 'Lain'}]}] : []};
    function Input() {} Object.defineProperty(Input.prototype, 'value', {set(value) {this.value = value;}});
    const context = vm.createContext({window: w, document: doc, HTMLInputElement: Input, Event: function() {}, Date: {now: () => now}, setTimeout: (fn, ms) => {now += ms; fn();}});
    vm.runInContext(fs.readFileSync(path.join(assets, 'navigate.js'), 'utf8'), context); await w.aniListaNavigate('Lain', 'Anime/Lain', 'n');
    if (outcome === 'cancel') {assert.equal(clicks, 0); assert.equal(messages.length, 0);} else {assert.equal(clicks, 1); assert.equal(messages[0].ok, outcome === 'success' || outcome === 'late');}
  }
  console.log('PASS: GM adapter, callback isolation, download lifecycle, storage, navigation confirmation and cancellation');
}
async function readyTests() {
  const sent = [], calls = [], delays = []; let mounted = false, inputHandler, observe, disconnected = false;
  const window = {chrome: {webview: {postMessage: x => sent.push(x)}}, setTimeout: (_, delay) => delays.push(delay), clearTimeout: () => {}, fetch: (url, options) => new Promise((resolve, reject) => {
    calls.push({url, options, resolve}); if (options && options.signal) options.signal.addEventListener('abort', () => reject(new Error('aborted')), {once: true});
  })}; window.top = window;
  const input = {dispatchEvent() {}}; function Input() {} Object.defineProperty(Input.prototype, 'value', {set(value) {this.value = value;}});
  const document = {body: {textContent: ''}, querySelector: () => mounted ? input : null, addEventListener: (_, handler) => inputHandler = handler};
  const context = vm.createContext({window, document, HTMLInputElement: Input, Event: function() {}, location: {origin: 'https://nuvem.anitsu.moe', href: 'https://nuvem.anitsu.moe/'}, URL, AbortController, MutationObserver: function(handler) {observe = handler; this.observe = () => {}; this.disconnect = () => disconnected = true;}});
  vm.runInContext(fs.readFileSync(path.join(assets, 'ready.js'), 'utf8'), context);
  assert.equal(sent.length, 0); mounted = true; observe(); observe(); assert.equal(sent.length, 1); assert.equal(disconnected, true);
  window.setTimeout(function() {return '/search?q=';}, 300); window.setTimeout(function() {return 'other';}, 300); assert.deepEqual(delays, [0, 300]);
  const first = window.fetch('/api/search?q=Lain'), second = window.fetch('/api/search?q=Lain'); assert.equal(calls.length, 1);
  calls[0].resolve(new Response('{"results":[]}')); const [a, b] = await Promise.all([first, second]); assert.notEqual(a, b); assert.equal(await a.text(), await b.text());
  const stale = window.fetch('/api/search?q=Old').catch(error => error.message);
  inputHandler({target: {matches: () => true, value: 'New'}, isTrusted: true}); assert.equal(calls[1].options.signal.aborted, true); assert.equal(await stale, 'aborted'); assert.equal(sent.at(-1).channel, 'anilista-cloud-typing');
  const retry = window.fetch('/api/search?q=Old'); assert.equal(calls.length, 3); calls[2].resolve(new Response('{}')); await retry;
  const normal = window.fetch('/api/files'); assert.equal(calls[3].options, undefined); calls[3].resolve(new Response('{}')); await normal;
  mounted = false; window.aniListaSetQuery('Lain'); assert.equal(input.value, undefined); mounted = true; observe(); assert.equal(input.value, 'Lain');
  mounted = false; document.body.textContent = 'Autenticação Necessária'; vm.runInContext(fs.readFileSync(path.join(assets, 'ready.js'), 'utf8'), context); assert.equal(sent.at(-1).channel, 'anilista-cloud-ready');
  console.log('PASS: early Cloud readiness, targeted debounce, request deduplication, stale cancellation and unchanged unrelated fetch/timers');
}
main().catch(error => {console.error(error); process.exitCode = 1;});
