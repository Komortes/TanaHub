# TanaHub Smoke Test

Run this checklist on a fresh local data profile before creating a release artifact.

## Reset Local Data

TanaHub stores local files in the OS application-data directory under `TanaHub/`:

- `library.json` — local library, tags, custom lists, notes, and progress
- `settings.json` — theme, cache, recognition preference, and AniList connection metadata
- `recognition_inbox.json` — recognition history
- `catalog_cache.json` — cached AniList catalog data

Close TanaHub before changing these files. For a clean smoke test, move the `TanaHub/` directory aside rather than deleting it so it can be restored after the test.

## Core Flows

### Discover and Library

- [ ] Search for an anime and a manga.
- [ ] Open each detail page and add it to the library.
- [ ] Increment episode/chapter progress, change status, set a score, and save notes.
- [ ] Create a tag and custom list from the detail page.
- [ ] Filter the Library by status, tag, custom list, type, and search text.
- [ ] Restart the app and confirm progress, notes, tags, and lists remain.

### Schedule and Offline State

- [ ] Open Schedule and switch each date range.
- [ ] Turn offline cache on and off in Settings.
- [ ] Confirm the topbar status gives a readable state and detail.

### Recognition

- [ ] Select a valid screenshot and confirm source preview, best match, and variants appear.
- [ ] Open the best match and return to Recognition.
- [ ] Use a non-matching image and confirm the no-match state is clear.
- [ ] Paste an image from the clipboard and confirm unsupported clipboard content has a clear error.
- [ ] Expand/collapse history and confirm one entry is added per recognition attempt.

### Sync and Export

- [ ] Connect AniList with a test account, import once, then restart the app.
- [ ] Export the library CSV and confirm tags and custom lists are present.
- [ ] Disconnect AniList and confirm the local library remains intact.

## Failure Capture

Record the OS, build version, exact flow, expected behavior, actual behavior, and whether the issue survives app restart. Do not include AniList access tokens or client secrets in reports.
