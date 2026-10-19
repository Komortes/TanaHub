<script lang="ts">
  import { artwork } from '../artwork';
  import { tick } from 'svelte';
  import { X, ImageOff } from '@lucide/svelte';
  import { modal } from '../modal';
  let { url, title, onClose, onLoaded }: { url: string; title: string; onClose: () => void; onLoaded: () => void } = $props();
  let failed = $state(false);
  let loading = $state(true);
  let closeButton: HTMLButtonElement;
  async function retry() {
    failed = false; loading = true;
    await tick();
    closeButton?.focus({ preventScroll: true });
  }
</script>

<dialog use:modal class="cover-lightbox" aria-label={`Cover of ${title}`} oncancel={onClose} onclick={(event) => { if (event.target === event.currentTarget) onClose(); }}>
  <div class="cover-viewer">
    <button bind:this={closeButton} class="close-cover" aria-label="Close cover" onclick={onClose}><X size={20} /></button>
    <div class="cover-stage" class:loading>
      {#if failed}<div class="image-error" role="status"><ImageOff size={32} /><p>This cover could not be loaded.</p><button class="ghost-button" onclick={retry}>Retry cover</button></div>
      {:else}<img use:artwork={{ url, lazy: false }} alt={`Cover of ${title}`} aria-busy={loading} onload={() => { loading = false; onLoaded(); }} onerror={() => { loading = false; failed = true; }} />{#if loading}<div class="loading-cover" role="status">Loading cover…</div>{/if}{/if}
    </div>
    <p class="cover-caption">{title}</p>
  </div>
</dialog>

<style>
  .cover-lightbox { position: fixed; margin: auto; inset: 0; width: fit-content; max-width: calc(100vw - 32px); max-height: calc(100dvh - 32px); padding: 0; border: 1px solid var(--border-lit); border-radius: 12px; background: var(--shell); color: var(--text); overflow: auto; animation: cover-in 160ms var(--ease); }
  .cover-lightbox::backdrop { background: #09080fe6; }
  .cover-viewer { position: relative; padding: 12px; }
  .cover-stage { position: relative; display: grid; place-items: center; }
  .cover-stage.loading { width: min(520px,calc(100vw - 58px),calc((100dvh - 122px) * 2 / 3)); aspect-ratio: 2 / 3; background: var(--raised); border-radius: 6px; }
  .cover-stage.loading img { position: absolute; }
  .loading-cover { position: absolute; inset: 0; display: grid; place-items: center; color: var(--secondary); font-size: 13px; }
  .cover-viewer img { display: block; width: auto; height: auto; max-width: min(520px,calc(100vw - 58px)); max-height: calc(100dvh - 122px); object-fit: contain; border-radius: 6px; margin: 0 auto; }
  .close-cover { position: absolute; top: 18px; right: 18px; z-index: 1; display: grid; place-items: center; width: 40px; height: 40px; color: var(--text); background: var(--shell); border: 1px solid var(--border-lit); border-radius: 8px; }
  .cover-caption { margin: 12px 4px 2px; max-width: 520px; font-size: 12px; line-height: 1.5; text-align: center; color: var(--secondary); overflow-wrap: anywhere; }
  .image-error { width: min(520px,calc(100vw - 58px)); padding: 72px 24px; text-align: center; color: var(--secondary); }.image-error :global(svg) { margin: 0 auto; }.image-error p { font-size: 12px; }.image-error button { min-height: 40px; margin-top: 12px; }
  @keyframes cover-in { from { opacity: 0; transform: scale(.98); } to { opacity: 1; transform: none; } }
</style>
