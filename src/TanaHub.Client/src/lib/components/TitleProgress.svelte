<script lang="ts">
  import { untrack } from 'svelte';
  import { Check, Minus, Plus, RotateCcw, X } from '@lucide/svelte';
  import type { Entry, Media } from '../types';
  import { maxProgress } from '../types';
  import { rememberDraft, restoreDraft } from '../drafts';

  let { media, entry, busy = false, onIncrement, onSet, onDraftChange }:
    { media: Media; entry: Entry; busy?: boolean; onIncrement: () => Promise<void>; onSet: (value: number) => Promise<void>; onDraftChange: (dirty: boolean) => void } = $props();
  let draft = $state<number | undefined>(untrack(() => restoreDraft<number | null>(entry.mediaId, 'progress', entry.progress) ?? undefined));
  let previousProgress = untrack(() => `${entry.mediaId}:${entry.progress}`);
  let validation = $state('');
  let confirmReset = $state(false);
  let input: HTMLInputElement;
  const dirty = $derived(draft !== entry.progress);
  const valid = $derived(typeof draft === 'number' && Number.isInteger(draft) && draft >= 0 && draft <= maxProgress);
  const total = $derived(media.totalUnits !== null && media.totalUnits > 0 ? media.totalUnits : null);
  const remaining = $derived(total === null ? null : Math.max(0, total - entry.progress));
  const fraction = $derived(total === null ? 0 : Math.min(1, entry.progress / total));
  const unit = $derived(media.type === 'Manga' ? 'chapter' : 'episode');
  const minutes = $derived(media.type === 'Anime' && remaining !== null && media.durationMinutes && media.durationMinutes > 0 ? remaining * media.durationMinutes : null);
  const timeRemaining = $derived(minutes === null ? '' : minutes >= 60 ? `${Math.floor(minutes / 60)}h${minutes % 60 ? ` ${minutes % 60}m` : ''}` : `${minutes}m`);

  $effect(() => {
    const current = `${entry.mediaId}:${entry.progress}`;
    if (current !== previousProgress) { draft = entry.progress; previousProgress = current; validation = ''; confirmReset = false; }
  });
  $effect(() => onDraftChange(dirty));
  $effect(() => rememberDraft(entry.mediaId, 'progress', entry.progress, draft ?? null));
  function cancelDraft() { draft = entry.progress; validation = ''; input?.focus({ preventScroll: true }); }
  function cancelReset() { confirmReset = false; input?.focus({ preventScroll: true }); }
  async function save() {
    if (busy || !dirty) return;
    if (!valid || draft === undefined) { validation = 'Enter a whole number starting at 0.'; return; }
    validation = '';
    try { await onSet(draft); } catch { /* Keep the draft for an explicit retry; parent shows the error. */ }
  }
  async function act(action: () => Promise<void>) {
    if (busy || dirty) return;
    validation = '';
    try { await action(); confirmReset = false; } catch { /* Parent shows the error and retains saved progress. */ }
  }
</script>

<div class="title-progress">
  <div class="progress-heading"><label for="title-progress">{media.type === 'Manga' ? 'Chapters read' : 'Episodes watched'}</label><span class="progress-total mono">{total === null ? 'Total unknown' : `${total} total`}</span></div>
  <form onsubmit={(event) => { event.preventDefault(); void save(); }}>
    <div class="stepper">
      <button type="button" class="stepper-minus" aria-label={`Remove one ${unit}`} disabled={busy || dirty || entry.progress === 0} onclick={() => void act(() => onSet(entry.progress - 1))}><Minus size={17} /></button>
      <input bind:this={input} id="title-progress" class="mono" type="number" min="0" max={maxProgress} step="1" bind:value={draft} disabled={busy} aria-label="Set progress" aria-invalid={!!validation} aria-describedby={validation ? 'progress-validation' : 'progress-hint'} onkeydown={(event) => { if (event.key === 'Escape' && !busy) { event.preventDefault(); cancelDraft(); } }} />
      <button type="button" class="stepper-plus" aria-label={`+1 ${unit}`} disabled={busy || dirty || entry.progress >= maxProgress || (total !== null && entry.progress >= total)} onclick={() => void act(onIncrement)}><Plus size={17} /><span>+1 {unit}</span></button>
    </div>
    {#if dirty}<div class="draft-actions"><span>Unsaved progress</span><button class="apply-button" type="submit" disabled={busy || !valid}><Check size={13} />Set progress</button><button type="button" class="cancel-button" aria-label="Cancel progress edit" disabled={busy} onclick={cancelDraft}><X size={13} /></button></div><p class="progress-hint">Draft kept in this window. Set progress to save it.</p>{/if}
  </form>
  <div class="progress-track" class:unknown={total === null} role={total === null ? 'presentation' : 'progressbar'} aria-label={total === null ? undefined : `${media.type === 'Manga' ? 'Chapters' : 'Episodes'} completed`} aria-valuemin={total === null ? undefined : 0} aria-valuemax={total ?? undefined} aria-valuenow={total === null ? undefined : Math.min(entry.progress,total)} aria-valuetext={total === null ? undefined : `${entry.progress} ${unit}${entry.progress === 1 ? '' : 's'} of ${total}`}><span style:transform={`scaleX(${fraction})`}></span></div>
  <p class="progress-hint" id="progress-hint">{#if remaining !== null}{remaining} {unit}{remaining === 1 ? '' : 's'} left{#if minutes !== null && remaining > 0}<span> · about {timeRemaining}</span>{/if}{:else}Track at your own pace.{/if}{#if entry.status === 'Completed' && entry.progress === 0}<span> · Marked completed; progress not logged.</span>{/if}</p>
  {#if validation}<p id="progress-validation" class="error-text" role="alert">{validation}</p>{/if}
  {#if entry.status === 'Planning' && entry.progress === 0}<p class="progress-hint">Logging your first {unit} moves this title to {media.type === 'Manga' ? 'Reading' : 'Watching'}.</p>{/if}
  <details class="more-actions">
    <summary>More progress actions</summary>
    <div class="secondary-actions">
      {#if total !== null}<button type="button" class="quiet-button" disabled={busy || dirty || entry.progress === total} onclick={() => void act(() => onSet(total!))}><Check size={13} />Set to {total}</button>{/if}
      <button type="button" class="quiet-button" disabled={busy || dirty || entry.progress === 0} onclick={() => confirmReset = true}><RotateCcw size={13} />Reset</button>
    </div>
    {#if confirmReset}<div class="reset-confirmation"><p>Reset your progress to 0? Your status and rating will stay the same.</p><div><button type="button" class="danger-button" disabled={busy || dirty} onclick={() => void act(() => onSet(0))}>Reset progress</button><button type="button" class="quiet-button" disabled={busy} onclick={cancelReset}>Keep progress</button></div></div>{/if}
  </details>
</div>

<style>
  .title-progress { min-width: 0; }
  .progress-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 12px; font-size: 12px; color: var(--secondary); }
  .progress-total { font-size: 10px; white-space: nowrap; }
  .stepper { display: grid; grid-template-columns: 40px minmax(60px,1fr) auto; height: 44px; border: 1px solid var(--border-lit); border-radius: 8px; background: var(--raised); }
  .stepper button { display: inline-flex; align-items: center; justify-content: center; gap: 6px; border: 0; border-radius: 0; background: transparent; color: var(--secondary); font-size: 12px; min-width: 40px; }
  .stepper-minus { border-radius: 7px 0 0 7px !important; }
  .stepper .stepper-plus { color: var(--cyan); background: color-mix(in srgb,var(--cyan) 8%,transparent); border-left: 1px solid var(--border); padding: 0 12px; border-radius: 0 7px 7px 0; }
  .stepper input { min-width: 0; width: 100%; padding: 4px 8px; border: 0; border-radius: 0; background: transparent; text-align: center; color: var(--cyan); font-size: 20px; appearance: textfield; -moz-appearance: textfield; }
  .stepper input::-webkit-inner-spin-button, .stepper input::-webkit-outer-spin-button { appearance: none; margin: 0; }
  .draft-actions { display: flex; gap: 8px; align-items: center; margin-top: 8px; font-size: 10px; color: var(--secondary); }
  .draft-actions > span { margin-right: auto; }
  .apply-button, .cancel-button, .quiet-button { display: inline-flex; align-items: center; justify-content: center; gap: 5px; background: transparent; border: 1px solid var(--border); color: var(--secondary); font-size: 11px; min-height: 34px; padding: 6px 9px; }
  .apply-button { color: var(--cyan); border-color: color-mix(in srgb,var(--cyan) 35%,var(--border)); }
  .cancel-button { padding: 6px; }
  .progress-track { height: 4px; background: var(--border); border-radius: 4px; overflow: hidden; margin-top: 14px; }
  .progress-track span { display: block; height: 100%; width: 100%; background: var(--cyan); transform-origin: left; transition: transform 160ms var(--ease); }
  .progress-track.unknown { background: transparent; border-bottom: 1px dashed var(--border-lit); }
  .progress-hint { min-height: 16px; font-size: 11px; line-height: 1.6; color: var(--secondary); margin: 8px 0 0; overflow-wrap: anywhere; }
  .progress-hint span { color: var(--muted); }
  .more-actions { margin-top: 12px; }
  summary { cursor: pointer; color: var(--secondary); font-size: 10px; min-height: 28px; padding: 6px 0; }
  summary:focus-visible { outline: 2px solid var(--lavender); outline-offset: 3px; border-radius: 3px; }
  .secondary-actions, .reset-confirmation > div { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 6px; }
  .reset-confirmation { margin-top: 10px; padding: 12px; background: var(--raised); border: 1px solid var(--border); border-radius: 7px; }
  .reset-confirmation p { font-size: 11px; line-height: 1.6; color: var(--secondary); margin: 0 0 8px; }
  .reset-confirmation .danger-button { min-height: 34px; padding: 6px 9px; font-size: 11px; }
  @media (hover: hover) and (pointer: fine) { .quiet-button:hover:enabled { background: var(--raised); color: var(--text); } .stepper button:hover:enabled { color: var(--text); } }
</style>
