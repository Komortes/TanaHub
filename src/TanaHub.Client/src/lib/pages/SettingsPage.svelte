<script lang="ts">
  import { tick } from 'svelte';
  import { Download, Upload, RefreshCw, ExternalLink, Check, Link2, ShieldCheck } from '@lucide/svelte';
  import { featureApi } from '../feature-api';
  import { saveExport } from '../native';
  import type { Settings, SettingsPatch, ImportPreview, UpdateCheck } from '../feature-types';
  let { data, loading = false, loadError = '', busy = '', onReload, onSave, onConnect, onSync, onDisconnect, onLibraryChanged }:
    { data: Settings | null; loading?: boolean; loadError?: string; busy?: string; onReload: () => void; onSave: (patch: SettingsPatch) => Promise<void>; onConnect: (id: string, secret?: string) => Promise<void>; onSync: () => Promise<{ importedCount: number }>; onDisconnect: () => Promise<void>; onLibraryChanged: () => void } = $props();
  let clientId = $state('');
  let clientIdDirty = $state(false);
  let clientSecret = $state('');
  let sauceKey = $state('');
  let message = $state('');
  let error = $state('');
  let feedbackSection = $state('preferences');
  let localBusy = $state('');
  let preview = $state<ImportPreview | null>(null);
  let importXml = $state('');
  let importName = $state('');
  let update = $state<UpdateCheck | null>(null);
  let confirmAniListImport = $state(false);
  let aniListImportButton = $state<HTMLButtonElement>();
  let confirmImportButton = $state<HTMLButtonElement>();
  const importWarning = 'Existing titles may have progress, status, scores, notes and dates replaced. Reviews, tags and custom lists are kept.';
  const disabled = $derived(!!busy || !!localBusy || loading);
  const clientIdChanged = $derived(clientId.trim() !== (data?.aniListClientId ?? '').trim());
  $effect(() => { if (!clientIdDirty) clientId = data?.aniListClientId ?? ''; });
  async function run(name: string, action: () => Promise<void>) {
    if (disabled) return;
    feedbackSection = ({ connect: 'anilist', sync: 'anilist', disconnect: 'anilist', 'anilist-help': 'anilist', 'save-key': 'recognition', 'clear-key': 'recognition', export: 'library', preview: 'library', import: 'library', updates: 'about', open: 'about' } as Record<string, string>)[name] ?? 'preferences';
    const focused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    localBusy = name; error = ''; message = '';
    try { await action(); } catch (reason) { error = reason instanceof Error ? reason.message : 'Could not complete this action. Try again.'; }
    finally {
      localBusy = '';
      await tick();
      const target = name === 'sync' && !confirmAniListImport ? aniListImportButton : focused;
      if (document.activeElement === document.body && target?.isConnected && !target.matches(':disabled')) target.focus({ preventScroll: true });
    }
  }
  async function askAniListImport() { confirmAniListImport = true; await tick(); confirmImportButton?.focus({ preventScroll: true }); }
  function cancelAniListImport() { confirmAniListImport = false; aniListImportButton?.focus({ preventScroll: true }); }
  async function save(patch: SettingsPatch) { await onSave(patch); message = 'Settings saved'; }
  async function toggle(event: Event, key: 'notificationsEnabled' | 'offlineCacheEnabled') {
    const control = event.currentTarget as HTMLInputElement;
    const requested = control.checked;
    await run('save', () => save({ [key]: requested }));
    await tick(); control.checked = data?.[key] ?? !requested;
  }
  async function select(event: Event, key: 'theme' | 'preferredSyncSource') {
    const control = event.currentTarget as HTMLSelectElement;
    const previous = data?.[key] ?? control.value;
    const requested = control.value;
    await run('save', () => save({ [key]: requested }));
    await tick(); control.value = data?.[key] ?? previous;
  }

  async function exportLibrary(format: 'csv' | 'malXml') {
    await run('export', async () => {
      const result = await featureApi.export(format);
      if (!result.exportedCount) { message = format === 'malXml' ? 'No entries have a MAL mapping to export.' : 'Your library is empty.'; return; }
      const saved = await saveExport(result.text, result.fileName, result.mimeType);
      message = saved ? `Exported ${result.exportedCount} titles${result.skippedCount ? ` · ${result.skippedCount} without a MAL mapping skipped` : ''}` : 'Export cancelled';
    });
  }
  async function pickImport(event: Event) {
    const input = event.currentTarget as HTMLInputElement;
    const file = input.files?.[0]; input.value = '';
    if (!file) return;
    preview = null; importXml = '';
    await run('preview', async () => {
      if (file.size > 4 * 1024 * 1024) throw new Error('Choose an XML file smaller than 4 MB.');
      const xml = await file.text();
      preview = await featureApi.importPreview(xml); importXml = xml; importName = file.name;
    });
  }
</script>

{#snippet feedback(section: string)}
  {#if feedbackSection === section}
    {#if error}<p class="error-text feedback" role="alert">{error}</p>{/if}
    {#if message}<p class="success feedback" role="status"><Check size={15} />{message}</p>{/if}
  {/if}
{/snippet}

<section class="settings-page page-enter" aria-label="Settings">
  <header><h1>Settings</h1><p>Make TanaHub feel like home.</p></header>
  {#if loading}<p role="status">Loading settings…</p>{/if}
  {#if loadError}<div class="load-error" role="alert"><div><strong>Settings unavailable</strong><p>{loadError}</p></div><button class="ghost-button" onclick={onReload}>Try again</button></div>{/if}
  {#if data}
    <div class="settings-grid">
      <section class="panel"><h2>Appearance &amp; preferences</h2>
        <label class="field">Theme<select aria-label="Theme" value={data.theme} disabled={disabled} onchange={(event) => void select(event, 'theme')}><option>Nebula dark</option><option>High contrast</option></select></label>
        <label class="toggle"><span><strong>Notifications</strong><small>Reminders for titles airing today.</small></span><input type="checkbox" role="switch" aria-label="Notifications" checked={data.notificationsEnabled} disabled={disabled} onchange={(event) => void toggle(event, 'notificationsEnabled')} /></label>
        <label class="toggle"><span><strong>Offline cache</strong><small>Keep previously loaded titles available.</small></span><input type="checkbox" role="switch" aria-label="Offline cache" checked={data.offlineCacheEnabled} disabled={disabled} onchange={(event) => void toggle(event, 'offlineCacheEnabled')} /></label>
        <label class="field">Catalog source<select aria-label="Catalog source" value={data.preferredSyncSource} disabled={disabled} onchange={(event) => void select(event, 'preferredSyncSource')}><option value="AniList">AniList · anime and manga</option>{#if data.preferredSyncSource === 'MyAnimeList'}<option value="MyAnimeList" disabled>MyAnimeList · uses AniList</option>{/if}<option value="MangaDex">MangaDex · manga</option></select></label>
        <p class="hint">AniList includes anime and manga. To browse MangaDex, select it here and choose Manga or All in Discover.</p>
        {#if data.preferredSyncSource === 'MyAnimeList'}<p class="hint">MyAnimeList uses the AniList catalog. You can still import and export MAL XML.</p>{/if}
        {@render feedback('preferences')}
      </section>
      <section class="panel"><h2><Link2 size={18} />AniList</h2>
        <p class="hint">An account is optional. You can browse and track titles without connecting AniList.</p>
        <p class="connection" class:connected={data.isAniListConnected}>{data.isAniListConnected ? `Connected as ${data.aniListUsername}` : 'Not connected'}</p>
        {#if data.aniListLastSyncAt}<p class="hint">Last import · {new Date(data.aniListLastSyncAt).toLocaleString()}</p>{/if}
        <label class="field">Client ID<input aria-label="AniList Client ID" bind:value={clientId} oninput={() => clientIdDirty = true} autocomplete="off" disabled={disabled} /></label>
        <label class="field">Client secret<input aria-label="AniList Client secret" type="password" bind:value={clientSecret} autocomplete="new-password" placeholder={data.hasAniListClientSecret && !clientIdChanged ? 'Saved · enter to replace' : 'Enter your client secret'} disabled={disabled} /></label>
        {#if clientIdChanged && data.hasAniListClientSecret && !clientSecret.trim()}<p class="hint">Enter the secret for this Client ID. The saved secret belongs to your previous ID.</p>{/if}
        <details class="connection-help"><summary>How to connect AniList</summary><ol><li>Create an application in your AniList developer settings.</li><li>Set its redirect URL to <span class="mono">http://localhost:7575/callback/</span>.</li><li>Paste its Client ID and Client secret above, then connect and approve access in your browser.</li></ol><button class="text-button" disabled={disabled} onclick={() => void run('anilist-help', () => featureApi.openExternal('https://anilist.co/settings/developer'))}>Open AniList developer settings <ExternalLink size={13} /></button></details>
        <div class="actions"><button class="primary-button" disabled={disabled || !clientId.trim() || (!clientSecret.trim() && (clientIdChanged || !data.hasAniListClientSecret))} onclick={() => void run('connect', async () => { await onConnect(clientId.trim(), clientSecret.trim() || undefined); clientIdDirty = false; clientSecret = ''; message = 'AniList connected'; })}><ExternalLink size={15} />{busy === 'connect' || localBusy === 'connect' ? 'Waiting for your browser…' : data.isAniListConnected ? 'Reconnect' : 'Connect AniList'}</button>
          {#if data.isAniListConnected}<button bind:this={aniListImportButton} class="ghost-button" disabled={disabled} onclick={() => void askAniListImport()}><Download size={15} />Import from AniList</button><button class="text-button" disabled={disabled} onclick={() => void run('disconnect', async () => { await onDisconnect(); confirmAniListImport = false; message = 'AniList disconnected'; })}>Disconnect</button>{/if}
        </div>
        {#if data.isAniListConnected}
          <p class="hint">Import is one-way. Local changes are not sent to AniList. Disconnecting keeps your local library.</p>
          {#if confirmAniListImport}<div class="import-preview" role="group" aria-label="Confirm AniList import"><strong>Import your anime and manga from AniList?</strong><p class="hint">{importWarning}</p><p class="hint">You can export a copy from Your library data before importing.</p><div class="actions"><button bind:this={confirmImportButton} class="primary-button" disabled={disabled} onclick={() => void run('sync', async () => { const result = await onSync(); confirmAniListImport = false; message = `Imported ${result.importedCount} titles`; })}>{localBusy === 'sync' ? 'Importing…' : 'Import now'}</button><button class="text-button" disabled={disabled} onclick={cancelAniListImport}>Cancel</button></div></div>{/if}
        {/if}
        {@render feedback('anilist')}
      </section>
      <section class="panel"><h2>Recognition</h2><p class="hint">Ready to use from Recognize. Images are sent to trace.moe or SauceNAO only when you choose Find matches.</p>
        <label class="field">SauceNAO API key<input aria-label="SauceNAO API key" type="password" bind:value={sauceKey} autocomplete="new-password" placeholder={data.hasSauceNaoApiKey ? 'Saved · enter to replace' : 'Optional API key'} disabled={disabled} /></label>
        <div class="actions"><button class="ghost-button" disabled={disabled || !sauceKey.trim()} onclick={() => void run('save-key', async () => { await save({ sauceNaoApiKey: sauceKey.trim() }); sauceKey = ''; })}>Save key</button>{#if data.hasSauceNaoApiKey}<button class="text-button" disabled={disabled} onclick={() => void run('clear-key', () => save({ sauceNaoApiKey: '' }))}>Remove key</button>{/if}</div>
        <p class="hint">A SauceNAO key is optional. You can search without one.</p>
        <p class="hint secret-hint"><ShieldCheck size={14} />Saved secrets stay in the app's local settings.</p>
        {@render feedback('recognition')}
      </section>
      <section class="panel" aria-labelledby="library-data-heading"><h2 id="library-data-heading" tabindex="-1">Your library data</h2><p class="hint">Take your collection with you or bring in a MyAnimeList export.</p>
        <div class="actions"><button class="ghost-button" disabled={disabled} onclick={() => void exportLibrary('csv')}><Download size={15} />Export CSV</button><button class="ghost-button" disabled={disabled} onclick={() => void exportLibrary('malXml')}><Download size={15} />Export MAL XML</button></div>
        <label class="import-button ghost-button" class:disabled><Upload size={15} />Import MAL XML<input aria-label="Import MAL XML" type="file" accept=".xml,application/xml,text/xml" disabled={disabled} onchange={(event) => void pickImport(event)} /></label>
        {#if preview}<div class="import-preview"><strong>{preview.count} titles in {importName}</strong><p class="hint">{importWarning}</p><ul>{#each preview.items.slice(0, 5) as entry}<li>{entry.media.title} <span>{entry.progress} {entry.mediaType === 'Manga' ? 'chapters' : 'episodes'}</span></li>{/each}</ul>{#if preview.count > 5}<p class="hint">And {preview.count - 5} more titles.</p>{/if}<div class="actions"><button class="primary-button" disabled={disabled || !preview.count} onclick={() => void run('import', async () => { const result = await featureApi.importApply(importXml); preview = null; importXml = ''; onLibraryChanged(); message = `Imported ${result.importedCount} titles`; })}>Import {preview.count} titles</button><button class="text-button" disabled={disabled} onclick={() => { preview = null; importXml = ''; }}>Cancel</button></div></div>{/if}
        {@render feedback('library')}
      </section>
      <section class="panel update-panel"><h2>About TanaHub</h2><p class="hint">TanaHub · {update?.currentVersion ?? '0.1.0'}</p><button class="ghost-button" disabled={disabled} onclick={() => void run('updates', async () => { update = await featureApi.updates(); message = update.isUpdateAvailable ? `Version ${update.latestRelease?.version} is available` : update.latestRelease ? 'You are up to date' : 'No published release yet'; })}><RefreshCw size={15} />{localBusy === 'updates' ? 'Checking…' : 'Check for updates'}</button>{#if update?.latestRelease}<button class="text-button" onclick={() => void run('open', () => featureApi.openExternal(update!.latestRelease!.releaseUrl))}>View release <ExternalLink size={13} /></button>{/if}{@render feedback('about')}</section>
    </div>
  {/if}
</section>

<style>
  header { margin-bottom:28px; } h1 { font-size:30px; letter-spacing:-.035em; margin:0 0 8px; } header p { margin:0; color:var(--secondary); font-size:14px; }
  .settings-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:18px; max-width:1100px; align-items:start; }
  .panel { background:var(--shell); border:1px solid var(--border); border-radius:12px; padding:22px; min-width:0; } h2 { font-size:16px; font-weight:600; margin:0 0 18px; display:flex; align-items:center; gap:8px; }
  .field { display:flex; flex-direction:column; gap:8px; font-size:12px; color:var(--secondary); margin:16px 0; } .field input,.field select { width:100%; min-height:40px; }
  .toggle { display:flex; align-items:center; justify-content:space-between; gap:20px; padding:14px 0; border-bottom:1px solid var(--border); } .toggle strong { display:block; font-size:13px; font-weight:500; } .toggle small { display:block; color:var(--secondary); line-height:1.5; margin-top:4px; } .toggle input { accent-color:var(--violet); width:18px; height:18px; flex-shrink:0; }
  .hint { color:var(--secondary); font-size:12px; line-height:1.6; overflow-wrap:anywhere; } .connection { font-size:13px; color:var(--secondary); overflow-wrap:anywhere; } .connected { color:var(--green); } .actions { display:flex; flex-wrap:wrap; gap:8px; align-items:center; margin-top:12px; } .actions > button { min-height:40px; max-width:100%; white-space:normal; } .text-button { border:0; background:transparent; color:var(--lavender); padding:10px 4px; font-size:12px; display:inline-flex; gap:5px; align-items:center; }
  .feedback { padding:12px; background:var(--raised); border:1px solid var(--border); border-radius:8px; margin:16px 0 0; overflow-wrap:anywhere; } .success { display:flex; gap:8px; color:var(--green); font-size:13px; } .success :global(svg) { flex-shrink:0; } .secret-hint { display:flex; gap:6px; align-items:flex-start; margin-top:20px; } .secret-hint :global(svg) { flex-shrink:0; margin-top:3px; }
  .import-button { position:relative; overflow:hidden; margin-top:12px; border-radius:8px; min-height:40px; cursor:pointer; } .import-button input { position:absolute; opacity:0; inset:0; width:100%; cursor:pointer; } .import-button:focus-within { outline:2px solid var(--lavender); outline-offset:3px; } .disabled { opacity:.5; cursor:default; }
  .import-preview { margin-top:18px; border-top:1px solid var(--border); padding-top:18px; font-size:12px; } ul { padding-left:16px; color:var(--secondary); } li { padding:5px 0; overflow-wrap:anywhere; } li span { color:var(--muted); margin-left:6px; } .update-panel { grid-column:1 / -1; }
  .connection-help { margin: 16px 0; color: var(--secondary); font-size: 12px; line-height: 1.6; } .connection-help summary { cursor: pointer; padding: 6px 0; color: var(--lavender); } .connection-help ol { padding-left: 20px; }
  @media(max-width:760px) { .settings-grid { grid-template-columns:1fr; } .panel { padding:18px; } }
</style>
