<script lang="ts">
  import { untrack } from 'svelte';
  import { X, Star, Plus, BookOpen, Trash2, Check } from '@lucide/svelte';
  import { modal } from '../modal';
  import { formatLabel, statuses, statusLabel, unitLabel } from '../types';
  import type { Entry, Media, Status } from '../types';
  let { media, entry, loading = false, busy = false, loadError = '', onClose, onAdd, onProgress, onRate, onStatus, onNotes, onRemove }:
    { media: Media; entry?: Entry; loading?: boolean; busy?: boolean; loadError?: string; onClose: () => void; onAdd: () => Promise<void>; onProgress: () => Promise<void>; onRate: (anchor: DOMRect) => void; onStatus: (status: Status) => Promise<void>; onNotes: (notes: string) => Promise<void>; onRemove: () => Promise<void> } = $props();
  let notes = $state(untrack(() => entry?.notes ?? ''));
  let previousNotes = untrack(() => entry?.notes);
  $effect(() => { const savedNotes = entry?.notes; if (savedNotes !== previousNotes) { notes = savedNotes ?? ''; previousNotes = savedNotes; } });
  let error = $state('');
  let confirmRemove = $state(false);
  let failed = $state(false);
  $effect(() => { media.posterUrl; failed = false; });
  const description = $derived(new DOMParser().parseFromString((media.description ?? '').replace(/<br\s*\/?\s*>/gi, '\n'), 'text/html').body.textContent ?? '');
  async function change(action: () => Promise<void>) {
    error = '';
    try { await action(); }
    catch (reason) { error = reason instanceof Error ? reason.message : 'Could not save this change.'; }
  }
</script>

<dialog use:modal class="detail-dialog" aria-labelledby="detail-title" oncancel={(event) => { if (busy) event.preventDefault(); else onClose(); }} onclick={(event) => { if (event.target === event.currentTarget && !busy) onClose(); }}>
  <div class="detail-content">
    <button class="icon-button close-button" aria-label="Close details" onclick={onClose} disabled={busy}><X size={20} /></button>
    <div class="detail-top">
      <div class="detail-poster">{#if media.posterUrl && !failed}<img src={media.posterUrl} alt={`Cover of ${media.title}`} onerror={() => failed = true} />{:else}<BookOpen size={44} strokeWidth={1.25} />{/if}</div>
      <div class="detail-summary">
        <h2 id="detail-title">{media.title}</h2>
        {#if media.romaji !== media.title}<p class="romaji">{media.romaji}</p>{/if}
        <div class="detail-meta"><span>{media.type}</span>{#if media.format !== 'Unknown'}<span>{formatLabel(media.format)}</span>{/if}{#if media.year}<span class="mono">{media.year}</span>{/if}{#if media.totalUnits}<span><span class="mono">{media.totalUnits}</span> {media.type === 'Manga' ? 'chapters' : 'episodes'}</span>{/if}</div>
        {#if media.averageScore !== null}<p class="community"><Star size={15} fill="currentColor" /><span class="mono">{media.averageScore}/100</span><span>Community score</span></p>{/if}
        <div class="genre-list">{#each media.genres as genre}<span>{genre}</span>{/each}</div>
        {#if !entry}<button class="primary-button detail-add" onclick={() => change(onAdd)} disabled={loading || busy}><Plus size={17} />{busy ? 'Adding…' : 'Add to library'}</button>{/if}
      </div>
    </div>
    {#if loading}<p class="detail-loading" role="status">Loading details…</p>{/if}
    {#if loadError}<p class="error-text" role="status">{loadError}</p>{/if}
    {#if description}<section class="synopsis"><h3>Synopsis</h3><p>{description}</p></section>{/if}
    {#if entry}
      <section class="tracking-section">
        <div class="section-heading"><h3>Your library</h3><span><Check size={13} />Saved locally</span></div>
        <div class="tracking-fields">
          <label>Status<select aria-label="Library status" value={entry.status} disabled={busy} onchange={async (event) => { const select = event.currentTarget; await change(() => onStatus(select.value as Status)); select.value = entry?.status ?? 'Planning'; }}>{#each statuses as status}<option value={status}>{statusLabel(status, media.type)}</option>{/each}</select></label>
          <div class="progress-field"><span>Progress</span><div><span class="mono">{unitLabel(media.type)} {entry.progress}<span class="secondary"> / {media.totalUnits ?? '—'}</span></span><button class="icon-button" aria-label={`Add ${media.type === 'Manga' ? 'chapter' : 'episode'} to ${media.title}`} onclick={() => change(onProgress)} disabled={busy || (media.totalUnits !== null && entry.progress >= media.totalUnits)}><Plus size={18} /></button></div></div>
          <div class="rating-field"><span>Your rating</span><button class="detail-rating" aria-label={`Rate ${media.title}`} onclick={(event) => onRate(event.currentTarget.getBoundingClientRect())} disabled={busy}><Star size={18} fill={entry.score !== null ? 'currentColor' : 'none'} /><span class="mono">{entry.score ?? '—'}<span class="secondary"> / 10</span></span></button></div>
        </div>
        <label class="notes-label">Personal notes<textarea aria-label="Personal notes" bind:value={notes} placeholder="What would you like to remember?" rows="3" maxlength="20000" disabled={busy}></textarea></label>
        <div class="notes-actions"><button class="ghost-button" onclick={() => change(() => onNotes(notes))} disabled={busy || notes === (entry.notes ?? '')}>Save notes</button><span>Only on this device</span></div>
      </section>
      <div class="remove-row">
        {#if confirmRemove}<span>Remove this title and its progress?</span><button class="ghost-button" onclick={() => confirmRemove = false} disabled={busy}>Cancel</button><button class="danger-button" onclick={() => change(onRemove)} disabled={busy}>Remove title</button>{:else}<button class="remove-button" onclick={() => confirmRemove = true} disabled={busy}><Trash2 size={14} />Remove from library</button>{/if}
      </div>
    {/if}
    {#if error}<p class="error-text" role="alert">{error}</p>{/if}
  </div>
</dialog>

<style>
  .detail-dialog { width: 780px; max-width: calc(100vw - 32px); max-height: calc(100dvh - 48px); padding: 0; color: var(--text); background: var(--shell); border: 1px solid var(--border-lit); border-radius: 14px; overscroll-behavior: contain; animation: detail-in 200ms var(--ease); }
  .detail-dialog::backdrop { background: #09080fbb; }
  .detail-content { padding: 30px; position: relative; }
  .close-button { position: absolute; top: 14px; right: 14px; }
  .detail-top { display: grid; grid-template-columns: 180px minmax(0,1fr); gap: 26px; padding-right: 10px; }
  .detail-poster { aspect-ratio: 2 / 3; background: var(--raised); border-radius: 9px; overflow: hidden; display: grid; place-items: center; color: var(--secondary); }
  .detail-poster img { width: 100%; height: 100%; object-fit: cover; }
  h2 { font-size: 23px; font-weight: 650; line-height: 1.3; letter-spacing: -.025em; margin: 10px 0 8px; }
  .romaji { color: var(--secondary); font-size: 13px; margin: 0 0 12px; }
  .detail-meta { display: flex; flex-wrap: wrap; gap: 8px; color: var(--secondary); font-size: 12px; }
  .detail-meta > span + span::before { content: '·'; margin-right: 8px; }
  .community { display: flex; gap: 6px; align-items: center; color: var(--amber); font-size: 12px; margin: 16px 0; }
  .community > span:last-child { color: var(--secondary); margin-left: 5px; }
  .genre-list { display: flex; flex-wrap: wrap; gap: 6px; }
  .genre-list span { font-size: 11px; color: var(--secondary); background: var(--raised); padding: 5px 8px; border-radius: 5px; }
  .detail-add { margin-top: 22px; }
  h3 { margin: 0 0 12px; font-size: 14px; font-weight: 600; }
  .synopsis { margin-top: 26px; }
  .synopsis p { margin: 0; color: var(--secondary); line-height: 1.75; font-size: 13px; white-space: pre-line; }
  .tracking-section { margin-top: 28px; border-top: 1px solid var(--border); padding-top: 22px; }
  .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
  .section-heading > span { font-size: 11px; color: var(--secondary); display: flex; align-items: center; gap: 5px; margin-bottom: 12px; }
  .tracking-fields { display: grid; grid-template-columns: 1.2fr 1fr .8fr; gap: 16px; align-items: end; margin-bottom: 22px; }
  .tracking-fields label, .progress-field, .rating-field { display: flex; flex-direction: column; gap: 8px; color: var(--secondary); font-size: 12px; }
  .tracking-fields select { padding: 10px; min-height: 42px; font-size: 13px; width: 100%; }
  .progress-field > div { height: 42px; display: flex; align-items: center; justify-content: space-between; gap: 8px; border: 1px solid var(--border); padding: 0 6px 0 10px; border-radius: 8px; color: var(--cyan); background: var(--raised); }
  .detail-rating { height: 42px; display: flex; gap: 8px; align-items: center; justify-content: center; color: var(--amber); background: var(--raised); border: 1px solid var(--border); font-size: 14px; }
  .secondary { color: var(--secondary); }
  .notes-label { display: flex; flex-direction: column; gap: 10px; color: var(--secondary); font-size: 12px; }
  textarea { width: 100%; min-height: 90px; font-size: 13px; line-height: 1.6; resize: vertical; }
  .notes-actions { display: flex; gap: 14px; align-items: center; margin-top: 10px; }
  .notes-actions span { font-size: 11px; color: var(--muted); }
  .remove-row { display: flex; align-items: center; flex-wrap: wrap; justify-content: flex-end; gap: 10px; border-top: 1px solid var(--border); margin-top: 24px; padding-top: 18px; }
  .remove-row > span { color: var(--secondary); font-size: 12px; margin-right: auto; }
  .remove-button { display: flex; gap: 7px; align-items: center; border: none; background: transparent; color: var(--muted); padding: 6px 0; font-size: 12px; }
  .remove-button:hover { color: var(--red); }
  .detail-loading { color: var(--muted); font-size: 12px; }
  @media (max-width: 600px) { .detail-dialog { max-height: calc(100dvh - 24px); max-width: calc(100vw - 24px); } .detail-content { padding: 22px 18px; } .detail-top { grid-template-columns: 110px minmax(0,1fr); gap: 16px; margin-top: 20px; padding: 0; } h2 { font-size: 18px; margin-top: 0; } .detail-meta { gap: 5px; font-size: 11px; } .community { flex-wrap: wrap; margin: 12px 0; } .community > span:last-child { margin: 0; } .genre-list { gap: 4px; } .genre-list span { font-size: 10px; padding: 4px 6px; } .tracking-fields { grid-template-columns: 1fr 1fr; gap: 14px; } .tracking-fields > label { grid-column: 1 / -1; } }
  @media (prefers-reduced-motion: reduce) { .detail-dialog { animation: none; } }
  @keyframes detail-in { from { opacity: 0; transform: translateY(10px) scale(.99); } to { opacity: 1; transform: none; } }
</style>
