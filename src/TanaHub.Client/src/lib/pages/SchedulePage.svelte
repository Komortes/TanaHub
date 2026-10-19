<script lang="ts">
  import { CalendarDays, Clock3, RefreshCw, BookMarked, AlertCircle } from '@lucide/svelte';
  import { featureApi } from '../feature-api';
  import { formatLabel, statusLabel } from '../types';
  import type { ScheduleResult } from '../feature-types';
  import MiniCover from './MiniCover.svelte';
  import { dayKey, offsetDay, scheduleWindow, type ScheduleRange } from './schedule-time';

  let { onOpenId, revision = 0, active = true }: { onOpenId: (id: string) => void; revision?: number; active?: boolean } = $props();
  const localZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  const zones = [...new Set([localZone, 'UTC', 'Europe/Prague', 'Asia/Tokyo', 'America/New_York'])];
  const ranges: ScheduleRange[] = ['Today', 'Tomorrow', 'This week', 'Next week'];
  let zone = $state(localZone);
  let range = $state<ScheduleRange>('Today');
  let libraryOnly = $state(false);
  let result = $state<ScheduleResult | null>(null);
  let loading = $state(true);
  let error = $state('');
  let notificationError = $state('');
  let refresh = $state(0);
  let generation = 0;
  const time = (value: string) => new Intl.DateTimeFormat(undefined, { timeZone: zone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(value));
  const heading = (value: string) => {
    const date = new Date(value);
    const key = dayKey(date, zone);
    const today = dayKey(new Date(), zone);
    return key === today ? 'Today' : key === offsetDay(today, 1) ? 'Tomorrow' : new Intl.DateTimeFormat(undefined, { timeZone: zone, weekday: 'long', month: 'short', day: 'numeric' }).format(date);
  };
  const groups = $derived.by(() => {
    const grouped = new Map<string, ScheduleResult['items']>();
    for (const item of [...(result?.items ?? [])].sort((a, b) => Date.parse(a.airingAt) - Date.parse(b.airingAt))) {
      const key = dayKey(new Date(item.airingAt), zone);
      grouped.set(key, [...(grouped.get(key) ?? []), item]);
    }
    return [...grouped.values()];
  });

  $effect(() => {
    if (!active) return;
    const selectedRange = range;
    const selectedZone = zone;
    const window = scheduleWindow(range, zone);
    const filter = libraryOnly;
    revision; refresh;
    const current = ++generation;
    loading = true; error = ''; notificationError = ''; result = null;
    void featureApi.schedule(window.from, window.to, filter).then(data => {
      if (current !== generation) return;
      result = data;
      if (selectedRange === 'Today' && selectedZone === localZone) void featureApi.scheduleNotify(window.from, window.to)
        .catch(reason => { if (current === generation) notificationError = reason instanceof Error ? reason.message : 'Airing reminders could not be sent.'; });
    })
      .catch(reason => { if (current === generation) error = reason instanceof Error ? reason.message : 'Could not load the airing schedule.'; })
      .finally(() => { if (current === generation) loading = false; });
    return () => { if (generation === current) generation += 1; };
  });
</script>

<header class="schedule-header"><div><h1>Schedule</h1><p>Make room for your next episode.</p></div><button class="ghost-button" aria-label="Refresh schedule" onclick={() => refresh += 1} disabled={loading}><RefreshCw size={15} />Refresh</button></header>
<div class="schedule-controls"><div class="range-tabs" aria-label="Schedule range">{#each ranges as option}<button class:selected={range === option} aria-pressed={range === option} onclick={() => range = option}>{option}</button>{/each}</div><div class="schedule-filters"><label class="library-filter"><input type="checkbox" bind:checked={libraryOnly} />My library only</label><label class="zone-label"><Clock3 size={14} /><select aria-label="Schedule timezone" bind:value={zone}>{#each zones as item}<option value={item}>{item === localZone ? `Local · ${item.replaceAll('_', ' ')}` : item.replaceAll('_', ' ')}</option>{/each}</select></label></div></div>
<div class="schedule-caption"><span><CalendarDays size={14} />{range === 'Today' ? 'Remaining today' : range === 'Tomorrow' ? 'Tomorrow’s releases' : range === 'This week' ? 'Next 7 days' : 'The following 7 days'}</span><span>AniList airing · {zone.replaceAll('_', ' ')}</span></div>
{#if notificationError}<p class="error-text" role="status">{notificationError}</p>{/if}
<section aria-label="Upcoming episodes" aria-busy={loading}>
  {#if error}<div class="load-error" role="alert"><AlertCircle size={20} /><div><strong>The schedule could not load</strong><p>{error}</p></div><button class="ghost-button" onclick={() => refresh += 1}>Try again</button></div>
  {:else if loading}<div class="schedule-skeletons" role="status" aria-label="Loading airing schedule">{#each Array(4) as _}<span></span>{/each}</div>
  {:else if !groups.length}<div class="empty-state"><div class="empty-icon"><CalendarDays size={30} strokeWidth={1.4} /></div><h2>No upcoming episodes</h2><p>{libraryOnly ? 'No titles in your library are airing in this period. Try another date range or turn off the library filter.' : 'No episodes are scheduled in this period. Try another date range.'}</p></div>
  {:else}<div class="schedule-days">{#each groups as items}<section class="day-group" aria-label={heading(items[0].airingAt)}><h2>{heading(items[0].airingAt)}<span>{items.length} {items.length === 1 ? 'episode' : 'episodes'}</span></h2><div class="schedule-rows">{#each items as item (`${item.mediaId}:${item.episode}:${item.airingAt}`)}<button class="schedule-row" aria-label={`Open ${item.title}, episode ${item.episode}`} title={item.title} onclick={() => onOpenId(item.mediaId)}><span class="airing-time mono">{time(item.airingAt)}</span><span class="schedule-cover"><MiniCover url={item.posterUrl} title={item.title} /></span><span class="episode-info"><strong>{item.title}</strong><span><span class="mono">EP {item.episode}</span><span class="format" title={formatLabel(item.format)}>{formatLabel(item.format)}</span>{#if item.inLibrary}<span class="library-badge"><BookMarked size={11} />{item.status ? statusLabel(item.status, 'Anime') : 'In library'}</span>{/if}</span></span><span class="row-chevron" aria-hidden="true">›</span></button>{/each}</div></section>{/each}</div>{/if}
</section>

<style>
  .schedule-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 28px; }
  h1 { font-size: 30px; font-weight: 700; letter-spacing: -.035em; line-height: 1.2; margin: 0 0 8px; }
  .schedule-header p { color: var(--secondary); font-size: 14px; margin: 0; }
  .schedule-controls { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 16px; border-bottom: 1px solid var(--border); padding-bottom: 20px; }
  .range-tabs { display: flex; gap: 4px; overflow-x: auto; max-width: 100%; padding: 2px; }
  .range-tabs button { background: var(--shell); border: 1px solid var(--border); color: var(--secondary); padding: 9px 14px; white-space: nowrap; font-size: 12px; }
  .range-tabs button.selected { background: var(--violet-soft); color: var(--lavender); border-color: var(--border-lit); }
  .schedule-filters { display: flex; align-items: center; flex-wrap: wrap; gap: 18px; }
  .library-filter { display: flex; align-items: center; gap: 8px; color: var(--secondary); font-size: 12px; }
  .library-filter input { accent-color: var(--violet); width: 15px; height: 15px; margin: 0; padding: 0; }
  .zone-label { display: flex; align-items: center; gap: 8px; color: var(--secondary); }
  .zone-label select { min-height: 36px; padding: 7px 32px 7px 8px; max-width: 210px; font-size: 11px; }
  .schedule-caption { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 10px; margin: 18px 0 24px; color: var(--secondary); font-size: 12px; }
  .schedule-caption > span:first-child { display: inline-flex; gap: 8px; align-items: center; }
  .schedule-caption > span:last-child { color: var(--muted); font-size: 11px; }
  .schedule-days { display: flex; flex-direction: column; gap: 28px; }
  .day-group > h2 { display: flex; align-items: center; justify-content: space-between; gap: 10px; font-size: 15px; font-weight: 600; margin: 0 0 14px; }
  .day-group > h2 > span { color: var(--muted); font-size: 11px; font-weight: 400; }
  .schedule-rows { display: flex; flex-direction: column; gap: 10px; }
  .schedule-row { display: grid; grid-template-columns: 70px 64px minmax(0,1fr) 20px; align-items: center; gap: 18px; padding: 12px 18px; min-width: 0; width: 100%; background: var(--shell); border: 1px solid var(--border); color: var(--text); text-align: left; }
  .schedule-row:hover { background: var(--raised); border-color: var(--border-lit); }
  .airing-time { font-size: 20px; letter-spacing: -.025em; color: var(--lavender); }
  .episode-info { display: flex; flex-direction: column; gap: 10px; min-width: 0; }
  .episode-info > strong { font-size: 15px; font-weight: 600; line-height: 1.5; height: 3em; overflow-wrap: anywhere; display: -webkit-box; line-clamp: 2; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
  .episode-info > span { display: grid; grid-template-columns: max-content minmax(0,1fr); align-items: start; gap: 6px 12px; font-size: 12px; color: var(--secondary); min-height: 42px; }
  .episode-info > span > .mono { color: var(--cyan); white-space: nowrap; }
  .format { color: var(--muted); min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .library-badge { display: inline-flex; align-items: center; gap: 5px; grid-column: 1 / -1; justify-self: start; color: var(--lavender); background: var(--violet-soft); border-radius: 5px; padding: 4px 7px; font-size: 10px; }
  .row-chevron { font-size: 26px; color: var(--muted); }
  .schedule-skeletons { display: flex; flex-direction: column; gap: 10px; }
  .schedule-skeletons span { background: var(--shell); border: 1px solid var(--border); border-radius: 8px; height: 120px; animation: pulse 1.5s ease-in-out infinite; }
  @media (max-width: 640px) { h1 { font-size: 28px; } .schedule-header > button { font-size: 0; padding: 10px; } .schedule-controls { gap: 14px; } .range-tabs { width: 100%; } .range-tabs button { padding-inline: 10px; } .schedule-filters { width: 100%; gap: 12px; justify-content: space-between; } .zone-label select { max-width: 178px; } .schedule-row { grid-template-columns: 58px minmax(0,1fr); gap: 10px 14px; padding: 12px; } .schedule-cover { grid-column: 1; grid-row: 1 / span 2; } .airing-time { grid-column: 2; grid-row: 1; font-size: 14px; align-self: end; } .episode-info { grid-column: 2; grid-row: 2; gap: 5px; } .episode-info > strong { font-size: 13px; } .episode-info > span { font-size: 11px; gap: 6px 8px; } .row-chevron { display: none; } }
</style>
