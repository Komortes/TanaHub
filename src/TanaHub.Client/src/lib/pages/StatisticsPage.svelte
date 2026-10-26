<script lang="ts">
  import { ArrowLeft, RefreshCw, AlertCircle } from '@lucide/svelte';
  import { featureApi } from '../feature-api';
  import type { HomeSummary } from '../feature-types';
  import type { MediaType } from '../types';
  import InsightsPanel from './InsightsPanel.svelte';

  let { type = $bindable<MediaType | ''>(''), revision = 0, active = true, onBack }:
    { type?: MediaType | ''; revision?: number; active?: boolean; onBack: () => void } = $props();
  let insights = $state<HomeSummary['insights'] | null>(null);
  let loading = $state(true);
  let error = $state('');
  let refresh = $state(0);
  let heading: HTMLHeadingElement;
  let generation = 0;
  let requestedType: MediaType | undefined;

  $effect(() => {
    if (!active) return;
    const selectedType = type || undefined;
    if (selectedType !== requestedType) { insights = null; requestedType = selectedType; }
    revision; refresh;
    const current = ++generation;
    loading = true; error = '';
    void featureApi.home(selectedType)
      .then(result => { if (current === generation) insights = result.insights; })
      .catch(reason => { if (current === generation) error = reason instanceof Error ? reason.message : 'Could not load library statistics.'; })
      .finally(() => { if (current === generation) loading = false; });
    return () => { if (generation === current) generation += 1; };
  });
  function retry() { heading?.focus({ preventScroll: true }); refresh += 1; }
</script>

<section class="statistics-page" aria-label="Statistics">
  <button class="back-link" onclick={onBack}><ArrowLeft size={15} />Back to Home</button>
  <header class="statistics-header"><h1 bind:this={heading} tabindex="-1">Statistics</h1><button class="ghost-button refresh-statistics" aria-label="Refresh statistics" onclick={() => refresh += 1} disabled={loading}><RefreshCw size={15} /><span>Refresh</span></button></header>
  <div class="statistics-type" aria-label="Statistics media type">{#each ['', 'Anime', 'Manga'] as item}<button aria-pressed={type === item} class:selected={type === item} onclick={() => type = item as MediaType | ''}>{item || 'All titles'}</button>{/each}<span>{insights?.totalEntries ?? '—'} {insights?.totalEntries === 1 ? 'title' : 'titles'} tracked</span></div>
  {#if error}<div class="load-error" role="alert"><AlertCircle size={20} /><div><strong>Statistics could not load</strong><p>{error}</p></div><button class="ghost-button" onclick={retry}>Try again</button></div>{/if}
  <div aria-busy={loading}>
    {#if insights}<InsightsPanel {insights} mode="both" />
    {:else if loading}<p class="statistics-notice" role="status">Loading library statistics…</p>{/if}
  </div>
</section>

<style>
  .statistics-page { max-width: 1100px; }
  .back-link { display: inline-flex; align-items: center; gap: 7px; min-height: 44px; padding: 8px 0; margin-bottom: 12px; background: transparent; border: 0; color: var(--secondary); font-size: 12px; }
  .back-link:hover { color: var(--text); }
  .statistics-header { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 20px; }
  h1 { font-size: 30px; font-weight: 650; letter-spacing: -.035em; line-height: 1.2; margin: 0; }
  .refresh-statistics { min-height: 44px; }
  .statistics-type { display: flex; align-items: center; flex-wrap: wrap; gap: 4px; padding-bottom: 26px; }
  .statistics-type button { background: transparent; color: var(--secondary); border: 0; border-radius: 0; border-bottom: 2px solid transparent; min-height: 44px; padding: 11px 14px; font-size: 12px; }
  .statistics-type button:first-child { padding-left: 0; padding-right: 16px; }
  .statistics-type button.selected { color: var(--text); border-bottom-color: var(--bookmark); }
  .statistics-type > span { color: var(--muted); font-size: 12px; margin-left: 16px; }
  .statistics-notice { color: var(--secondary); font-size: 12px; line-height: 1.6; margin: 0; }
  @media (max-width: 640px) { h1 { font-size: 28px; } .statistics-type > span { flex-basis: 100%; margin: 10px 0 0; } .refresh-statistics { min-width: 44px; padding: 12px; } .refresh-statistics > span { display: none; } }
</style>
