export type MediaType = 'Anime' | 'Manga';
export type Status = 'Watching' | 'Planning' | 'Completed' | 'Paused' | 'Dropped' | 'Repeating';
export type PageName = 'home' | 'library' | 'discover' | 'schedule' | 'recognize' | 'settings';
export type Layout = 'grid' | 'list';

export interface Media {
  id: string;
  type: MediaType;
  title: string;
  romaji: string;
  posterUrl: string | null;
  bannerUrl: string | null;
  format: string;
  releaseStatus: string;
  year: number | null;
  averageScore: number | null;
  totalUnits: number | null;
  description: string | null;
  genres: string[];
  nativeTitle?: string | null;
  episodeCount?: number | null;
  durationMinutes?: number | null;
  studio?: string | null;
  chapterCount?: number | null;
  volumeCount?: number | null;
  characters?: { name: string; role: string; imageUrl: string | null }[];
}

export interface Entry {
  mediaId: string;
  mediaType: MediaType;
  status: Status;
  progress: number;
  score: number | null;
  notes: string | null;
  review: string | null;
  updatedAt: string;
  media: Media;
  tags?: string[];
  customLists?: string[];
  startedAt?: string | null;
  completedAt?: string | null;
}

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number | null;
  hasNextPage: boolean;
}

export const statuses: Status[] = ['Watching', 'Planning', 'Completed', 'Paused', 'Dropped', 'Repeating'];
export const maxProgress = 2_147_483_647;
export const mediaKey = (id: string) => id.replace(/[A-Z]/g, letter => letter.toLowerCase());
export const statusLabel = (status: Status, type: MediaType) =>
  status === 'Watching' && type === 'Manga' ? 'Reading' : status === 'Repeating' ? (type === 'Manga' ? 'Rereading' : 'Rewatching') : status;
export const unitLabel = (type: MediaType) => type === 'Manga' ? 'CH' : 'EP';
export const formatLabel = (format: string) => format === 'Tv' ? 'TV' : format.replace(/([a-z])([A-Z])/g, '$1 $2');
export const ratingLabel = (score: number | null) => score === null ? 'Not rated yet' :
  ['Terrible', 'Awful', 'Bad', 'Poor', 'Below average', 'Average', 'Fine', 'Good', 'Great', 'Excellent', 'Masterpiece'][score];
