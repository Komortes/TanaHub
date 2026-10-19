<div align="center">

<img src="assets/logo-wordmark.png" alt="TanaHub" width="440">

**Your anime and manga library, on your desktop.**<br>
Local SQLite storage · Tauri + Svelte · .NET 10

[![CI](https://github.com/Komortes/TanaHub/actions/workflows/ci.yml/badge.svg)](https://github.com/Komortes/TanaHub/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-10B981)](LICENSE)

[Download preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.0) · [User guide](docs/getting-started.md) · [Run from source](#run-from-source) · [Contributing](CONTRIBUTING.md)

</div>

## What is TanaHub?

TanaHub tracks anime and manga in a local library. Browse titles, record progress and ratings, organise lists, and find the source of a screenshot or illustration. Your library works without an account; online catalogs and recognition use the providers listed below.

The new desktop client uses Tauri 2, Svelte 5 and TypeScript with a self-contained .NET sidecar. The existing Avalonia client remains available during migration. Both use the same application services and SQLite library; avoid editing the same profile from both clients at once.

**Status:** `0.1.0` preview for early testing. The new client has been exercised natively on Apple Silicon macOS; Windows and Linux packages have automated build/core checks, with interactive validation still pending. See [migration status and limits](docs/tauri-migration.md) and [preview release notes](docs/releases/0.1.0.md).

## Features

- **Track:** episode, chapter and volume progress; statuses; personal scores, notes and reviews; editable start/completion dates.
- **Organise:** tags, custom lists, search, filters, sorting, grid/list views and library statistics.
- **Discover:** AniList anime and manga, optional MangaDex manga catalog, title details, characters, related titles, recommendations and airing schedules.
- **Recognise:** anime screenshots through [trace.moe](https://trace.moe), artwork through [SauceNAO](https://saucenao.com), with saved recognition history.
- **Exchange data:** AniList OAuth import, MyAnimeList XML import/export and CSV export. AniList import is not continuous two-way sync.
- **Use locally:** SQLite persistence, catalog caches, themes and keyboard navigation. Update checks link to GitHub releases; installation is manual.

## Getting started

Download a package from [TanaHub 0.1.0 Preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.0):

| Platform | Download |
|---|---|
| macOS 15+, Apple Silicon | [DMG installer](https://github.com/Komortes/TanaHub/releases/download/v0.1.0/TanaHub-0.1.0-osx-arm64.dmg) |
| Windows x64 | [EXE installer](https://github.com/Komortes/TanaHub/releases/download/v0.1.0/TanaHub-0.1.0-win-x64.exe) |
| Linux x64 | [Debian package](https://github.com/Komortes/TanaHub/releases/download/v0.1.0/TanaHub-0.1.0-linux-x64.deb) · [AppImage](https://github.com/Komortes/TanaHub/releases/download/v0.1.0/TanaHub-0.1.0-linux-x64.AppImage) |

Packages include the .NET runtime. You do not need Node, Rust, a .NET SDK or an account to track titles. Preview macOS builds are ad-hoc signed and not notarized; Windows installers are unsigned, so OS trust warnings are expected. Read [installation instructions and your first-title workflow](docs/getting-started.md) before using the preview with an existing library.

## Run from source

The commands below run a development checkout.

### Requirements

- [.NET SDK](https://dotnet.microsoft.com/download) compatible with [global.json](global.json), currently 10.0.201 with `latestFeature` roll-forward.
- Node.js 24 or newer and npm.
- Rust stable and the [Tauri prerequisites for your OS](https://v2.tauri.app/start/prerequisites/).
- The macOS Tauri client targets macOS 15 or newer. Windows and Linux builds must be made on their target hosts.

### New desktop client

```sh
git clone https://github.com/Komortes/TanaHub.git
cd TanaHub/src/TanaHub.Client
npm run doctor
npm ci
npm run desktop:dev
```

`npm run desktop:build` publishes the .NET sidecar and packages the current host's desktop client. Packaged users do not need the .NET SDK or runtime. Packaging, signing limits and checksums are described in [the release guide](docs/release.md).

For browser development, run `npm run prepare:bridge` and then `npm run dev`. The local frontend opens at `http://127.0.0.1:1420`; the packaged app uses native IPC.

### Existing Avalonia client

From the repository root:

```sh
dotnet restore TanaHub.sln
dotnet run --project src/TanaHub.Desktop/TanaHub.Desktop.csproj
```

### Optional integrations

Credentials are entered in **Settings**, not in repository files.

| Integration | Credentials |
|---|---|
| AniList import | Client ID and Client Secret from [AniList developer settings](https://anilist.co/settings/developer) |
| SauceNAO artwork lookup | API key from [SauceNAO](https://saucenao.com/user.php) |
| trace.moe screenshots | No key required for the public API; provider limits apply |

## Development checks

From the repository root:

```sh
node scripts/check-release-version.mjs
node --test scripts/tests/release.test.mjs
node --test scripts/tests/environment.test.mjs
dotnet restore TanaHub.sln
dotnet format TanaHub.sln --verify-no-changes --no-restore
dotnet build TanaHub.sln -c Release
dotnet test TanaHub.sln -c Release --no-build
```

From `src/TanaHub.Client`:

```sh
npm run build
npm run test:transport
npm run prepare:bridge
npm run test:core
npx playwright install chromium
npm test
cargo check --locked --manifest-path src-tauri/Cargo.toml
```

Browser tests use controlled fixtures; the core test uses a real self-contained sidecar and an isolated profile. See [CONTRIBUTING.md](CONTRIBUTING.md) for testing without changing your library.

## Repository map

| Path | Purpose |
|---|---|
| `src/TanaHub.Client` | Svelte frontend, Tauri shell and browser tests |
| `src/TanaHub.Bridge` | JSON stdio adapter to the .NET services; [protocol reference](src/TanaHub.Bridge/README.md) |
| `src/TanaHub.Desktop`, `src/TanaHub.UI` | Existing Avalonia client and its composition root |
| `src/TanaHub.Application` | Use cases, service contracts and library insights |
| `src/TanaHub.Infrastructure` | Catalogs, recognition, SQLite, imports, settings and caches |
| `src/TanaHub.Domain` | Core models and enums |
| `tests` | Application, Infrastructure and Bridge .NET test projects |
| `docs` | Release guide, smoke checks, migration notes and [architecture decisions](docs/decisions) |
| `scripts` | Release version checks, asset collection and legacy Avalonia publishing |

## Data and security

Local data is stored under the OS application-data directory in `TanaHub/`. Use an absolute `TANAHUB_DATA_DIRECTORY` to run with a separate profile. Back up the directory while the app is closed before testing another build.

Sensitive settings use AES-256-GCM with a per-installation `.settings.key` stored beside `settings.json`. The key has owner-only permissions on macOS and Linux. Anyone who can read both files under your OS account can decrypt the values; this is not an OS keychain. Never share an app-data directory or include credentials in issue reports. See [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE).
