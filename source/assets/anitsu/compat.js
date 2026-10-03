/* AniLista adapter for the unmodified Anitsu Downloader 1.6.8 release. */
(function () {
  'use strict';
  if (location.origin !== 'https://nuvem.anitsu.moe' || window !== window.top || window.__aniListaGM) return;
  window.__aniListaGM = true;
  const page = crypto.randomUUID(), pending = new Map();
  let next = 0;
  function request(kind, details) {
    const id = page + '-' + (++next), job = {details, frame: null}; pending.set(id, job);
    window.chrome.webview.postMessage({channel: 'anilista-downloader', page, id, kind, url: details.url, name: details.name || '', method: details.method || 'GET', headers: details.headers || {}, data: details.data || '', timeout: details.timeout || 20000});
    return {abort() { pending.delete(id); if (job.frame) job.frame.remove(); window.chrome.webview.postMessage({channel: 'anilista-downloader', page, id, kind: 'abort'}); }};
  }
  window.GM_addStyle = text => {const style = document.createElement('style'); style.textContent = text; document.head.appendChild(style); return style;};
  window.GM_getValue = (key, fallback) => {try {const raw = localStorage.getItem('anilista-gm-' + key); return raw === null ? fallback : JSON.parse(raw);} catch {return fallback;}};
  window.GM_setValue = (key, value) => localStorage.setItem('anilista-gm-' + key, JSON.stringify(value));
  window.GM_xmlhttpRequest = details => request('request', details);
  window.GM_download = details => request('download', details);
  window.aniListaEmbeddedReady = () => {
    // WebView2 cannot load the IDM browser extension. Keep direct/ABDM available.
    const idm = document.getElementById('anu-idm'), label = document.getElementById('anu-idm-label');
    if (idm && idm.checked) {idm.checked = false; if (idm.onchange) idm.onchange();}
    if (idm) idm.disabled = true;
    if (label) {label.hidden = true; label.style.display = 'none';}
  };
  window.chrome.webview.addEventListener('message', event => {
    const message = event.data;
    if (!message || message.channel !== 'anilista-downloader' || message.page !== page) return;
    const job = pending.get(message.id); if (!job) return;
    if (message.event === 'start') {
      const frame = document.createElement('iframe'); frame.hidden = true; frame.src = job.details.url; job.frame = frame; document.body.appendChild(frame); return;
    }
    if (message.event === 'progress') { if (job.details.onprogress) job.details.onprogress({loaded: message.loaded, total: message.total}); return; }
    pending.delete(message.id); if (job.frame) job.frame.remove();
    if (message.event === 'load') {if (job.details.onload) job.details.onload({status: message.status || 200, responseText: message.body || '', responseHeaders: message.headers || '', finalUrl: job.details.url});}
    else if (message.event === 'timeout') {if (job.details.ontimeout) job.details.ontimeout(); else if (job.details.onerror) job.details.onerror({error: 'timeout'});}
    else if (job.details.onerror) job.details.onerror({error: message.status || message.error || 'network'});
  });
})();
