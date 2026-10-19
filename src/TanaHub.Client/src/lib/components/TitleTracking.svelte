<script lang="ts">
  import { tick } from 'svelte';
  import { Check, ChevronRight, LoaderCircle, Star } from '@lucide/svelte';
  import { ratingLabel, statuses, statusLabel } from '../types';
  import type { Entry, Media, Status } from '../types';
  import RatingDialog from './RatingDialog.svelte';
  import TitleProgress from './TitleProgress.svelte';

  let { media, entry, busy = false, readOnly = false, onProgress, onProgressSet, onScore, onStatus }:
    { media: Media; entry: Entry; busy?: boolean; readOnly?: boolean; onProgress: () => Promise<void>; onProgressSet: (value: number) => Promise<void>; onScore: (score: number | null) => Promise<void>; onStatus: (status: Status) => Promise<void> } = $props();
  let saving = $state(false);
  let draftDirty = $state(false);
  let ratingOpen = $state(false);
  let error = $state('');
  let panel: HTMLElement;
  const locked = $derived(busy || saving || readOnly);
  const saveMessage = $derived(readOnly ? 'Tracking unavailable' : locked ? 'Saving…' : error ? 'Could not save' : draftDirty ? 'Unsaved progress' : 'Saved locally');

  async function change(action: () => Promise<void>) {
    if (locked) throw new Error('Please wait for the current change to save.');
    saving = true; error = '';
    const focus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    try { await action(); }
    catch (reason) { error = reason instanceof Error ? reason.message : 'Could not save this change.'; throw reason; }
    finally {
      saving = false;
      await tick();
      if (focus && panel?.isConnected && document.activeElement === document.body) {
        const target = focus.isConnected && !focus.matches(':disabled') ? focus : panel.querySelector<HTMLElement>('#title-progress');
        target?.focus({ preventScroll: true });
      }
    }
  }
  async function statusChange(event: Event) {
    const select = event.currentTarget as HTMLSelectElement;
    const status = select.value as Status;
    try { await change(() => onStatus(status)); } catch { /* Error is displayed below. */ }
    finally { select.value = entry.status; }
  }
</script>

<section bind:this={panel} class="tracking-panel" aria-labelledby="tracking-heading" aria-busy={locked}>
  <div class="section-heading"><div><h2 id="tracking-heading">Your tracking</h2><span class="saved" class:save-error={!!error} role="status">{#if busy || saving}<LoaderCircle size={12} class="saving-icon" />{:else if !readOnly && !error && !draftDirty}<Check size={12} />{/if}{saveMessage}</span></div><label class="status-field"><span class="visually-hidden">Library status</span><select aria-label="Library status" value={entry.status} disabled={locked} onchange={statusChange}>{#each statuses as status}<option value={status}>{statusLabel(status, media.type)}</option>{/each}</select></label></div>
  <div class="tracking-fields"><TitleProgress {media} {entry} busy={locked} onIncrement={() => change(onProgress)} onSet={(value) => change(() => onProgressSet(value))} onDraftChange={(dirty) => draftDirty = dirty} /><div class="rating-field"><h3>Your rating</h3><button class="rating-trigger" aria-label={`Rate ${media.title}`} aria-haspopup="dialog" disabled={locked} onclick={() => ratingOpen = true}><Star size={20} fill={entry.score === null ? 'none' : 'currentColor'} /><span class="rating-copy"><span class="rating-number">{#if entry.score === null}Add a rating{:else}<strong class="mono">{entry.score}</strong><span class="mono"> / 10</span>{/if}</span><span class="rating-meaning">{entry.score === null ? 'How was it?' : ratingLabel(entry.score)}</span></span><ChevronRight size={16} class="rating-chevron" /></button></div></div>
  {#if error}<p class="error-text" role="alert">{error} {draftDirty ? 'Your progress edit is kept for retry.' : 'Could not confirm this change. Try again.'}</p>{/if}
</section>
{#if ratingOpen}<RatingDialog {entry} onClose={() => ratingOpen = false} onSave={onScore} />{/if}

<style>
  .tracking-panel { padding: 20px; border: 1px solid var(--border); border-radius: 10px; background: var(--shell); container-type: inline-size; }
  .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 20px; }
  h2 { font-size: 15px; font-weight: 600; letter-spacing: -.015em; margin: 0 0 6px; }
  .saved { display: inline-flex; align-items: center; gap: 5px; font-size: 10px; color: var(--secondary); }
  .saved :global(svg) { color: var(--green); }
  .saved.save-error { color: var(--red); }
  .saved :global(.saving-icon) { color: var(--secondary); animation: spin 800ms linear infinite; }
  .status-field { min-width: 0; max-width: 48%; }
  select { width: 100%; min-height: 38px; font-size: 12px; padding: 8px 28px 8px 10px; }
  .tracking-fields { display: grid; grid-template-columns: minmax(0,1fr); gap: 20px; }
  .rating-field { min-width: 0; }
  .rating-field h3 { margin: 0 0 12px; color: var(--secondary); font-size: 12px; font-weight: 500; }
  .rating-trigger { width: 100%; display: flex; align-items: center; gap: 12px; text-align: left; padding: 12px 14px; min-height: 68px; background: var(--raised); border: 1px solid var(--border); color: var(--amber); }
  .rating-copy { min-width: 0; display: grid; gap: 4px; }
  .rating-number { font-size: 13px; color: var(--text); }.rating-number strong { font-size: 20px; color: var(--amber); font-weight: 600; }.rating-number > span { color: var(--secondary); font-size: 11px; }
  .rating-meaning { color: var(--secondary); font-size: 11px; }
  .rating-trigger :global(.rating-chevron) { margin-left: auto; color: var(--secondary); }
  @container (min-width: 500px) { .tracking-fields { grid-template-columns: minmax(230px,1fr) minmax(170px,.55fr); gap: 24px; } .rating-field { border-left: 1px solid var(--border); padding-left: 20px; } }
  @media (hover: hover) and (pointer: fine) { .rating-trigger:hover:enabled { border-color: color-mix(in srgb,var(--amber) 55%,var(--border)); background: color-mix(in srgb,var(--amber) 4%,var(--raised)); } }
  .error-text { margin: 14px 0 0; }
  .visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
  @keyframes spin { to { transform: rotate(360deg); } }
  @media (max-width: 640px) { .tracking-panel { padding: 16px; } .section-heading { gap: 12px; } }
</style>
