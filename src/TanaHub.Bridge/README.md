# TanaHub bridge

The Tauri frontend talks to the existing C# application and infrastructure through a local stdio process. The bridge uses the same SQLite library, offline catalog cache and settings as the Avalonia app. `TANAHUB_DATA_DIRECTORY` keeps its current meaning: an absolute override of the default application-data `TanaHub` directory. Test runs always set an isolated temporary directory.

Build with `dotnet build src/TanaHub.Bridge/TanaHub.Bridge.csproj`; run the resulting executable or `dotnet src/TanaHub.Bridge/bin/Debug/net10.0/TanaHub.Bridge.dll`. Avoid `dotnet run` in a protocol connection because build messages may appear on stdout.

Each UTF-8 newline carries one request and one response. Requests run in order. EOF exits the process. Diagnostics go to stderr; stdout contains JSON only.

```json
{"id":"request-1","method":"library.score","params":{"mediaId":"anilist:154587","score":9}}
```

Success is `{ "id": "request-1", "result": ... }`; failure is `{ "id": "request-1", "error": { "code": "validation_error", "message": "..." } }`. Invalid JSON has `id: null`; malformed input does not terminate the process. Codes are `invalid_request`, `validation_error`, `method_not_found`, `not_found`, `external_service_error`, `storage_error`, `cancelled` and `internal_error`.

| Method | Parameters | Result |
| --- | --- | --- |
| `library.list` | `page=1`, `pageSize=48`, optional `type`, `status`, `search` | Page of library entries |
| `library.get` | `mediaId` | Library entry or `null` when absent |
| `library.lookup` | `mediaIds` (0–50 nonempty strings of at most 200 characters) | `{items:Entry[]}`; current local entries in requested order, duplicate and missing IDs omitted, no remote hydration |
| `catalog.search` | `page=1`, `pageSize=48`, optional `type`, `search`, `sort` | Page of media |
| `media.detail` | `mediaId` | Media |
| `library.add` | `mediaId`, `status=Planning` | Library entry; existing entries are preserved |
| `library.progress` | `mediaId`, `amount=1` | Updated library entry |
| `library.score` | `mediaId`, `score` | Updated library entry |
| `library.status` | `mediaId`, `status` | Updated library entry |
| `library.notes` | `mediaId`, `notes` | Updated library entry |
| `library.remove` | `mediaId` | `{ mediaId, removed }` |

`type` is `Anime` or `Manga`. Statuses are `Watching`, `Completed`, `Planning`, `Paused`, `Dropped`, `Repeating`; persisted `Current` maps to `Watching` and is also accepted as input. Sorts are `Popularity`, `Score`, `Trending`, `Newest`. Personal scores retain the existing integer 0–10 scale; `null` clears the score. Catalog `averageScore` uses 0–100. Notes can be a string or `null` to clear them. Pages are 1–100000, page sizes 1–100, positive progress increments at most 1000000. Progress cannot overflow a signed 32-bit integer.

Page results are `{ items, page, pageSize, totalCount, hasNextPage }`; the catalog can return `totalCount: null`, in which case `hasNextPage` is false.

Media contains `id`, `type`, `title`, `romaji`, `posterUrl`, `bannerUrl`, `format`, `releaseStatus`, `year`, `averageScore`, `totalUnits`, `description`, `genres`. Library entries contain `mediaId`, `mediaType`, `status`, `progress`, `score`, `notes`, `review`, `updatedAt` and nested `media`. Nullable fields remain explicit JSON `null`.

Library listing and mutation responses hydrate from session catalog results, the shared offline cache and local fallback catalog without a network request per entry. Persisted library posters take precedence. Uncached titles remain usable offline with their ID as the title and their persisted poster. Library search matches locally available display titles, romaji, IDs and notes before pagination. An explicit detail request refreshes metadata through the catalog service, falling back to cached/local metadata or a persisted library entry when offline. Its saved poster is used when catalog metadata has no poster. Adding a previously unknown title may also query the selected remote catalog.

## Expanded application contracts

All fields below use camelCase. Input is limited to 20 MiB per JSON line. Images are raw base64, at most 10 MiB decoded; supported MIME types are `image/png`, `image/jpeg`, `image/webp`, and their file signatures must match. Local file paths are never accepted or returned.

Media additionally includes nullable `nativeTitle`, `episodeCount`, `durationMinutes`, `studio`, `chapterCount`, `volumeCount`, plus `characters: [{name,imageUrl,role}]`. Entry additionally includes `tags: string[]`, `customLists: string[]`, `startedAt`, `completedAt` (nullable ISO date/time with an explicit timezone).

| Method | Input | Output |
| --- | --- | --- |
| `home.get` | optional `type` | `{insights,continueItems:Entry[],recentItems:Entry[]}` |
| `home.recommendations` | optional `type`, `limit=8` (1–24) | `{items:Media[],genres:[{name,weight}],notice:string\|null}` |
| `schedule.list` | ISO `from`, `to`, `libraryOnly=false`; positive range at most 31 days | `{items:[{mediaId,title,episode,airingAt,format,releaseStatus,posterUrl,inLibrary,status}],from,to}` |
| `schedule.notify` | today's `from`, `to` range (at most 26 hours) | `{notifiedCount}`; reuses last fetched schedule, library membership, saved notification preference and session deduplication |
| `media.related` | `mediaId`, `forceRefresh=false` | `{titles:[{mediaId,title,mediaType,format,relation,source,sourceRelation,aniListId,malId,mangaDexId,year,episodeCount,chapterCount,volumeCount,posterUrl}],notice,isStale}` |
| `library.progress.set` | `mediaId`, `progress` integer 0–2147483647 | Entry |
| `library.review` | `mediaId`, `review` (normalized text up to 4000 characters or null) | Entry; null/blank clears |
| `library.organization` | `mediaId`, optional `tags`, `customLists` (up to 50 strings of 100 characters) | Entry; omitted collection preserved |
| `library.dates` | `mediaId`, optional `startedAt`, `completedAt` | Entry; omitted date preserved, null clears; completion cannot precede start |
| `library.filters` | none | `{tags:string[],customLists:string[]}` |
| `settings.get` | none | Settings |
| `settings.save` | partial editable settings below | Settings |
| `anilist.connect` | optional `clientId`, `clientSecret`; omitted values use saved credentials | Settings; opens existing browser OAuth flow |
| `anilist.sync` | none | `{importedCount,settings}` |
| `anilist.disconnect` | none | Settings; clears account/token, preserves app credentials |
| `recognition.run` | `provider:trace\|art`, `imageBase64`, `mimeType`, `uploadConfirmed:true`, optional `sourceName` | `{provider,matches:RecognitionMatch[],savedAttemptId:string\|null}` |
| `recognition.history` | `limit=50` (1–200) | `{items:RecognitionHistoryItem[]}` |
| `recognition.save` | `provider`, `title`, optional `mediaId` (`anilist:id`), `romaji`, `nativeTitle`, `episode`, `similarity` (0–1), `thumbnailUrl` (HTTP/S), `sourceName` | RecognitionHistoryItem |
| `recognition.remove` | `id` | `{removed}` |
| `library.export` | `format:csv\|malXml` | `{text,fileName,mimeType,exportedCount,skippedCount}` |
| `library.import.preview` | `xml` up to 4 MiB | `{items:Entry[],count}`; first 100 preview entries, full count, no mutation |
| `library.import.apply` | same XML | `{importedCount}`; reuses MAL parser/merge, preserves existing local notes, artwork, dates, review and organization |
| `updates.check` | none | `{currentVersion,isUpdateAvailable,latestRelease:null\|{version,tagName,name,releaseUrl,publishedAt}}` |

`library.list` also supports `tag`, `customList`, and `sort: Updated|Title|Score|Progress`; text search includes tags and custom lists. Sort and search precede pagination. `catalog.search` also supports `genres: string[]`, `seasonYear` (1900–2200), `format`, `releaseStatus`, `countryCode` (two characters), using the existing application query contract.

Insights reuse `LibraryInsightsCalculator`: `totalEntries`, `completedEntries`, `backlogEntries`, `droppedEntries`, `completionRate`, `droppedRatio`, `averageScore`, `episodesWatched`, `chaptersRead`, `estimatedWatchMinutes`, `byMediaType`, `byStatus`. Segments are `{key,count,completedCount,completionRate,averageScore}`; `Current` is presented as `Watching`. Optional home type filters all metrics and collections. Recommendations reuse `LibraryRecommendationBuilder` and exclude all owned titles.

Settings: `{theme,notificationsEnabled,offlineCacheEnabled,recognitionServicesEnabled,preferredSyncSource,aniListClientId,hasAniListClientSecret,hasSauceNaoApiKey,isAniListConnected,aniListUsername,aniListUserId,aniListLastSyncAt}`. Editable fields are theme (`Nebula dark` or `High contrast`), the three booleans, preferredSyncSource (`AniList`, `MyAnimeList`, `MangaDex`), aniListClientId, aniListClientSecret and sauceNaoApiKey. Omitted credentials preserve saved values; empty strings explicitly clear. Secrets and access tokens are never included in outputs, diagnostics, or auth error payloads. The existing encrypted settings service remains responsible for storage.

RecognitionMatch: `{mediaId,title,romaji,nativeTitle,episode,similarity,thumbnailUrl,sourceUrl,author}`. Similarity is 0–1; all fields except title and similarity may be null. Trace results reuse the existing rank/dedupe selector and automatically save the best match. Art results use the optional saved SauceNAO key and can work anonymously; art history is explicitly saved because legacy persistence has no author/source URL fields. Recognition requires `uploadConfirmed:true` for each explicit search. The client shows the selected provider beside its Find matches action; choosing, pasting, or dropping an image does not upload it. The persisted `recognitionServicesEnabled` field remains in the settings contract for the legacy desktop client but does not gate explicit bridge searches and is not changed by them.

RecognitionHistoryItem: `{id,createdAt,provider,sourceName,mediaId,title,romajiTitle,nativeTitle,episode,similarity,thumbnailUrl}`. History omits legacy local source paths. Additional error codes are `unavailable` and `authorization_failed`. The existing OAuth flow can last five minutes; the host should isolate it from normal library requests and allow sufficient timeout. No credentials are returned after connection.
