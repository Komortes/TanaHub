<script lang="ts">
  import { untrack, onMount, tick } from 'svelte';
  import { BookOpen, Compass, SearchX, RefreshCw, AlertCircle, Check, X } from '@lucide/svelte';
  import Sidebar from './lib/components/Sidebar.svelte';
  import UpdateNotice from './lib/components/UpdateNotice.svelte';
  import CollectionToolbar from './lib/components/CollectionToolbar.svelte';
  import MediaCard from './lib/components/MediaCard.svelte';
  import TitlePage from './lib/components/TitlePage.svelte';
  import RatingDialog from './lib/components/RatingDialog.svelte';
  import HomePage from './lib/pages/HomePage.svelte';
  import StatisticsPage from './lib/pages/StatisticsPage.svelte';
  import SchedulePage from './lib/pages/SchedulePage.svelte';
  import RecognizePage from './lib/pages/RecognizePage.svelte';
  import SettingsPage from './lib/pages/SettingsPage.svelte';
  import { api } from './lib/api';
  import { featureApi } from './lib/feature-api';
  import type { Settings, SettingsPatch } from './lib/feature-types';
  import type { Entry, Layout, Media, MediaType, PageName, Status } from './lib/types';
  import { mediaKey } from './lib/types';
  import { clearDrafts } from './lib/drafts';
  import { desktopUpdates } from './lib/desktop-updates.svelte';

  function preferences(): { layout: Layout; coverSize: number } {
    try {
      const value = JSON.parse(localStorage.getItem('tanahub-view') ?? '{}');
      const old = Number.isFinite(value.coverSize) ? value.coverSize : 170;
      const size = value.version === 2 ? old : Number.isFinite(value.coverSize) ? Math.round(old * .75 / 10) * 10 : 170;
      return { layout: value.layout === 'list' ? 'list' : 'grid', coverSize: Math.max(140, Math.min(220, size)) };
    } catch { return { layout: 'grid', coverSize: 170 }; }
  }
  const saved = preferences();
  let page = $state<PageName>('library');
  let homeView = $state<'overview' | 'statistics'>('overview');
  let homeType = $state<MediaType | ''>('');
  let type = $state<MediaType | ''>('');
  const collectionViews: Record<'library' | 'discover', { type: MediaType | ''; search: string }> = {
    library: { type: '', search: '' }, discover: { type: 'Anime', search: '' },
  };
  let status = $state<Status | ''>('');
  let search = $state('');
  let sort = $state('Popularity');
  let genre = $state('');
  let preset = $state('Popular');
  let librarySort = $state('Updated');
  let tag = $state('');
  let customList = $state('');
  let organizationFilters = $state({ tags: [] as string[], customLists: [] as string[] });
  let revision = $state(0);
  let settings = $state<Settings | null>(null);
  let settingsLoading = $state(false);
  let settingsError = $state('');
  let settingsBusy = $state('');
  let requestedSettingsSection = $state('');
  let layout = $state<Layout>(saved.layout);
  let coverSize = $state(saved.coverSize);
  let entries = $state<Entry[]>([]);
  let catalog = $state<Media[]>([]);
  let knownEntries = $state<Record<string, Entry>>({});
  let total = $state<number | null>(null);
  let hasNext = $state(false);
  let currentPage = $state(1);
  let loading = $state(true);
  let loadingMore = $state(false);
  let error = $state('');
  let retry = $state(0);
  let busyIds = $state<string[]>([]);
  let selected = $state<Media | null>(null);
  let detailLoading = $state(false);
  let libraryReady = $state(false);
  let detailError = $state('');
  let rating = $state<{ entry: Entry; anchor: DOMRect } | null>(null);
  let toast = $state('');
  let toastError = $state(false);
  let requestVersion = 0, detailVersion = 0, filtersVersion = 0, settingsVersion = 0;
  let returnScroll = 0;
  let returnFocus: HTMLElement | null = null;
  let returnFocusLabel = '';
  let returnFocusTitle = '';
  let returnMediaId = '';
  let statisticsReturnFocus: HTMLElement | null = null;
  let statisticsReturnScroll = 0;
  let homeViewVersion = 0;
  type CollectionQuery = { page: 'library' | 'discover'; type: MediaType | ''; status: Status | ''; search: string; sort: string; filters: Record<string, unknown> };
  let collectionRunning = false;
  let pendingCollection: { query: CollectionQuery; version: number; next: number } | null = null;
  let failedCollection = $state<{ query: CollectionQuery; version: number; next: number } | null>(null);
  let toastTimeout: ReturnType<typeof setTimeout>;
  const visibleMedia = $derived(page === 'library' ? entries.map(entry => entry.media) : catalog);
  const selectedEntry = $derived(selected ? knownEntries[mediaKey(selected.id)] : undefined);
  const hasCollectionFilters = $derived(!!search.trim() || (page === 'library' ? !!(status || tag || customList) : page === 'discover' && !!(genre || preset !== 'Popular')));
  const collectionFilters = $derived(page === 'library' ? { sort: librarySort, tag: tag || undefined, customList: customList || undefined } : { genres: genre ? [genre] : [], seasonYear: preset === 'This year' ? new Date().getFullYear() : undefined, releaseStatus: preset === 'Upcoming' ? 'NotYetReleased' : undefined, countryCode: preset === 'Manhwa' ? 'KR' : undefined });

  $effect(() => { try { localStorage.setItem('tanahub-view', JSON.stringify({ layout, coverSize, version: 2 })); } catch { /* Private WebViews may disable storage. */ } });
  $effect(() => {
    if (page !== 'library' && page !== 'discover') { requestVersion += 1; pendingCollection = null; return; }
    const query: CollectionQuery = { page, type, status, search: search.trim(), sort, filters: collectionFilters };
    retry;
    const version = ++requestVersion;
    untrack(() => {
      loading = true; loadingMore = false; error = ''; failedCollection = null; total = null; hasNext = false; currentPage = 1;
      if (query.page === 'library') entries = []; else catalog = [];
    });
    const timer = setTimeout(() => void load(query, version), query.search ? 250 : 0);
    return () => clearTimeout(timer);
  });
  $effect(() => { if (settings) document.documentElement.dataset.theme = settings.theme; });
  $effect(() => {
    const names: Record<PageName, string> = { home: 'Home', library: 'My library', discover: 'Discover', schedule: 'Schedule', recognize: 'Recognize', settings: 'Settings' };
    document.title = `${selected?.title ?? (page === 'home' && homeView === 'statistics' ? 'Statistics' : names[page])} · TanaHub`;
  });
  $effect(() => {
    if (page !== 'settings' || !settings || requestedSettingsSection !== 'library') return;
    requestedSettingsSection = '';
    void tick().then(() => {
      const heading = document.getElementById('library-data-heading');
      heading?.scrollIntoView({ block: 'center' });
      heading?.focus({ preventScroll: true });
    });
  });
  $effect(() => {
    if (page !== 'library') return;
    revision;
    const version = ++filtersVersion;
    void featureApi.libraryFilters().then(result => { if (version === filtersVersion) organizationFilters = result; }).catch(() => {});
  });
  onMount(() => {
    void loadSettings();
    desktopUpdates.start();
    if (location.hash) applyRoute(); else history.replaceState({ tanahub: true }, '', '#library');
    window.addEventListener('popstate', applyRoute);
    document.addEventListener('click', focusActivatedButton, true);
    return () => { desktopUpdates.dispose(); window.removeEventListener('popstate', applyRoute); document.removeEventListener('click', focusActivatedButton, true); clearTimeout(toastTimeout); };
  });
  function focusActivatedButton(event: MouseEvent) {
    // WebKit and accessibility activation may click a button without focusing it.
    const button = event.target instanceof Element ? event.target.closest('button') : null;
    if (button instanceof HTMLElement && !button.matches(':disabled')) button.focus({ preventScroll: true });
  }
  async function load(query: CollectionQuery, version: number, next = 1) {
    if (version !== requestVersion) return;
    if (collectionRunning) { pendingCollection = { query, version, next }; return; }
    collectionRunning = true;
    try {
      if (query.page === 'library') {
        const result = await api.library(query.type, query.status, query.search, next, query.filters);
        if (version !== requestVersion) return;
        entries = next === 1 ? result.items : [...new Map([...entries, ...result.items].map(entry => [mediaKey(entry.mediaId), entry])).values()];
        knownEntries = { ...knownEntries, ...Object.fromEntries(result.items.map(entry => [mediaKey(entry.mediaId), entry])) };
        total = result.totalCount; hasNext = result.hasNextPage;
      } else {
        const result = await api.catalog(query.type, query.search, query.sort, next, query.filters);
        if (version !== requestVersion) return;
        const tracked = await api.lookup(result.items.map(media => media.id));
        if (version !== requestVersion) return;
        const refreshedIds = new Set(result.items.map(media => mediaKey(media.id)));
        knownEntries = { ...Object.fromEntries(Object.entries(knownEntries).filter(([id]) => !refreshedIds.has(id))), ...Object.fromEntries(tracked.items.map(entry => [mediaKey(entry.mediaId), entry])) };
        catalog = next === 1 ? result.items : [...new Map([...catalog, ...result.items].map(media => [mediaKey(media.id), media])).values()];
        total = result.totalCount; hasNext = result.hasNextPage;
      }
      currentPage = next; error = ''; failedCollection = null;
    } catch (reason) {
      if (version === requestVersion) {
        error = reason instanceof Error ? reason.message : 'Could not load titles. Please try again.';
        failedCollection = { query, version, next };
      }
    }
    finally {
      collectionRunning = false;
      if (version === requestVersion) { loading = false; loadingMore = false; }
      const pending = pendingCollection; pendingCollection = null;
      if (pending && pending.version === requestVersion) void load(pending.query, pending.version, pending.next);
    }
  }
  function loadMore() {
    if (loading || loadingMore || (page !== 'library' && page !== 'discover')) return;
    loadingMore = true; void load({ page, type, status, search: search.trim(), sort, filters: collectionFilters }, requestVersion, currentPage + 1);
  }
  function retryCollection() {
    if (loading || loadingMore) return;
    if (failedCollection && failedCollection.version === requestVersion && failedCollection.next > 1) {
      loadingMore = true;
      void load(failedCollection.query, failedCollection.version, failedCollection.next);
    } else { focusMainHeading(); retry += 1; }
  }
  function resetFilters() {
    search = '';
    if (page === 'library') { status = ''; tag = ''; customList = ''; }
    else { genre = ''; if (preset !== 'Popular') { preset = 'Popular'; sort = 'Popularity'; type = 'Anime'; } }
  }
  function pushRoute(route: string) { if (location.hash !== route) history.pushState({ tanahub: true, from: location.hash || '#library' }, '', route); }
  function navigate(next: PageName, push = true) {
    if (next === page && !selected && !(next === 'home' && homeView === 'statistics')) return;
    homeViewVersion += 1;
    requestedSettingsSection = '';
    if (page === 'library' || page === 'discover') collectionViews[page] = { type, search };
    page = next;
    homeView = 'overview';
    if (next === 'library' || next === 'discover') ({ type, search } = collectionViews[next]);
    else search = '';
    selected = null; rating = null; detailVersion += 1;
    if (push) pushRoute(`#${next}`); window.scrollTo(0, 0);
    void tick().then(focusMainHeading);
  }
  function showHomeView(view: 'overview' | 'statistics', push = true) {
    const returning = page === 'home' && homeView === 'statistics' && view === 'overview';
    if (push && view === 'statistics' && page === 'home' && !selected) {
      statisticsReturnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      statisticsReturnScroll = window.scrollY;
    }
    if (page !== 'home') navigate('home', false);
    homeView = view;
    selected = null; rating = null; detailVersion += 1;
    const version = ++homeViewVersion;
    if (push) pushRoute(view === 'statistics' ? '#home/statistics' : '#home');
    if (!returning) window.scrollTo(0, 0);
    void tick().then(() => {
      if (version !== homeViewVersion || page !== 'home' || homeView !== view || selected) return;
      if (returning && statisticsReturnFocus?.isConnected) {
        window.scrollTo(0, statisticsReturnScroll);
        statisticsReturnFocus.focus({ preventScroll: true });
      } else { window.scrollTo(0, 0); focusMainHeading(); }
    });
  }
  function backToHome() {
    if (history.state?.tanahub && history.state.from === '#home') history.back();
    else { history.replaceState({ tanahub: true }, '', '#home'); showHomeView('overview', false); }
  }
  function focusMainHeading() {
    const heading = document.querySelector<HTMLElement>('main h1');
    if (heading) { heading.tabIndex = -1; heading.focus({ preventScroll: true }); }
  }
  function openImport() {
    navigate('settings');
    requestedSettingsSection = 'library';
  }
  async function restoreCollection() {
    await tick(); window.scrollTo(0, returnScroll);
    const original = returnFocus?.isConnected ? returnFocus : returnFocusLabel ? document.querySelector<HTMLElement>(`[aria-label="${CSS.escape(returnFocusLabel)}"]`) : returnFocusTitle ? document.querySelector<HTMLElement>(`button[title="${CSS.escape(returnFocusTitle)}"]`) : null;
    const target = original ?? document.querySelector<HTMLElement>('main input[type="search"]') ?? document.querySelector<HTMLElement>('main h1');
    if (target?.matches('h1')) target.tabIndex = -1;
    target?.focus({ preventScroll: true });
  }
  async function closeDetails() {
    if (history.state?.tanahub && history.state.from) history.back();
    else { detailVersion += 1; selected = null; rating = null; history.replaceState({ tanahub: true }, '', `#${page}`); await restoreCollection(); }
  }
  function applyRoute() {
    const route = location.hash.slice(1);
    if (route.startsWith('title/')) { try { void openId(decodeURIComponent(route.slice(6)), false); } catch { navigate('library', false); } }
    else if (route === 'home/statistics') showHomeView('statistics', false);
    else if (['home', 'library', 'discover', 'schedule', 'recognize', 'settings'].includes(route)) {
      const next = route as PageName; const wasDetail = !!selected;
      selected = null; rating = null; detailVersion += 1;
      if (page !== next) navigate(next, false);
      else if (next === 'home' && homeView === 'statistics') showHomeView('overview', false);
      else if (wasDetail) void restoreCollection();
    }
  }
  async function loadSettings() {
    const version = ++settingsVersion; settingsLoading = true; settingsError = '';
    try { const result = await featureApi.settings(); if (version === settingsVersion) settings = result; }
    catch (reason) { if (version === settingsVersion) settingsError = reason instanceof Error ? reason.message : 'Could not load settings.'; }
    finally { if (version === settingsVersion) settingsLoading = false; }
  }
  async function settingsAction<T>(name: string, action: () => Promise<T>): Promise<T> {
    if (settingsBusy) throw new Error('A settings action is still in progress.'); settingsBusy = name;
    try { return await action(); } finally { settingsBusy = ''; }
  }
  async function saveSettings(patch: SettingsPatch) { await settingsAction('save', async () => { settings = await featureApi.saveSettings(patch); if (patch.preferredSyncSource) retry += 1; }); }
  function libraryChanged() { knownEntries = {}; revision += 1; retry += 1; }
  function notify(message: string, isError = false) { toast = message; toastError = isError; clearTimeout(toastTimeout); toastTimeout = setTimeout(() => toast = '', isError ? 6500 : 3500); }
  function merge(entry: Entry) {
    revision += 1;
    const isNewEntry = !knownEntries[mediaKey(entry.mediaId)];
    if (isNewEntry && page === 'library') retry += 1;
    knownEntries = { ...knownEntries, [mediaKey(entry.mediaId)]: entry }; entries = entries.map(item => mediaKey(item.mediaId) === mediaKey(entry.mediaId) ? entry : item);
    if (page === 'library' && status && entry.status !== status && entries.some(item => mediaKey(item.mediaId) === mediaKey(entry.mediaId))) { entries = entries.filter(item => mediaKey(item.mediaId) !== mediaKey(entry.mediaId)); if (total !== null) total -= 1; if (hasNext) retry += 1; }
    if (page === 'library' && (search.trim() || tag || customList || librarySort !== 'Updated') && entries.some(item => mediaKey(item.mediaId) === mediaKey(entry.mediaId))) retry += 1;
  }
  async function mutate(id: string, action: () => Promise<Entry>, message?: string) {
    if (busyIds.includes(mediaKey(id))) throw new Error('This title is still saving. Please wait a moment.'); busyIds = [...busyIds, mediaKey(id)];
    try { const entry = await action(); merge(entry); if (message) notify(message); } finally { busyIds = busyIds.filter(item => item !== mediaKey(id)); }
  }
  async function cardAction(action: () => Promise<void>) { try { await action(); } catch (reason) { notify(reason instanceof Error ? reason.message : 'Could not save this change.', true); } }
  async function openId(id: string, push = true) {
    const cached = knownEntries[mediaKey(id)]?.media ?? catalog.find(media => mediaKey(media.id) === mediaKey(id));
    await openDetails(cached ?? { id, title: id, romaji: '', type: 'Anime', format: 'Unknown', releaseStatus: 'Unknown', year: null, averageScore: null, totalUnits: null, posterUrl: null, bannerUrl: null, description: null, genres: [] }, push);
  }
  async function openDetails(media: Media, push = true) {
    if (!selected) { returnScroll = window.scrollY; returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null; returnFocusLabel = returnFocus?.getAttribute('aria-label') ?? ''; returnFocusTitle = returnFocus?.getAttribute('title') ?? ''; returnMediaId = media.id; }
    if (push) pushRoute(`#title/${encodeURIComponent(media.id)}`); window.scrollTo(0, 0);
    selected = media; detailLoading = true; libraryReady = false; detailError = '';
    const version = ++detailVersion;
    const results = await Promise.allSettled([api.detail(media.id), api.entry(media.id)]);
    if (version !== detailVersion || selected?.id !== media.id) return;
    if (results[0].status === 'fulfilled') selected = results[0].value; else detailError = 'Could not refresh catalog details. Showing available information.';
    if (results[1].status === 'fulfilled') {
      libraryReady = true;
      const entry = results[1].value;
      if (entry) {
        merge(entry);
        if (results[0].status === 'rejected') selected = entry.media;
      } else { const { [mediaKey(media.id)]: ignored, ...rest } = knownEntries; knownEntries = rest; }
    } else detailError = 'Could not load library status. Please try again.';
    detailLoading = false;
  }
  async function removeSelected() {
    if (!selected) return; const id = selected.id; busyIds = [...busyIds, mediaKey(id)];
    try {
      const result = await api.remove(id); if (!result.removed) throw new Error('This title could not be removed. Please try again.');
      clearDrafts(id);
      const wasListed = entries.some(entry => mediaKey(entry.mediaId) === mediaKey(id)); const { [mediaKey(id)]: ignored, ...rest } = knownEntries; knownEntries = rest;
      entries = entries.filter(entry => mediaKey(entry.mediaId) !== mediaKey(id));
      if (mediaKey(id) === mediaKey(returnMediaId)) { returnFocus = null; returnFocusLabel = ''; returnFocusTitle = ''; }
      if (wasListed && page === 'library' && total !== null) total -= 1;
      if (wasListed && page === 'library' && hasNext) retry += 1;
      revision += 1; if (selected?.id === id) await closeDetails(); notify('Removed from your library');
    } finally { busyIds = busyIds.filter(item => item !== mediaKey(id)); }
  }
</script>

<a class="skip-link" href="#main-content" onclick={(event) => { event.preventDefault(); focusMainHeading(); }}>Skip to content</a>
<div class="app-shell">
  <Sidebar {page} onNavigate={navigate} />
  <main id="main-content">
    <UpdateNotice inSettings={page === 'settings' && !selected} busy={!!settingsBusy || busyIds.length > 0} onDismiss={focusMainHeading} />
    {#if selected}
      {#key mediaKey(selected.id)}
        <TitlePage media={selected} entry={selectedEntry} loading={detailLoading} {libraryReady} busy={busyIds.includes(mediaKey(selected.id))} loadError={detailError} onClose={() => void closeDetails()} onRetry={() => void openDetails(selected!, false)} onAdd={() => mutate(selected!.id, () => api.add(selected!.id), 'Saved to your library')} onProgress={() => mutate(selected!.id, () => api.progress(selected!.id), 'Progress saved')} onProgressSet={(progress) => mutate(selected!.id, () => featureApi.progressSet(selected!.id, progress), 'Progress saved')} onScore={(score) => mutate(selected!.id, () => api.score(selected!.id, score), 'Rating saved')} onStatus={(newStatus) => mutate(selected!.id, () => api.status(selected!.id, newStatus), 'Status saved')} onNotes={(notes) => mutate(selected!.id, () => api.notes(selected!.id, notes), 'Notes saved')} onReview={(review) => mutate(selected!.id, () => featureApi.review(selected!.id, review), 'Review saved')} onOrganization={(tags, lists) => mutate(selected!.id, () => featureApi.organization(selected!.id, tags, lists), 'Organization saved')} onDates={(startedAt, completedAt) => mutate(selected!.id, () => featureApi.dates(selected!.id, startedAt, completedAt), 'Dates saved')} onRemove={removeSelected} onOpenRelated={(id) => void openId(id)} />
      {/key}
    {/if}
    <div hidden={!!selected} inert={!!selected}>
    {#if page === 'home'}
      {#if homeView === 'statistics'}<StatisticsPage bind:type={homeType} {revision} active={!selected} onBack={backToHome} />{/if}
      <div hidden={homeView === 'statistics'} inert={homeView === 'statistics'}>
        <HomePage bind:type={homeType} {revision} active={!selected && homeView === 'overview'} onStatistics={() => showHomeView('statistics')} onOpen={(media) => void openDetails(media)} onProgress={(entry) => mutate(entry.mediaId, () => api.progress(entry.mediaId), 'Progress saved')} onRate={(entry, anchor) => rating = { entry, anchor }} onNavigate={navigate} />
      </div>
    {:else if page === 'schedule'}
      <SchedulePage {revision} active={!selected} onOpenId={(id) => void openId(id)} />
    {:else if page === 'recognize'}
      <RecognizePage active={!selected} onOpenId={(id) => void openId(id)} onAddId={(id) => mutate(id, () => api.add(id), 'Saved to your library')} onNavigate={navigate} />
    {:else if page === 'settings'}
      <SettingsPage data={settings} loading={settingsLoading} loadError={settingsError} busy={settingsBusy} onReload={() => void loadSettings()} onSave={saveSettings} onConnect={(id, secret) => settingsAction('connect', async () => { settings = await featureApi.connect(id, secret); notify('AniList connected'); })} onSync={() => settingsAction('sync', async () => { const result = await featureApi.sync(); settings = result.settings; libraryChanged(); return result; })} onDisconnect={() => settingsAction('disconnect', async () => { settings = await featureApi.disconnect(); })} onLibraryChanged={libraryChanged} />
    {:else}
      <div class="page-enter">
        <CollectionToolbar {page} bind:type bind:status bind:search bind:sort bind:layout bind:coverSize bind:genre bind:preset bind:librarySort bind:tag bind:customList emptyLibrary={page === 'library' && !type && total === 0 && !hasCollectionFilters} tags={organizationFilters.tags} customLists={organizationFilters.customLists} {total} />
        <section class="collection" aria-label={page === 'library' ? 'Your library titles' : 'Catalog titles'} aria-busy={loading}>
          {#if error}<div class="load-error" role="alert"><AlertCircle size={20} /><div><strong>{failedCollection && failedCollection.next > 1 ? 'Could not load more titles' : 'Could not load your titles'}</strong><p>{error}</p></div><button class="ghost-button" disabled={loading || loadingMore} onclick={retryCollection}><RefreshCw size={15} />{loadingMore ? 'Retrying…' : 'Try again'}</button></div>{/if}
          {#if loading}<div class="media-grid skeleton-grid" style:--cover-size={`${coverSize}px`} role="status" aria-label="Loading titles">{#each Array(8) as _}<div class="skeleton-card"><div></div><span></span><span></span></div>{/each}</div>
          {:else if !visibleMedia.length && !error}
            <div class="empty-state"><div class="empty-icon">{#if hasCollectionFilters}<SearchX size={32} strokeWidth={1.3} />{:else if page === 'discover'}<Compass size={32} strokeWidth={1.3} />{:else}<BookOpen size={32} strokeWidth={1.3} />{/if}</div><h2>{hasCollectionFilters ? 'No matching titles' : page === 'discover' ? 'No titles found' : 'Your library is empty'}</h2><p>{hasCollectionFilters ? 'Try changing or resetting your filters.' : page === 'discover' ? 'Try another search or media type.' : 'Find a title in Discover, or import an existing list.'}</p>{#if hasCollectionFilters}<button class="ghost-button" onclick={resetFilters}>Reset filters</button>{:else if page === 'library'}<div class="empty-actions"><button class="primary-button" onclick={() => navigate('discover')}><Compass size={16} />Discover titles</button><button class="ghost-button" onclick={openImport}>Import a list</button></div><p class="onboarding-note">No account required. Your library stays on this device.</p>{/if}</div>
          {:else if !error || visibleMedia.length}
            <div class:media-grid={layout === 'grid'} class:media-list={layout === 'list'} style:--cover-size={`${coverSize}px`}>
              {#each visibleMedia as media (mediaKey(media.id))}<MediaCard {media} entry={knownEntries[mediaKey(media.id)]} {layout} busy={busyIds.includes(mediaKey(media.id))} onOpen={() => void openDetails(media)} onProgress={() => cardAction(() => mutate(media.id, () => api.progress(media.id), 'Progress saved'))} onRate={(anchor) => rating = { entry: knownEntries[mediaKey(media.id)], anchor }} onAdd={() => cardAction(() => mutate(media.id, () => api.add(media.id), 'Saved to your library'))} />{/each}
            </div>
            {#if hasNext}<div class="load-more"><button class="ghost-button" disabled={loadingMore} onclick={loadMore}>{loadingMore ? 'Loading…' : 'Load more titles'}</button></div>{/if}
          {/if}
        </section>
      </div>
    {/if}
    </div>
  </main>
</div>
{#if rating}<RatingDialog entry={rating.entry} onClose={() => rating = null} onSave={(score) => { const id = rating!.entry.mediaId; return mutate(id, () => api.score(id, score), 'Rating saved'); }} />{/if}
{#if toast}<div class="toast" class:error-toast={toastError} role={toastError ? 'alert' : 'status'}>{#if toastError}<AlertCircle size={16} />{:else}<Check size={16} />{/if}<span>{toast}</span><button class="icon-button" aria-label="Dismiss notification" onclick={() => toast = ''}><X size={15} /></button></div>{/if}
