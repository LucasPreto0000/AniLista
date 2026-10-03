(function () {
  if (location.origin !== 'https://nuvem.anitsu.moe' || window !== window.top) return;
  // Remove only the Cloud's 300 ms search debounce, leaving other timers intact.
  const schedule = window.setTimeout;
  window.setTimeout = function (callback, delay, ...args) {
    if (delay === 300 && typeof callback === 'function' && Function.prototype.toString.call(callback).includes('/search?q=')) delay = 0;
    return schedule.call(window, callback, delay, ...args);
  };
  const fetch = window.fetch.bind(window), searches = new Map();
  let activeQuery = '', activeSearch = null;
  let queryObserver = null;
  document.addEventListener('input', event => {
    if (!event.target.matches || !event.target.matches('input[placeholder="Buscar pastas..."]')) return;
    if (activeSearch && event.target.value !== activeQuery) activeSearch.abort();
    if (event.isTrusted) {if (queryObserver) queryObserver.disconnect(); window.chrome.webview.postMessage({channel: 'anilista-cloud-typing'});}
  }, true);
  window.fetch = function (resource, options) {
    const url = new URL(typeof resource === 'string' ? resource : resource.url, location.href);
    if (url.origin !== location.origin || url.pathname !== '/api/search' || (options && options.method && options.method !== 'GET')) return fetch(resource, options);
    const name = url.searchParams.get('q');
    if (activeSearch && activeQuery !== name) activeSearch.abort();
    let request = searches.get(url.href);
    if (!request) {
      const cancel = new AbortController(); activeSearch = cancel; activeQuery = name;
      if (options && options.signal) {if (options.signal.aborted) cancel.abort(); else options.signal.addEventListener('abort', () => cancel.abort(), {once: true});}
      request = fetch(resource, {...options, signal: cancel.signal}); searches.set(url.href, request);
      const clear = () => {if (searches.get(url.href) === request) searches.delete(url.href); if (activeSearch === cancel) activeSearch = null;};
      cancel.signal.addEventListener('abort', clear, {once: true});
      request.then(clear, clear);
    }
    return request.then(response => response.clone());
  };
  window.aniListaSetQuery = function (name) {
    if (queryObserver) queryObserver.disconnect();
    const observer = new MutationObserver(apply); queryObserver = observer;
    const timer = schedule.call(window, () => observer.disconnect(), 19000);
    function apply() {
      const input = document.querySelector('input[placeholder="Buscar pastas..."]'); if (!input) return;
      observer.disconnect(); window.clearTimeout(timer);
      if (input.value !== name) {Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(input, name); input.dispatchEvent(new Event('input', {bubbles: true}));}
    }
    observer.observe(document, {childList: true, subtree: true}); apply();
  };
  let sent = false;
  const observer = new MutationObserver(check);
  function check() {
    if (sent) return;
    const input = document.querySelector('input[placeholder="Buscar pastas..."]');
    const login = document.body && document.body.textContent.includes('Autenticação Necessária');
    if (!input && !login) return;
    sent = true; observer.disconnect();
    window.chrome.webview.postMessage({channel: 'anilista-cloud-ready'});
  }
  observer.observe(document, {childList: true, subtree: true}); check();
})();
