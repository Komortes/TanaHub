<script lang="ts">
  import { version } from '../../../package.json';
  import { desktopUpdates as updates } from '../desktop-updates.svelte';
  let { busy = false, onBusyChange }: { busy?: boolean; onBusyChange: (busy: boolean) => void } = $props();
  const mb = (bytes: number) => (bytes / 1024 / 1024).toFixed(1);
  $effect(() => onBusyChange(['downloading', 'installing', 'installed'].includes(updates.phase)));
</script>

<section class="panel update-panel" aria-busy={updates.working}>
  <h2>About TanaHub</h2><p class="hint">TanaHub · {version}</p>
  <p class="hint">Updates include previews and are verified before installation. Your library stays on this device.</p>
  {#if updates.message}<p class="hint" role="status">{updates.message}</p>{/if}
  {#if updates.error}<p class="update-error" role="alert">{updates.error}</p>{/if}
  {#if updates.phase === 'downloading'}
    <progress max="100" value={updates.percent} aria-label="Update download progress"></progress>
    <p class="hint" aria-live="polite">Downloaded {mb(updates.downloaded)} MB{#if updates.total} of {mb(updates.total!)} MB ({updates.percent}%){/if}</p>
  {/if}
  {#if updates.phase === 'ready'}<p class="hint">Save your changes before restarting. TanaHub will replace the application and reopen it.</p>{/if}
  <div class="update-actions">
    {#if updates.phase === 'installed'}<button class="primary-button" onclick={() => void updates.restart()}>Restart now</button>
    {:else if updates.phase === 'available'}<button class="primary-button" disabled={busy || updates.working} onclick={() => { if (!busy) void updates.download(); }}>Download update</button>
    {:else if updates.phase === 'ready'}<button class="primary-button" disabled={busy || updates.working} onclick={() => { if (!busy) void updates.install(); }}>Install and restart</button>
    {:else}<button class="ghost-button" disabled={busy || updates.working} onclick={() => { if (!busy) void updates.check(); }}>{updates.phase === 'checking' ? 'Checking…' : 'Check for updates'}</button>{/if}
    {#if !updates.working}<button class="text-button" onclick={() => void updates.browse()}>Browse releases</button>{/if}
  </div>
  {#if updates.update?.body}<details><summary>What's new in {updates.update.version}</summary><p class="release-notes">{updates.update.body.slice(0, 12_000)}</p></details>{/if}
</section>


<style>
  .update-panel { grid-column: 1 / -1; background: var(--shell); border: 1px solid var(--border); border-radius: 12px; padding: 22px; min-width: 0; }
  h2 { font-size: 16px; font-weight: 600; margin: 0 0 18px; }
  .hint { color: var(--secondary); font-size: 12px; line-height: 1.6; overflow-wrap: anywhere; }
  .text-button { border: 0; background: transparent; color: var(--lavender); padding: 10px 4px; font-size: 12px; }
  .update-actions { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
  .update-actions > button { min-height: 40px; max-width: 100%; white-space: normal; }
  .update-error { color: var(--red); font-size: 12px; line-height: 1.6; overflow-wrap: anywhere; }
  progress { width: 100%; accent-color: var(--violet); }
  details { margin-top: 16px; font-size: 12px; } summary { cursor: pointer; }
  .release-notes { white-space: pre-wrap; overflow-wrap: anywhere; max-height: 240px; overflow: auto; color: var(--secondary); line-height: 1.6; }
  @media (max-width: 760px) { .update-panel { padding: 18px; } }
</style>
