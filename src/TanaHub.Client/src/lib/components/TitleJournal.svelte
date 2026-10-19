<script lang="ts">
  import { tick, untrack } from 'svelte';
  import { Check, Folder, Pencil, Plus, Save, Tag, Trash2, X } from '@lucide/svelte';
  import type { Entry } from '../types';
  import TitleDates from './TitleDates.svelte';

  let { entry, busy = false, onNotes, onReview, onOrganization, onDates, onRemove }:
    { entry: Entry; busy?: boolean; onNotes: (notes: string) => Promise<void>; onReview: (review: string) => Promise<void>; onOrganization: (tags: string[], lists: string[]) => Promise<void>; onDates: (startedAt: string | null, completedAt: string | null) => Promise<void>; onRemove: () => Promise<void> } = $props();
  type Editor = 'notes' | 'review' | 'organization';
  let notes = $state(untrack(() => entry.notes ?? ''));
  let review = $state(untrack(() => entry.review ?? ''));
  let tagsText = $state(untrack(() => (entry.tags ?? []).join(', ')));
  let listsText = $state(untrack(() => (entry.customLists ?? []).join(', ')));
  let previous = untrack(() => savedValues(entry));
  let editing = $state({ notes: false, review: false, organization: false });
  let saving = $state('');
  let error = $state('');
  let confirmRemove = $state(false);
  let panel: HTMLElement;
  const locked = $derived(busy || !!saving);
  const tags = $derived(parseItems(tagsText));
  const lists = $derived(parseItems(listsText));
  const tagsError = $derived(tags.length > 50 ? 'Use up to 50 tags.' : tags.some(item => item.length > 100) ? 'Each tag must be 100 characters or fewer.' : '');
  const listsError = $derived(lists.length > 50 ? 'Use up to 50 custom lists.' : lists.some(item => item.length > 100) ? 'Each custom list must be 100 characters or fewer.' : '');
  const organizationError = $derived(tagsError || listsError);
  const notesDirty = $derived(notes !== (entry.notes ?? ''));
  const reviewDirty = $derived(review !== (entry.review ?? ''));
  const reviewLimit = 4000;
  const reviewTooLong = $derived(review.trim().length > reviewLimit);
  const organizationDirty = $derived(JSON.stringify(tags) !== JSON.stringify(entry.tags ?? []) || JSON.stringify(lists) !== JSON.stringify(entry.customLists ?? []));
  const hasOrganization = $derived(!!(entry.tags?.length || entry.customLists?.length));

  function savedValues(value: Entry) { return { id: value.mediaId, notes: value.notes, review: value.review, tags: JSON.stringify(value.tags ?? []), lists: JSON.stringify(value.customLists ?? []) }; }
  function parseItems(value: string) {
    const names = new Map<string, string>();
    for (const item of value.split(',').map(item => item.trim()).filter(Boolean)) {
      // Unicode lowercasing can merge names the core's ordinal comparer keeps distinct.
      const key = item.replace(/[A-Z]/g, letter => letter.toLowerCase());
      if (!names.has(key)) names.set(key, item);
    }
    return [...names.values()];
  }
  $effect(() => {
    const next = savedValues(entry);
    if (next.id !== previous.id) { confirmRemove = false; error = ''; editing = { notes: false, review: false, organization: false }; }
    if (next.id !== previous.id || next.notes !== previous.notes) notes = entry.notes ?? '';
    if (next.id !== previous.id || next.review !== previous.review) review = entry.review ?? '';
    if (next.id !== previous.id || next.tags !== previous.tags) tagsText = (entry.tags ?? []).join(', ');
    if (next.id !== previous.id || next.lists !== previous.lists) listsText = (entry.customLists ?? []).join(', ');
    previous = next;
  });
  async function toggleEditor(name: Editor) {
    editing[name] = !editing[name];
    if (!editing[name]) return;
    await tick();
    panel?.querySelector<HTMLElement>(`#${name === 'organization' ? 'title-tags' : `title-${name}`}`)?.focus({ preventScroll: true });
  }
  async function discard(name: Editor) {
    if (name === 'notes') notes = entry.notes ?? '';
    if (name === 'review') review = entry.review ?? '';
    if (name === 'organization') { tagsText = (entry.tags ?? []).join(', '); listsText = (entry.customLists ?? []).join(', '); }
    error = '';
    editing[name] = false;
    await tick();
    panel?.querySelector<HTMLElement>(`#${name}-editor-toggle`)?.focus({ preventScroll: true });
  }
  async function change(name: string, action: () => Promise<void>) {
    if (locked) return;
    const focused = document.activeElement as HTMLElement | null;
    saving = name;
    error = '';
    try { await action(); }
    catch (reason) { error = reason instanceof Error ? reason.message : 'Could not save this change.'; }
    finally {
      saving = '';
      await tick();
      if (panel?.isConnected && document.activeElement === document.body && focused && panel.contains(focused)) {
        const fallback = name === 'organization' ? '#title-tags' : name === 'remove' ? '#remove-title' : `#title-${name}`;
        const target = focused.isConnected && !focused.matches(':disabled') ? focused : panel.querySelector<HTMLElement>(fallback);
        target?.focus({ preventScroll: true });
      }
    }
  }
  async function keepTitle() { confirmRemove = false; await tick(); panel?.querySelector<HTMLElement>('#remove-title')?.focus({ preventScroll: true }); }
</script>

<section class="journal-panel" aria-labelledby="journal-heading" aria-busy={locked} bind:this={panel}>
  <div class="section-heading"><h2 id="journal-heading">Your space</h2><span>Private, on this device</span></div>
  <div class="journal-section">
    <div class="editor-heading"><h3>Personal notes</h3>{#if notesDirty}<span class="draft-label">Unsaved draft</span>{/if}<button id="notes-editor-toggle" class="editor-toggle" aria-expanded={editing.notes} aria-controls="notes-editor" onclick={() => toggleEditor('notes')} disabled={locked}>{#if editing.notes}<X size={13} />Close notes editor{:else if notesDirty}<Pencil size={13} />Continue editing notes{:else if entry.notes}<Pencil size={13} />Edit notes{:else}<Plus size={13} />Add notes{/if}</button></div>
    {#if editing.notes}
      <form id="notes-editor" onsubmit={(event) => { event.preventDefault(); void change('notes', () => onNotes(notes)); }}>
        <label class="sr-only" for="title-notes">Personal notes</label>
        <textarea id="title-notes" bind:value={notes} placeholder="Where you left off, a favourite scene, something to remember…" rows="3" maxlength="20000" disabled={locked}></textarea>
        <div class="field-actions">{#if notesDirty}<button class="discard-button" type="button" disabled={locked} onclick={() => discard('notes')}>Discard notes draft</button>{/if}<button class="ghost-button" type="submit" disabled={locked || !notesDirty}><Save size={13} />{saving === 'notes' ? 'Saving…' : 'Save notes'}</button></div>
      </form>
    {:else if entry.notes}<p class="saved-text">{entry.notes}</p>{/if}
  </div>
  <div class="journal-section">
    <div class="editor-heading"><h3>Review</h3>{#if reviewDirty}<span class="draft-label">Unsaved draft</span>{/if}<button id="review-editor-toggle" class="editor-toggle" aria-expanded={editing.review} aria-controls="review-editor" onclick={() => toggleEditor('review')} disabled={locked}>{#if editing.review}<X size={13} />Close review editor{:else if reviewDirty}<Pencil size={13} />Continue editing review{:else if entry.review}<Pencil size={13} />Edit review{:else}<Plus size={13} />Add review{/if}</button></div>
    {#if editing.review}
      <form id="review-editor" onsubmit={(event) => { event.preventDefault(); if (!reviewTooLong) void change('review', () => onReview(review)); }}>
        <label class="sr-only" for="title-review">Review</label>
        <textarea id="title-review" bind:value={review} placeholder="Your thoughts after watching or reading." rows="4" maxlength={reviewLimit} disabled={locked} aria-invalid={reviewTooLong} aria-describedby="review-help"></textarea>
        <div id="review-help" class="field-help" class:limit-error={reviewTooLong}><span>{reviewTooLong ? 'Review must be 4,000 characters or fewer.' : 'Characters'}</span><span class="mono">{review.length.toLocaleString()} / {reviewLimit.toLocaleString()}</span></div>
        <div class="field-actions">{#if reviewDirty}<button class="discard-button" type="button" disabled={locked} onclick={() => discard('review')}>Discard review draft</button>{/if}<button class="ghost-button" type="submit" disabled={locked || !reviewDirty || reviewTooLong}><Save size={13} />{saving === 'review' ? 'Saving…' : 'Save review'}</button></div>
      </form>
    {:else if entry.review}<p class="saved-text">{entry.review}</p>{/if}
  </div>
  <div class="journal-section">
    <div class="editor-heading"><h3>Tags & lists</h3>{#if organizationDirty}<span class="draft-label">Unsaved draft</span>{/if}<button id="organization-editor-toggle" class="editor-toggle" aria-expanded={editing.organization} aria-controls="organization-editor" onclick={() => toggleEditor('organization')} disabled={locked}>{#if editing.organization}<X size={13} />Close organization editor{:else if organizationDirty}<Pencil size={13} />Continue editing organization{:else if hasOrganization}<Pencil size={13} />Edit organization{:else}<Plus size={13} />Add tags or lists{/if}</button></div>
    {#if editing.organization}
      <form id="organization-editor" onsubmit={(event) => { event.preventDefault(); if (!organizationError) void change('organization', () => onOrganization(tags, lists)); }}>
        <div class="organization-fields">
          <div><label for="title-tags"><Tag size={13} />Tags</label><input id="title-tags" bind:value={tagsText} placeholder="comfort, seasonal, rewatch" disabled={locked} maxlength="6000" aria-invalid={!!tagsError} aria-describedby={tagsError ? 'organization-help organization-validation' : 'organization-help'} /></div>
          <div><label for="title-lists"><Folder size={13} />Custom lists</label><input id="title-lists" bind:value={listsText} placeholder="Friday queue, favourites" disabled={locked} maxlength="6000" aria-invalid={!!listsError} aria-describedby={listsError ? 'organization-help organization-validation' : 'organization-help'} /></div>
        </div>
        <p class="organization-help" id="organization-help">Separate names with commas. Up to 50 names per field, 100 characters per name.</p>
        {#if organizationError}<p class="error-text" id="organization-validation" role="alert">{organizationError}</p>{/if}
        <div class="field-actions">{#if organizationDirty}<button class="discard-button" type="button" disabled={locked} onclick={() => discard('organization')}>Discard organization draft</button>{/if}<button class="ghost-button" type="submit" disabled={locked || !organizationDirty || !!organizationError}><Check size={13} />{saving === 'organization' ? 'Saving…' : 'Save organization'}</button></div>
      </form>
    {:else if hasOrganization}
      <div class="saved-organization">{#if entry.tags?.length}<div class="chips" aria-label="Tags"><Tag size={13} />{#each entry.tags as tag}<span>{tag}</span>{/each}</div>{/if}{#if entry.customLists?.length}<div class="chips" aria-label="Custom lists"><Folder size={13} />{#each entry.customLists as list}<span>{list}</span>{/each}</div>{/if}</div>
    {/if}
  </div>
  <TitleDates {entry} busy={locked} onSave={onDates} />
  {#if error}<p class="error-text" role="alert">{error}</p>{/if}
  <div class="remove-row">
    {#if confirmRemove}<div class="confirmation"><p>Remove this title and its saved progress, notes and review?</p><div><button class="ghost-button" onclick={keepTitle} disabled={locked}>Keep title</button><button class="danger-button" onclick={() => change('remove', onRemove)} disabled={locked}>{saving === 'remove' ? 'Removing…' : 'Remove title'}</button></div></div>{:else}<button id="remove-title" class="remove-button" onclick={() => confirmRemove = true} disabled={locked}><Trash2 size={14} />Remove from library</button>{/if}
  </div>
</section>

<style>
  .journal-panel { padding: 22px; border: 1px solid var(--border); border-radius: 10px; background: var(--shell); }
  .section-heading { display: flex; align-items: baseline; justify-content: space-between; flex-wrap: wrap; gap: 8px; margin-bottom: 12px; }
  h2 { margin: 0; font-size: 15px; font-weight: 600; letter-spacing: -.015em; }
  h3 { margin: 0; font-size: 12px; font-weight: 500; color: var(--secondary); }
  .section-heading > span { color: var(--secondary); font-size: 11px; }
  .journal-section + .journal-section { border-top: 1px solid var(--border); }
  .editor-heading { display: flex; align-items: center; flex-wrap: wrap; gap: 4px 10px; min-height: 48px; }
  .editor-toggle { margin-left: auto; display: inline-flex; align-items: center; gap: 5px; min-height: 36px; padding: 7px 0 7px 8px; font-size: 11px; background: transparent; border: 0; color: var(--lavender); }
  .draft-label { color: var(--amber); font-size: 10px; }
  form { padding: 4px 0 14px; }
  label { display: flex; gap: 6px; align-items: center; color: var(--secondary); font-size: 12px; margin-bottom: 8px; }
  .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0; }
  textarea, input { display: block; width: 100%; min-width: 0; font-size: 12px; line-height: 1.6; }
  textarea { resize: vertical; min-height: 90px; max-height: 500px; }
  .field-actions { display: flex; justify-content: flex-end; align-items: center; flex-wrap: wrap; gap: 8px; margin-top: 8px; }
  .field-help { display: flex; justify-content: space-between; flex-wrap: wrap; gap: 6px 12px; margin-top: 6px; font-size: 10px; color: var(--secondary); }
  .field-help.limit-error { color: var(--red); }
  .field-actions button { min-height: 36px; padding: 7px 11px; font-size: 11px; }
  .discard-button { background: transparent; color: var(--secondary); border: 0; margin-right: auto; }
  .saved-text { margin: 0 0 16px; font-size: 13px; line-height: 1.7; white-space: pre-wrap; overflow-wrap: anywhere; }
  .organization-fields { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 18px; }
  .saved-organization { display: flex; flex-wrap: wrap; gap: 8px 14px; padding: 0 0 14px; }
  .chips { display: flex; align-items: center; gap: 5px; flex-wrap: wrap; color: var(--secondary); max-width: 100%; }
  .chips span { display: block; max-width: 100%; overflow-wrap: anywhere; font-size: 10px; padding: 3px 6px; background: var(--raised); border: 1px solid var(--border); border-radius: 4px; color: var(--secondary); }
  .organization-help { margin: 10px 0 0; font-size: 11px; color: var(--secondary); }
  .remove-row { margin-top: 10px; padding-top: 10px; border-top: 1px solid var(--border); }
  .remove-button { color: var(--secondary); background: transparent; border: none; display: inline-flex; align-items: center; gap: 6px; font-size: 11px; padding: 8px 0; }
  .remove-button:hover:enabled { color: var(--red); }
  .confirmation { color: var(--secondary); font-size: 12px; }.confirmation p { margin: 0 0 12px; line-height: 1.6; }.confirmation > div { display: flex; gap: 8px; flex-wrap: wrap; }
  .error-text { margin: 14px 0 0; }
  @media (max-width: 640px) { .journal-panel { padding: 18px; } .organization-fields { grid-template-columns: 1fr; gap: 16px; } .editor-heading { gap: 4px 8px; } .draft-label { order: 3; width: 100%; padding-bottom: 8px; } }
</style>
