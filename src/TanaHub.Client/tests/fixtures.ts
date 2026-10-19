import { test as base, expect, type Page as BrowserPage } from '@playwright/test';
import type { Entry, Media, Page, Status } from '../src/lib/types';
import type { HomeSummary, InsightSegment, RecognitionHistoryItem, RelatedTitlesResult, Settings, ScheduleResult } from '../src/lib/feature-types';

export interface RpcRequest {
  method: string;
  params: Record<string, unknown>;
}

const cover = (title: string, color: string) => 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(
  `<svg xmlns="http://www.w3.org/2000/svg" width="400" height="600" viewBox="0 0 400 600"><rect width="400" height="600" fill="${color}"/><circle cx="260" cy="150" r="170" fill="#dcd6fa" opacity=".14"/><path d="M0 430 160 180 400 420V600H0Z" fill="#131426" opacity=".5"/><text x="28" y="510" fill="#fff" font-size="24" font-family="sans-serif">${title.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')}</text></svg>`,
);

export const media = (id: string, title: string, options: Partial<Media> = {}): Media => ({
  id, title, romaji: title, type: 'Anime', posterUrl: cover(title, '#464275'), bannerUrl: null,
  format: 'Tv', releaseStatus: 'Finished', year: 2023, averageScore: 86, totalUnits: 12,
  description: `The story of ${title}.`, genres: ['Drama', 'Adventure'], ...options,
});

const initialMedia = [
  media('anilist:154587', "Frieren: Beyond Journey's End", { totalUnits: 28, nativeTitle: '葬送のフリーレン', episodeCount: 28, durationMinutes: 24, studio: 'MADHOUSE', characters: [{ name: 'Frieren', role: 'Main', imageUrl: cover('Frieren', '#464275') }] }),
  media('anilist:101922', 'Demon Slayer: Kimetsu no Yaiba', { year: 2019, totalUnits: 26, posterUrl: cover('Demon Slayer', '#6e424c') }),
  media('anilist:98436', 'The Apothecary Diaries', { totalUnits: 24, posterUrl: cover('The Apothecary Diaries', '#2b6b58') }),
  media('anilist:30002', 'Berserk', { type: 'Manga', format: 'Manga', year: 1989, totalUnits: null, posterUrl: cover('Berserk', '#873540') }),
  media('anilist:141', 'Serial Experiments Lain', { year: 1998, totalUnits: 13 }),
  media('anilist:128547', 'Odd Taxi', { year: 2021, totalUnits: 13, posterUrl: cover('Odd Taxi', '#985b3c') }),
  media('anilist:132029', 'Dandadan', { type: 'Manga', format: 'Manga', totalUnits: null }),
];

function entry(item: Media, options: Partial<Entry> = {}): Entry {
  return {
    mediaId: item.id, mediaType: item.type, status: 'Watching', progress: 3, score: null,
    notes: null, review: null, updatedAt: '2026-10-02T12:00:00Z', media: item, ...options,
  };
}

function paginate<T>(items: T[], params: Record<string, unknown>): Page<T> {
  const page = Number(params.page ?? 1);
  const pageSize = Number(params.pageSize ?? 48);
  const offset = (page - 1) * pageSize;
  return { items: items.slice(offset, offset + pageSize), page, pageSize, totalCount: items.length, hasNextPage: offset + pageSize < items.length };
}

/** An isolated in-memory RPC service. No request reaches the user's desktop library. */
export class MockLibrary {
  readonly requests: RpcRequest[] = [];
  private readonly seedMedia = structuredClone(initialMedia);
  readonly entries = new Map(this.seedMedia.slice(0, 4).map((item, index) => [item.id, entry(item, { score: index === 0 ? 9 : null })]));
  readonly catalog = new Map(this.seedMedia.map((item) => [item.id, item]));
  settings: Settings = { theme: 'Nebula dark', notificationsEnabled: true, offlineCacheEnabled: true, recognitionServicesEnabled: true, preferredSyncSource: 'AniList', aniListClientId: '', hasAniListClientSecret: false, hasSauceNaoApiKey: false, isAniListConnected: false, aniListUsername: '', aniListUserId: 0, aniListLastSyncAt: null };
  readonly recognitionHistory = new Map<string, RecognitionHistoryItem>();
  readonly relations = new Map<string, RelatedTitlesResult>();
  private recognitionSequence = 0;
  private failure: { method: string; message: string } | null = null;
  override: ((request: RpcRequest) => Promise<unknown | undefined>) | null = null;

  constructor() {
    const related = media('anilist:fixture-sequel', 'Frieren: The Next Journey', { year: 2026 });
    this.catalog.set(related.id, related);
    this.relations.set('anilist:154587', { titles: [{ mediaId: related.id, title: related.title, mediaType: 'Anime', format: related.format, relation: 'Sequel', source: 'AniList', sourceRelation: 'SEQUEL', year: related.year, episodeCount: 12, chapterCount: null, volumeCount: null, posterUrl: related.posterUrl }], notice: null, isStale: false });
  }

  failNext(method: string, message = 'Could not save your changes. Please try again.'): void {
    this.failure = { method, message };
  }

  async attach(page: BrowserPage): Promise<void> {
    await page.route('**/api/rpc', async (route) => {
      const request = route.request().postDataJSON() as RpcRequest;
      this.requests.push(request);
      if (this.failure?.method === request.method) {
        const message = this.failure.message;
        this.failure = null;
        await route.fulfill({ json: { error: { code: 'storage_error', message } } });
        return;
      }
      const overridden = await this.override?.(request);
      const result = overridden === undefined ? this.dispatch(request) : overridden;
      await route.fulfill({ json: { result } });
    });
  }

  private dispatch({ method, params }: RpcRequest): unknown {
    const item = this.catalog.get(String(params.mediaId));
    const existing = this.entries.get(String(params.mediaId));
    switch (method) {
      case 'library.list': {
        const search = String(params.search ?? '').toLocaleLowerCase();
        const records = [...this.entries.values()].filter((record) =>
          (!params.type || record.mediaType === params.type) &&
          (!params.status || record.status === params.status) &&
          (!params.tag || record.tags?.includes(String(params.tag))) &&
          (!params.customList || record.customLists?.includes(String(params.customList))) &&
          (!search || `${record.media.title} ${record.media.romaji} ${record.notes ?? ''}`.toLocaleLowerCase().includes(search)),
        );
        records.sort((left, right) => params.sort === 'Title' ? left.media.title.localeCompare(right.media.title)
          : params.sort === 'Score' ? (right.score ?? -1) - (left.score ?? -1)
            : params.sort === 'Progress' ? right.progress - left.progress
              : Date.parse(right.updatedAt) - Date.parse(left.updatedAt));
        return paginate(records, params);
      }
      case 'catalog.search': {
        const search = String(params.search ?? '').toLocaleLowerCase();
        return paginate([...this.catalog.values()].filter((record) =>
          (!params.type || record.type === params.type) && (!search || record.title.toLocaleLowerCase().includes(search)),
        ), params);
      }
      case 'media.detail': return item;
      case 'media.related': return this.relations.get(String(params.mediaId)) ?? { titles: [], notice: null, isStale: false };
      case 'library.filters': return { tags: [...new Set([...this.entries.values()].flatMap(record => record.tags ?? []))], customLists: [...new Set([...this.entries.values()].flatMap(record => record.customLists ?? []))] };
      case 'library.get': return existing ?? null;
      case 'library.lookup': return { items: [...new Set(params.mediaIds as string[])].flatMap(id => this.entries.has(id) ? [this.entries.get(id)!] : []) };
      case 'library.add': {
        if (!item) throw new Error('Unknown fixture media');
        const added = existing ?? entry(item, { status: (params.status ?? 'Planning') as Status, progress: 0 });
        this.entries.set(item.id, added);
        return added;
      }
      case 'library.score':
      case 'library.progress':
      case 'library.progress.set':
      case 'library.status':
      case 'library.notes':
      case 'library.review':
      case 'library.organization':
      case 'library.dates': {
        if (!existing) throw new Error('Unknown fixture entry');
        const changes = method === 'library.score' ? { score: params.score as number | null }
          : method === 'library.progress' ? { progress: existing.progress + Number(params.amount ?? 1) }
            : method === 'library.progress.set' ? { progress: Number(params.progress) }
            : method === 'library.status' ? { status: params.status as Status }
              : method === 'library.notes' ? { notes: params.notes as string | null }
                : method === 'library.review' ? { review: params.review as string | null }
                  : method === 'library.organization' ? { tags: params.tags as string[], customLists: params.customLists as string[] }
                    : { startedAt: params.startedAt as string | null, completedAt: params.completedAt as string | null };
        const updated = { ...existing, ...changes, updatedAt: '2026-10-02T12:01:00Z' };
        this.entries.set(existing.mediaId, updated);
        return updated;
      }
      case 'library.remove': return { removed: this.entries.delete(String(params.mediaId)) };
      case 'settings.get': return this.settings;
      case 'settings.save': {
        const { aniListClientSecret, sauceNaoApiKey, ...plain } = params;
        this.settings = { ...this.settings, ...plain };
        if (aniListClientSecret !== undefined) this.settings.hasAniListClientSecret = !!aniListClientSecret;
        if (sauceNaoApiKey !== undefined) this.settings.hasSauceNaoApiKey = !!sauceNaoApiKey;
        return this.settings;
      }
      case 'anilist.connect': this.settings = { ...this.settings, aniListClientId: String(params.clientId ?? this.settings.aniListClientId), hasAniListClientSecret: true, isAniListConnected: true, aniListUsername: 'Fixture user', aniListUserId: 42 }; return this.settings;
      case 'anilist.sync': this.settings = { ...this.settings, aniListLastSyncAt: '2026-10-03T08:00:00Z' }; return { importedCount: this.entries.size, settings: this.settings };
      case 'anilist.disconnect': this.settings = { ...this.settings, isAniListConnected: false, aniListUsername: '', aniListUserId: 0 }; return this.settings;
      case 'home.get': return this.home(params.type);
      case 'home.recommendations': return { items: [...this.catalog.values()].filter(record => !this.entries.has(record.id) && (!params.type || record.type === params.type)).slice(0, Number(params.limit ?? 8)), genres: [{ name: 'Adventure', weight: 2 }], notice: null };
      case 'schedule.list': return this.schedule(params);
      case 'schedule.notify': return { notifiedCount: 0 };
      case 'recognition.history': return { items: [...this.recognitionHistory.values()].reverse() };
      case 'recognition.remove': return { removed: this.recognitionHistory.delete(String(params.id)) };
      case 'recognition.save': return this.saveRecognition(params);
      case 'recognition.run': {
        if (params.uploadConfirmed !== true) throw new Error('Confirm sending this image to the selected recognition provider.');
        const title = initialMedia[0];
        const provider = params.provider === 'art' ? 'art' : 'trace';
        const match = { mediaId: title.id, title: title.title, romaji: title.romaji, nativeTitle: title.nativeTitle ?? null, episode: provider === 'trace' ? '4' : null, similarity: .96, thumbnailUrl: title.posterUrl, sourceUrl: 'https://example.test/source', author: provider === 'art' ? 'Fixture artist' : null };
        const saved = provider === 'trace' ? this.saveRecognition({ ...match, provider, sourceName: params.sourceName }) : null;
        return { provider, matches: [match], savedAttemptId: saved?.id ?? null };
      }
      case 'library.export': return { text: params.format === 'malXml' ? '<?xml version="1.0"?><myanimelist></myanimelist>' : 'title,progress\nFrieren,3', fileName: params.format === 'malXml' ? 'library.xml' : 'library.csv', mimeType: params.format === 'malXml' ? 'application/xml' : 'text/csv', exportedCount: this.entries.size, skippedCount: 0 };
      case 'library.import.preview': return { items: [...this.entries.values()].slice(0, 1), count: 1 };
      case 'library.import.apply': return { importedCount: 1 };
      case 'updates.check': return { currentVersion: '0.1.0', isUpdateAvailable: true, latestRelease: { version: '0.2.0', tagName: 'v0.2.0', name: 'TanaHub 0.2.0', releaseUrl: 'https://example.test/release', publishedAt: '2026-10-03T08:00:00Z' } };
      default: throw new Error(`Unexpected RPC method: ${method}`);
    }
  }

  private saveRecognition(params: Record<string, unknown>): RecognitionHistoryItem {
    const saved: RecognitionHistoryItem = { id: `recognition:${++this.recognitionSequence}`, createdAt: '2026-10-03T08:00:00Z', provider: String(params.provider ?? 'trace'), sourceName: String(params.sourceName ?? 'Screenshot.png'), mediaId: params.mediaId as string | null, title: String(params.title), romajiTitle: params.romaji as string | null ?? null, nativeTitle: params.nativeTitle as string | null ?? null, episode: params.episode as string | null ?? null, similarity: params.similarity as number | null ?? null, thumbnailUrl: params.thumbnailUrl as string | null ?? null };
    this.recognitionHistory.set(saved.id, saved);
    return saved;
  }

  private home(type: unknown): HomeSummary {
    const records = [...this.entries.values()].filter(record => !type || record.mediaType === type);
    const completed = records.filter(record => record.status === 'Completed').length;
    const dropped = records.filter(record => record.status === 'Dropped').length;
    const scored = records.filter(record => record.score !== null);
    const average = scored.length ? scored.reduce((sum, record) => sum + record.score!, 0) / scored.length : null;
    const segments = (key: 'mediaType' | 'status'): InsightSegment[] => [...new Set(records.map(record => record[key]))].map(value => { const matches = records.filter(record => record[key] === value); const finished = matches.filter(record => record.status === 'Completed').length; const rated = matches.filter(record => record.score !== null); return { key: value, count: matches.length, completedCount: finished, completionRate: finished / matches.length, averageScore: rated.length ? rated.reduce((sum, record) => sum + record.score!, 0) / rated.length : null }; });
    const episodes = records.filter(record => record.mediaType === 'Anime').reduce((sum, record) => sum + record.progress, 0);
    return { insights: { totalEntries: records.length, completedEntries: completed, backlogEntries: records.filter(record => record.status === 'Planning').length, droppedEntries: dropped, completionRate: records.length ? completed / records.length : 0, droppedRatio: records.length ? dropped / records.length : 0, averageScore: average, episodesWatched: episodes, chaptersRead: records.filter(record => record.mediaType === 'Manga').reduce((sum, record) => sum + record.progress, 0), estimatedWatchMinutes: episodes * 24, byMediaType: segments('mediaType'), byStatus: segments('status') }, continueItems: records.filter(record => ['Watching', 'Repeating'].includes(record.status)), recentItems: records };
  }

  private schedule(params: Record<string, unknown>): ScheduleResult {
    const from = String(params.from);
    const to = String(params.to);
    const start = new Date(from).getTime();
    const records = [this.seedMedia[0], this.seedMedia[5]].map((item, index) => ({ mediaId: item.id, title: item.title, episode: 4 + index, airingAt: new Date(start + (index + 1) * 3_600_000).toISOString(), format: item.format, releaseStatus: 'Releasing', posterUrl: item.posterUrl, inLibrary: this.entries.has(item.id), status: this.entries.get(item.id)?.status ?? null }));
    return { from, to, items: records.filter(item => Date.parse(item.airingAt) < Date.parse(to) && (!params.libraryOnly || item.inLibrary)) };
  }
}

export const test = base.extend<{ library: MockLibrary; appErrors: string[] }>({
  library: async ({ page }, use) => {
    const library = new MockLibrary();
    await library.attach(page);
    await use(library);
  },
  appErrors: [async ({ page }, use) => {
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    page.on('console', (message) => { if (message.type() === 'error') errors.push(message.text()); });
    await use(errors);
    expect(errors, 'The rendered app should not emit runtime or console errors').toEqual([]);
  }, { auto: true }],
});

export { expect };
