/* Native Messaging carries only search results; authentication stays in Anitsu. */
(function (root) {
  'use strict';
  const CLOUD = 'https://nuvem.anitsu.moe/';
  const EXTENSION = 'peoanblcadbpgganeojampdfibjknnib';
  function site(url) { try { const u = new URL(url); return u.protocol === 'https:' && u.hostname === 'nuvem.anitsu.moe' && !u.port && !u.username && !u.password; } catch { return false; } }

  async function fetchSearch(title, id) {
    if (location.origin !== 'https://nuvem.anitsu.moe') return {status: 0, body: ''};
    const jobs = window.__aniListaJobs || (window.__aniListaJobs = {});
    const controller = new AbortController(); jobs[id] = controller;
    const timeout = setTimeout(() => controller.abort(), 19000);
    try {
      const response = await fetch('/api/search?q=' + encodeURIComponent(title), {credentials: 'same-origin', signal: controller.signal});
      const body = await response.text(); return {status: response.status, body: new TextEncoder().encode(body).length <= 480000 ? body : ''};
    } catch { return {status: 0, body: ''}; }
    finally { clearTimeout(timeout); delete jobs[id]; }
  }
  async function openFolder(name, path, id) {
    if (location.origin !== 'https://nuvem.anitsu.moe') return {ok: false, message: 'Entre no Anitsu Cloud.'};
    const jobs = window.__aniListaJobs || (window.__aniListaJobs = {});
    const job = {aborted: false, abort() {this.aborted = true;}}; jobs[id] = job;
    try {
      const input = document.querySelector('input[placeholder="Buscar pastas..."]');
      if (!input) return {ok: false, message: 'Abra o Cloud e entre na sua conta.'};
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(input, name);
      input.dispatchEvent(new Event('input', {bubbles: true}));
      const deadline = Date.now() + 17000;
      while (Date.now() < deadline && !job.aborted) {
        await new Promise(resolve => setTimeout(resolve, 250));
        if (job.aborted) return {ok: false, message: 'Cancelado.'};
        const matches = [...document.querySelectorAll('button')].filter(button => [...button.querySelectorAll('span, p, div')].some(child => child.children.length === 0 && child.textContent.trim() === path));
        if (matches.length === 1 && !matches[0].disabled) {
          if (job.aborted) return {ok: false}; matches[0].click();
          while (Date.now() < deadline && !job.aborted) {
            await new Promise(resolve => setTimeout(resolve, 200));
            if (job.aborted) return {ok: false, message: 'Cancelado.'};
            // Cloud updates its breadcrumbs only after /files loads successfully.
            const opened = [...document.querySelectorAll('nav')].some(nav => [...nav.children].slice(1).map(child => child.textContent.trim()).join('/') === path.replace(/^\/+|\/+$/g, ''));
            if (opened) return {ok: true};
          }
          break;
        }
      }
      return {ok: false, message: 'Não foi possível localizar a pasta na interface. Caminho: ' + path};
    } finally { delete jobs[id]; }
  }
  function abortJob(id) { const jobs = window.__aniListaJobs; if (jobs && jobs[id]) jobs[id].abort(); }

  class Bridge {
    constructor(api) { this.api = api; this.port = null; this.jobs = new Map(); this.selectedTab = null; this.owned = new Set(); }
    connect() {
      if (this.port) return;
      const port = this.api.runtime.connectNative('br.anilista.anitsu'); this.port = port;
      port.onMessage.addListener(message => this.receive(message));
      port.onDisconnect.addListener(() => {
        if (this.port !== port) return;
        this.port = null; void this.cancelAll();
        void this.api.action.setBadgeText({text: 'OFF'});
        void this.api.action.setTitle({title: 'Abra o AniLista, ative o modo Extensão e clique para conectar.'});
        void this.api.runtime.lastError;
      });
      port.postMessage({type: 'hello', extension: EXTENSION});
      void this.api.action.setBadgeText({text: 'ON'});
      void this.api.action.setTitle({title: 'AniLista conectado'});
    }
    async cancelAll() { for (const id of this.jobs.keys()) await this.cancel(id); }
    async cancel(id) {
      const job = this.jobs.get(id); if (!job) return; job.canceled = true;
      if (job.tab) try { await this.api.scripting.executeScript({target: {tabId: job.tab}, world: 'MAIN', func: abortJob, args: [id]}); } catch {}
    }
    async tab(job) {
      const tabs = await this.api.tabs.query({url: 'https://nuvem.anitsu.moe/*'});
      let tab = tabs.find(t => t.id === this.selectedTab) || tabs.find(t => site(t.url));
      if (!tab) { tab = await this.api.tabs.create({url: CLOUD, active: false}); this.owned.add(tab.id); }
      job.tab = tab.id;
      for (let attempt = 0; attempt < 80; attempt++) {
        if (job.canceled) throw new Error('Cancelado');
        tab = await this.api.tabs.get(tab.id);
        if (tab.status === 'complete') { if (!site(tab.url)) throw new Error('Entre no Anitsu Cloud.'); return tab; }
        await new Promise(resolve => setTimeout(resolve, 200));
      }
      throw new Error('O Anitsu não carregou.');
    }
    async receive(message) {
      if (message.type === 'cancel') { await this.cancel(message.id); return; }
      if (!['search', 'open'].includes(message.type) || !/^[a-f0-9]{32}$/.test(message.id || '') || this.jobs.has(message.id)) return;
      const port = this.port, job = {canceled: false, tab: null}; this.jobs.set(message.id, job);
      try {
        const payload = message.payload || {};
        if (message.type === 'search' && (typeof payload.title !== 'string' || !payload.title.trim() || payload.title.length > 180)) throw new Error('Título inválido');
        if (message.type === 'open' && (typeof payload.name !== 'string' || typeof payload.path !== 'string' || payload.path.length > 2048 || /[\x00-\x1f\\:]/.test(payload.path) || payload.path.split('/').some(p => p === '.' || p === '..'))) throw new Error('Pasta inválida');
        const tab = await this.tab(job); if (job.canceled) return;
        const result = await this.api.scripting.executeScript({target: {tabId: tab.id}, world: 'MAIN', func: message.type === 'search' ? fetchSearch : openFolder, args: message.type === 'search' ? [payload.title, message.id] : [payload.name, payload.path, message.id]});
        if (job.canceled || this.port !== port) return;
        const reply = result[0] && result[0].result || {status: 0, ok: false};
        if (message.type === 'search') {
          let found = false; try { found = reply.status === 200 && JSON.parse(reply.body).results.length > 0; } catch {}
          if (found) this.selectedTab = tab.id;
          else if (reply.status === 401 || reply.status === 403) { await this.api.tabs.update(tab.id, {active: true}); this.owned.delete(tab.id); }
          else if (this.owned.has(tab.id)) { await this.api.tabs.remove(tab.id); this.owned.delete(tab.id); }
        } else if (reply.ok) { await this.api.tabs.update(tab.id, {active: true}); this.owned.delete(tab.id); this.selectedTab = tab.id; }
        if (!job.canceled && this.port === port) {
          const outgoing = {...reply, type: 'result', id: message.id};
          if (new TextEncoder().encode(JSON.stringify(outgoing)).length > 524288) port.postMessage({type: 'result', id: message.id, status: 0, ok: false, message: 'Resposta grande demais.'});
          else port.postMessage(outgoing);
        }
      } catch (error) { if (!job.canceled && this.port === port) port.postMessage({type: 'result', id: message.id, status: 0, ok: false, message: error.message}); }
      finally { this.jobs.delete(message.id); }
    }
  }
  if (typeof module !== 'undefined') module.exports = {Bridge, site, openFolder};
  else { const bridge = new Bridge(chrome); chrome.action.onClicked.addListener(() => bridge.connect()); chrome.runtime.onStartup.addListener(() => bridge.connect()); bridge.connect(); }
})(this);
