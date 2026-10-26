<script lang="ts">
  import { artwork } from '../artwork';
  import { tick } from 'svelte';
  import { Plus, Star, BookOpen, Play } from '@lucide/svelte';
  import { formatLabel, statusLabel, unitLabel, maxProgress } from '../types';
  import type { Entry, Layout, Media } from '../types';
  let { media, entry, layout, busy = false, onOpen, onProgress, onRate, onAdd }:
    { media: Media; entry?: Entry; layout: Layout; busy?: boolean; onOpen: () => void; onProgress: () => Promise<void>; onRate: (anchor: DOMRect) => void; onAdd: () => Promise<void> } = $props();
  let failed = $state(false);
  let card: HTMLElement;
  async function act(action: () => Promise<void>) {
    const focused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const ownedFocus = !!focused && card?.contains(focused);
    try { await action(); }
    finally {
      await tick();
      if (ownedFocus && card?.isConnected && !card.closest('[hidden], [inert]') && focused && document.activeElement === document.body) {
        const target = focused.isConnected && !focused.matches(':disabled') ? focused : card.querySelector<HTMLElement>('.progress-button:not(:disabled)') ?? card.querySelector<HTMLElement>('.title-button');
        target?.focus({ preventScroll: true });
      }
    }
  }
  $effect(() => { media.posterUrl; media.previewUrl; media.thumbnailUrl; failed = false; });
  const hasTotal = $derived(media.totalUnits !== null && media.totalUnits > 0);
  const progressRatio = $derived(hasTotal ? Math.max(0, Math.min(1, (entry?.progress ?? 0) / media.totalUnits!)) : 0);
  const metadata = $derived([
    media.format !== 'Unknown' ? formatLabel(media.format) : media.type,
    media.year,
    hasTotal ? `${media.totalUnits} ${media.type === 'Manga' ? media.totalUnits === 1 ? 'chapter' : 'chapters' : media.totalUnits === 1 ? 'episode' : 'episodes'}` : null,
  ].filter(Boolean).join(' · '));
</script>

<article bind:this={card} class="media-card" class:list-card={layout === 'list'} aria-label={media.title}>
  <button class="poster" onclick={onOpen} aria-label={`Open ${media.title}`}>
    {#if media.posterUrl && !failed}
      <img use:artwork={{ url: media.posterUrl, thumbnail: media.thumbnailUrl, preview: media.previewUrl }} alt={`Cover of ${media.title}`} loading="lazy" decoding="async" onerror={() => failed = true} />
    {:else}
      <div class="cover-fallback"><BookOpen size={32} strokeWidth={1.25} /><span>{media.title}</span></div>
    {/if}
    <span class="poster-action">View details</span>
  </button>
  <div class="card-info">
    <button class="title-button" onclick={onOpen} title={media.title}><span>{media.title}</span></button>
    <div class="metadata" title={metadata}>{metadata}</div>
    {#if entry}
      <div class="tracking-row">
        <span class="status-badge" data-status={entry.status}>
          {#if entry.status === 'Watching'}<Play size={11} fill="currentColor" />{/if}
          {statusLabel(entry.status, media.type)}
        </span>
        <button class="progress-button" aria-label={`Add ${media.type === 'Manga' ? 'chapter' : 'episode'} to ${media.title}`} title={entry.status === 'Planning' && entry.progress === 0 ? `Start ${media.type === 'Manga' ? 'reading' : 'watching'} and record the first ${media.type === 'Manga' ? 'chapter' : 'episode'}` : media.type === 'Manga' ? 'Read one more chapter' : 'Watched one more episode'} onclick={() => void act(onProgress)} disabled={busy || entry.progress >= maxProgress || (hasTotal && entry.progress >= media.totalUnits!)}><Plus size={17} /></button>
      </div>
      <div class="card-bottom">
        <div class="progress-info">
          <div class="progress-track" class:unknown-total={!hasTotal} aria-hidden="true"><span style:transform={`scaleX(${progressRatio})`}></span></div>
          <span class="mono progress-text">{unitLabel(media.type)} {entry.progress}<span class="total"> / {media.totalUnits ?? '—'}</span></span>
        </div>
        <button class="rating-button" class:unrated={entry.score === null} aria-label={`Rate ${media.title}`} title="Set your rating" onclick={(event) => onRate(event.currentTarget.getBoundingClientRect())} disabled={busy}><Star size={17} fill={entry.score !== null ? 'currentColor' : 'none'} /><span class="mono">{entry.score ?? '—'}<span class="rating-total">/10</span></span></button>
      </div>
    {:else}
      <div class="tracking-row catalog-action"><button class="add-button" onclick={() => void act(onAdd)} disabled={busy} aria-label={`Add ${media.title} to library`}><Plus size={15} /><span>Add to library</span></button></div>
      <div class="card-bottom catalog-bottom">
        <div class="progress-info"><div class="progress-track catalog-track" aria-hidden="true"></div><span class="progress-text total">Score</span></div>
        {#if media.averageScore !== null}<span class="community-score" title="Community score"><Star size={16} fill="currentColor" /><span class="mono">{media.averageScore}<span class="rating-total">/100</span></span></span>{:else}<span class="community-score">Unrated</span>{/if}
      </div>
    {/if}
  </div>
</article>

<style>
  .media-card { min-width: 0; border: 0; background: transparent; border-radius: 0; }
  .media-card:focus-within .title-button { color: var(--bookmark-bright); }
  .poster { display: block; position: relative; width: 100%; aspect-ratio: 2 / 3; padding: 0; border: 0; border-radius: 4px; overflow: hidden; background: var(--raised); }
  .poster img { display: block; width: 100%; height: 100%; object-fit: cover; }
  .poster-action { position: absolute; bottom: 8px; left: 8px; right: 8px; opacity: 0; background: var(--shell); padding: 8px 6px; border: 1px solid var(--border-lit); border-radius: 4px; color: var(--text); font-size: 11px; text-align: center; white-space: nowrap; transition: opacity 160ms var(--ease); }
  .poster:focus-visible .poster-action { opacity: 1; }
  .cover-fallback { height: 100%; display: flex; flex-direction: column; gap: 18px; align-items: center; justify-content: center; padding: 24px; color: var(--secondary); }
  .cover-fallback span { font-size: 16px; line-height: 1.5; font-weight: 600; text-align: center; overflow-wrap: anywhere; display: -webkit-box; line-clamp: 4; -webkit-line-clamp: 4; -webkit-box-orient: vertical; overflow: hidden; }
  .card-info { padding: 12px 0 0; }
  .title-button { color: var(--text); text-align: left; font-size: 14px; font-weight: 600; line-height: 1.45; background: none; border: none; padding: 0; width: 100%; display: block; overflow: hidden; height: 2.9em; border-radius: 2px; overflow-wrap: anywhere; }
  .title-button > span { display: -webkit-box; line-clamp: 2; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; height: 2.9em; }
  .metadata { display: -webkit-box; line-clamp: 2; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; line-height: 1.5; height: 3em; font-size: 11px; color: var(--secondary); margin: 5px 0 8px; }
  .tracking-row { display: flex; justify-content: space-between; gap: 6px; align-items: center; height: 32px; margin-bottom: 8px; }
  .status-badge { display: inline-flex; gap: 4px; align-items: center; color: var(--secondary); font-size: 11px; font-weight: 500; white-space: nowrap; }
  .status-badge[data-status='Completed'] { color: var(--green); }
  .status-badge[data-status='Paused'] { color: var(--amber); }
  .status-badge[data-status='Dropped'] { color: var(--red); }
  .progress-button { display: inline-flex; justify-content: center; align-items: center; width: 32px; height: 32px; flex-shrink: 0; padding: 0; border: 1px solid var(--border-lit); border-radius: 6px; background: var(--shell); color: var(--text); }
  .progress-button:hover:enabled { background: var(--raised); border-color: var(--bookmark); }
  .card-bottom { display: grid; grid-template-columns: minmax(0,1fr) auto; align-items: center; gap: 4px 8px; min-height: 28px; }
  .progress-info { display: contents; }
  .progress-track { grid-column: 1; grid-row: 1; height: 3px; width: 100%; border-radius: 1px; background: var(--border); overflow: hidden; }
  .progress-track span { height: 100%; width: 100%; display: block; background: var(--bookmark); border-radius: inherit; transform-origin: left; transition: transform 180ms var(--ease); }
  .unknown-total { opacity: .5; }
  .progress-text { grid-column: 1 / -1; grid-row: 2; display: block; position: relative; min-width: 0; min-height: 2.9em; padding-left: 9px; font-size: 11px; line-height: 1.45; color: var(--text); overflow-wrap: anywhere; }
  .progress-text::before { content: ''; position: absolute; left: 0; top: 2px; width: 2px; height: 12px; border-radius: 1px; background: var(--bookmark); }
  .total { color: var(--secondary); }
  .rating-button { grid-column: 2; grid-row: 1; display: inline-flex; align-items: center; flex-shrink: 0; gap: 6px; border: 0; background: transparent; color: var(--amber); font-size: 12px; padding: 0 0 0 5px; min-height: 24px; }
  .rating-button:hover:enabled { color: var(--bookmark-bright); }
  .rating-button.unrated { color: var(--secondary); }
  .rating-total { color: var(--secondary); font-size: 10px; }
  .catalog-track { visibility: hidden; }
  .catalog-bottom .progress-text::before { display: none; }
  .community-score { grid-column: 2; grid-row: 1; color: var(--secondary); display: flex; align-items: center; flex-shrink: 0; gap: 5px; font-size: 12px; min-height: 24px; }
  .add-button { background: transparent; color: var(--text); display: flex; gap: 5px; align-items: center; justify-content: center; width: 100%; height: 32px; padding: 6px; border: 1px solid var(--border-lit); font-size: 11px; white-space: nowrap; }
  .add-button:hover:enabled { background: var(--raised); border-color: var(--bookmark); }
  .list-card { display: grid; grid-template-columns: clamp(56px,calc(var(--cover-size,170px) * .4),88px) minmax(0,1fr); align-items: center; gap: 16px; padding: 12px 0; border-bottom: 1px solid var(--border); }
  .list-card .poster { border-radius: 6px; }
  .list-card .poster-action { display: none; }
  .list-card .card-info { display: grid; grid-template-columns: minmax(180px,1fr) minmax(120px,.55fr) minmax(160px,.65fr); align-items: center; gap: 6px 24px; padding: 0; }
  .list-card .title-button { grid-column: 1; font-size: 15px; }
  .list-card .metadata { grid-column: 1; grid-row: 2; margin: 0; }
  .list-card .tracking-row { grid-column: 2; grid-row: 1 / span 2; margin: 0; }
  .list-card .card-bottom, .list-card .catalog-bottom { grid-column: 3; grid-row: 1 / span 2; }
  @media (max-width: 900px) { .list-card .card-info { grid-template-columns: minmax(0,1fr) 160px; gap: 8px 14px; } .list-card .tracking-row { grid-column: 1; grid-row: 3; } .list-card .card-bottom, .list-card .catalog-bottom { grid-column: 2; grid-row: 2 / span 2; } }
  @media (hover: hover) and (pointer: fine) { .poster:hover .poster-action { opacity: 1; }.title-button:hover { color: var(--bookmark-bright); } }
  @media (max-width: 520px) { .card-info { padding-top: 10px; } .title-button { font-size: 13px; } .metadata { font-size: 10px; } .rating-total { display: none; } .rating-button { gap: 4px; } .list-card { grid-template-columns: clamp(56px,calc(var(--cover-size,170px) * .36),80px) minmax(0,1fr); gap: 12px; padding: 12px 0; } .list-card .card-info { display: flex; flex-direction: column; align-items: stretch; padding: 0; gap: 4px; } .list-card .tracking-row { margin-top: 4px; } .list-card .title-button { font-size: 14px; } .list-card .card-bottom { margin-top: 2px; } }
</style>
