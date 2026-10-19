# Contributing

TanaHub is in pre-release development. The Tauri client is the current UI direction; Avalonia remains available during migration. Start with [README](README.md) and [the documentation index](docs/README.md).

## Work safely

Use a short-lived branch and keep commits focused. Preserve existing work in a dirty checkout. Never commit app-data folders, credentials, generated build output or test captures. Keep npm and Cargo lockfiles in version control.

Run desktop experiments with an absolute `TANAHUB_DATA_DIRECTORY` pointing to a fresh temporary directory. For example, from `src/TanaHub.Client` on macOS/Linux:

```sh
TANAHUB_DATA_DIRECTORY="$(mktemp -d)" npm run desktop:dev
```

On Windows PowerShell:

```powershell
$env:TANAHUB_DATA_DIRECTORY = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
npm run desktop:dev
```

Do not run both clients against one profile during writes. Recognition sends the explicitly selected image to its provider; use test material you are allowed to upload.

## Architecture and style

- Keep Domain independent of infrastructure. Define service contracts in Application and implementations in Infrastructure.
- Keep native permissions narrow. Production UI calls the sidecar through Tauri IPC; the HTTP adapter is development-only.
- Follow `.editorconfig`, use UTF-8/LF and preserve the existing Svelte component conventions.
- Document significant decisions in `docs/decisions`; keep [the bridge protocol](src/TanaHub.Bridge/README.md) current when changing messages.

## Validate a change

Run the commands under [Development checks](README.md#development-checks). Tests must use isolated profiles. Add a regression test for changed behavior; documentation-only changes need link and command verification.

For native changes, also build on the target OS and complete the relevant [smoke checks](docs/smoke-test.md). Browser fixtures cannot prove OS dialogs, signing, notifications or external-account flows.

In a pull request, explain the problem, resulting behavior and checks run. Include screenshots for visible UI changes and state any native platform you could not verify. Report security-sensitive issues through [SECURITY.md](SECURITY.md), with credentials removed.

## Release changes

Follow [docs/release.md](docs/release.md). Update all manifests together and run the version checker. Release notes belong in `docs/releases/<version>.md`; unreleased changes stay under Unreleased in the changelog until publication.
