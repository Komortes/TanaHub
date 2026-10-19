# TanaHub Smoke Test

Run this checklist on a fresh local data profile before creating a release artifact.

## Use an isolated profile

Set `TANAHUB_DATA_DIRECTORY` to a fresh absolute temporary path before launching a smoke build. This preserves your normal library. Both clients share the default profile; do not run them against it at the same time during writes.

TanaHub stores local files in the OS application-data directory under `TanaHub/`:

- `library.db` (and SQLite WAL/SHM files) — current local library, tags, custom lists, notes and progress
- `library.json` — legacy library import, if present
- `settings.json` — theme, cache, recognition preference, and AniList connection metadata
- `.settings.key` — local settings encryption key; never share it
- `recognition_inbox.json` — recognition history
- `catalog_cache.json` — cached AniList catalog data

Close TanaHub before backing up the full profile. Keep the database, settings and key together. Use a separate profile for clean smoke checks rather than moving or deleting the normal one.

## Core Flows

### Discover and Library

- [ ] Search for an anime and a manga.
- [ ] Open each detail page and add it to the library.
- [ ] Increment episode/chapter progress, change status, set a score, and save notes.
- [ ] Edit start/completion dates, test invalid chronology, discard a draft, and confirm a status change retains saved manual dates.
- [ ] Compare anime and manga details: Watching / Reading, +1 Episode / +1 Chapter, and Episodes / Chapters. Library and Home progress use ep. / ch.; repeating entries use Rewatching / Rereading. Long manga progress (for example 1100/1200 ch.) stays inside its column.
- [ ] Create a tag and custom list from the detail page.
- [ ] Filter the Library by status, tag, custom list, type, and search text.
- [ ] Restart the app and confirm progress, notes, tags, and lists remain.

### Schedule and Offline State

- [ ] Open Schedule and switch each date range.
- [ ] Turn offline cache on and off in Settings.
- [ ] Confirm the topbar status gives a readable state and detail.
- [ ] Run Settings → Updates → Check now and confirm the update status is readable.

### Keyboard and Accessibility

- [ ] Navigate the sidebar, search, detail actions, settings, and recognition history with keyboard only.
- [ ] Confirm the focused sidebar item has a visible focus state.
- [ ] Confirm icon-only controls expose readable tooltips.

### Recognition

- [ ] Select a valid screenshot and confirm source preview, best match, and variants appear.
- [ ] Open the best match and return to Recognition.
- [ ] Use a non-matching image and confirm the no-match state is clear.
- [ ] Paste an image from the clipboard and confirm unsupported clipboard content has a clear error.
- [ ] Expand/collapse history and confirm one entry is added per recognition attempt.

### Sync and Export

- [ ] Connect AniList with a test account, import once, then restart the app.
- [ ] Export the library CSV and confirm tags and custom lists are present.
- [ ] Export MAL XML, cancel each native file dialog, and preview/import the XML into a separate empty test profile.
- [ ] Disconnect AniList and confirm the local library remains intact.

## Failure Capture

Record the OS, build version, exact flow, expected behavior, actual behavior, and whether the issue survives app restart. Do not include AniList access tokens or client secrets in reports.


### Isolated desktop profile

Set `TANAHUB_DATA_DIRECTORY` to an absolute path before launching a smoke build.
It redirects the SQLite library, legacy JSON import, settings, recognition inbox,
and catalog cache. This works on macOS as well as Windows and Linux; `XDG_CONFIG_HOME`
does not override macOS Application Support.

```sh
TANAHUB_DATA_DIRECTORY=/tmp/tanahub-smoke dotnet run --project src/TanaHub.Desktop
```

Start with an empty directory to verify first launch. Add a title, increment progress,
add it again from Discover, and confirm progress survives. Close and reopen with the
same directory to verify persistence. After removing every test entry, restart again:
the library must remain empty.

## UI feedback and interaction regression checks

- Open a title, add it to the library, and change its progress, score, and status. The poster, scroll position, and editing fields must remain in place. Buttons should be usable again after the local save, even if catalog recommendations are still loading.
- Type unsaved notes, a review, tags, and custom lists. Change progress or score; all drafts must remain intact.
- Success feedback disappears after 4 seconds; error feedback after 8 seconds. Dismiss closes it immediately. Trigger the same action twice: the first timer must not dismiss the newer message.
- Trigger feedback, then open a different title, go Back, or select another sidebar page. The feedback must disappear immediately. A delayed save from the old title must not display feedback on the new page.
- Hover card edges, secondary actions, status pills, and score buttons. Hit targets stay in place; no gray theme flash or loss of the selected colour occurs. Tab focus remains visible.
- The community rating uses a 100-point scale; personal rating buttons use a 10-point scale and have labels/tooltips. Search feedback stays in Discover; schedule errors stay in Schedule.
- Save a score from Library, then continue using the same row. Its controls, hover, and keyboard focus must survive the background refresh. Check that completed and removed entries leave only the affected lists.
- Start a progress save and immediately select a different status. Both changes must persist in click order. Repeat with tags/custom lists and confirm neither value is overwritten.
- Open a title from Home or Library, then select that same sidebar section again (mouse or Enter). The section must open and cancel a pending title transition. Switching sections starts at the page heading.
- Load another page of Discover results with a slow response. Existing rows and the Load more button stay in place; the button disables and shows its own loading indicator. The indicator above the results must stay hidden during append.
- Resize the desktop window between 920 and 1180 px. Library and Schedule filters move above their rows when needed; title text, progress, score, and Open/actions never overlap. Select another schedule range and confirm neighboring buttons retain their positions.
- On title detail, tracking actions have their own panel. Synopsis and details appear before related titles. Resize while editing personal fields; drafts must remain intact. Tab through poster cards, secondary buttons, status pills, and rating bars: the focus outline must remain visible without changing control size.

## Related titles

- Open Attack on Titan from AniList. The detail appears before relations finish loading. Previous/next parts, source/adaptations, and side stories are separate groups; other versions and fan works start collapsed.
- Follow the manga source or next season. The new page starts at its heading. Back returns to the previous title, then to the original page.
- Open the same manga through MangaDex. Its direct relations and AniList adaptations appear together where exact external IDs exist. Different editions with different MangaDex IDs remain separate.
- Open a related title already imported under a MAL key: its saved progress is retained and the existing entry opens. Anime and manga MAL namespaces remain separate; a colour edition does not borrow the original edition's progress.
- Change title or sidebar page during a slow relations request. A late result or error must not replace the new page or show a global notification.
- Make a title request fail while its previous relations are still loading. The previous title remains usable, loading restarts, and Retry still works.
- Disconnect the network after loading relations. Fresh cached results remain available; an expired result is shown as saved data if a live refresh fails. Disable offline cache to bypass saved data. A successful empty list is different from an unavailable source.
- Retry a partial or failed result. Existing cards stay visible during refresh, and the button is disabled while running.
