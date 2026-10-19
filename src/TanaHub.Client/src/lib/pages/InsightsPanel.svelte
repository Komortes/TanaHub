<script lang="ts">
  import { CircleCheck, Layers, CircleSlash, Star, Clock3, BookOpen } from '@lucide/svelte';
  import type { HomeSummary } from '../feature-types';
  let { insights, mode = 'both' }: { insights: HomeSummary['insights']; mode?: 'metrics' | 'charts' | 'both' } = $props();
  const percent = (ratio: number) => `${Math.round(ratio * 100)}%`;
  const watchTime = (minutes: number) => minutes >= 60 ? `${Math.floor(minutes / 60).toLocaleString()}h ${minutes % 60}m` : `${minutes}m`;
  const metrics = $derived([
    { label: 'Completion', value: percent(insights.completionRate), detail: `${insights.completedEntries} of ${insights.totalEntries} titles finished`, icon: CircleCheck, color: 'var(--green)' },
    { label: 'Backlog', value: insights.backlogEntries.toLocaleString(), detail: 'Titles saved for later', icon: Layers, color: 'var(--lavender)' },
    { label: 'Dropped', value: percent(insights.droppedRatio), detail: `${insights.droppedEntries} titles dropped`, icon: CircleSlash, color: 'var(--red)' },
    { label: 'Average rating', value: insights.averageScore === null ? '—' : `${insights.averageScore.toFixed(1)} / 10`, detail: 'Your personal ratings', icon: Star, color: 'var(--amber)' },
    { label: 'Watch time', value: watchTime(insights.estimatedWatchMinutes), detail: `${insights.episodesWatched.toLocaleString()} episodes watched · estimated`, icon: Clock3, color: 'var(--cyan)' },
    { label: 'Chapters read', value: insights.chaptersRead.toLocaleString(), detail: 'Across your manga library', icon: BookOpen, color: 'var(--lavender)' },
  ]);
  const charts = $derived([
    { heading: 'By media type', segments: insights.byMediaType },
    { heading: 'By library status', segments: insights.byStatus },
  ]);
  const label = (key: string) => key === 'Watching' ? 'In progress' : key === 'Repeating' ? 'Revisiting' : key;
</script>

{#if mode !== 'charts'}<section aria-label="Library statistics" class="metrics">
  {#each metrics as metric}
    <article class="metric"><div class="metric-label"><span style:color={metric.color}><metric.icon size={17} strokeWidth={1.7} /></span><h2>{metric.label}</h2></div><strong class="mono">{metric.value}</strong><p>{metric.detail}</p></article>
  {/each}
</section>{/if}
{#if mode !== 'metrics'}<div class="charts" class:stacked={mode === 'charts'}>
  {#each charts as chart}
    <section class="chart-panel" aria-label={chart.heading}>
      <h2>{chart.heading}</h2>
      {#if !chart.segments.length}<p class="chart-empty">Add titles to see your library take shape.</p>
      {:else}
        <ul>{#each chart.segments as segment, index}<li><div><span>{label(segment.key)}</span><strong class="mono">{segment.count}</strong></div><span class="bar-track" aria-hidden="true"><span style:width={`${Math.max(0, Math.min(100, segment.count / Math.max(1, insights.totalEntries) * 100))}%`} style:background={['var(--violet)', 'var(--cyan)', 'var(--amber)', 'var(--green)', 'var(--red)', 'var(--lavender)'][index % 6]}></span></span></li>{/each}</ul>
      {/if}
    </section>
  {/each}
</div>{/if}

<style>
  .metrics { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 12px; margin-bottom: 28px; }
  .metric, .chart-panel { border: 1px solid var(--border); border-radius: 10px; background: var(--shell); padding: 16px; min-width: 0; }
  .metric-label { display: flex; align-items: center; gap: 8px; }
  .metric-label > span { display: flex; }
  h2 { margin: 0; font-size: 12px; font-weight: 500; color: var(--secondary); }
  .metric > strong { display: block; font-size: 24px; letter-spacing: -.025em; line-height: 1.3; font-weight: 600; margin: 12px 0 6px; overflow-wrap: anywhere; }
  p { font-size: 11px; color: var(--muted); line-height: 1.5; margin: 0; }
  .charts { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 14px; margin-top: 28px; }
  .charts.stacked { grid-template-columns: minmax(0,1fr); margin-top: 0; }
  .chart-panel > h2 { color: var(--text); font-size: 13px; font-weight: 600; margin-bottom: 16px; }
  ul { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 14px; }
  li > div { display: flex; justify-content: space-between; gap: 12px; font-size: 12px; color: var(--secondary); }
  li strong { color: var(--text); font-size: 11px; font-weight: 500; }
  .bar-track { display: block; height: 5px; border-radius: 8px; overflow: hidden; background: var(--border); margin-top: 7px; }
  .bar-track > span { display: block; height: 100%; border-radius: inherit; transition: width 240ms var(--ease); }
  .chart-empty { margin-top: 20px; }
  @media (max-width: 640px) { .metrics { grid-template-columns: repeat(2,minmax(0,1fr)); gap: 10px; } .metric { padding: 14px; } .metric > strong { font-size: 20px; } .charts { grid-template-columns: minmax(0,1fr); } }
</style>
