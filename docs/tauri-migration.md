# Tauri + Svelte migration

The Tauri client runs alongside Avalonia and now exposes all six navigation sections. It keeps the existing C# Domain,
Application and Infrastructure projects and the same SQLite library and offline
cache. No library schema or personal-score conversion is required.

## Run

Install .NET SDK 10.0.201+, Node.js 24+, Rust and the platform-specific
[Tauri prerequisites](https://v2.tauri.app/start/prerequisites/).

```sh
cd src/TanaHub.Client
npm ci
npm run desktop:dev
```

For a packaged application on the current host:

```sh
npm run desktop:build
```

The preparation step publishes a self-contained, single-file .NET core alongside
the Tauri executable. End users do not need the .NET SDK or runtime installed.
The preview packages target Apple Silicon macOS 15+, Windows x64 and Linux x64.
Windows/Linux interactive platform verification is still pending. Cross
compilation is intentionally rejected by the preparation script.

macOS preview builds use an ad-hoc signature and are not notarized. Gatekeeper
warnings are expected; see [installation instructions](getting-started.md).
Trusted distribution without this exception requires Developer ID signing and
notarization; set `APPLE_SIGNING_IDENTITY` for a signed release build.
The macOS entitlement file permits JIT compilation and loading the bundled
SQLite native library under the hardened runtime. These correspond to the
[.NET host entitlements](https://learn.microsoft.com/en-us/dotnet/core/install/macos-notarization-issues);
no debugger or dynamic-loader environment permissions are enabled.

For browser development:

```sh
npm run prepare:bridge
npm run dev
```

The development UI uses `http://127.0.0.1:1420`. A same-origin development-only
adapter calls the same core process; production uses Tauri IPC and opens no HTTP
listener. Use `npm run dev -- --port 5174` if the default port is occupied.

The app uses the existing TanaHub data directory by default. To test separately,
set `TANAHUB_DATA_DIRECTORY` to an absolute temporary directory before launching.
Test scripts always use isolated profiles. Avoid running both clients against the
same profile while editing its library or cached settings.

## Migrated workflows

- Home: six real library metrics, status/type distributions, continue tracking,
  recent titles, genre-based recommendations, and today's library airing schedule.
- Library: all/anime/manga, status tabs, title/notes/organization search, tags and
  custom lists, sorting, pagination, grid/list views.
- Discover: search, media type, genre and sorting; trending, current-year,
  top-rated, upcoming and Korean manga collections.
- Full title page: banner, compact poster, localized titles, synopsis, genres,
  studio/duration/episode or chapter/volume metadata, characters, related titles,
  cached relation notices and refresh, cover lightbox, title copying, recognised
  provider links, expandable synopsis, keyboard focus and browser back navigation.
- Tracking: status, compact progress stepper with explicit draft confirmation,
  remaining counts/time, confirmed reset and record-all secondary actions,
  shared 0–10 rating modal with draft, Save, Cancel and separate Clear. Notes,
  review, tags and lists have compact summaries and editors opened on demand;
  closing an editor preserves its draft. Start/completion dates have explicit
  Save/Clear/Discard, local-calendar handling, chronology validation and visible
  empty states. Status changes preserve manually chosen dates. Confirmed removal
  remains available.
- Schedule: today/tomorrow/next-seven-days/following-seven-days, library filter,
  timezone selection and airing reminders using the existing notification service.
- Recognition: file/drop/clipboard images, trace.moe screenshots or SauceNAO art,
  ranked matches, source links, library actions and saved recognition history.
  Uploads happen only through the explicit provider search action, which confirms
  the selected image per request. The former enable-recognition preference is
  absent from this client; its stored value remains compatible with Avalonia.
- Settings: themes including high contrast, notifications, cache, catalog source,
  and optional SauceNAO key, with feedback next to each section's actions. Saved secrets are never sent
  back to the frontend.
- Account and data: AniList OAuth connection, import sync and disconnect; CSV/MAL
  XML export with native save dialog; XML preview followed by explicit import;
  GitHub release checks and system-browser links.
- Responsive layout: compact six-section navigation, cover widths 140–220px
  (170px default, capped instead of expanding into spare grid space), small list
  thumbnails, centered responsive rating modal and short reduced-motion-aware transitions.
  Narrow grids fit two compact columns; secondary collection filters and cover
  sizing open through a disclosure, with an active-filter count. Previous cover
  preferences are migrated once to the smaller scale.

## First-use and accessibility refinements — 19 October 2026

The library starts in All so imported manga is visible alongside anime. Library
and Discover retain independent searches and media types. Empty libraries offer
Discover and a direct import shortcut without unused filters; controls return
when the first title is added. The import shortcut waits for settings to load.

Compact navigation shows text labels, including narrow windows. Keyboard users
can skip navigation; section changes focus the heading and update the document
title. Returning from title details retains the existing focus behavior. Native
single-selects use CSS appearance control and the existing Lucide chevron so WebKit respects field sizing,
while retaining the platform picker and keyboard semantics. The macOS app was
checked for menu opening and Escape cancellation on an isolated profile. See
[CSS appearance](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/appearance)
for the native-widget styling behavior.

Settings explain optional AniList setup and the actual catalog providers. Legacy
MyAnimeList source values remain readable without silently rewriting settings.
AniList import is explicitly one-way, names the fields it can replace and requires
confirmation. Client ID drafts survive unrelated preference saves; changing the
ID requires its own secret instead of reusing credentials from the previous ID.
MAL XML previews carry the same replacement explanation.

Use `npm run doctor` in the client directory before installing dependencies when
setting up a source checkout. The [user guide](getting-started.md) covers the first
title, imports, exports, complete profile backups and current installer availability.

## Validation limits

Browser fixtures validate UI interactions without external account access or image
uploads. The real sidecar tests validate SQLite persistence, settings protection,
XML exchange and protocol boundaries in isolated profiles. Live catalog tests use
AniList metadata, covers, characters and relations. OAuth and recognition provider
flows are verified through the existing services with controlled test doubles;
complete live authorization and image recognition require the user's own action.
Native file dialogs require a desktop host. Windows and Linux packages have
automated native builds/core checks and still need interactive platform verification. Avalonia is retained until that is
complete; no production data conversion or deletion is performed.

## Boundaries

`TanaHub.Client` owns presentation and window lifecycle. `TanaHub.Bridge` owns
protocol validation, enum/DTO mapping and service composition. Rust launches a
fixed bundled executable and serializes requests over UTF-8 newline JSON, with
bounded requests/responses, method-specific timeouts and child cleanup on application exit. OAuth uses a separate core session so browser authorization does not block ordinary library operations. The
WebView receives no arbitrary process or filesystem permission.

Title copying uses the clipboard plugin's write-text permission only. Browser
development uses the browser clipboard API. No clipboard-read permission is
granted to the WebView. See the [UI refinement plan](ui-polish-plan-2026-10-04.md)
for the two review cycles and their actual validation results.

The preparation script stages and atomically replaces its executable so rebuilding
while a development core is running cannot overwrite that process's mapped code.

Opening title details retains the originating page's filters, scroll/focus and
recognition preview/results. Home/Schedule requests pause while their page is
hidden and resume on return. Library/cached tracking uses case-insensitive ASCII
comparison keys while retaining actual stored IDs. Failed pagination retries the
same page, and failed new queries never show cards from a previous filter.

Enabled button activation establishes its focus before opening dialogs/details,
including in WebKit where pointer/accessibility activation may not focus buttons
automatically. Back and modal dismissal restore their initiators; confirmed
removal restores collection search or the page heading.

Library lists use local metadata and persisted posters without one remote request
per card. Explicit details refresh remote metadata and fall back locally. A title
never cached locally can show its provider ID until its details are retrieved.
The core protocol is documented in [the bridge guide](../src/TanaHub.Bridge/README.md).

## Check

```sh
dotnet test TanaHub.sln --configuration Release
cd src/TanaHub.Client
npm run build
npm run test:transport
npm run test:core
npm exec playwright install chromium
npm test
```

Playwright fixtures intercept the development RPC so UI tests cannot alter the
user's library. Separate transport/core tests cover the actual process and SQLite
persistence across restart. Browser fixtures prove interaction and layout, not
live provider availability or platform WebView compatibility. The macOS package
should additionally be launched to verify native IPC and child cleanup.

The October autonomous pass validated the local macOS `.app` in an isolated
profile, including live title metadata, native IPC, keyboard date editing,
persistence after quit/restart and sidecar cleanup. It uses a local ad-hoc
signature. Windows/Linux, live OAuth, image uploads and native file dialogs still
require their own platform/user validation. See `AUTONOMOUS_PROGRESS.md` for the
exact iteration results and commands. `TANAHUB_TEST_BRIDGE_BINARY` can point
`test:core` at an independently published sidecar when checking an offline build.

## Visual reference

[Library concept](design/tauri-library-concept.png), generated with the built-in
Image Gen tool from the existing DESIGN.md palette. Preserve the near-black
indigo surfaces, restrained violet selection, cyan progress, amber score,
portrait 2:3 covers and numeric monospace. The reference's illustrative personal
score of 85/100 is intentionally adapted to the existing 0–10 storage contract.
The existing TanaHub logo, actual provider covers, Repeating status and live title
counts take precedence over representative content in the concept.
