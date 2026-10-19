<script lang="ts">
  import { artwork } from '../artwork';
  import { BookOpen } from '@lucide/svelte';
  let { url, title, thumbnail, preview }: { url: string | null; title: string; thumbnail?: string | null; preview?: string | null } = $props();
  let failed = $state(false);
  $effect(() => { url; failed = false; });
</script>

<span class="mini-cover">
  {#if url && !failed}<img use:artwork={{ url, thumbnail, preview }} alt={`Cover of ${title}`} loading="lazy" decoding="async" onerror={() => failed = true} />
  {:else}<span class="cover-placeholder" role="img" aria-label={`Cover unavailable for ${title}`}><BookOpen size={24} strokeWidth={1.25} /></span>{/if}
</span>

<style>
  .mini-cover { display: block; width: 100%; aspect-ratio: 2 / 3; overflow: hidden; border-radius: 7px; background: var(--raised); color: var(--secondary); }
  img { display: block; width: 100%; height: 100%; object-fit: cover; transition: transform 180ms var(--ease); }
  .cover-placeholder { height: 100%; display: flex; flex-direction: column; justify-content: center; align-items: center; gap: 10px; padding: 10px; }
</style>
