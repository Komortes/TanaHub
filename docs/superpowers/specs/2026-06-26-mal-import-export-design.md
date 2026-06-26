# MAL Import/Export Design

## Goal

Close the roadmap item for MAL import/export with a local file workflow that works without MyAnimeList OAuth or network sync.

## Scope

This slice implements MAL-style XML import/export:

- Export the local TanaHub library to a `myanimelist` XML document.
- Import `myanimelist` XML documents containing `anime` and `manga` entries.
- Map imported entries into local `UserMediaEntry` records with IDs `mal:anime:<id>` and `mal:manga:<id>`.
- Preserve list status, progress, and score where present.

This is not account sync. OAuth-based MyAnimeList API sync can be added later as a separate integration.

## Mapping

Anime export uses `series_animedb_id`, `series_title`, `my_watched_episodes`, `my_score`, and `my_status`.
Manga export uses `manga_mangadb_id`, `manga_title`, `my_read_chapters`, `my_score`, and `my_status`.

Status mapping:

- `Current` → `Watching` for anime, `Reading` for manga
- `Planning` → `Plan to Watch` for anime, `Plan to Read` for manga
- `Completed` → `Completed`
- `Paused` → `On-Hold`
- `Dropped` → `Dropped`
- `Repeating` → `Watching` / `Reading`

## UI

Settings keeps the existing CSV export and adds:

- `Export MAL XML`
- `Import MAL XML`

Import opens an XML file, parses entries, upserts them into the local library, refreshes the library, and updates the same export/import status text.

## Testing

Unit tests cover XML export shape, status/progress/score mapping, anime import, manga import, and invalid XML validation.
