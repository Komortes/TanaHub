<script lang="ts">
  import { Upload, Clipboard, ScanSearch, Image, X, Settings2, History, Trash2, RefreshCw, ArrowUpRight, AlertCircle } from '@lucide/svelte';
  import { featureApi } from '../feature-api';
  import type { PageName } from '../types';
  import type { RecognitionResult, RecognitionMatch, RecognitionHistoryItem } from '../feature-types';
  import MiniCover from './MiniCover.svelte';
  import RecognitionMatchCard from './RecognitionMatchCard.svelte';

  let { onOpenId, onAddId, onNavigate, active: pageActive = true }: { onOpenId: (id: string) => void; onAddId: (id: string) => Promise<void>; onNavigate: (page: PageName) => void; active?: boolean } = $props();
  let provider = $state<'trace' | 'art'>('trace');
  let fileInput: HTMLInputElement;
  let sourceName = $state('');
  let mime = $state('');
  let imageBase64 = $state('');
  let preview = $state('');
  let fileSize = $state(0);
  let dragging = $state(false);
  let reading = $state(false);
  let working = $state(false);
  let result = $state<RecognitionResult | null>(null);
  let history = $state<RecognitionHistoryItem[]>([]);
  let historyLoading = $state(true);
  let historyError = $state('');
  let error = $state('');
  let notice = $state('');
  let busy = $state<string[]>([]);
  let savedMatches = $state<string[]>([]);
  let fileGeneration = 0;
  let clipboardGeneration = 0;
  let runGeneration = 0;
  let historyGeneration = 0;
  let historyChanges: Map<string, RecognitionHistoryItem | null> | null = null;
  let active = true;
  const providerName = $derived(provider === 'trace' ? 'trace.moe' : 'SauceNAO');
  const matchKey = (match: RecognitionMatch) => `${match.mediaId ?? match.sourceUrl ?? match.title}:${match.episode ?? ''}`;
  const date = (value: string) => new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value));

  $effect(() => {
    active = true;
    void loadHistory();
    return () => { active = false; fileGeneration += 1; runGeneration += 1; historyGeneration += 1; };
  });
  $effect(() => {
    // A clipboard request from the previous visit must stay cancelled after Back.
    if (!pageActive) clipboardGeneration += 1;
  });
  async function loadHistory() {
    const current = ++historyGeneration;
    const changes = new Map<string, RecognitionHistoryItem | null>();
    historyChanges = changes;
    historyLoading = true; historyError = '';
    try {
      const data = await featureApi.recognitionHistory();
      if (active && current === historyGeneration) {
        // Keep confirmed writes that finished after this snapshot was requested.
        // Other rows from the refreshed source still belong in the result.
        const saved = [...changes.values()].reverse().filter((item): item is RecognitionHistoryItem => item !== null);
        history = [...saved, ...data.items.filter(item => !changes.has(item.id))];
      }
    }
    catch (reason) { if (active && current === historyGeneration) historyError = reason instanceof Error ? reason.message : 'Could not load recognition history.'; }
    finally { if (active && current === historyGeneration) { historyLoading = false; historyChanges = null; } }
  }
  function changeProvider(next: 'trace' | 'art') { provider = next; result = null; error = ''; notice = ''; savedMatches = []; runGeneration += 1; }
  function clearImage() { sourceName = ''; imageBase64 = ''; mime = ''; preview = ''; fileSize = 0; result = null; error = ''; notice = ''; savedMatches = []; reading = false; fileGeneration += 1; if (fileInput) fileInput.value = ''; }
  async function selectFile(file?: File) {
    if (!file || working) return;
    const current = ++fileGeneration;
    const supported = ['image/jpeg', 'image/png', 'image/webp'];
    const inferred = /\.jpe?g$/i.test(file.name) ? 'image/jpeg' : /\.png$/i.test(file.name) ? 'image/png' : /\.webp$/i.test(file.name) ? 'image/webp' : '';
    const contentType = file.type || inferred;
    error = ''; notice = ''; result = null; imageBase64 = ''; preview = ''; sourceName = ''; savedMatches = []; reading = false;
    if (!supported.includes(contentType)) { error = 'Choose a JPG, PNG, or WebP image.'; return; }
    if (file.size > 5 * 1024 * 1024) { error = 'This image is too large. Choose an image under 5 MB.'; return; }
    if (!file.size) { error = 'This image is empty. Choose another image.'; return; }
    reading = true;
    try {
      const data = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => typeof reader.result === 'string' ? resolve(reader.result) : reject(new Error('Could not read this image.'));
        reader.onerror = () => reject(new Error('Could not read this image.'));
        reader.onabort = () => reject(new Error('Image reading was interrupted.'));
        reader.readAsDataURL(file);
      });
      if (!active || current !== fileGeneration) return;
      imageBase64 = data.slice(data.indexOf(',') + 1); preview = data; mime = contentType; sourceName = file.name || 'Clipboard image'; fileSize = file.size;
    } catch (reason) { if (active && current === fileGeneration) error = reason instanceof Error ? reason.message : 'Could not read this image.'; }
    finally { if (active && current === fileGeneration) reading = false; }
  }
  function paste(event: ClipboardEvent) {
    if (!pageActive || working || (event.target instanceof HTMLElement && ['INPUT', 'TEXTAREA', 'SELECT'].includes(event.target.tagName))) return;
    const file = [...(event.clipboardData?.files ?? [])].find(item => item.type.startsWith('image/'));
    if (file) { event.preventDefault(); void selectFile(file); }
  }
  async function pasteImage() {
    if (!pageActive) return;
    const current = ++clipboardGeneration;
    if (!navigator.clipboard?.read) { error = 'Paste an image with Ctrl+V or ⌘V, or choose an image file.'; return; }
    try {
      const items = await navigator.clipboard.read();
      if (!active || !pageActive || current !== clipboardGeneration) return;
      for (const item of items) {
        const contentType = item.types.find(type => ['image/png', 'image/jpeg', 'image/webp'].includes(type));
        if (contentType) {
          const blob = await item.getType(contentType);
          if (!active || !pageActive || current !== clipboardGeneration) return;
          await selectFile(new File([blob], `Clipboard image.${contentType.split('/')[1]}`, { type: contentType })); return;
        }
      }
      error = 'The clipboard does not contain a supported image.';
    } catch { if (active && pageActive && current === clipboardGeneration) error = 'Paste an image with Ctrl+V or ⌘V, or choose an image file.'; }
  }
  async function recognize() {
    if (working || reading || !imageBase64) return;
    const current = ++runGeneration;
    working = true; error = ''; notice = ''; result = null; savedMatches = [];
    try {
      const data = await featureApi.recognize(provider, imageBase64, mime, sourceName);
      if (!active || current !== runGeneration) return;
      result = data;
      if (data.savedAttemptId && data.matches[0]) savedMatches = [matchKey(data.matches[0])];
      await loadHistory();
    } catch (reason) { if (active && current === runGeneration) error = reason instanceof Error ? reason.message : 'Could not recognize this image. Try again.'; }
    finally { if (active && current === runGeneration) working = false; }
  }
  async function action(key: string, run: () => Promise<void>) {
    if (busy.includes(key)) return;
    busy = [...busy, key]; error = ''; notice = '';
    try { await run(); }
    catch (reason) { error = reason instanceof Error ? reason.message : 'Could not complete this action.'; }
    finally { busy = busy.filter(item => item !== key); }
  }
  async function saveMatch(match: RecognitionMatch) {
    const saved = await featureApi.saveRecognition({ provider, mediaId: match.mediaId, title: match.title, romaji: match.romaji, nativeTitle: match.nativeTitle, episode: match.episode, similarity: match.similarity, thumbnailUrl: match.thumbnailUrl, sourceName });
    if (!active) return;
    historyChanges?.set(saved.id, saved);
    savedMatches = [...savedMatches, matchKey(match)]; history = [saved, ...history.filter(item => item.id !== saved.id)]; notice = 'Match saved to recognition history.';
  }
  async function removeHistory(item: RecognitionHistoryItem) {
    const response = await featureApi.removeRecognition(item.id);
    if (!response.removed) throw new Error('This history item could not be removed. Try again.');
    if (active) {
      historyChanges?.set(item.id, null);
      history = history.filter(record => record.id !== item.id);
      if (!history.some(record => record.mediaId === item.mediaId && record.title === item.title && record.episode === item.episode)) {
        const removedKeys = (result?.matches ?? []).filter(match => match.mediaId === item.mediaId && match.title === item.title && match.episode === item.episode).map(matchKey);
        savedMatches = savedMatches.filter(key => !removedKeys.includes(key));
      }
    }
  }
</script>

<svelte:window onpaste={paste} />
<header class="recognize-header"><div><h1>Recognize</h1><p>Find the story behind an image.</p></div><button class="ghost-button" aria-label="Recognition settings" title="Recognition settings" onclick={() => onNavigate('settings')}><Settings2 size={15} />Settings</button></header>
<div class="provider-tabs" aria-label="Recognition service"><button class:selected={provider === 'trace'} aria-pressed={provider === 'trace'} disabled={working} onclick={() => changeProvider('trace')}>Anime screenshot<span>trace.moe</span></button><button class:selected={provider === 'art'} aria-pressed={provider === 'art'} disabled={working} onclick={() => changeProvider('art')}>Artwork &amp; manga<span>SauceNAO</span></button></div>
<input class="file-input" bind:this={fileInput} type="file" accept="image/jpeg,image/png,image/webp" aria-label="Choose recognition image" onchange={(event) => void selectFile(event.currentTarget.files?.[0])} disabled={working} />
<div class="recognition-workspace">
  <section class:dragging class="image-panel" aria-label="Recognition image" ondragover={(event) => { event.preventDefault(); if (!working) dragging = true; }} ondragleave={() => dragging = false} ondrop={(event) => { event.preventDefault(); dragging = false; void selectFile(event.dataTransfer?.files[0]); }}>
    {#if preview}<div class="source-preview"><img src={preview} alt="Selected source for recognition" /><button class="icon-button clear-image" aria-label="Clear recognition image" onclick={clearImage} disabled={working}><X size={17} /></button></div><div class="source-caption"><strong title={sourceName}>{sourceName}</strong><span class="mono">{(fileSize / 1024 / 1024).toFixed(2)} MB</span></div>
    {:else}<div class="upload-empty"><span class="upload-icon"><Upload size={30} strokeWidth={1.4} /></span><h2>{dragging ? 'Drop your image here' : 'Choose or drop an image'}</h2><p>{provider === 'trace' ? 'Use a clear anime screenshot for the best chance of finding a match.' : 'Find artwork, manga panels, or illustrations and their original source.'}</p><span>JPG, PNG, or WebP · up to 5 MB</span></div>{/if}
    <div class="upload-actions"><button class="ghost-button" onclick={() => fileInput.click()} disabled={working || reading}><Image size={15} />{preview ? 'Change image' : 'Choose image'}</button><button class="ghost-button" onclick={() => void pasteImage()} disabled={working || reading}><Clipboard size={15} />Paste image</button></div><button class="primary-button recognize-button" aria-describedby="recognition-upload-note" onclick={() => void recognize()} disabled={working || reading || !imageBase64}><ScanSearch size={17} />{working ? `Searching ${providerName}…` : reading ? 'Reading image…' : `Find matches with ${providerName}`}</button><p id="recognition-upload-note" class="provider-note">Find matches sends this image to <strong>{providerName}</strong>. Choosing or pasting an image keeps it here until you search.</p>
  </section>
  <section class="results-panel" aria-label="Recognition results"><div class="section-heading"><h2>Matches</h2>{#if result}<span>{result.matches.length} {result.matches.length === 1 ? 'match' : 'matches'}</span>{/if}</div>
    {#if working}<div class="results-empty" role="status"><ScanSearch size={30} strokeWidth={1.4} /><h3>Looking for a match</h3><p>Searching {providerName}. This can take a moment.</p></div>
    {:else if result?.matches.length}<div class="matches">{#each result.matches as match, index (`${matchKey(match)}:${index}`)}<RecognitionMatchCard {match} saved={savedMatches.includes(matchKey(match))} busy={busy.includes(matchKey(match))} onOpen={() => match.mediaId && onOpenId(match.mediaId)} onAdd={() => void action(matchKey(match), async () => { if (match.mediaId) { await onAddId(match.mediaId); if (active) notice = 'Added to your library.'; } })} onSource={() => void action(matchKey(match), async () => { if (match.sourceUrl) await featureApi.openExternal(match.sourceUrl); })} onSave={() => void action(matchKey(match), () => saveMatch(match))} />{/each}</div>
    {:else if result}<div class="results-empty"><ScanSearch size={30} strokeWidth={1.4} /><h3>No matches found</h3><p>Try a clearer image, a different crop, or the other recognition service.</p></div>
    {:else}<div class="results-empty"><ScanSearch size={30} strokeWidth={1.4} /><h3>Your matches will appear here</h3><p>Choose an image, then search to find a title or the original artwork.</p></div>{/if}
  </section>
</div>
{#if error}<div class="recognition-error" role="alert"><AlertCircle size={17} /><p>{error}</p></div>{/if}
{#if notice}<p class="recognition-notice" role="status">{notice}</p>{/if}
<section class="recognition-history" aria-label="Recognition history"><div class="section-heading"><h2><History size={17} />Recognition history</h2><button class="icon-button" aria-label="Refresh recognition history" onclick={() => void loadHistory()} disabled={historyLoading}><RefreshCw size={15} /></button></div>
  {#if historyError}<p class="error-text" role="status">{historyError}</p>
  {:else if historyLoading && !history.length}<p class="history-empty" role="status">Loading recognition history…</p>
  {:else if !history.length}<p class="history-empty">Your saved recognition matches will appear here.</p>
  {:else}<div class="history-rows">{#each history as item (item.id)}<article class="history-row" aria-label={`Recognition history ${item.title}`}><div class="history-cover"><MiniCover url={item.thumbnailUrl} title={item.title} /></div><div class="history-copy"><h3>{item.title}</h3><p>{item.sourceName}<span>· {item.provider}</span></p><div><time datetime={item.createdAt}>{date(item.createdAt)}</time>{#if item.episode}<span class="mono">EP {item.episode}</span>{/if}{#if item.similarity !== null}<span class="mono">{(item.similarity * 100).toFixed(1)}%</span>{/if}</div></div><div class="history-actions">{#if item.mediaId}<button class="ghost-button" aria-label={`Open history ${item.title}`} onclick={() => item.mediaId && onOpenId(item.mediaId)}><ArrowUpRight size={14} />Open</button><button class="ghost-button" aria-label={`Add history ${item.title} to library`} onclick={() => void action(item.id, async () => { if (item.mediaId) await onAddId(item.mediaId); })} disabled={busy.includes(item.id)}>Add</button>{/if}<button class="icon-button" aria-label={`Remove recognition ${item.title}`} onclick={() => void action(item.id, () => removeHistory(item))} disabled={busy.includes(item.id)}><Trash2 size={16} /></button></div></article>{/each}</div>{/if}
</section>

<style>
  .recognize-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 24px; }
  h1 { font-size: 30px; font-weight: 700; letter-spacing: -.035em; line-height: 1.2; margin: 0 0 8px; }
  .recognize-header p { color: var(--secondary); font-size: 14px; margin: 0; }
  .provider-tabs { display: grid; grid-template-columns:repeat(2,minmax(0,1fr)); max-width:520px; gap: 8px; margin-bottom: 24px; }
  .provider-tabs button { display: flex; align-items: center; gap: 12px; padding: 10px 16px; color: var(--secondary); background: var(--shell); border: 1px solid var(--border); font-size: 12px; }
  .provider-tabs button > span { font-size: 11px; color: var(--secondary); }
  .provider-tabs button.selected { border-color: var(--border-lit); color: var(--lavender); background: var(--violet-soft); }
  .file-input { display: none; }
  .recognition-workspace { display: grid; grid-template-columns: minmax(280px,.9fr) minmax(0,1.1fr); gap: 24px; align-items: start; }
  .image-panel { border: 1px dashed var(--border-lit); border-radius: 12px; background: var(--shell); padding: 20px; transition: border-color 160ms var(--ease), background 160ms var(--ease); min-width: 0; }
  .image-panel.dragging { background: var(--violet-soft); border-color: var(--violet); }
  .upload-empty { min-height: 220px; display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; padding: 20px 10px; }
  .upload-icon { width: 60px; height: 60px; display: grid; place-items: center; border-radius: 14px; background: var(--violet-soft); color: var(--lavender); margin-bottom: 18px; }
  .upload-empty h2 { margin: 0 0 10px; font-size: 16px; font-weight: 600; }
  .upload-empty p { color: var(--secondary); line-height: 1.7; font-size: 12px; max-width: 280px; margin: 0 0 12px; }
  .upload-empty > span:last-child { color: var(--muted); font-size: 11px; }
  .upload-actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 10px; margin: 16px 0; }
  .upload-actions button { min-height:40px; }
  .source-preview { position: relative; min-height: 160px; border-radius: 8px; overflow: hidden; background: var(--ink); }
  .source-preview > img { width: 100%; display: block; max-height: 340px; object-fit: contain; }
  .clear-image { position: absolute; top: 8px; right: 8px; width:40px; height:40px; background: var(--shell); border: 1px solid var(--border); }
  .source-caption { display: flex; justify-content: space-between; gap: 10px; align-items: center; margin-top: 12px; font-size: 11px; color: var(--secondary); }
  .source-caption > strong { font-weight: 500; min-width: 0; overflow-wrap: anywhere; display:-webkit-box; -webkit-box-orient:vertical; -webkit-line-clamp:2; line-clamp:2; overflow:hidden; line-height:1.5; }
  .source-caption > span { white-space: nowrap; font-size: 10px; color: var(--muted); }
  .recognize-button { width: 100%; min-height:44px; white-space:normal; }
  .provider-note { font-size: 11px; line-height: 1.6; color: var(--secondary); margin: 12px 0 0; text-align: center; text-wrap:pretty; }
  .provider-note strong { font-weight:500; color:var(--text); }
  .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-bottom: 16px; }
  .section-heading h2 { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 15px; font-weight: 600; }
  .section-heading > span { color: var(--muted); font-size: 11px; }
  .results-empty { min-height: 290px; display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; color: var(--secondary); border: 1px solid var(--border); border-radius: 10px; background: var(--shell); padding: 28px; }
  .results-empty h3 { font-size: 14px; font-weight: 600; color: var(--text); margin: 16px 0 8px; }
  .results-empty p { font-size: 12px; line-height: 1.7; max-width: 280px; margin: 0; }
  .matches { display: flex; flex-direction: column; gap: 12px; }
  .recognition-error { display: flex; align-items: center; gap: 10px; color: var(--red); border: 1px solid var(--border); border-radius: 8px; padding: 14px; margin-top: 18px; }
  .recognition-error p { font-size: 12px; line-height: 1.6; margin: 0; }
  .recognition-notice { color: var(--green); font-size: 12px; margin-top: 18px; }
  .recognition-history { border-top: 1px solid var(--border); padding-top: 24px; margin-top: 32px; }
  .history-empty { font-size: 12px; line-height: 1.6; color: var(--secondary); margin: 0; }
  .history-rows { display: flex; flex-direction: column; gap: 10px; }
  .history-row { display: grid; grid-template-columns: 56px minmax(0,1fr) auto; gap: 16px; align-items: center; border: 1px solid var(--border); border-radius: 9px; background: var(--shell); padding: 12px; }
  .history-copy { min-width: 0; }
  .history-copy h3 { font-size: 13px; font-weight: 500; line-height: 1.5; overflow-wrap: anywhere; margin: 0 0 5px; }
  .history-copy p { display: flex; flex-wrap: wrap; gap: 5px; font-size: 11px; line-height: 1.5; color: var(--secondary); margin: 0 0 7px; overflow-wrap: anywhere; }
  .history-copy > div { display: flex; align-items: center; flex-wrap: wrap; gap: 8px 12px; color: var(--muted); font-size: 10px; }
  .history-copy > div > .mono { color: var(--cyan); }
  .history-actions { display: flex; align-items: center; gap: 6px; }
  .history-actions .ghost-button { min-height: 34px; font-size: 11px; padding: 7px 10px; }
  @media (max-width: 1050px) { .recognition-workspace { grid-template-columns: minmax(0,1fr); } .source-preview > img { max-height: 320px; } }
  @media (max-width: 640px) { h1 { font-size: 28px; } .recognize-header > button { font-size: 0; padding: 10px; } .provider-tabs { gap: 6px; } .provider-tabs button { flex-direction: column; align-items: flex-start; gap: 5px; padding: 10px 12px; font-size: 11px; } .image-panel { padding: 16px; } .upload-actions { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); } .upload-actions button { font-size:11px; padding:10px 8px; } .upload-empty { min-height:180px; padding:16px 8px; } .results-empty { min-height:220px; padding:22px; } .history-row { grid-template-columns: 52px minmax(0,1fr); gap: 10px 14px; } .history-actions { grid-column: 2; } .history-cover { grid-row: 1 / span 2; align-self: start; } }
</style>
