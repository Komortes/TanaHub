# 0005. Tauri and Svelte client with a .NET sidecar

## Status

Accepted for the new client. Partially supersedes [0002](0002-avalonia-over-electron.md); Avalonia remains available during native validation.

## Date

2026-10-12

## Context

The new interface is already implemented in Svelte with a Tauri shell. Catalog access, persistence, settings protection and imports already exist in the .NET core. Reimplementing those services in JavaScript would create competing data and validation paths.

## Decision

Use Tauri 2 with the system webview and Svelte 5 for the new interface. Publish the .NET Bridge as a self-contained sidecar and exchange JSON messages over stdio. Keep Domain, Application and Infrastructure as the service authority and retain the existing SQLite profile. Use a same-origin HTTP adapter only for local browser development.

## Consequences

- The desktop package includes the .NET runtime, while the UI uses the host webview instead of bundled Chromium.
- Native packaging needs Node, Rust, .NET and the host's Tauri dependencies. Builds and smoke checks must run on each target OS.
- IPC validation, sidecar lifetime and native permissions need explicit checks.
- Avalonia stays runnable until platform verification is sufficient. Both clients must not edit one profile concurrently.
- The current migration introduces no new library schema or score conversion. Future schema changes need a separate compatibility decision.
