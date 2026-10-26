<script lang="ts">
  import { tick } from 'svelte';
  import { desktopUpdates as updates } from '../desktop-updates.svelte';
  import { modal } from '../modal';
  let { inSettings = false, busy = false, onDismiss }: { inSettings?: boolean; busy?: boolean; onDismiss: () => void } = $props();
  let actionButton = $state<HTMLButtonElement>();
  const mb = (bytes: number) => (bytes / 1024 / 1024).toFixed(1);
  async function download() {
    if (busy) return;
    await updates.download();
    await tick();
    // Keep keyboard users on the next action unless they navigated or moved focus.
    if (document.activeElement === document.body) actionButton?.focus({ preventScroll: true });
  }
  async function later() { updates.dismissed = true; await tick(); onDismiss(); }
</script>

{#if !inSettings && updates.showNotice}
  <section class="update-notice" aria-label="Application update" aria-busy={updates.phase === 'downloading'}>
    <div class="notice-copy">
      <p class="notice-title" role="status">{#if updates.phase === 'available'}TanaHub {updates.update?.version} is available{:else if updates.phase === 'downloading'}Downloading TanaHub {updates.update?.version}…{:else if updates.phase === 'ready'}TanaHub {updates.update?.version} is ready to install{:else}Update could not finish{/if}</p>
      {#if updates.error}<p class="update-error" role="alert">{updates.error}</p>
      {:else if updates.phase === 'available'}<p>Download now, then choose when to restart. Your library stays on this device.</p>
      {:else if updates.phase === 'ready'}<p>Signature verified. Save your changes before restarting. Your library will be kept.</p>{/if}
      {#if updates.phase === 'downloading'}<progress max="100" value={updates.percent} aria-label="Update download progress"></progress><p>Downloaded {mb(updates.downloaded)} MB{#if updates.total} of {mb(updates.total)} MB ({updates.percent}%){/if}</p>{/if}
    </div>
    <div class="notice-actions">
      {#if updates.phase === 'available'}<button bind:this={actionButton} class="primary-button" disabled={busy} onclick={download}>Download update</button>
      {:else if updates.phase === 'ready'}<button bind:this={actionButton} class="primary-button" disabled={busy} onclick={() => { if (!busy) void updates.install(); }}>Install and restart</button>
      {:else if updates.phase !== 'downloading'}<button bind:this={actionButton} class="primary-button" disabled={busy || updates.working} onclick={() => { if (!busy) void updates.check(); }}>Retry check</button>{/if}
      {#if updates.phase !== 'downloading'}<button class="ghost-button" title="Remind me next launch" onclick={later}>Later</button>{/if}
    </div>
  </section>
{/if}
{#if updates.phase === 'installing' || updates.phase === 'installed'}
  <dialog use:modal aria-label="Installing update" oncancel={event => event.preventDefault()}>
    <h2>{updates.phase === 'installed' ? 'Update installed' : 'Installing update…'}</h2>
    <p>TanaHub will restart when installation finishes.</p>
    {#if updates.phase === 'installed'}{#if updates.error}<p role="alert">{updates.error}</p>{/if}<button class="primary-button" onclick={() => void updates.restart()}>Restart now</button>{/if}
  </dialog>
{/if}

<style>
  .update-notice { position: sticky; top: 16px; z-index: 5; display: flex; align-items: center; gap: 16px; margin-bottom: 24px; padding: 16px; border: 1px solid var(--border-lit); border-left: 3px solid var(--violet); border-radius: 12px; background: var(--shell); }
  .notice-copy { min-width: 0; flex: 1; }
  p { margin: 6px 0 0; font-size: 12px; line-height: 1.6; color: var(--secondary); overflow-wrap: anywhere; }
  .notice-title { margin: 0; color: var(--text); font-size: 14px; font-weight: 600; }
  .update-error { color: var(--red); }
  .notice-actions { display: flex; gap: 8px; flex-wrap: wrap; }
  .notice-actions button { min-height: 40px; max-width: 100%; white-space: normal; }
  progress { width: 100%; margin-top: 8px; accent-color: var(--violet); }
  dialog { margin: auto; padding: 28px; border: 1px solid var(--border-lit); border-radius: 12px; background: var(--shell); color: var(--text); max-width: calc(100vw - 32px); }
  dialog::backdrop { background: rgb(var(--scrim-rgb) / 90%); } dialog h2 { font-size: 18px; } dialog p { margin-bottom: 16px; font-size: 13px; }
  @media (max-width: 760px) { .update-notice { flex-direction: column; align-items: stretch; } }
  @media (max-width: 640px) { .update-notice { top: 80px; } }
</style>
