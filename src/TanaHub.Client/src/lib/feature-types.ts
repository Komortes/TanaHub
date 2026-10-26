import type { Entry, Media, MediaType, Status } from './types';
export interface InsightSegment { key: string; count: number; completedCount: number; completionRate: number; averageScore: number | null }
export interface HomeSummary {
  insights: { totalEntries: number; completedEntries: number; backlogEntries: number; droppedEntries: number; completionRate: number; droppedRatio: number; averageScore: number | null; episodesWatched: number; chaptersRead: number; estimatedWatchMinutes: number; byMediaType: InsightSegment[]; byStatus: InsightSegment[] };
  continueItems: Entry[]; recentItems: Entry[];
}
export interface Recommendations { items: Media[]; genres: { name: string; weight: number }[]; notice: string | null }
export interface ScheduleResult { items: { mediaId: string; title: string; episode: number; airingAt: string; format: string; releaseStatus: string; posterUrl: string | null; inLibrary: boolean; status: Status | null }[]; from: string; to: string }
export interface NotificationSupport { supported: boolean; reason: string | null }
export interface Settings {
  theme: string; notificationsEnabled: boolean; offlineCacheEnabled: boolean; recognitionServicesEnabled: boolean; preferredSyncSource: string; aniListClientId: string; hasAniListClientSecret: boolean; hasSauceNaoApiKey: boolean; isAniListConnected: boolean; aniListUsername: string; aniListUserId: number; aniListLastSyncAt: string | null;
}
export type SettingsPatch = Partial<Pick<Settings, 'theme' | 'notificationsEnabled' | 'offlineCacheEnabled' | 'recognitionServicesEnabled' | 'preferredSyncSource' | 'aniListClientId'>> & { aniListClientSecret?: string; sauceNaoApiKey?: string };
export interface RelatedTitle { mediaId: string; title: string; mediaType: MediaType; format: string; relation: string; source: string; sourceRelation: string; year: number | null; episodeCount: number | null; chapterCount: number | null; volumeCount: number | null; posterUrl: string | null }
export interface RelatedTitlesResult { titles: RelatedTitle[]; notice: string | null; isStale: boolean }
export interface LibraryExport { text: string; fileName: string; mimeType: string; exportedCount: number; skippedCount: number }
export interface ImportPreview { items: Entry[]; count: number }
export interface UpdateCheck { currentVersion: string; isUpdateAvailable: boolean; latestRelease: { version: string; tagName: string; name: string; releaseUrl: string; publishedAt: string | null } | null }

export interface RecognitionMatch { mediaId: string | null; title: string; romaji: string | null; nativeTitle: string | null; episode: string | null; similarity: number; thumbnailUrl: string | null; sourceUrl: string | null; author: string | null }
export interface RecognitionResult { provider: 'trace' | 'art'; matches: RecognitionMatch[]; savedAttemptId: string | null }
export interface RecognitionHistoryItem { id: string; createdAt: string; provider: string; sourceName: string; mediaId: string | null; title: string; romajiTitle: string | null; nativeTitle: string | null; episode: string | null; similarity: number | null; thumbnailUrl: string | null }
