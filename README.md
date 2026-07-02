# TanaHub

A native desktop app for tracking anime and manga. Built with .NET and Avalonia UI — no Electron, no web wrapper.

[![CI](https://github.com/Komortes/TanaHub/actions/workflows/ci.yml/badge.svg)](https://github.com/Komortes/TanaHub/actions/workflows/ci.yml)

![Platform](https://img.shields.io/badge/platform-macOS%20%7C%20Windows%20%7C%20Linux-blue)
![.NET](https://img.shields.io/badge/.NET-10-purple)
![License](https://img.shields.io/badge/license-MIT-green)

---

## What it does

- **Discover** — search and browse anime/manga via AniList, with cover art, scores, and metadata
- **Library** — track your watching/reading list with statuses: Watching, Completed, Planning, Paused, Dropped
- **Title detail** — synopsis, characters, relations, airing info, and personal notes all in one place
- **Schedule** — see what's airing this week and when
- **Recognize** — identify an anime from a screenshot using trace.moe
- **AniList sync** — connect your AniList account via OAuth and sync your list both ways
- **Offline cache** — catalog data is cached locally so the app works without a connection
- **Recommendations** — get local suggestions based on genres in your library
- **Update checks** — see whether a newer GitHub release is available
- **Import/export** — dump your library to CSV or MAL XML, and import MAL XML backups

---

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| UI | Avalonia UI (native, cross-platform) |
| Anime catalog | AniList GraphQL API |
| Manga catalog | MangaDex REST API |
| Anime recognition | trace.moe API |
| Persistence | JSON files (local) |
| Architecture | Clean Architecture — Domain / Application / Infrastructure / UI |

---

## Architecture

```
src/
  TanaHub.Desktop/          Entry point, DI composition root
  TanaHub.UI/               Views, ViewModels, controls (Avalonia MVVM)
  TanaHub.Application/      Use cases, service interfaces, CSV export
  TanaHub.Infrastructure/   API clients, file persistence, sync, notifications
  TanaHub.Domain/           Core models and enums

tests/
  TanaHub.Application.Tests/
  TanaHub.Infrastructure.Tests/
```

The infrastructure layer is swappable — the application core has zero knowledge of AniList or any external API. Adding a new catalog source means implementing one interface.

---

## Getting started

**Requirements:** .NET SDK 10.0.201+

```bash
git clone https://github.com/Komortes/TanaHub.git
cd TanaHub

dotnet restore TanaHub.sln
dotnet build TanaHub.sln
dotnet run --project src/TanaHub.Desktop/TanaHub.Desktop.csproj
```

```bash
# Tests
dotnet test TanaHub.sln
```

Before creating a release artifact, run the [smoke test checklist](docs/smoke-test.md) on a clean local data profile.

Release packaging instructions are available in [docs/release.md](docs/release.md).

---

## Planned

- [x] Ratings and reviews per title
- [x] Reading progress tracking for manga (chapter/volume)
- [x] Library statistics and watch-time charts
- [x] MAL (MyAnimeList) import/export
- [x] Custom lists and tags
- [x] Recommendations based on your library
- [x] Auto-update checker

---

## Security note

Your AniList Client Secret and OAuth access token are stored locally in `settings.json`, encrypted at rest with a per-installation AES-256-GCM key (`.settings.key`, kept in the same app-data folder with owner-only file permissions on macOS/Linux). This protects against casual exposure (e.g. accidentally syncing the file to cloud storage or backups being read by another tool), but it is not a substitute for an OS keychain: anyone with read access to both files under your OS user account can still decrypt the values. Do not commit or share your app-data folder.

---

## License

MIT — see [LICENSE](LICENSE)
