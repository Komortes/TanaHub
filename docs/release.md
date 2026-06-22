# TanaHub Release Artifacts

## Local Publish

macOS/Linux:

```bash
scripts/publish-release.sh osx-arm64 0.1.0
scripts/publish-release.sh linux-x64 0.1.0
```

Windows PowerShell:

```powershell
./scripts/publish-release.ps1 -Runtime win-x64 -Version 0.1.0
```

Each command creates a self-contained app directory under `artifacts/<runtime-id>/`. Run the smoke checklist before sharing it.

## GitHub Release Artifacts

Pushing a tag such as `v0.1.0` triggers the Release Artifacts workflow. It publishes and uploads self-contained directories for:

- `osx-arm64`
- `win-x64`
- `linux-x64`

The workflow artifacts are unsigned. macOS may show a Gatekeeper warning until an Apple Developer signing and notarization pipeline is configured. Windows SmartScreen can likewise warn for an unsigned executable.

## Verify Before Sharing

1. Download the artifact for the target OS.
2. Unpack it into a writable folder.
3. Start the app and complete [the smoke test](smoke-test.md) on a clean local data profile.
4. Record the tag, OS, and architecture used for the check.
