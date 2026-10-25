# Changelog

Changes are recorded here for each release.

## Unreleased

### Fixed

- Recognition-history refresh preserves successful concurrent saves and deletions while including newly fetched rows.
- Cancelling or completing MAL XML import restores keyboard focus to the import control; failed imports remain usable for retry.

### Improved

- Quieter graphite and paper surfaces put cover art first. Navigation separates library browsing from secondary tools and keeps readable labels and usable targets on narrow windows.

## [0.1.4 preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.4) — 2026-10-21

### Fixed

- Hidden catalog/library cards stop taking new image-loading slots while a title is open. Visible artwork loads next, and returning to the collection resumes its covers without exceeding four concurrent loads.
- Refreshing related titles retries failed covers, including when their image URL has not changed.
- Enlarged covers retain a usable loading frame and offer Retry cover after a failure. Shared header/original failures keep the viewer open; retry and dismissal preserve keyboard focus.
- Background title-detail refreshes no longer dismiss an enlarged cover when its original image URL is unchanged.

### Improved

- Small title-header covers now choose thumbnails or previews according to their rendered size and screen density. Enlarge still loads the original.

### Maintenance

- Updated Vite to 8.3.3 for upstream development-server and launch-editor security fixes.

## [0.1.3 preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.3) — 2026-10-19

### Added

- Automatic background update checks once per supported desktop app launch. An accessible in-app notice offers download, explicit installation/restart and Later; network failures stay quiet during startup.
- Shared updater state keeps downloads available across navigation and in Settings, without duplicate startup checks or interrupting keyboard focus.

## [0.1.2 preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.2) — 2026-10-19

### Added

- Signed in-app updates with separate download/install steps, progress, version-bound signatures and restart. Supported by macOS, Windows and Linux AppImage; DEB uses the package manager.
- A 256 MiB persistent artwork cache with bounded downloads, expiry, eviction and saved-image fallback when refresh fails.
- Signed update manifests, local/optional CI signing and publication of preview/stable update feeds after a complete release is published.

### Improved

- Thumbnail/preview/original cover selection follows rendered size and screen density. Offscreen artwork waits for scrolling, and concurrent image loads are limited to four.
- Failed smaller images fall back to the original; loading is shared by covers, banners, characters and related titles.

Older 0.1.0/0.1.1 previews require a one-time installation to acquire the updater. See [0.1.2 notes](docs/releases/0.1.2.md) and [updating TanaHub](docs/updating.md).

## [0.1.1 preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.1) — 2026-10-19

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
