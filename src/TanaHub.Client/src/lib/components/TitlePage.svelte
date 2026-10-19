<script lang="ts">
  import { ArrowLeft, ArrowUpRight, BookOpen, Calendar, Check, Clock3, Copy, Maximize2, Plus, Star, Tv, Users } from '@lucide/svelte';
  import { formatLabel } from '../types';
  import type { Entry, Media, Status } from '../types';
  import TitleTracking from './TitleTracking.svelte';
  import TitleJournal from './TitleJournal.svelte';
  import TitleRelations from './TitleRelations.svelte';
  import CoverLightbox from './CoverLightbox.svelte';
  import { copyText, openExternal } from '../native';

  let { media, entry, loading = false, libraryReady = true, busy = false, loadError = '', onClose, onAdd, onProgress, onProgressSet, onScore, onStatus, onNotes, onReview, onOrganization, onDates, onRemove, onOpenRelated, onRetry }:
    { media: Media; entry?: Entry; loading?: boolean; libraryReady?: boolean; busy?: boolean; loadError?: string; onClose: () => void; onAdd: () => Promise<void>; onProgress: () => Promise<void>; onProgressSet: (progress: number) => Promise<void>; onScore: (score: number | null) => Promise<void>; onStatus: (status: Status) => Promise<void>; onNotes: (notes: string) => Promise<void>; onReview: (review: string) => Promise<void>; onOrganization: (tags: string[], lists: string[]) => Promise<void>; onDates: (startedAt: string | null, completedAt: string | null) => Promise<void>; onRemove: () => Promise<void>; onOpenRelated: (id: string) => void; onRetry?: () => void } = $props();
  let failedPoster = $state(false);
  let failedBanner = $state(false);
  let failedCharacters = $state<string[]>([]);
  let saving = $state(false);
  let actionError = $state('');
  let coverOpen = $state(false);
  let expandedSynopsis = $state(false);
  let copied = $state(false);
  let copyBusy = $state(false);
  let convenienceError = $state('');
  let convenienceVersion = 0;
  let copyTimer: ReturnType<typeof setTimeout>;
  const sourceLink = $derived(/^anilist:\d+$/.test(media.id) ? { label: 'AniList', url: `https://anilist.co/${media.type === 'Manga' ? 'manga' : 'anime'}/${media.id.slice(8)}` } : /^mangadex:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(media.id) ? { label: 'MangaDex', url: `https://mangadex.org/title/${media.id.slice(9)}` } : null);
  const description = $derived(new DOMParser().parseFromString((media.description ?? '').replace(/<br\s*\/?\s*>/gi, '\n'), 'text/html').body.textContent?.trim() ?? '');
  const characters = $derived(media.characters ?? []);
  const metadata = $derived([
    { label: 'Format', value: media.format === 'Unknown' ? media.type : formatLabel(media.format) },
    { label: 'Year', value: media.year },
    { label: 'Release status', value: formatLabel(media.releaseStatus) },
    ...(media.type === 'Anime' ? [{ label: 'Episodes', value: media.episodeCount ?? media.totalUnits }, { label: 'Episode length', value: media.durationMinutes ? `${media.durationMinutes} min` : null }, { label: 'Studio', value: media.studio }] : [{ label: 'Chapters', value: media.chapterCount ?? media.totalUnits }, { label: 'Volumes', value: media.volumeCount }]),
    { label: 'Native title', value: media.nativeTitle },
  ].filter(item => item.value !== null && item.value !== undefined && item.value !== '' && item.value !== 'Unknown'));
  $effect(() => { media.posterUrl; failedPoster = false; coverOpen = false; });
  $effect(() => { media.bannerUrl; failedBanner = false; });
  $effect(() => { media.id; failedCharacters = []; actionError = ''; expandedSynopsis = false; copied = false; copyBusy = false; convenienceError = ''; convenienceVersion += 1; return () => { clearTimeout(copyTimer); convenienceVersion += 1; }; });
  function focusTitle(node: HTMLElement) { node.focus({ preventScroll: true }); }
  async function add() {
    if (busy || saving) return;
    saving = true;
    actionError = '';
    try { await onAdd(); }
    catch (reason) { actionError = reason instanceof Error ? reason.message : 'Could not add this title to your library.'; }
    finally { saving = false; }
  }
  async function copyTitle() {
    if (copyBusy) return;
    const version = convenienceVersion;
    const title = media.title;
    copyBusy = true; convenienceError = ''; copied = false;
    try {
      await copyText(title);
      if (version !== convenienceVersion) return;
      copied = true;
      clearTimeout(copyTimer); copyTimer = setTimeout(() => copied = false, 2500);
    } catch { if (version === convenienceVersion) convenienceError = 'Could not copy the title. Select the title to copy it, or try again.'; }
    finally { if (version === convenienceVersion) copyBusy = false; }
  }
  async function openSource() {
    if (!sourceLink) return;
    const version = convenienceVersion;
    convenienceError = '';
    try { await openExternal(sourceLink.url); } catch { if (version === convenienceVersion) convenienceError = 'Could not open this source. Please try again.'; }
  }
</script>

<article class="title-page" aria-labelledby="title-page-heading">
  <button class="back-button" onclick={onClose} disabled={busy || saving}><ArrowLeft size={16} />Back</button>
  <header class="title-header" class:has-banner={!!media.bannerUrl && !failedBanner}>
    {#if media.bannerUrl && !failedBanner}<img class="title-banner" src={media.bannerUrl} alt="" onerror={() => failedBanner = true} />{/if}
    <div class="title-summary">
      {#if media.posterUrl && !failedPoster}<button class="title-poster poster-zoom" aria-label={`Enlarge cover of ${media.title}`} onclick={() => coverOpen = true}><img src={media.posterUrl} alt={`Cover of ${media.title}`} onerror={() => { failedPoster = true; coverOpen = false; }} /><span class="zoom-hint"><Maximize2 size={14} />Enlarge</span></button>{:else}<div class="title-poster"><BookOpen size={34} strokeWidth={1.25} /></div>{/if}
      <div class="title-identity">
        <div class="title-eyebrow"><span>{#if media.type === 'Anime'}<Tv size={12} />{:else}<BookOpen size={12} />{/if}{media.type}</span>{#if media.format !== 'Unknown'}<span>{formatLabel(media.format)}</span>{/if}{#if media.year}<span class="mono"><Calendar size={12} />{media.year}</span>{/if}</div>
        <h1 id="title-page-heading" tabindex="-1" use:focusTitle>{media.title}</h1>
        {#if media.romaji && media.romaji !== media.title}<p class="romaji">{media.romaji}</p>{/if}
        {#if media.nativeTitle && media.nativeTitle !== media.title && media.nativeTitle !== media.romaji}<p class="native-title">{media.nativeTitle}</p>{/if}
        <div class="title-signals">{#if media.averageScore !== null}<span class="community-score"><Star size={15} fill="currentColor" /><span class="mono">{media.averageScore}<span class="secondary"> / 100</span></span><span class="signal-label">Community</span></span>{/if}{#if media.durationMinutes}<span class="duration"><Clock3 size={13} /><span class="mono">{media.durationMinutes}</span> min / ep.</span>{/if}</div>
        <div class="genres">{#each media.genres as genre}<span>{genre}</span>{/each}</div>
        <div class="title-tools"><button class="title-tool" aria-label="Copy title" onclick={() => void copyTitle()} disabled={copyBusy}>{#if copied}<Check size={13} />Copied{:else}<Copy size={13} />Copy title{/if}</button>{#if sourceLink}<button class="title-tool" onclick={() => void openSource()} aria-label={`Open on ${sourceLink.label}`}><ArrowUpRight size={13} />{sourceLink.label}</button>{/if}<span class="visually-hidden" role="status">{copied ? 'Title copied' : ''}</span></div>
        {#if convenienceError}<p class="convenience-error" role="alert">{convenienceError}</p>{/if}
        {#if !entry}<button class="primary-button add-button" onclick={add} disabled={loading || !libraryReady || busy || saving}><Plus size={16} />{saving || busy ? 'Adding…' : 'Add to library'}</button>{/if}
      </div>
    </div>
  </header>
  {#if loading}<p class="detail-status" role="status">Loading full details…</p>{/if}
  {#if loadError}<div class="detail-error" role="status"><p>{loadError}</p>{#if onRetry}<button class="ghost-button" onclick={onRetry} disabled={loading || busy}>Try again</button>{/if}</div>{/if}
  {#if actionError}<p class="error-text" role="alert">{actionError}</p>{/if}
  <div class="title-columns" style:--details-row={entry ? '3' : '2'}>
    <div class="title-main">
      {#if entry}<TitleTracking {media} {entry} {busy} readOnly={loading || !libraryReady} {onProgress} {onProgressSet} {onScore} {onStatus} />{/if}
      <section class="content-panel synopsis" aria-labelledby="synopsis-heading"><h2 id="synopsis-heading">Synopsis</h2>{#if description}<p id="title-synopsis" class:folded={!expandedSynopsis && description.length > 600}>{description}</p>{#if description.length > 600}<button class="synopsis-toggle" aria-expanded={expandedSynopsis} aria-controls="title-synopsis" onclick={() => expandedSynopsis = !expandedSynopsis}>{expandedSynopsis ? 'Show less' : 'Show more'}</button>{/if}{:else if loading}<div class="synopsis-skeleton" aria-hidden="true"><span></span><span></span><span></span></div>{:else}<p class="no-description">This source has no synopsis for this title.</p>{/if}</section>
      {#if characters.length}<section class="content-panel characters" aria-labelledby="characters-heading"><div class="section-heading"><h2 id="characters-heading">Characters</h2><span><Users size={13} /><span class="mono">{characters.length}</span></span></div><div class="character-grid">{#each characters as character, index (`${character.name}:${index}`)}<div class="character"><div class="character-image">{#if character.imageUrl && !failedCharacters.includes(character.imageUrl)}<img src={character.imageUrl} alt="" loading="lazy" onerror={() => failedCharacters = [...failedCharacters, character.imageUrl!]} />{:else}<Users size={20} />{/if}</div><div><h3>{character.name}</h3><span>{formatLabel(character.role)}</span></div></div>{/each}</div></section>{/if}
      {#if entry}<TitleJournal {entry} busy={busy || loading || !libraryReady} {onNotes} {onReview} {onOrganization} {onDates} {onRemove} />{/if}
      <TitleRelations mediaId={media.id} {loading} {busy} onOpen={onOpenRelated} />
    </div>
    <aside class="content-panel metadata-panel" aria-labelledby="metadata-heading"><h2 id="metadata-heading">Details</h2><dl>{#each metadata as item (item.label)}<div><dt>{item.label}</dt><dd>{item.value}</dd></div>{/each}</dl></aside>
  </div>
</article>
{#if coverOpen && media.posterUrl && !failedPoster}<CoverLightbox url={media.posterUrl} title={media.title} onClose={() => coverOpen = false} />{/if}

<style>
  .title-page { max-width: 1260px; margin: 0 auto; min-width: 0; padding-bottom: 16px; animation: page-in 180ms var(--ease); }
  .back-button { display: inline-flex; align-items: center; gap: 7px; padding: 7px 0; border: none; background: transparent; color: var(--secondary); font-size: 12px; margin-bottom: 18px; }.back-button:hover:enabled { color: var(--text); }
  .title-header { position: relative; overflow: hidden; border-radius: 10px; margin-bottom: 24px; }
  .title-banner { position: absolute; top: 0; left: 0; width: 100%; height: 100%; object-fit: cover; opacity: .12; mask-image: linear-gradient(to bottom,#000,transparent); -webkit-mask-image: linear-gradient(to bottom,#000,transparent); pointer-events: none; }
  .title-summary { position: relative; display: grid; grid-template-columns: 140px minmax(0,1fr); gap: 24px; align-items: start; }
  .has-banner .title-summary { padding: 20px; }
  .title-poster { width: 140px; aspect-ratio: 2 / 3; border-radius: 8px; background: var(--raised); border: 1px solid var(--border-lit); display: grid; place-items: center; color: var(--secondary); overflow: hidden; }.title-poster img { width: 100%; height: 100%; object-fit: cover; }
  .poster-zoom { position: relative; padding: 0; }.zoom-hint { position: absolute; bottom: 8px; left: 8px; right: 8px; display: flex; justify-content: center; align-items: center; gap: 5px; background: var(--shell); border: 1px solid var(--border-lit); border-radius: 5px; padding: 6px; color: var(--text); font-size: 10px; opacity: 0; transition: opacity 120ms var(--ease); }.poster-zoom:focus-visible .zoom-hint { opacity: 1; }
  .title-tools { display: flex; flex-wrap: wrap; gap: 8px 16px; margin-top: 12px; }.title-tool { display: inline-flex; align-items: center; gap: 5px; padding: 6px 0; min-height: 32px; background: transparent; border: 0; color: var(--secondary); font-size: 11px; }.title-tool:hover:enabled { color: var(--text); }
  .convenience-error { font-size: 11px; line-height: 1.6; margin: 4px 0 0; color: var(--secondary); }
  .visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
  .synopsis p.folded { display: -webkit-box; -webkit-line-clamp: 6; line-clamp: 6; -webkit-box-orient: vertical; overflow: hidden; }.synopsis-toggle { min-height: 36px; display: inline-flex; align-items: center; margin-top: 8px; padding: 4px 0; color: var(--lavender); background: transparent; border: 0; font-size: 11px; }
  @media (hover: hover) and (pointer: fine) { .poster-zoom:hover .zoom-hint { opacity: 1; } }
  @media (hover: none) { .zoom-hint { left: auto; padding: 5px; opacity: 1; font-size: 0; gap: 0; } }
  .title-identity { min-width: 0; padding-top: 4px; }.title-eyebrow { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-bottom: 10px; }.title-eyebrow span { display: inline-flex; align-items: center; gap: 5px; color: var(--secondary); border: 1px solid var(--border); background: var(--shell); border-radius: 4px; font-size: 10px; line-height: 1.5; padding: 3px 6px; }.title-eyebrow span:first-child { color: var(--lavender); border-color: var(--border-lit); }
  h1 { font-size: clamp(21px,2.2vw,28px); line-height: 1.3; font-weight: 650; letter-spacing: -.025em; margin: 0 0 8px; overflow-wrap: anywhere; outline: none; }
  .romaji, .native-title { color: var(--secondary); font-size: 12px; line-height: 1.5; margin: 4px 0; overflow-wrap: anywhere; }.native-title { color: var(--muted); font-size: 11px; }
  .title-signals { display: flex; flex-wrap: wrap; align-items: center; gap: 12px 20px; margin-top: 14px; }.community-score, .duration { display: inline-flex; align-items: center; gap: 6px; font-size: 12px; }.community-score { color: var(--amber); }.community-score .mono { font-size: 14px; color: var(--text); }.secondary { color: var(--secondary); font-size: 10px; }.signal-label { font-size: 10px; color: var(--secondary); }.duration { color: var(--secondary); font-size: 11px; }
  .genres { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 14px; }.genres span { font-size: 10px; line-height: 1.5; padding: 3px 7px; border-radius: 4px; color: var(--secondary); background: var(--shell); border: 1px solid var(--border); }.add-button { margin-top: 16px; }
  .detail-status { color: var(--secondary); font-size: 12px; margin: -8px 0 18px; }.detail-error { display: flex; align-items: center; justify-content: space-between; gap: 16px; color: var(--secondary); padding: 14px 16px; background: var(--shell); border: 1px solid var(--border); border-radius: 8px; margin-bottom: 18px; font-size: 12px; }.detail-error p { margin: 0; line-height: 1.6; }.detail-error button { flex-shrink: 0; }
  .title-columns { display: grid; grid-template-columns: minmax(0,1fr) 248px; gap: 20px; align-items: start; }.title-main { display: flex; flex-direction: column; gap: 20px; min-width: 0; }
  .content-panel { padding: 22px; border: 1px solid var(--border); border-radius: 10px; background: var(--shell); min-width: 0; }.content-panel h2 { font-size: 15px; letter-spacing: -.015em; font-weight: 600; margin: 0 0 16px; }
  .synopsis p { font-size: 13px; color: var(--secondary); line-height: 1.85; white-space: pre-line; overflow-wrap: anywhere; margin: 0; }.synopsis .no-description { font-size: 12px; }
  .synopsis-skeleton { display: grid; gap: 12px; }.synopsis-skeleton span { height: 9px; border-radius: 3px; background: var(--raised); animation: pulse 1.5s ease-in-out infinite; }.synopsis-skeleton span:last-child { width: 70%; }
  .metadata-panel dl { display: grid; gap: 16px; margin: 0; }.metadata-panel dt { font-size: 10px; color: var(--muted); margin-bottom: 5px; }.metadata-panel dd { margin: 0; font-size: 12px; color: var(--secondary); overflow-wrap: anywhere; line-height: 1.5; }
  .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-bottom: 16px; }.section-heading h2 { margin: 0; }.section-heading > span { display: inline-flex; gap: 5px; align-items: center; color: var(--secondary); font-size: 11px; }
  .character-grid { display: grid; grid-template-columns: repeat(auto-fit,minmax(min(100%,170px),1fr)); gap: 14px; }.character { display: flex; align-items: center; gap: 10px; min-width: 0; }.character-image { width: 42px; height: 56px; display: grid; place-items: center; background: var(--raised); border-radius: 5px; overflow: hidden; flex-shrink: 0; color: var(--secondary); }.character-image img { width: 100%; height: 100%; object-fit: cover; }.character > div:last-child { min-width: 0; }.character h3 { font-size: 12px; font-weight: 500; line-height: 1.5; margin: 0 0 4px; overflow-wrap: anywhere; }.character span { color: var(--muted); font-size: 10px; }
  @keyframes page-in { from { opacity: .5; transform: translateY(5px); } }
  @media (max-width: 1100px) { .title-columns { grid-template-columns: minmax(0,1fr) 208px; gap: 16px; } .content-panel { padding: 18px; } }
  @media (max-width: 880px) { .title-columns { grid-template-columns: 1fr; gap: 16px; }.metadata-panel { grid-row: var(--details-row); }.metadata-panel dl { grid-template-columns: repeat(auto-fit,minmax(130px,1fr)); gap: 14px 20px; }.title-main { display: contents; } }
  @media (max-width: 640px) { .back-button { margin-bottom: 12px; }.title-summary { grid-template-columns: 100px minmax(0,1fr); gap: 16px; }.title-poster { width: 100px; }.has-banner .title-summary { padding: 12px; }.title-identity { padding-top: 0; } h1 { font-size: 20px; }.title-eyebrow { gap: 4px; margin-bottom: 8px; }.title-eyebrow span { padding: 2px 5px; font-size: 9px; }.title-signals { gap: 10px; margin-top: 10px; }.signal-label { display: none; }.genres { gap: 4px; margin-top: 10px; }.genres span { font-size: 9px; padding: 2px 5px; }.detail-error { flex-wrap: wrap; }.title-header { margin-bottom: 20px; }.metadata-panel dl { grid-template-columns: repeat(2,minmax(0,1fr)); }.character-grid { grid-template-columns: 1fr 1fr; gap: 12px; }.character-image { width: 34px; height: 46px; } }
</style>
