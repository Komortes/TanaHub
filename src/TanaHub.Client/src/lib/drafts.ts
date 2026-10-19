import { mediaKey } from './types';

type Field = 'notes' | 'review' | 'organization' | 'dates' | 'progress';
type Value = string | string[] | number | null;
type Draft = { saved: Value; value: Value };
const prefix = 'tanahub-draft:v1:';
// A null entry also remembers an explicit discard if storage removal fails.
const memory = new Map<string, Draft | null>();
const keyFor = (id: string, field: Field) => `${prefix}${mediaKey(id)}:${field}`;
const same = (a: Value, b: Value) => JSON.stringify(a) === JSON.stringify(b);

function valid(value: unknown, field: Field): value is Value {
  if (field === 'progress') return value === null || typeof value === 'number' && Number.isFinite(value);
  if (field === 'notes' || field === 'review') return typeof value === 'string' && value.length <= 20_000;
  return Array.isArray(value) && value.length === 2 && value.every(item => typeof item === 'string' && item.length <= 6_000);
}

/** Drafts belong to this window. A changed saved value invalidates an older draft. */
export function restoreDraft<T extends Value>(id: string, field: Field, saved: T): T {
  const key = keyFor(id, field);
  let draft = memory.get(key);
  try {
    if (!memory.has(key)) {
      const stored = sessionStorage.getItem(key);
      if (stored && stored.length <= 65_536) draft = JSON.parse(stored) as Draft;
    }
  } catch { /* Private WebViews can disable session storage; retain the in-memory draft. */ }
  if (draft && same(draft.saved, saved) && valid(draft.value, field)) {
    memory.set(key, draft);
    return draft.value as T;
  }
  rememberDraft(id, field, saved, saved);
  return saved;
}

export function rememberDraft(id: string, field: Field, saved: Value, value: Value) {
  const key = keyFor(id, field);
  const dirty = !same(saved, value);
  memory.set(key, dirty ? { saved, value } : null);
  try {
    if (dirty) sessionStorage.setItem(key, JSON.stringify({ saved, value }));
    else sessionStorage.removeItem(key);
  } catch { /* Drafts still survive navigation when storage is unavailable. */ }
}

export function clearDrafts(id: string) {
  for (const field of ['notes', 'review', 'organization', 'dates', 'progress'] as const) rememberDraft(id, field, null, null);
}
