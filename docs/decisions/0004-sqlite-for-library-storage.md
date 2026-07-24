# 0004. Move library storage from JSON to SQLite

## Status

Accepted

## Context

[0001-json-file-persistence.md](0001-json-file-persistence.md) recorded a decision to keep the user's library as a JSON file, revisited if a concrete signal justified a database. On reflection, the "no native dependency" cost that motivated keeping JSON was overstated: modern .NET's self-contained, RID-specific publishing bundles SQLite's native provider automatically, and TanaHub already builds per-OS artifacts in CI — this is a solved packaging problem, not Electron-scale bloat. Weighed against that smaller-than-assumed cost, SQLite offers real SQL, indexes, and an actual migrations story, all of which are more legible to the portfolio audience `PRODUCT.md` describes than either JSON or LiteDB.

## Decision

Move `IUserLibraryService`'s storage from `FileUserLibraryService` (JSON file, full-file rewrite per write) to `SqliteUserLibraryService` (EF Core over SQLite, real `Database.Migrate()` migrations, targeted row-level writes). Existing `library.json` files are imported once on first startup after the update, then renamed to `.bak`.

Scope is deliberately narrow: only library storage moves. `AppSettings`, the recognition inbox, and the offline catalog cache stay JSON — none of their shapes (a single config object, an append-mostly attempt log, an external-API cache) benefit from a relational store the way the library's filterable, growing entry list does.

## Consequences

Easier: real indexed queries become possible if a future feature needs them (not exercised yet — current filtering still happens in memory, matching the small-scale reasoning `0001` already established); no full-library rewrite on every single-field edit; a genuine, checked-in migration history.

Harder: schema changes now require writing and testing an EF Core migration instead of just adding an optional record property; the SQLite file is opaque to casual inspection compared to a JSON file (mitigated by `Tags`/`CustomLists` still being stored as readable JSON text within the SQLite row).
