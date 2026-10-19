# Updating TanaHub

Version 0.1.3 checks for updates automatically at startup. In 0.1.2, use the manual
check in Settings to upgrade. Install the latest preview once if you use 0.1.0 or
0.1.1; those versions cannot acquire an updater through their old checker.

Use the startup notice or open **Settings → Check for updates**. Choose **Download update**,
then **Install and restart** after saving your edits. The package and its version
are checked against the publisher's signing key before installation. The preview
channel includes newer previews and stable releases.

Updates replace the application and its bundled core. Your library and settings
stay in the existing app-data directory. The complete application package is
downloaded; these are not differential patches. Close other copies of TanaHub
before installing an update.

- **macOS:** move TanaHub from the DMG to Applications before updating it.
- **Windows:** the installed application uses its updater installer and reopens.
- **Linux AppImage:** keep the AppImage in a writable location. The updater replaces
  that AppImage in place. **DEB installations use the package manager**; the app
  explains this and offers Browse releases.

If a download fails or its signature is invalid, installation does not begin.
Retry the update check or use the installers on GitHub. If installation fails,
TanaHub restores access to its core and keeps the update available for retry.
If only restarting fails after installation, quit and reopen the application.

Update signatures are separate from OS signing: these previews remain ad-hoc
signed/not notarized on macOS and unsigned on Windows. Back up your app-data folder
with TanaHub closed before trying a preview.

## Startup notifications

Starting with 0.1.3, supported macOS, Windows and Linux AppImage packages check
for updates in the background once per launch.

When a newer version is available, a notice appears above the current page with
**Download update** and **Later**. Downloading does not start automatically. After
the signature is verified, **Install and restart** lets you finish saving first.
The download and its progress remain available when you change pages. Settings
uses the same update state and still provides a manual check/retry.

**Later** hides the notice for the rest of that launch; the next launch checks again.
A failed startup check or an up-to-date result does not show a notice or interrupt
library use. The browser preview and unsupported native package types do not
request the native update feed. No operating-system notification permission is
needed.

## Artwork cache

The desktop client stores supported provider artwork in its OS app-cache folder,
with a 256 MiB budget and a maximum of 8 MiB per image. Old entries are evicted. Cached artwork is refreshed after 30 days; a failed refresh keeps the saved image available.
With `TANAHUB_DATA_DIRECTORY`, the cache lives in its `artwork-cache/` subfolder.
The cache is disposable and is not your library. Catalog offline-cache settings
control catalog metadata; artwork has its own bounded cache.

The UI fetches images near the viewport, limits concurrent loads and chooses
thumbnail/preview/original sizes according to the rendered size and screen density.
The full cover remains available through Enlarge. Unknown image hosts use normal
WebView loading rather than the native download cache.

## Maintainer release procedure

Keep the existing updater public key in `tauri.conf.json`. Back up the corresponding
private key securely: generating a new key for each release breaks updates for
installed clients. Never commit it or include it in release assets. The local
maintainer copy is under ignored `artifacts/updater-signing/`, with owner-only access.

The release workflow always builds the three native packages. There are two signing
routes:

1. Configure `TAURI_SIGNING_PRIVATE_KEY` in GitHub Actions secrets, with optional
   `TAURI_SIGNING_PRIVATE_KEY_PASSWORD`. Tauri then emits signed update artifacts
   during packaging. The collector preserves the exact signed archive bytes.
2. Keep the key local. Download the successful CI artifacts into one flat directory
   and run, from the repository root:

   ```sh
   node scripts/sign-release.mjs artifacts/release-ready /absolute/private/tanahub.key v0.1.3
   ```

   This signs the macOS app archive, Windows EXE and Linux AppImage, binds their
   version and independently verifies the signatures before creating `latest.json`.
   It also refreshes the platform SHA-256 manifests. The key never leaves the machine.

Inspect/upload all packages, `.sig` files, checksum manifests and `latest.json`
before publishing the draft. `scripts/update-manifest.mjs` rejects missing,
changed or wrongly signed update packages. Preserve already published version tags
and assets.

The **Publish update channel** workflow runs after publication. It accepts only a
published release's complete, signed platform metadata, then advances `preview.json`
on the dedicated `updates` branch. Stable releases also advance `stable.json`.
Version comparisons prevent reruns or older releases from rolling the feed back.
Clients use the HTTPS raw GitHub feed; each version's `latest.json` asset remains
an immutable release record. A release without signed metadata does not change the
feed. The workflow can also be dispatched manually with the published tag.

The signing protocol follows [Tauri's updater documentation](https://v2.tauri.app/plugin/updater/).
The application requires signatures to bind the announced version as well as the
package bytes. CI verifies the tooling with real, disposable Tauri signing keys;
those test keys never become application signing keys.
