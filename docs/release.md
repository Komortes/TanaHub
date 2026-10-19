# Release guide

The primary release target is the Tauri + Svelte client with its self-contained .NET sidecar. Version `0.1.1` is the current preview; `0.1.0` is the first public preview. Creating a tag, uploading source changes and publishing a release are separate maintainer actions.

## Version and notes

Keep these versions equal: `Directory.Build.props`, the client `package.json` and npm lockfile, `src-tauri/tauri.conf.json`, `Cargo.toml` and the client package in `Cargo.lock`.

```sh
node scripts/check-release-version.mjs v0.1.1
node --test scripts/tests/release.test.mjs
```

The tag must be exactly `v<version>`. Prepare `docs/releases/<version>.md` and update [CHANGELOG.md](../CHANGELOG.md) before tagging. Do not date an unreleased version as if it had shipped.

## Local native build

Install the requirements from [README](../README.md#getting-started), then:

```sh
cd src/TanaHub.Client
npm ci
npm run prepare:bridge
npm run test:core
npm run tauri -- build --ci -- --locked
```

This builds for the current host. The sidecar preparation script rejects cross compilation. On macOS, `--bundles app` can build just the application for local testing.

For macOS DMG packaging in a noninteractive environment, set `CI=true` as well as passing `--ci`. This skips Finder window styling and avoids an AppleScript automation prompt. GitHub Actions already sets `CI=true`.

The release workflow currently targets:

| Host | Runtime | Assets |
|---|---|---|
| Apple Silicon macOS | `osx-arm64` | `.dmg`, `.app.tar.gz` |
| Windows x64 | `win-x64` | NSIS `.exe` installer |
| Linux x64 (Ubuntu 22.04 runner) | `linux-x64` | `.deb`, `.AppImage` |

After building, collect consistently named assets and SHA-256 checksums from the repository root:

```sh
node scripts/collect-release-assets.mjs osx-arm64
```

Replace the runtime with `win-x64` or `linux-x64` on those hosts. Output goes to `artifacts/release/<runtime>/`. The collector requires all configured installers; a macOS app-only build does not produce a complete release asset set. Optional second and third arguments override the bundle directory and output directory.

## GitHub workflow

[Release](../.github/workflows/release.yml) first runs the reusable [CI](../.github/workflows/ci.yml): .NET format/build/tests, frontend build and browser scenarios, transport tests, self-contained core tests, release tooling tests and native Rust checks.

- Manual dispatch on a branch builds downloadable workflow artifacts only.
- A matching `v<version>` tag, or dispatch on that tag, builds all three platforms and creates a **draft prerelease** with version-specific notes and checksums.
- The draft is created only after every platform packages successfully. Reruns can update a draft; an already published release is left unchanged and its publication step is skipped successfully.
- Build jobs have read-only repository permissions. Only the draft-release job receives `contents: write`.

Before creating the tag, commit and review the intended source state and ensure CI is green. After reviewing the draft, download each target artifact, verify its checksums and run the available [smoke checks](smoke-test.md) on a fresh profile. Record the commit, OS, architecture and result. A preview may explicitly document remaining interactive platform checks; a stable release requires them to pass. Publish the draft explicitly only when these results and its signing limitations are acceptable.

Maintainers can also promote the exact artifacts from a successful manual run.
Download all three platform artifacts from that run, verify the full asset set and
checksums, then create a draft with `gh release create` using `--target` set to the
run's full tested commit SHA. Inspect uploaded asset digests before publishing.
Creating the tag can trigger another Release run; cancel that duplicate when the
same source and installer bytes have already passed the complete workflow. Never
replace published assets or move the release tag to a different commit.

Workflow definitions follow the [Tauri pipeline guidance](https://v2.tauri.app/distribute/pipelines/github/) and use [GitHub token permissions](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token) scoped by job.

## Signing and validation limits

macOS uses an ad-hoc signature by default and is not notarized. Windows installers are unsigned. These are preview assets and may trigger OS trust warnings. Developer ID signing/notarization and Windows code signing need separately configured credentials and verification before a general public release.

Linux AppImage downloads need `chmod +x` before launch. Windows NSIS installers use [Tauri's default WebView2 installation behavior](https://v2.tauri.app/distribute/windows-installer/#webview2-installation-options) and may need an internet connection when WebView2 is missing.

Native macOS `.app` startup, catalog access, editing, persistence and CSV/MAL XML file dialogs have been exercised on isolated profiles. The release workflow builds Windows/Linux packages and tests their self-contained cores on their native runners. Interactive Windows/Linux installation and file dialogs, real AniList OAuth and live recognition uploads still need end-to-end validation. Automated fixtures do not replace those checks.

To check a downloaded platform checksum file, put it beside the assets and run `shasum -a 256 -c <checksum-file>` on macOS or `sha256sum -c <checksum-file>` on Linux. On Windows, use `Get-FileHash <asset> -Algorithm SHA256` and compare with the listed digest. Checksums detect changed bytes; they do not replace code signing.

## Existing Avalonia artifacts

The scripts below still package the original Avalonia application. They are separate from the Tauri release workflow:

```sh
scripts/publish-release.sh osx-arm64 0.1.0
scripts/publish-release.sh linux-x64 0.1.0
```

```powershell
./scripts/publish-release.ps1 -Runtime win-x64 -Version 0.1.0
```

They create self-contained directories under `artifacts/<runtime>/`.

## Withdrawal and rollback

If a preview fails native smoke checks, keep the release as a draft. If a published build corrupts data or breaks a core flow, withdraw its assets from distribution and document the affected version. Do not rewrite an existing release tag; prepare a new patch version. Close the app and restore a pre-upgrade profile backup before returning to an older binary, especially after any future schema migration.
