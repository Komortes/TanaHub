import { isDesktop } from './native';

interface Source { url: string; fallback?: string | null; preview?: string | null; thumbnail?: string | null; lazy?: boolean }
const pending: (() => void)[] = [];
let active = 0;
function pump() { while (active < 4 && pending.length) { active++; pending.shift()!(); } }

const resolving = new Map<string, Promise<string>>();
async function imageSource(url: string): Promise<string> {
  if (!isDesktop() || !url.startsWith('https://')) return url;
  let request = resolving.get(url);
  if (!request) {
    request = import('@tauri-apps/api/core').then(async ({ invoke, convertFileSrc }) => {
      const path = await invoke<string | null>('cache_artwork', { url });
      return path ? convertFileSrc(path) : url;
    }).catch(() => url).finally(() => resolving.delete(url));
    resolving.set(url, request);
  }
  return request;
}

// Observe before assigning src: browser-native lazy loading may fetch several screens ahead.
export function artwork(node: HTMLImageElement, initial: Source) {
  let current = initial;
  let dispose = () => {};
  let rendered = '';
  function render() {
    // AniList supplies 100px thumbnails and 230px previews. Keep originals on Retina screens.
    const pixels = node.getBoundingClientRect().width * (window.devicePixelRatio || 1);
    const selected = pixels > 0 && pixels <= 100 && current.thumbnail ? current.thumbnail
      : pixels > 0 && pixels <= 230 && current.preview ? current.preview : current.url;
    const source = { url: selected, fallback: selected !== current.url ? current.url : current.fallback, lazy: current.lazy };
    const key = `${source.url}\n${source.fallback}\n${source.lazy}`;
    if (key === rendered) return;
    dispose(); rendered = key; dispose = mount(source);
  }
  render();
  const sizeObserver = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(render);
  sizeObserver?.observe(node);
  function mount(source: Source) {
    let stopped = false;
    let running = false;
    let finished = false;
    let fallback = false;
    let timer: ReturnType<typeof setTimeout>;
    let observer: IntersectionObserver | undefined;
    node.removeAttribute('src');
    node.loading = 'eager';
    node.decoding = 'async';
    node.classList.add('artwork-loading');

    function release() {
      if (finished) return;
      finished = true; clearTimeout(timer);
      node.classList.remove('artwork-loading');
      if (running) { active--; pump(); }
    }
    function loaded() { release(); }
    function failed(event: Event) {
      if (!fallback && source.fallback && source.fallback !== source.url) {
        event.stopImmediatePropagation();
        fallback = true;
        void assign(source.fallback);
      } else release();
    }
    async function assign(url: string) {
      const resolved = await imageSource(url);
      if (stopped) return;
      clearTimeout(timer);
      timer = setTimeout(() => node.dispatchEvent(new Event('error')), 20_000);
      node.src = resolved;
    }
    const job = () => {
      running = true;
      if (stopped) { release(); return; }
      node.addEventListener('load', loaded);
      node.addEventListener('error', failed, true);
      void assign(source.url);
    };
    function enqueue() { observer?.disconnect(); pending.push(job); pump(); }
    if (source.lazy !== false && typeof IntersectionObserver !== 'undefined') {
      observer = new IntersectionObserver(entries => {
        if (entries.some(entry => entry.isIntersecting)) enqueue();
      }, { rootMargin: '240px' });
      observer.observe(node);
    } else enqueue();

    return () => {
      stopped = true; observer?.disconnect();
      node.removeEventListener('load', loaded);
      node.removeEventListener('error', failed, true);
      const index = pending.indexOf(job);
      if (index >= 0) pending.splice(index, 1);
      release();
    };
  }
  return {
    update(source: Source) {
      current = source; render();
    },
    destroy() { sizeObserver?.disconnect(); dispose(); },
  };
}
