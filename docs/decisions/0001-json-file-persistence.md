# 0001. Persist user data as local JSON files

## Status

Superseded by [0004](0004-sqlite-for-library-storage.md)

## Context

`FileUserLibraryService` and `FileAppSettingsService` (in `TanaHub.Infrastructure`) persist the user's library and settings as JSON files on disk, written atomically via `AtomicFileWriter`. `ANITRACK_SPEC.md`, the original planning doc, sketched a SQLite + EF Core persistence layer instead. This ADR records why the shipped implementation diverged from that plan, and evaluates whether it should change.

TanaHub is a single-user desktop app. There is no multi-writer contention, no server process, and the realistic library size for one person is in the hundreds to low thousands of entries (AniList/MAL power users rarely track more than a few thousand titles total across anime and manga). Current read/write access patterns are "load the whole library into memory, filter and sort in memory" — there is no indexed or partial-query need today.

## Decision

Compared against the criteria that actually matter for this app:

| Criterion | JSON files | SQLite | LiteDB |
|---|---|---|---|
| Library size TanaHub targets (hundreds–low thousands of rows) | Fine — full load is sub-millisecond at this scale | Fine, but overkill | Fine |
| Backup / portability | Trivial — plain files, diffable, copy/sync just works | Single opaque file; still copyable but not diffable | Single opaque file; still copyable but not diffable |
| Schema evolution | No migrations — tolerant `record` deserialization with new optional properties | Needs migration tooling (EF Core migrations or hand-rolled) | Needs versioned document handling |
| Query needs | None today — in-memory filter/sort covers current UI | Would only pay off with indexed/relational queries, which nothing in the app needs yet | Same |
| Cross-platform shipping cost | Zero extra — `System.Text.Json` ships with .NET | Needs a native SQLite provider per OS/arch, which is exactly the kind of native-dependency weight `0002-avalonia-over-electron.md` is trying to avoid | Pure managed code, no native dependency — the strongest alternative if this ever changes |
| Atomicity / corruption safety | Already solved — `AtomicFileWriter` writes to a temp file and renames | Built-in (ACID) | Built-in (single-file transactions) |

**Decision: keep JSON file persistence.** None of the criteria that would justify a database — indexed queries, multi-writer concurrency, dataset sizes where full-file rewrites are slow — apply at TanaHub's actual scale. Introducing SQLite now would add native-dependency shipping cost and migration tooling to solve a problem the app doesn't have.

This is revisited, not closed permanently. Concrete signals that would justify migrating (to LiteDB first, as the pure-managed-code option, before SQLite):
- Observable UI lag on library save/load once real usage data shows libraries in the tens-of-thousands-of-entries range, or
- A feature that genuinely needs indexed/partial queries instead of full-library in-memory operations (e.g. cross-library search at a scale where linear scan is visibly slow).

## Consequences

Easier: no native dependencies to ship per-platform, no migration framework to maintain, backups are just files, `AtomicFileWriter` already gives crash safety.

Harder: no indexed queries if a future feature needs them; every read still means deserializing the whole library file, which is the thing to watch for the revisit signal above.
