# Manga Reading Progress Design

## Goal

Close the roadmap item for manga reading progress with the simplest user-facing behavior: a local chapter counter that behaves like anime episode progress.

## Design

- Reuse the existing `UserMediaEntry.Progress` integer as the current chapter for manga.
- Use `Manga.ChapterCount` only as optional catalog metadata for display and “set to max”.
- When a manga has a known chapter count, display progress as `current/total`, for example `12/84`.
- When the chapter count is unknown, display only the current chapter number, for example `12`, not `12/?`.
- Keep volume count as read-only title metadata. Do not add manual volume progress in this slice because the requested interaction is “mark which chapter I am on”.

## Data Flow

Catalog services may provide `Manga.ChapterCount` and `Manga.VolumeCount`. The library stores only the user’s current progress in `UserMediaEntry.Progress`. Detail and library view-models format the display based on media type and available totals.

## Testing

Add view-model/unit tests for the formatting contract:

- manga with known chapters displays `current/total`;
- manga with unknown chapters displays only `current`;
- anime with known episodes still displays `current/total`.
