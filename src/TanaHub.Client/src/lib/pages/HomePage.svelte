<script lang="ts">
  import { tick } from 'svelte';
  import { RefreshCw, ChevronRight, Plus, Star, BookOpen, AlertCircle } from '@lucide/svelte';
  import { featureApi } from '../feature-api';
  import { unitLabel, statusLabel, formatLabel, maxProgress } from '../types';
  import type { Entry, Media, MediaType, PageName } from '../types';
  import type { HomeSummary, Recommendations, ScheduleResult } from '../feature-types';
  import MiniCover from './MiniCover.svelte';
  import InsightsPanel from './InsightsPanel.svelte';

  let { onOpen, onProgress, onRate, onNavigate, revision = 0, active = true }:
    { onOpen: (media: Media) => void; onProgress: (entry: Entry) => Promise<void>; onRate: (entry: Entry, anchor: DOMRect) => void; onNavigate: (page: PageName) => void; revision?: number; active?: boolean } = $props();
  let type = $state<MediaType | ''>('');
  let summary = $state<HomeSummary | null>(null);
  let recommendations = $state<Recommendations | null>(null);
  let airing = $state<ScheduleResult | null>(null);
  let loading = $state(true);
  let recommendationLoading = $state(true);
  let airingLoading = $state(true);
  let error = $state('');
  let recommendationError = $state('');
  let airingError = $state('');
  let notificationError = $state('');
  let actionError = $state('');
  let busy = $state<string[]>([]);
  let refresh = $state(0);
  let generation = 0;
  let requestedType: MediaType | undefined;
  const localZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  const time = (value: string) => new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(value));

  $effect(() => {
    if (!active) return;
    const selectedType = type || undefined;
    if (selectedType !== requestedType) {
      summary = null; recommendations = null; airing = null;
      requestedType = selectedType;
    }
    revision; refresh;
    const current = ++generation;
    loading = true; recommendationLoading = true; airingLoading = true;
    error = ''; recommendationError = ''; airingError = ''; notificationError = '';
    void featureApi.home(selectedType).then(result => { if (current === generation) summary = result; })
      .catch(reason => { if (current === generation) error = reason instanceof Error ? reason.message : 'Could not load your dashboard.'; })
      .finally(() => { if (current === generation) loading = false; });
    void featureApi.recommendations(8, selectedType).then(result => { if (current === generation) recommendations = result; })
      .catch(reason => { if (current === generation) recommendationError = reason instanceof Error ? reason.message : 'Recommendations are unavailable right now.'; })
      .finally(() => { if (current === generation) recommendationLoading = false; });
    if (selectedType === 'Manga') { airing = null; airingLoading = false; }
    else {
      const now = new Date();
      const end = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
      void featureApi.schedule(now.toISOString(), end.toISOString(), true).then(result => {
        if (current !== generation) return;
        airing = result;
        void featureApi.scheduleNotify(now.toISOString(), end.toISOString()).catch(reason => { if (current === generation) notificationError = reason instanceof Error ? reason.message : 'Airing reminders could not be sent.'; });
      })
        .catch(reason => { if (current === generation) airingError = reason instanceof Error ? reason.message : 'Airing times are unavailable right now.'; })
        .finally(() => { if (current === generation) airingLoading = false; });
    }
    return () => { if (generation === current) generation += 1; };
  });
  async function progress(entry: Entry) {
    if (busy.includes(entry.mediaId)) return;
    const focused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const card = focused?.closest<HTMLElement>('.continue-card');
    let saved = false;
    busy = [...busy, entry.mediaId]; actionError = '';
    try { await onProgress(entry); saved = true; }
    catch (reason) { actionError = reason instanceof Error ? reason.message : 'Could not save your progress.'; }
    finally {
      busy = busy.filter(id => id !== entry.mediaId);
      await tick();
      if (card?.isConnected && focused && document.activeElement === document.body) {
        const reachesEnd = saved && (entry.progress >= maxProgress - 1 || (entry.media.totalUnits !== null && entry.media.totalUnits > 0 && entry.progress >= entry.media.totalUnits - 1));
        const target = !reachesEnd && focused.isConnected && !focused.matches(':disabled') ? focused : card.querySelector<HTMLElement>('.cover-button');
        target?.focus({ preventScroll: true });
      }
    }
  }
</script>

<header class="home-header"><h1>Home</h1><button class="ghost-button" aria-label="Refresh dashboard" onclick={() => refresh += 1} disabled={loading}><RefreshCw size={15} />Refresh</button></header>
<div class="home-type" aria-label="Dashboard media type">{#each ['', 'Anime', 'Manga'] as item}<button aria-pressed={type === item} class:selected={type === item} onclick={() => type = item as MediaType | ''}>{item || 'All titles'}</button>{/each}<span>{summary?.insights.totalEntries ?? '—'} {summary?.insights.totalEntries === 1 ? 'title' : 'titles'} tracked</span></div>
{#if error}<div class="load-error" role="alert"><AlertCircle size={20} /><div><strong>Your dashboard could not load</strong><p>{error}</p></div><button class="ghost-button" onclick={() => refresh += 1}>Try again</button></div>{/if}
{#if actionError}<p class="error-text" role="alert">{actionError}</p>{/if}
<div class="dashboard-columns">
  <div class="dashboard-main">
    <section aria-label="Continue tracking" aria-busy={loading}>
      <div class="section-heading"><h2>In progress</h2><button class="section-link" onclick={() => onNavigate('library')}>Library<ChevronRight size={14} /></button></div>
      {#if summary?.continueItems.length}
        <div class="continue-grid">
          {#each summary.continueItems.slice(0, 8) as entry (entry.mediaId)}
            <article class="continue-card" aria-label={entry.media.title}>
              <button class="cover-button" aria-label={`Open ${entry.media.title}`} onclick={() => onOpen(entry.media)}><MiniCover url={entry.media.posterUrl} thumbnail={entry.media.thumbnailUrl} preview={entry.media.previewUrl} title={entry.media.title} /></button>
              <div class="continue-info"><button class="title-link" title={entry.media.title} onclick={() => onOpen(entry.media)}>{entry.media.title}</button><span class="entry-status">{statusLabel(entry.status, entry.mediaType)}</span><div class="entry-bottom"><span class="mono progress-position">{unitLabel(entry.mediaType)} {entry.progress}<span class="secondary"> / {entry.media.totalUnits ?? '—'}</span></span><button class="rate-button" aria-label={`Rate ${entry.media.title}`} onclick={(event) => onRate(entry, event.currentTarget.getBoundingClientRect())} disabled={busy.includes(entry.mediaId)}><Star size={14} fill={entry.score === null ? 'none' : 'currentColor'} /><span class="mono">{entry.score ?? '—'}</span></button></div><button class="progress-action" aria-label={`Add ${entry.mediaType === 'Manga' ? 'chapter' : 'episode'} to ${entry.media.title}`} onclick={() => void progress(entry)} disabled={busy.includes(entry.mediaId) || entry.progress >= maxProgress || (entry.media.totalUnits !== null && entry.media.totalUnits > 0 && entry.progress >= entry.media.totalUnits)}><Plus size={15} />{entry.mediaType === 'Manga' ? 'Read a chapter' : 'Watched an episode'}</button></div>
            </article>
          {/each}
        </div>
      {:else if loading}<p class="section-notice" role="status">Loading in-progress titles…</p>
      {:else if error}<p class="section-notice">In-progress titles are unavailable until the dashboard reloads.</p>
      {:else}<div class="quiet-empty"><BookOpen size={26} strokeWidth={1.4} /><h3>Nothing in progress</h3><p>Add a title and start tracking at your own pace.</p><button class="ghost-button" onclick={() => onNavigate('discover')}>Discover titles</button></div>{/if}
    </section>
    <section aria-label="Recommendations">
      <div class="section-heading"><h2>Recommended for you</h2><button class="section-link" onclick={() => onNavigate('discover')}>Discover<ChevronRight size={14} /></button></div>
      {#if recommendationLoading && !recommendations}<p class="section-notice" role="status">Finding recommendations…</p>
      {:else if recommendationError}<p class="section-notice" role="status">{recommendationError}</p>
      {:else if recommendations?.items.length}<p class="section-notice">{recommendations.notice || (recommendations.genres.length ? `Inspired by your interest in ${recommendations.genres.slice(0, 3).map(genre => genre.name).join(', ')}.` : 'More stories to explore.')}</p><div class="recommendation-grid">{#each recommendations.items as media (media.id)}<button class="recommendation-card" aria-label={`Open ${media.title}`} title={media.title} onclick={() => onOpen(media)}><MiniCover url={media.posterUrl} thumbnail={media.thumbnailUrl} preview={media.previewUrl} title={media.title} /><strong>{media.title}</strong><span><span class="recommendation-format" title={formatLabel(media.format)}>{formatLabel(media.format)}</span><span class="mono">{media.averageScore === null ? '—' : `${media.averageScore}/100`}</span></span></button>{/each}</div>
      {:else}<p class="section-notice">{recommendations?.notice || 'Rate or track a few titles to discover recommendations based on your library.'}</p>{/if}
    </section>
    {#if summary}<InsightsPanel insights={summary.insights} mode="metrics" />{/if}
  </div>
  <aside class="dashboard-aside">
    {#if type !== 'Manga'}
      <section aria-label="Airing today"><div class="section-heading"><h2>Airing today</h2><button class="section-link" onclick={() => onNavigate('schedule')}>Schedule<ChevronRight size={14} /></button></div><p class="section-notice">In your library · {localZone}</p>
        {#if airingLoading && !airing}<p class="section-notice" role="status">Loading upcoming episodes…</p>
        {:else if airingError}<p class="section-notice" role="status">{airingError}</p>
        {:else if airing?.items.length}<div class="compact-rows">{#each airing.items.slice(0, 6) as item}<button class="compact-row" onclick={() => onOpen({ id: item.mediaId, title: item.title, romaji: item.title, type: 'Anime', posterUrl: item.posterUrl, bannerUrl: null, format: item.format, releaseStatus: item.releaseStatus, year: null, averageScore: null, totalUnits: null, description: null, genres: [] })} aria-label={`Open ${item.title}`}><span class="row-cover"><MiniCover url={item.posterUrl} title={item.title} /></span><span class="row-copy"><strong>{item.title}</strong><span>Episode {item.episode}</span></span><span class="row-time mono">{time(item.airingAt)}</span></button>{/each}</div>
        {:else}<p class="section-notice">No more episodes scheduled for your library today.</p>{/if}
        {#if notificationError}<p class="error-text" role="status">{notificationError}</p>{/if}
      </section>
    {/if}
    <section aria-label="Recently updated"><div class="section-heading"><h2>Recently updated</h2></div>
      {#if summary?.recentItems.length}<div class="compact-rows">{#each summary.recentItems.slice(0, 6) as entry (entry.mediaId)}<button class="compact-row" aria-label={`Open recent ${entry.media.title}`} onclick={() => onOpen(entry.media)}><span class="row-cover"><MiniCover url={entry.media.posterUrl} thumbnail={entry.media.thumbnailUrl} preview={entry.media.previewUrl} title={entry.media.title} /></span><span class="row-copy"><strong>{entry.media.title}</strong><span>{statusLabel(entry.status, entry.mediaType)}</span></span><span class="recent-progress mono">{unitLabel(entry.mediaType)} {entry.progress}</span></button>{/each}</div>
      {:else if loading}<p class="section-notice" role="status">Loading recent activity…</p>
      {:else if error}<p class="section-notice">Recent activity is unavailable until the dashboard reloads.</p>
      {:else}<p class="section-notice">Your recent library activity will appear here.</p>{/if}
    </section>
    {#if summary}<InsightsPanel insights={summary.insights} mode="charts" />{/if}
  </aside>
</div>

<style>
  .home-header { display: flex; justify-content: space-between; align-items: center; gap: 16px; margin-bottom: 20px; }
  h1 { font-size: 30px; font-weight: 650; letter-spacing: -.035em; line-height: 1.2; margin: 0; }
  .home-type { display: flex; align-items: center; flex-wrap: wrap; gap: 4px; padding-bottom: 26px; }
  .home-type button { background: transparent; color: var(--secondary); border: 0; border-radius: 0; border-bottom: 2px solid transparent; padding: 11px 14px; font-size: 12px; min-height: 44px; }
  .home-type button:first-child { padding-left: 0; padding-right: 16px; }
  .home-type button.selected { color: var(--paper); border-bottom-color: var(--bookmark); }
  .home-type > span { color: var(--muted); font-size: 12px; margin-left: 16px; }
  .dashboard-columns { display: grid; grid-template-columns: minmax(0,2fr) minmax(260px,.9fr); gap: 32px; }
  .dashboard-main, .dashboard-aside { display: flex; flex-direction: column; gap: 32px; min-width: 0; }
  .dashboard-aside { border-left: 1px solid var(--border); padding-left: 24px; }
  .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-bottom: 8px; }
  h2 { font-size: 15px; font-weight: 600; margin: 0; }
  .section-link { background: transparent; color: var(--secondary); border: none; padding: 8px 0 8px 8px; display: flex; align-items: center; gap: 2px; font-size: 12px; min-height: 36px; }
  .section-link:hover { color: var(--bookmark-bright); }
  .continue-grid { display: grid; grid-template-columns: repeat(auto-fill,minmax(min(100%,280px),1fr)); column-gap: 24px; }
  .continue-card { display: grid; grid-template-columns: 64px minmax(0,1fr); border-bottom: 1px solid var(--border); padding: 16px 0; gap: 14px; }
  .cover-button { padding: 0; border: 0; background: transparent; align-self: start; }
  .continue-info { min-width: 0; display: flex; flex-direction: column; justify-content: center; }
  .title-link { display: -webkit-box; line-clamp: 2; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; height: 2.9em; color: var(--text); font-size: 14px; line-height: 1.45; font-weight: 550; text-align: left; border: 0; background: transparent; padding: 0; border-radius: 2px; }
  .title-link:hover { color: var(--bookmark-bright); }
  .entry-status { font-size: 11px; color: var(--secondary); margin-top: 4px; }
  .entry-bottom { display: grid; grid-template-columns: minmax(0,1fr) auto; align-items: center; gap: 6px; margin: 4px 0; font-size: 12px; }
  .progress-position { min-width: 0; line-height: 1.5; overflow-wrap: anywhere; border-left: 2px solid var(--bookmark); padding-left: 7px; color: var(--text); }
  .secondary { color: var(--secondary); }
  .rate-button { display: flex; align-items: center; justify-content: flex-end; gap: 4px; border: 0; background: transparent; color: var(--amber); padding: 6px 0 6px 6px; font-size: 12px; min-height: 36px; min-width: 44px; }
  .progress-action { display: flex; align-items: center; gap: 6px; background: transparent; color: var(--bookmark-bright); border: 1px solid var(--border-lit); border-radius: 4px; padding: 8px; font-size: 11px; min-height: 36px; justify-content: center; }
  .progress-action:hover:enabled { background: var(--bookmark-wash); }
  .quiet-empty { border-top: 1px solid var(--border); border-bottom: 1px solid var(--border); padding: 32px 20px; text-align: center; color: var(--secondary); }
  .quiet-empty h3 { color: var(--text); font-size: 14px; font-weight: 600; margin: 12px 0 8px; }
  .quiet-empty p { font-size: 12px; line-height: 1.6; margin: 0 0 16px; }
  .section-notice { color: var(--secondary); line-height: 1.6; font-size: 12px; margin: 0 0 14px; }
  .recommendation-grid { display: grid; grid-template-columns: repeat(auto-fill,minmax(0,min(100%,150px))); gap: 20px 16px; }
  .recommendation-card { padding: 0; border: 0; background: transparent; text-align: left; color: var(--text); min-width: 0; }
  .recommendation-card > strong { display: -webkit-box; line-clamp: 2; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; font-size: 12px; line-height: 1.5; margin: 9px 0 4px; font-weight: 500; height: 3em; }
  .recommendation-card > span { display: flex; justify-content: space-between; gap: 8px; font-size: 10px; color: var(--secondary); }
  .recommendation-card > span > .mono { color: var(--secondary); flex-shrink: 0; }
  .recommendation-format { min-width: 0; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
  .compact-rows { display: flex; flex-direction: column; }
  .compact-row { display: grid; grid-template-columns: 40px minmax(0,1fr) auto; align-items: center; gap: 12px; min-width: 0; width: 100%; padding: 12px 0; border: 0; border-bottom: 1px solid var(--border); border-radius: 0; background: transparent; color: var(--text); text-align: left; }
  .compact-row:hover { background: var(--shell); }
  .row-copy { min-width: 0; display: flex; flex-direction: column; gap: 5px; }
  .row-copy > strong { font-size: 12px; line-height: 1.45; font-weight: 500; height: 2.9em; display: -webkit-box; line-clamp: 2; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; }
  .row-copy > span { color: var(--secondary); font-size: 11px; }
  .row-time { font-size: 12px; color: var(--text); }
  .recent-progress { font-size: 10px; color: var(--secondary); white-space: nowrap; }
  @media (max-width: 1100px) { .dashboard-columns { grid-template-columns: minmax(0,1fr); } .dashboard-aside { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); border-left: 0; border-top: 1px solid var(--border); padding: 24px 0 0; } }
  @media (max-width: 640px) { h1 { font-size: 28px; } .home-header > button { font-size: 0; padding: 12px; min-width: 44px; min-height: 44px; } .home-type > span { flex-basis: 100%; margin: 10px 0 0; } .dashboard-aside { grid-template-columns: minmax(0,1fr); } .progress-action, .rate-button { min-height: 44px; } .recommendation-grid { grid-template-columns: repeat(auto-fill,minmax(min(100%,130px),1fr)); } .recommendation-card { max-width: 160px; } }
</style>
