# Changelog

Changes are recorded here for each release.

## Unreleased — 0.1.1 preview

### Fixed

- The first episode/chapter starts Watching/Reading for a new Planning title, so Home reflects the activity immediately.
- Unsaved notes, reviews, tags, lists, dates and progress survive navigation and reloads in the same window, with explicit Save/Discard actions.
- Add/progress actions restore usable keyboard focus, including failed writes and the final episode/chapter.
- MAL XML previews show imported names; names and episode/chapter totals survive offline restarts. Imports match known MAL IDs and media types instead of duplicating tracked titles.
- Failed imported-metadata writes leave the library unchanged and can be retried. Singular title/episode/chapter labels are corrected.
- Settings explains stable-only update checks and provides access to preview downloads.

No database schema change is required. See [the 0.1.1 preview notes](docs/releases/0.1.1.md) for packages and validation limits.

## [0.1.0 preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.0) — 2026-10-19

### Added

- First-use guidance, direct import entry point, keyboard skip link, page heading focus, labelled compact navigation and build-environment doctor.

- Tauri 2 and Svelte 5 desktop client backed by a self-contained .NET JSON stdio sidecar.
- Home, Library, Discover, Schedule, Recognition and Settings pages; full title details with characters and related titles.
- Episode/chapter tracking, scores, notes, reviews, tags, custom lists and editable start/completion dates.
- AniList import, MyAnimeList XML exchange, CSV export, local catalogs and recognition history.
- Browser, native transport and sidecar regression coverage alongside the .NET tests.
- Release version consistency checks, platform asset checksums and a workflow that prepares draft prereleases after CI.

### Fixed

- AniList Client ID drafts lost during unrelated preference saves, and a new ID incorrectly allowed to reuse the old ID's saved secret.

- Native macOS select controls ignoring field sizes and rendering as compressed system menulists.

- Manga hidden by the default library filter, collection searches lost on navigation and media-type filters carried between collections.
- AniList import looked like two-way sync and applied changes without explaining replacements. Import now requires confirmation; catalog settings distinguish supported providers from legacy MyAnimeList preferences.

- Progress overflow and aggregated statistics above the signed 32-bit limit.
- Lost drafts, overwritten manual tracking dates and invalid date chronology.
- Failed-page retries, stale collection results and actions enabled after library loading failures.
- Title navigation losing underlying filters, recognition results or keyboard focus.
- Library identity matching for provider IDs and organisation names with Unicode casing differences.

### Compatibility

- Existing SQLite storage and application services are retained. Avalonia remains runnable during migration.
- The macOS Tauri client requires macOS 15+ on Apple Silicon. Windows/Linux x64 packages have automated build and core checks; interactive validation is pending.
- Preview binaries use ad-hoc macOS signing and unsigned Windows packaging. Real OAuth, recognition uploads and native import/export dialogs need final native checks.

See [the preview notes](docs/releases/0.1.0.md) for distribution details.
