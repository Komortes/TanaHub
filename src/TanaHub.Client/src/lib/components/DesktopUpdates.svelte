<script lang="ts">
  import { onDestroy } from 'svelte';
  import type { Update } from '@tauri-apps/plugin-updater';
  import { version } from '../../../package.json';
  import { openExternal } from '../native';
  import { modal } from '../modal';
  let { busy = false, onBusyChange }: { busy?: boolean; onBusyChange: (busy: boolean) => void } = $props();
  let phase = $state<'idle' | 'checking' | 'available' | 'downloading' | 'ready' | 'installing' | 'installed'>('idle');
  let update = $state<Update | null>(null);
  let message = $state('');
  let error = $state('');
  let downloaded = $state(0);
  let total = $state<number | undefined>();
  let disposed = false;
  const working = $derived(['checking', 'downloading', 'installing'].includes(phase));
  const percent = $derived(total ? Math.min(100, Math.round(downloaded / total * 100)) : undefined);
  const mb = (bytes: number) => (bytes / 1024 / 1024).toFixed(1);
  async function invoke<T>(command: string): Promise<T> { return (await import('@tauri-apps/api/core')).invoke<T>(command); }
  async function restart() {
    try { await (await import('@tauri-apps/plugin-process')).relaunch(); }
    catch (reason) { error = `Update installed. Quit and reopen TanaHub to finish, or try Restart now. ${String(reason)}`; }
  }
  async function release() { const previous = update; update = null; await previous?.close().catch(() => {}); }
  onDestroy(() => { disposed = true; if (!working) void release(); });

  async function findUpdate() {
    if (working || busy) return;
    phase = 'checking'; error = ''; message = ''; onBusyChange(true);
    try {
      await release();
      const support = await invoke<{ supported: boolean; reason: string | null }>('update_support');
      if (!support.supported) { message = support.reason ?? 'Download an installer from Browse releases.'; phase = 'idle'; return; }
      update = await (await import('@tauri-apps/plugin-updater')).check({ timeout: 20_000 });
      message = update ? `Version ${update.version} is available` : 'You are up to date';
      phase = update ? 'available' : 'idle';
    } catch (reason) { error = `Could not check for updates. ${String(reason)}`; phase = 'idle'; }
    finally { if (disposed) await release(); onBusyChange(false); }
  }
  async function download() {
    if (!update || working || busy) return;
    phase = 'downloading'; error = ''; downloaded = 0; total = undefined; onBusyChange(true);
    try {
      await update.download(event => {
        if (event.event === 'Started') total = event.data.contentLength;
        if (event.event === 'Progress') downloaded += event.data.chunkLength;
      }, { timeout: 300_000 });
      phase = 'ready'; message = 'Update downloaded and signature verified.';
    } catch (reason) { error = `Could not download a trusted update. ${String(reason)}`; phase = 'idle'; await release(); }
    finally { if (disposed) await release(); onBusyChange(false); }
  }
  async function install() {
    if (!update || phase !== 'ready' || busy) return;
    phase = 'installing'; error = ''; onBusyChange(true);
    try {
      await invoke('prepare_update');
      await update.install();
      phase = 'installed';
      message = 'Update installed. Restart TanaHub to finish.';
      await restart();
    } catch (reason) {
      await invoke('resume_after_update').catch(() => {});
      error = `Could not install the update. ${String(reason)} Close other copies of TanaHub and try again. On macOS, move TanaHub to Applications first.`;
      phase = 'ready';
    } finally { if (phase !== 'installed') onBusyChange(false); }
  }
  async function browse() {
    try { await openExternal('https://github.com/Komortes/TanaHub/releases'); }
    catch { error = 'Could not open GitHub. Please try again.'; }
  }
</script>

<section class="panel update-panel" aria-busy={working}>
  <h2>About TanaHub</h2><p class="hint">TanaHub · {version}</p>
  <p class="hint">Updates include previews and are verified before installation. Your library stays on this device.</p>
  {#if message}<p class="hint" role="status">{message}</p>{/if}
  {#if error}<p class="update-error" role="alert">{error}</p>{/if}
  {#if phase === 'downloading'}
    <progress max="100" value={percent} aria-label="Update download progress"></progress>
    <p class="hint" aria-live="polite">Downloaded {mb(downloaded)} MB{#if total} of {mb(total)} MB ({percent}%){/if}</p>
  {/if}
  {#if phase === 'ready'}<p class="hint">Save your changes before restarting. TanaHub will replace the application and reopen it.</p>{/if}
  <div class="update-actions">
    {#if phase === 'installed'}<button class="primary-button" onclick={restart}>Restart now</button>
    {:else if phase === 'available'}<button class="primary-button" disabled={busy || working} onclick={download}>Download update</button>
    {:else if phase === 'ready'}<button class="primary-button" disabled={busy || working} onclick={install}>Install and restart</button>
    {:else}<button class="ghost-button" disabled={busy || working} onclick={findUpdate}>{phase === 'checking' ? 'Checking…' : 'Check for updates'}</button>{/if}
    {#if !working}<button class="text-button" onclick={browse}>Browse releases</button>{/if}
  </div>
  {#if update?.body}<details><summary>What's new in {update.version}</summary><p class="release-notes">{update.body.slice(0, 12_000)}</p></details>{/if}
</section>
{#if phase === 'installing' || phase === 'installed'}<dialog use:modal aria-label="Installing update" oncancel={event => event.preventDefault()}><h2>{phase === 'installed' ? 'Update installed' : 'Installing update…'}</h2><p>TanaHub will restart when installation finishes.</p>{#if phase === 'installed'}{#if error}<p role="alert">{error}</p>{/if}<button class="primary-button" onclick={restart}>Restart now</button>{/if}</dialog>{/if}

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
  dialog { margin: auto; padding: 28px; border: 1px solid var(--border-lit); border-radius: 12px; background: var(--shell); color: var(--text); max-width: calc(100vw - 32px); }
  dialog::backdrop { background: #09080fe6; } dialog h2 { font-size: 18px; } dialog p { font-size: 13px; }
  @media (max-width: 760px) { .update-panel { padding: 18px; } }
</style>
