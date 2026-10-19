import type { Entry, Media, MediaType, Page, Status } from './types';

export async function request<T>(method: string, params: Record<string, unknown> = {}): Promise<T> {
  if ('__TAURI_INTERNALS__' in window) {
    const { invoke } = await import('@tauri-apps/api/core');
    try {
      return await invoke<T>('backend_request', { method, params });
    } catch (error) {
      throw new Error(typeof error === 'string' ? error : 'Could not reach the library. Please try again.');
    }
  }
  if (!import.meta.env.DEV) throw new Error('Open TanaHub in the desktop app to access your library.');
  const response = await fetch('/api/rpc', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ method, params }), signal: AbortSignal.timeout(method === 'anilist.connect' ? 390000 : method === 'anilist.sync' ? 330000 : 150000),
  });
  const data = await response.json();
  if (!response.ok || data.error) throw new Error(data.error?.message ?? 'Could not reach the library. Please try again.');
  return data.result as T;
}

export const api = {
  library: (type: MediaType | '', status: Status | '', search: string, page = 1, filters: Record<string, unknown> = {}) => request<Page<Entry>>('library.list', { type: type || undefined, status: status || undefined, search, page, pageSize: 48, ...filters }),
  catalog: (type: MediaType | '', search: string, sort: string, page = 1, filters: Record<string, unknown> = {}) => request<Page<Media>>('catalog.search', { type: type || undefined, search, sort, page, pageSize: 24, ...filters }),
  lookup: (mediaIds: string[]) => request<{ items: Entry[] }>('library.lookup', { mediaIds }),
  detail: (mediaId: string) => request<Media>('media.detail', { mediaId }),
  entry: (mediaId: string) => request<Entry | null>('library.get', { mediaId }),
  add: (mediaId: string, status: Status = 'Planning') => request<Entry>('library.add', { mediaId, status }),
  progress: (mediaId: string, amount = 1) => request<Entry>('library.progress', { mediaId, amount }),
  score: (mediaId: string, score: number | null) => request<Entry>('library.score', { mediaId, score }),
  status: (mediaId: string, status: Status) => request<Entry>('library.status', { mediaId, status }),
  notes: (mediaId: string, notes: string) => request<Entry>('library.notes', { mediaId, notes }),
  remove: (mediaId: string) => request<{ removed: boolean }>('library.remove', { mediaId }),
};
