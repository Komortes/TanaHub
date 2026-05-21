# TanaHub

TanaHub is a native desktop anime and manga tracker.

This repository currently contains the initial solution skeleton only. Feature implementation will be added incrementally.

## Structure

```text
src/
  TanaHub.Desktop/         Entry point placeholder
  TanaHub.UI/              UI layer placeholder
  TanaHub.Application/     Application services and use cases
  TanaHub.Infrastructure/  External services, persistence, integrations
  TanaHub.Domain/          Domain models and contracts
tests/
  TanaHub.Application.Tests/
  TanaHub.Infrastructure.Tests/
```

## Requirements

- .NET SDK 10.0.201 or newer compatible SDK

## Commands

```bash
dotnet restore TanaHub.sln
dotnet build TanaHub.sln -m:1 /p:BuildInParallel=false
dotnet test TanaHub.sln --no-build -m:1 /p:BuildInParallel=false
```
