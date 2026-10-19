<script lang="ts">
  import { tick, untrack } from 'svelte';
  import { CalendarDays, Pencil, Plus, Save, X } from '@lucide/svelte';
  import type { Entry } from '../types';
  import { rememberDraft, restoreDraft } from '../drafts';

  let { entry, busy = false, onSave }:
    { entry: Entry; busy?: boolean; onSave: (startedAt: string | null, completedAt: string | null) => Promise<void> } = $props();
  const draft = untrack(() => restoreDraft(entry.mediaId, 'dates', [dayValue(entry.startedAt), dayValue(entry.completedAt)]));
  let start = $state(draft[0]);
  let completion = $state(draft[1]);
  let previous = untrack(() => ({ id: entry.mediaId, start: entry.startedAt, completion: entry.completedAt }));
  let editing = $state(untrack(() => start !== dayValue(entry.startedAt) || completion !== dayValue(entry.completedAt)));
  let saving = $state(false);
  let error = $state('');
  let toggle = $state<HTMLButtonElement>();
  let input = $state<HTMLInputElement>();
  const locked = $derived(busy || saving);
  const dirty = $derived(start !== dayValue(entry.startedAt) || completion !== dayValue(entry.completedAt));
  const invalidOrder = $derived(!!start && !!completion && completion < start);
  $effect(() => rememberDraft(entry.mediaId, 'dates', [dayValue(entry.startedAt), dayValue(entry.completedAt)], [start, completion]));

  function dayValue(value: string | null | undefined) {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return `${date.getFullYear().toString().padStart(4, '0')}-${(date.getMonth() + 1).toString().padStart(2, '0')}-${date.getDate().toString().padStart(2, '0')}`;
  }
  function dateLabel(value: string | null | undefined) {
    return dayValue(value) ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(value!)) : '';
  }
  function timestamp(day: string, saved: string | null | undefined) {
    if (day === dayValue(saved)) return saved ?? null;
    return day ? new Date(`${day}T00:00:00`).toISOString() : null;
  }
  $effect(() => {
    const next = { id: entry.mediaId, start: entry.startedAt, completion: entry.completedAt };
    if (next.id !== previous.id) { editing = false; error = ''; }
    if (next.id !== previous.id || (next.start !== previous.start && untrack(() => start === dayValue(previous.start)))) start = dayValue(next.start);
    if (next.id !== previous.id || (next.completion !== previous.completion && untrack(() => completion === dayValue(previous.completion)))) completion = dayValue(next.completion);
    previous = next;
  });
  async function toggleEditor() {
    if (locked) return;
    editing = !editing;
    if (editing) { await tick(); input?.focus({ preventScroll: true }); }
  }
  async function discard() {
    start = dayValue(entry.startedAt); completion = dayValue(entry.completedAt); error = ''; editing = false;
    await tick(); toggle?.focus({ preventScroll: true });
  }
  async function save() {
    if (locked || !dirty || invalidOrder) return;
    saving = true; error = '';
    const focused = document.activeElement as HTMLElement | null;
    try {
      const startedAt = timestamp(start, entry.startedAt);
      let completedAt = timestamp(completion, entry.completedAt);
      // A calendar-day edit must permit completion on the start day, even when
      // the untouched start timestamp includes a time later than midnight.
      if (startedAt && completedAt && start === completion && Date.parse(completedAt) < Date.parse(startedAt)) completedAt = startedAt;
      await onSave(startedAt, completedAt);
    } catch (reason) { error = reason instanceof Error ? reason.message : 'Could not save dates. Your draft is kept for retry.'; }
    finally {
      saving = false; await tick();
      if (input?.isConnected && document.activeElement === document.body && focused) input.focus({ preventScroll: true });
    }
  }
</script>

<div class="dates-section">
  <div class="editor-heading"><h3>Tracking dates</h3>{#if dirty}<span class="draft-label">Unsaved draft</span>{/if}<button bind:this={toggle} class="editor-toggle" aria-expanded={editing} aria-controls="dates-editor" disabled={locked} onclick={toggleEditor}>{#if editing}<X size={13} />Close dates editor{:else if dirty}<Pencil size={13} />Continue editing dates{:else if entry.startedAt || entry.completedAt}<Pencil size={13} />Edit dates{:else}<Plus size={13} />Add dates{/if}</button></div>
  {#if editing}
    <form id="dates-editor" onsubmit={(event) => { event.preventDefault(); void save(); }}>
      <div class="date-fields">
        <div class="date-field"><label for="title-started-on">Started on</label><input id="title-started-on" bind:this={input} type="date" bind:value={start} disabled={locked} aria-describedby={start ? 'dates-help' : 'start-date-empty dates-help'} />{#if !start}<p id="start-date-empty" class="empty-date">No start date recorded</p>{/if}</div>
        <div class="date-field"><label for="title-completed-on">Completed on</label><input id="title-completed-on" type="date" bind:value={completion} disabled={locked} aria-invalid={invalidOrder} aria-describedby={invalidOrder ? 'dates-validation dates-help' : completion ? 'dates-help' : 'completion-date-empty dates-help'} />{#if !completion}<p id="completion-date-empty" class="empty-date">No completion date recorded</p>{/if}</div>
      </div>
      <p id="dates-help" class="date-help">Dates use your device's local timezone. Saving dates keeps your status and progress.</p>
      {#if invalidOrder}<p class="error-text" id="dates-validation" role="alert">Completion date cannot be before the start date.</p>{/if}
      {#if error}<p class="error-text" role="alert">{error}</p>{/if}
      <div class="field-actions">{#if dirty}<button class="text-button" type="button" disabled={locked} onclick={discard}>Discard dates draft</button>{/if}<button class="text-button" type="button" disabled={locked || (!start && !completion)} onclick={() => { start = ''; completion = ''; error = ''; }}>Clear dates</button><button class="ghost-button" type="submit" disabled={locked || !dirty || invalidOrder}><Save size={13} />{saving ? 'Saving…' : 'Save dates'}</button></div>
    </form>
  {:else if entry.startedAt || entry.completedAt}
    <dl class="dates"><CalendarDays size={13} />{#if dateLabel(entry.startedAt)}<div><dt>Started</dt><dd class="mono">{dateLabel(entry.startedAt)}</dd></div>{/if}{#if dateLabel(entry.completedAt)}<div><dt>Completed</dt><dd class="mono">{dateLabel(entry.completedAt)}</dd></div>{/if}</dl>
  {/if}
</div>

<style>
  .dates-section { border-top: 1px solid var(--border); }
  .editor-heading { display: flex; align-items: center; flex-wrap: wrap; gap: 4px 10px; min-height: 48px; }
  h3 { margin: 0; font-size: 12px; font-weight: 500; color: var(--secondary); }
  .editor-toggle { margin-left: auto; display: inline-flex; align-items: center; gap: 5px; min-height: 36px; padding: 7px 0 7px 8px; font-size: 11px; background: transparent; border: 0; color: var(--lavender); }
  .draft-label { color: var(--amber); font-size: 10px; }
  form { padding: 4px 0 14px; }
  .date-fields { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 18px; }
  .date-field { display: grid; gap: 8px; align-content: start; min-width: 0; }
  label { color: var(--secondary); font-size: 12px; }
  input { width: 100%; min-width: 0; min-height: 38px; font-size: 12px; }
  .empty-date { color: var(--secondary); font-size: 11px; line-height: 1.5; margin: -2px 0 0; }
  .date-help { font-size: 11px; color: var(--secondary); line-height: 1.6; margin: 10px 0 0; }
  .field-actions { display: flex; justify-content: flex-end; flex-wrap: wrap; align-items: center; gap: 8px; margin-top: 8px; }
  .field-actions button { min-height: 36px; padding: 7px 11px; font-size: 11px; }
  .text-button { border: 0; background: transparent; color: var(--secondary); }
  .dates { display: flex; gap: 12px 20px; margin: 0 0 14px; color: var(--secondary); font-size: 11px; flex-wrap: wrap; align-items: center; }
  .dates div { display: flex; gap: 8px; }.dates dd { margin: 0; color: var(--text); }
  .error-text { margin: 10px 0 0; }
  @media (max-width: 640px) { .date-fields { grid-template-columns: 1fr; gap: 16px; } }
</style>
