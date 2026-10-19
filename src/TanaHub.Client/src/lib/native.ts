export const isDesktop = () => '__TAURI_INTERNALS__' in window;

export async function openExternal(url: string) {
  const parsed = new URL(url);
  if (!['https:', 'http:'].includes(parsed.protocol) || parsed.username || parsed.password) throw new Error('This link cannot be opened.');
  if (isDesktop()) {
    const { invoke } = await import('@tauri-apps/api/core');
    await invoke('open_external', { url: parsed.href });
  } else window.open(parsed.href, '_blank', 'noopener,noreferrer');
}

export async function copyText(text: string): Promise<void> {
  if (isDesktop()) {
    const { writeText } = await import('@tauri-apps/plugin-clipboard-manager');
    await writeText(text);
  } else {
    if (!navigator.clipboard?.writeText) throw new Error('Clipboard is unavailable.');
    await navigator.clipboard.writeText(text);
  }
}

export async function saveExport(text: string, fileName: string, mimeType: string): Promise<boolean> {
  if (isDesktop()) {
    const { invoke } = await import('@tauri-apps/api/core');
    return invoke<boolean>('save_export', { text, fileName, format: fileName.endsWith('.xml') ? 'xml' : 'csv' });
  }
  const url = URL.createObjectURL(new Blob([text], { type: mimeType }));
  const anchor = document.createElement('a');
  anchor.href = url; anchor.download = fileName; anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
  return true;
}
