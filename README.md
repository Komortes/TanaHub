# TanaHub

A native desktop anime and manga tracker built with .NET and Avalonia UI.

## Features

- Browse and search titles via AniList catalog
- Track watching/reading progress per title
- Personal library with status management (Watching, Completed, Planning, Paused, Dropped)
- Airing schedule view
- Export library to CSV
- Dark theme with a custom design system

## Tech Stack

- **.NET 10** — runtime and async infrastructure
- **Avalonia UI** — cross-platform native UI (no Electron)
- **AniList GraphQL API** — catalog and schedule data
- **Clean Architecture** — Domain / Application / Infrastructure / UI layers

## Structure

```text
src/
  TanaHub.Desktop/         Entry point, DI composition root
  TanaHub.UI/              Views and ViewModels (Avalonia)
  TanaHub.Application/     Use cases, service interfaces, export
  TanaHub.Infrastructure/  AniList client, file persistence, settings
  TanaHub.Domain/          Models and enums
tests/
  TanaHub.Application.Tests/
  TanaHub.Infrastructure.Tests/
```

## Requirements

- .NET SDK 10.0.201+

## Build & Run

```bash
dotnet restore TanaHub.sln
dotnet build TanaHub.sln -m:1 /p:BuildInParallel=false
dotnet run --project src/TanaHub.Desktop/TanaHub.Desktop.csproj
```

## Tests

```bash
dotnet test TanaHub.sln --no-build -m:1 /p:BuildInParallel=false
```

## License

MIT — see [LICENSE](LICENSE)
