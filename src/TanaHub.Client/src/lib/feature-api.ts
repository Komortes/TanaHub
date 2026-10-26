import { request } from './api';
import { openExternal } from './native';
import type { Entry, MediaType } from './types';
import type { HomeSummary, Recommendations, ScheduleResult, NotificationSupport, Settings, SettingsPatch, RelatedTitlesResult, LibraryExport, ImportPreview, UpdateCheck, RecognitionResult, RecognitionHistoryItem } from './feature-types';
export const featureApi = {
  home: (type?: MediaType) => request<HomeSummary>('home.get', { type }),
  recommendations: (limit = 8, type?: MediaType) => request<Recommendations>('home.recommendations', { limit, type }),
  schedule: (from: string, to: string, libraryOnly = false) => request<ScheduleResult>('schedule.list', { from, to, libraryOnly }),
  scheduleNotify: (from: string, to: string) => request<{ notifiedCount: number }>('schedule.notify', { from, to }),
  notificationSupport: () => request<NotificationSupport>('notifications.support'),
  relations: (mediaId: string, forceRefresh = false) => request<RelatedTitlesResult>('media.related', { mediaId, forceRefresh }),
  progressSet: (mediaId: string, progress: number) => request<Entry>('library.progress.set', { mediaId, progress }),
  review: (mediaId: string, review: string) => request<Entry>('library.review', { mediaId, review }),
  organization: (mediaId: string, tags: string[], customLists: string[]) => request<Entry>('library.organization', { mediaId, tags, customLists }),
  dates: (mediaId: string, startedAt: string | null, completedAt: string | null) => request<Entry>('library.dates', { mediaId, startedAt, completedAt }),
  libraryFilters: () => request<{ tags: string[]; customLists: string[] }>('library.filters'),
  settings: () => request<Settings>('settings.get'),
  saveSettings: (patch: SettingsPatch) => request<Settings>('settings.save', patch),
  connect: (clientId?: string, clientSecret?: string) => request<Settings>('anilist.connect', { clientId, clientSecret }),
  sync: () => request<{ importedCount: number; settings: Settings }>('anilist.sync'),
  disconnect: () => request<Settings>('anilist.disconnect'),
  export: (format: 'csv' | 'malXml') => request<LibraryExport>('library.export', { format }),
  importPreview: (xml: string) => request<ImportPreview>('library.import.preview', { xml }),
  importApply: (xml: string) => request<{ importedCount: number }>('library.import.apply', { xml }),
  updates: () => request<UpdateCheck>('updates.check'),
  recognize: (provider: 'trace' | 'art', imageBase64: string, mimeType: string, sourceName: string) => request<RecognitionResult>('recognition.run', { provider, imageBase64, mimeType, sourceName, uploadConfirmed: true }),
  recognitionHistory: () => request<{ items: RecognitionHistoryItem[] }>('recognition.history'),
  removeRecognition: (id: string) => request<{ removed: boolean }>('recognition.remove', { id }),
  saveRecognition: (params: Record<string, unknown>) => request<RecognitionHistoryItem>('recognition.save', params),
  openExternal,
};
