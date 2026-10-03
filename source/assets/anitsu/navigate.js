window.aniListaNavigate = async function (name, path, id) {
  const canceled = () => window.__aniListaNavCancel && window.__aniListaNavCancel[id];
  let ok = false;
  try {
    const input = document.querySelector('input[placeholder="Buscar pastas..."]');
    if (input && !canceled()) {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(input, name);
      input.dispatchEvent(new Event('input', {bubbles: true}));
      const deadline = Date.now() + 17000;
      while (!canceled() && Date.now() < deadline) {
        await new Promise(resolve => setTimeout(resolve, 250));
        if (canceled()) break;
        const matches = [...document.querySelectorAll('button')].filter(button => !button.disabled && [...button.querySelectorAll('span, p, div')].some(child => !child.children.length && child.textContent.trim() === path));
        if (matches.length === 1) {
          if (canceled()) break; matches[0].click();
          while (!canceled() && Date.now() < deadline) {
            await new Promise(resolve => setTimeout(resolve, 200));
            if (canceled()) break;
            if ([...document.querySelectorAll('nav')].some(nav => [...nav.children].slice(1).map(child => child.textContent.trim()).join('/') === path.replace(/^\/+|\/+$/g, ''))) {ok = true; break;}
          }
          break;
        }
      }
    }
  } catch {}
  if (!canceled()) window.chrome.webview.postMessage({id, ok});
  if (window.__aniListaNavCancel) delete window.__aniListaNavCancel[id];
};
