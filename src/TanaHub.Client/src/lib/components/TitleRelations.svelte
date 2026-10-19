<script lang="ts">
  import { untrack } from 'svelte';
  import { ArrowUpRight, BookOpen, RefreshCw } from '@lucide/svelte';
  import { featureApi } from '../feature-api';
  import { formatLabel } from '../types';
  import type { RelatedTitle, RelatedTitlesResult } from '../feature-types';
  let { mediaId, loading = false, busy = false, onOpen }:
    { mediaId: string; loading?: boolean; busy?: boolean; onOpen: (id: string) => void } = $props();
  let result = $state<RelatedTitlesResult | null>(null);
  let fetching = $state(false);
  let error = $state('');
  let failedPosters = $state<string[]>([]);
  let activeId = '';
  let generation = 0;
  const labels: Record<string, string> = { Prequel: 'Previous part', Sequel: 'Next part', Source: 'Source material', Adaptation: 'Adaptation', Parent: 'Main story', SideStory: 'Side story', SpinOff: 'Spin-off', Alternative: 'Alternative version', Summary: 'Recap', Compilation: 'Compilation', Contains: 'Contains', SameUniverse: 'Same universe', Character: 'Shared characters', Colored: 'Colour edition', Monochrome: 'Monochrome edition', Doujinshi: 'Fan work', Serialization: 'Serialization', PreSerialization: 'Pilot version' };
  const groupNames = ['Previous & next parts', 'Source & adaptations', 'Side stories & spin-offs', 'Other versions & connections', 'Fan works'];
  function groupIndex(relation: string) { return ['Prequel', 'Sequel'].includes(relation) ? 0 : ['Source', 'Adaptation'].includes(relation) ? 1 : ['Parent', 'SideStory', 'SpinOff'].includes(relation) ? 2 : relation === 'Doujinshi' ? 4 : 3; }
  function relationOrder(relation: string) { return relation === 'Prequel' ? 0 : relation === 'Sequel' ? 1 : 2; }
  const groups = $derived(groupNames.map((title, index) => ({ title, index, titles: (result?.titles ?? []).filter(item => groupIndex(item.relation) === index).sort((a, b) => relationOrder(a.relation) - relationOrder(b.relation) || (a.year ?? 9999) - (b.year ?? 9999) || a.title.localeCompare(b.title)) })).filter(group => group.titles.length));

  $effect(() => {
    const id = mediaId;
    const waitForDetails = loading;
    untrack(() => {
      if (id !== activeId) { activeId = id; result = null; failedPosters = []; error = ''; }
      if (!waitForDetails) void load(id, false);
    });
    return () => { generation += 1; };
  });

  async function load(id: string, force: boolean) {
    const requestGeneration = ++generation;
    fetching = true;
    error = '';
    try {
      const response = await featureApi.relations(id, force);
      if (generation === requestGeneration && activeId === id) result = response;
    } catch (reason) {
      if (generation === requestGeneration && activeId === id) error = reason instanceof Error ? reason.message : 'Could not load related titles.';
    } finally {
      if (generation === requestGeneration && activeId === id) fetching = false;
    }
  }
  function metadata(title: RelatedTitle) {
    const count = title.mediaType === 'Anime' ? title.episodeCount : title.chapterCount;
    return [title.format !== 'Unknown' ? formatLabel(title.format) : title.mediaType, title.year, count ? `${count} ${title.mediaType === 'Anime' ? 'ep.' : 'ch.'}` : null].filter(Boolean).join(' · ');
  }
</script>

<section class="relations-panel" aria-labelledby="relations-heading" aria-busy={fetching || loading}>
  <div class="section-heading"><h2 id="relations-heading">Related titles</h2><button class="refresh-button" onclick={() => load(mediaId, true)} disabled={fetching || loading || busy} aria-label="Refresh related titles"><RefreshCw size={13} class={fetching ? 'spinning' : ''} />{fetching ? 'Loading…' : 'Refresh'}</button></div>
  {#if error}<div class="relation-notice error-notice" role="alert"><p>{error}</p><button class="ghost-button" onclick={() => load(mediaId, true)} disabled={fetching || loading || busy}>Try again</button></div>{/if}
  {#if result?.notice || result?.isStale}<p class="relation-notice" role="status">{result.notice ?? 'Showing saved connections. Refresh to check for updates.'}</p>{/if}
  {#if (fetching || loading) && !result}<div class="relation-skeleton" role="status"><span></span><div><span></span><span></span></div><span class="visually-hidden">Loading related titles…</span></div>{/if}
  {#if result && !result.titles.length && !error}<p class="empty-relations">No related titles listed by this source.</p>{/if}
  {#each groups as group (group.title)}
    <details open={group.index < 3 && group.titles.length <= 6}>
      <summary>{group.title}<span class="mono">{group.titles.length}</span></summary>
      <div class="related-grid">
        {#each group.titles as title (`${title.mediaId}:${title.relation}`)}
          <button class="related-title" onclick={() => onOpen(title.mediaId)} disabled={busy} title={title.title} aria-label={`Open ${title.title}, ${labels[title.relation] ?? 'Related title'}, ${metadata(title)}`}>
            <span class="relation-poster">{#if title.posterUrl && !failedPosters.includes(title.mediaId)}<img src={title.posterUrl} alt="" loading="lazy" onerror={() => failedPosters = [...failedPosters, title.mediaId]} />{:else}<BookOpen size={18} />{/if}</span>
            <span class="relation-info"><span class="relation-kind">{labels[title.relation] ?? 'Related title'}</span><strong>{title.title}</strong><span class="relation-meta" title={`${metadata(title)}${title.source ? ` · ${title.source}` : ''}`}>{metadata(title)}{#if title.source}<span class="relation-source"> · {title.source}</span>{/if}</span></span>
            <ArrowUpRight size={14} />
          </button>
        {/each}
      </div>
    </details>
  {/each}
</section>

<style>
  .relations-panel { border: 1px solid var(--border); border-radius: 10px; background: var(--shell); padding: 22px; }
  .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 14px; }
  h2 { font-size: 15px; font-weight: 600; margin: 0; letter-spacing: -.015em; }
  .refresh-button { display: inline-flex; align-items: center; gap: 5px; padding: 6px; background: transparent; border: none; color: var(--secondary); font-size: 11px; }
  .refresh-button:hover:enabled { color: var(--text); }
  .relation-notice { color: var(--secondary); font-size: 12px; line-height: 1.6; margin: 0 0 14px; }
  .error-notice { display: flex; align-items: center; justify-content: space-between; gap: 12px; }.error-notice p { color: var(--red); margin: 0; }.error-notice button { flex-shrink: 0; }
  .empty-relations { color: var(--secondary); font-size: 12px; margin: 8px 0 0; line-height: 1.6; }
  details + details { margin-top: 18px; }
  summary { cursor: pointer; display: flex; align-items: center; gap: 8px; color: var(--secondary); font-size: 11px; font-weight: 600; padding: 8px 0; list-style: none; }
  summary::-webkit-details-marker { display: none; } summary::before { content: '›'; font-size: 17px; width: 10px; display: inline-block; transition: transform 150ms var(--ease); } details[open] summary::before { transform: rotate(90deg); }
  summary .mono { color: var(--muted); font-size: 10px; font-weight: 400; } summary:focus-visible { outline: 2px solid var(--lavender); outline-offset: 3px; border-radius: 3px; }
  .related-grid { display: grid; grid-template-columns: repeat(auto-fit,minmax(min(100%,240px),1fr)); gap: 8px; }
  .related-title { display: flex; align-items: center; gap: 10px; padding: 8px; width: 100%; min-width: 0; text-align: left; border: 1px solid var(--border); color: var(--text); background: transparent; border-radius: 7px; }
  .related-title:hover:enabled { border-color: var(--border-lit); background: var(--raised); }.related-title > :global(svg) { color: var(--muted); margin-left: auto; }
  .relation-poster { width: 40px; height: 60px; flex-shrink: 0; border-radius: 4px; overflow: hidden; display: grid; place-items: center; background: var(--raised); color: var(--secondary); }.relation-poster img { width: 100%; height: 100%; object-fit: cover; }
  .relation-info { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 0; }.relation-info strong { font-size: 12px; font-weight: 600; line-height: 1.4; height: 2.8em; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; }
  .relation-kind { font-size: 9px; color: var(--lavender); line-height: 1.4; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }.relation-meta { font-size: 10px; color: var(--secondary); line-height: 1.4; height: 2.8em; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; }
  .relation-skeleton { display: flex; align-items: center; gap: 12px; padding: 12px 0; }.relation-skeleton > span:first-child { width: 40px; height: 60px; border-radius: 4px; }.relation-skeleton div { width: 55%; display: grid; gap: 10px; }.relation-skeleton div span { height: 9px; border-radius: 3px; }.relation-skeleton div span:last-child { width: 60%; }.relation-skeleton > span:first-child, .relation-skeleton div span { background: var(--raised); animation: pulse 1.5s ease-in-out infinite; }
  .visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
  .refresh-button :global(.spinning) { animation: spin 1s linear infinite; } @keyframes spin { to { transform: rotate(360deg); } }
  @media (max-width: 640px) { .relations-panel { padding: 18px; } .error-notice { flex-wrap: wrap; } }
</style>
