<script lang="ts">
  import { Plus, ExternalLink, Bookmark, Check, ArrowUpRight } from '@lucide/svelte';
  import type { RecognitionMatch } from '../feature-types';
  import MiniCover from './MiniCover.svelte';
  let { match, saved = false, busy = false, onOpen, onAdd, onSource, onSave }:
    { match: RecognitionMatch; saved?: boolean; busy?: boolean; onOpen: () => void; onAdd: () => void; onSource: () => void; onSave: () => void } = $props();
  const similarity = $derived(`${(Math.max(0, Math.min(1, match.similarity)) * 100).toFixed(1)}%`);
</script>

<article class="match-card" aria-label={`Match ${match.title}`}>
  <div class="match-cover"><MiniCover url={match.thumbnailUrl} title={match.title} /></div>
  <div class="match-info"><h3>{match.title}</h3>{#if match.author}<p class="author">{match.author}</p>{/if}<div class="match-details"><span class="similarity mono">{similarity}<span> similarity</span></span>{#if match.episode}<span class="episode mono">EP {match.episode}</span>{/if}</div><div class="match-actions">{#if match.mediaId}<button class="ghost-button" onclick={onOpen} disabled={busy}>Open title<ArrowUpRight size={13} /></button><button class="primary-button" aria-label={`Add ${match.title} to library`} onclick={onAdd} disabled={busy}><Plus size={13} />Add</button>{/if}{#if match.sourceUrl}<button class="ghost-button" onclick={onSource} disabled={busy}>View source<ExternalLink size={13} /></button>{/if}<button class="save-match" onclick={onSave} disabled={busy || saved} aria-label={saved ? `${match.title} saved to history` : `Save ${match.title} to recognition history`}>{#if saved}<Check size={14} />Saved{:else}<Bookmark size={14} />Save match{/if}</button></div></div>
</article>

<style>
  .match-card { display: grid; grid-template-columns: 80px minmax(0,1fr); gap: 16px; border-bottom: 1px solid var(--border); padding: 16px 0; }
  .match-info { min-width: 0; display: flex; flex-direction: column; justify-content: center; }
  h3 { font-size: 15px; line-height: 1.45; font-weight: 600; margin: 0 0 7px; overflow-wrap: anywhere; }
  .author { font-size: 12px; line-height: 1.5; color: var(--secondary); margin: 0 0 8px; }
  .match-details { display: flex; align-items: center; flex-wrap: wrap; gap: 8px 14px; margin-bottom: 14px; }
  .similarity { color: var(--text); font-size: 14px; }
  .similarity > span { color: var(--secondary); font-family: -apple-system, BlinkMacSystemFont, sans-serif; font-size: 11px; margin-left: 4px; }
  .episode { font-size: 11px; color: var(--secondary); border-left: 2px solid var(--bookmark); padding-left: 8px; }
  .match-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 7px; }
  .match-actions > .ghost-button, .match-actions > .primary-button { min-height: 36px; padding: 7px 10px; font-size: 11px; }
  .save-match { display: inline-flex; align-items: center; gap: 5px; background: transparent; border: 0; color: var(--secondary); font-size: 11px; padding: 7px 5px; }
  .save-match:hover:enabled { color: var(--lavender); }
  @media (max-width: 520px) { .match-card { grid-template-columns: 64px minmax(0,1fr); gap: 12px; padding: 16px 0; } .match-actions > .ghost-button, .match-actions > .primary-button, .save-match { min-height: 44px; } h3 { font-size: 13px; } .match-details { margin-bottom: 10px; } }
</style>
