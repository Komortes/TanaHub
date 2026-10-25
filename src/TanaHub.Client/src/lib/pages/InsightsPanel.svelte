<script lang="ts">
  import type { HomeSummary } from '../feature-types';
  let { insights, mode = 'both' }: { insights: HomeSummary['insights']; mode?: 'metrics' | 'charts' | 'both' } = $props();
  const percent = (ratio: number) => `${Math.round(ratio * 100)}%`;
  const watchTime = (minutes: number) => minutes >= 60 ? `${Math.floor(minutes / 60).toLocaleString()}h ${minutes % 60}m` : `${minutes}m`;
  const metrics = $derived([
    { label: 'Completion', value: percent(insights.completionRate), detail: `${insights.completedEntries} of ${insights.totalEntries} ${insights.totalEntries === 1 ? 'title' : 'titles'} finished` },
    { label: 'Backlog', value: insights.backlogEntries.toLocaleString(), detail: 'Titles saved for later' },
    { label: 'Dropped', value: percent(insights.droppedRatio), detail: `${insights.droppedEntries} ${insights.droppedEntries === 1 ? 'title' : 'titles'} dropped` },
    { label: 'Average rating', value: insights.averageScore === null ? '—' : `${insights.averageScore.toFixed(1)} / 10`, detail: 'Your personal ratings' },
    { label: 'Watch time', value: watchTime(insights.estimatedWatchMinutes), detail: `${insights.episodesWatched.toLocaleString()} ${insights.episodesWatched === 1 ? 'episode' : 'episodes'} watched · estimated` },
    { label: 'Chapters read', value: insights.chaptersRead.toLocaleString(), detail: 'Across your manga library' },
  ]);
  const charts = $derived([
    { heading: 'By media type', segments: insights.byMediaType },
    { heading: 'By library status', segments: insights.byStatus },
  ]);
  const label = (key: string) => key === 'Watching' ? 'In progress' : key === 'Repeating' ? 'Revisiting' : key;
</script>

{#if mode !== 'charts'}<section aria-label="Library statistics" class="metrics">
  <h2>Library statistics</h2>
  <dl class="metric-grid">{#each metrics as metric}
    <div class="metric"><dt>{metric.label}</dt><dd><strong class="mono">{metric.value}</strong><p>{metric.detail}</p></dd></div>
  {/each}</dl>
</section>{/if}
{#if mode !== 'metrics'}<div class="charts" class:stacked={mode === 'charts'}>
  {#each charts as chart}
    <section class="chart-panel" aria-label={chart.heading}>
      <h2>{chart.heading}</h2>
      {#if !chart.segments.length}<p class="chart-empty">Add titles to see your library take shape.</p>
      {:else}
        <ul>{#each chart.segments as segment}<li><div><span>{label(segment.key)}</span><strong class="mono">{segment.count}</strong></div><span class="bar-track" aria-hidden="true"><span style:width={`${Math.max(0, Math.min(100, segment.count / Math.max(1, insights.totalEntries) * 100))}%`}></span></span></li>{/each}</ul>
      {/if}
    </section>
  {/each}
</div>{/if}

<style>
  h2 { margin: 0; font-size: 14px; font-weight: 600; color: var(--text); }
  .metrics { border-top: 1px solid var(--border); padding-top: 22px; }
  .metric-grid { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 22px 24px; margin: 20px 0 0; }
  .metric { min-width: 0; }
  dt { font-size: 12px; color: var(--secondary); }
  dd { margin: 6px 0 0; }
  .metric strong { display: block; font-size: 19px; line-height: 1.4; font-weight: 500; overflow-wrap: anywhere; }
  p { font-size: 11px; color: var(--muted); line-height: 1.6; margin: 4px 0 0; }
  .charts { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 24px; margin-top: 28px; }
  .charts.stacked { grid-template-columns: minmax(0,1fr); margin-top: 0; }
  .chart-panel { min-width: 0; border-top: 1px solid var(--border); padding-top: 20px; }
  .chart-panel > h2 { font-size: 13px; margin-bottom: 18px; }
  ul { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 14px; }
  li > div { display: flex; justify-content: space-between; gap: 12px; font-size: 12px; color: var(--secondary); }
  li strong { color: var(--text); font-size: 11px; font-weight: 500; }
  .bar-track { display: block; height: 3px; overflow: hidden; background: var(--border); margin-top: 7px; }
  .bar-track > span { display: block; height: 100%; background: var(--cyan); transition: width 240ms var(--ease); }
  .chart-empty { margin-top: 20px; }
  @media (max-width: 640px) { .metric-grid { grid-template-columns: repeat(2,minmax(0,1fr)); gap: 20px 16px; } .metric strong { font-size: 18px; } .charts { grid-template-columns: minmax(0,1fr); } }
</style>
