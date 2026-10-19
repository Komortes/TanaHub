<script lang="ts">
  import { onMount, tick, untrack } from 'svelte';
  import { X, Star, Minus, Plus, RotateCcw, LoaderCircle } from '@lucide/svelte';
  import { modal } from '../modal';
  import { ratingLabel } from '../types';
  import type { Entry } from '../types';
  import MiniCover from '../pages/MiniCover.svelte';

  let { entry, onClose, onSave }: { entry: Entry; onClose: () => void; onSave: (score: number | null) => Promise<void> } = $props();
  let value = $state<number | null>(untrack(() => entry.score));
  let saving = $state(false);
  let error = $state('');
  let slider: HTMLInputElement;
  let backdropPressed = false;
  const changed = $derived(value !== entry.score);
  onMount(() => slider?.focus({ preventScroll: true }));

  function choose(next: number | null) {
    if (saving) return;
    const focused = document.activeElement;
    value = next;
    error = '';
    if (focused instanceof HTMLButtonElement) void tick().then(() => {
      if (focused.disabled && slider?.isConnected && (document.activeElement === focused || document.activeElement === document.body)) slider.focus({ preventScroll: true });
    });
  }
  function keyboard(event: KeyboardEvent) {
    if (saving) return;
    if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault();
      choose(event.key === 'Home' ? 0 : 10);
    } else if (value === null && ['ArrowLeft', 'ArrowDown', 'ArrowRight', 'ArrowUp'].includes(event.key)) {
      event.preventDefault();
      choose(event.key === 'ArrowLeft' || event.key === 'ArrowDown' ? 0 : 1);
    }
  }
  async function save() {
    if (saving || !changed) return;
    saving = true; error = '';
    try {
      await onSave(value);
      saving = false;
      await tick();
      onClose();
    } catch (reason) {
      error = reason instanceof Error ? reason.message : 'Could not save your rating. Try again.';
      saving = false;
      await tick();
      slider?.focus({ preventScroll: true });
    }
  }
</script>

<dialog use:modal class="rating-dialog" aria-labelledby="rating-heading" aria-describedby="rating-title" oncancel={(event) => { if (saving) event.preventDefault(); else onClose(); }} onpointerdown={(event) => backdropPressed = event.target === event.currentTarget} onclick={(event) => { if (backdropPressed && event.target === event.currentTarget && !saving) onClose(); }}>
  <form class="rating-panel" onsubmit={(event) => { event.preventDefault(); void save(); }} aria-busy={saving}>
    <header class="dialog-heading"><h2 id="rating-heading">Your rating</h2><button type="button" class="icon-button" aria-label="Close rating" onclick={onClose} disabled={saving}><X size={18} /></button></header>
    <div class="rating-body">
    <div class="rated-title"><div class="rating-cover"><MiniCover url={entry.media.posterUrl} thumbnail={entry.media.thumbnailUrl} preview={entry.media.previewUrl} title={entry.media.title} /></div><div><p id="rating-title" class="rating-title" title={entry.media.title}>{entry.media.title}</p><span>Only visible in your library</span></div></div>
    <div class="score-preview" aria-live="polite" aria-atomic="true"><div class="rating-value"><Star size={24} fill={value === null ? 'none' : 'currentColor'} strokeWidth={1.7} /><strong class="mono">{value ?? '—'}</strong><span class="mono">/ 10</span></div><p class="rating-label">{ratingLabel(value)}</p></div>
    <div class="score-control">
      <button type="button" class="score-step" aria-label="Decrease rating" disabled={saving || value === 0} onclick={() => choose(Math.max(0, (value ?? 1) - 1))}><Minus size={18} /></button>
      <div class="slider-wrap"><input bind:this={slider} type="range" min="0" max="10" step="1" value={value ?? 0} style:--range-fill={`${(value ?? 0) * 10}%`} style:--range-color="var(--amber)" aria-label="Your rating out of 10" aria-valuetext={value === null ? 'Not rated' : `${value} out of 10, ${ratingLabel(value)}`} oninput={(event) => choose(Number(event.currentTarget.value))} onkeydown={keyboard} disabled={saving} /><div class="slider-ticks mono" aria-hidden="true"><span>0</span><span>5</span><span>10</span></div></div>
      <button type="button" class="score-step" aria-label="Increase rating" disabled={saving || value === 10} onclick={() => choose(Math.min(10, (value ?? 0) + 1))}><Plus size={18} /></button>
    </div>
    <div class="score-detail"><span>{changed ? 'Unsaved rating' : entry.score === null ? 'Choose a score when you’re ready' : `Saved: ${entry.score} / 10`}</span><button type="button" class="clear-rating" disabled={saving || value === null} onclick={() => choose(null)}><RotateCcw size={13} />Clear</button></div>
    {#if error}<p class="error-text" role="alert">{error}</p>{/if}
    </div>
    <footer class="rating-actions"><button type="button" class="ghost-button" onclick={onClose} disabled={saving}>Cancel</button><button type="submit" class="primary-button" disabled={saving || !changed}>{#if saving}<LoaderCircle size={15} class="saving-icon" />{/if}{saving ? 'Saving…' : 'Save rating'}</button></footer>
  </form>
</dialog>

<style>
  .rating-dialog { position: fixed; inset: 0; margin: auto; width: 400px; max-width: calc(100vw - 32px); max-height: calc(100dvh - 32px); overflow: hidden; padding: 0; border: 1px solid var(--border-lit); border-radius: 14px; background: var(--shell); color: var(--text); box-shadow: 0 20px 80px #0006; animation: pop-in 180ms var(--ease); }
  .rating-dialog::backdrop { background: #09080fa8; }
  .rating-panel { padding: 24px; display: flex; flex-direction: column; max-height: calc(100dvh - 34px); }
  .rating-body { min-height: 0; overflow: auto; overscroll-behavior: contain; margin-inline: -4px; padding-inline: 4px; }
  .dialog-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-shrink: 0; }
  .dialog-heading .icon-button { width: 36px; height: 36px; margin: -6px -6px -6px 0; }
  h2 { margin: 0; font-size: 17px; font-weight: 600; letter-spacing: -.02em; }
  .rated-title { display: flex; align-items: center; gap: 12px; margin-top: 20px; }
  .rating-cover { width: 40px; height: 60px; flex: 0 0 40px; overflow: hidden; border-radius: 5px; background: var(--raised); }
  .rated-title > div:last-child { min-width: 0; }
  .rating-title { margin: 0 0 6px; color: var(--text); font-size: 13px; font-weight: 550; line-height: 1.5; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; }
  .rated-title span { color: var(--secondary); font-size: 11px; }
  .score-preview { padding: 28px 0 18px; text-align: center; }
  .rating-value { display: flex; align-items: center; gap: 10px; justify-content: center; color: var(--amber); }
  .rating-value strong { font-size: 48px; font-weight: 600; line-height: 1; min-width: 2ch; text-align: center; font-variant-numeric: tabular-nums; }
  .rating-value > span { color: var(--secondary); font-size: 14px; align-self: flex-end; padding-bottom: 4px; }
  .rating-label { color: var(--amber); font-size: 13px; font-weight: 500; margin: 10px 0 0; min-height: 20px; }
  .score-control { display: grid; grid-template-columns: 40px minmax(0,1fr) 40px; align-items: start; gap: 14px; }
  .score-step { height: 40px; display: grid; place-items: center; padding: 0; border: 1px solid var(--border-lit); background: var(--raised); color: var(--text); }
  .slider-wrap { min-width: 0; }
  .slider-wrap input { width: 100%; accent-color: var(--amber); padding: 0; height: 40px; display: block; }
  .slider-wrap input::-webkit-slider-thumb { width: 20px; height: 20px; margin-top: -8px; box-shadow: 0 0 0 4px color-mix(in srgb,var(--amber) 12%,transparent); }
  .slider-wrap input::-moz-range-thumb { width: 20px; height: 20px; }
  .slider-ticks { display: flex; justify-content: space-between; font-size: 10px; color: var(--secondary); padding: 0 2px; }
  .score-detail { display: flex; align-items: center; justify-content: space-between; gap: 8px; min-height: 36px; margin-top: 14px; color: var(--secondary); font-size: 11px; }
  .clear-rating { display: inline-flex; align-items: center; gap: 5px; background: transparent; border: 0; padding: 8px 0 8px 8px; color: var(--secondary); font-size: 11px; min-height: 36px; }
  .rating-actions { display: grid; grid-template-columns: 1fr 1.4fr; gap: 10px; border-top: 1px solid var(--border); padding-top: 18px; margin-top: 14px; flex-shrink: 0; }
  .rating-actions button { min-height: 42px; }
  .error-text { margin: 8px 0 0; line-height: 1.6; }
  .rating-actions :global(.saving-icon) { animation: spin 800ms linear infinite; }
  @media (hover: hover) and (pointer: fine) { .score-step:hover:enabled { border-color: var(--amber); color: var(--amber); } .clear-rating:hover:enabled { color: var(--text); } }
  @media (max-width: 380px) { .rating-panel { padding: 20px; } .score-control { gap: 10px; } }
  @media (max-height: 520px) {
    .rating-panel { padding: 16px 20px; }
    .rated-title { margin-top: 12px; }
    .rating-cover, .rated-title span { display: none; }
    .rating-title { margin: 0; -webkit-line-clamp: 1; line-clamp: 1; }
    .score-preview { padding: 12px 0 8px; }
    .rating-value strong { font-size: 36px; }
    .rating-label { margin-top: 6px; }
    .score-detail { margin-top: 4px; }
    .rating-actions { padding-top: 12px; margin-top: 8px; }
  }
  @media (prefers-reduced-motion: reduce) { .rating-dialog, .rating-actions :global(.saving-icon) { animation: none; } }
  @keyframes pop-in { from { opacity: 0; transform: translateY(6px) scale(.98); } to { opacity: 1; transform: none; } }
  @keyframes spin { to { transform: rotate(360deg); } }
</style>
