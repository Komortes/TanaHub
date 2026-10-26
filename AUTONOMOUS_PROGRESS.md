# Autonomous development progress

## Overnight interface iteration — 26 October 2026

The user authorized autonomous iterative UI work, ordinary design decisions,
gradual commits and continued development overnight. The direction is a personal
media library: cover-led shelves, quiet graphite/paper surfaces and a progress
bookmark, with the next episode/chapter ahead of analytics. Published 0.1.4
installers remain unchanged; this work is unreleased on main.

Browser plugin is unavailable. Use the repository Playwright workflow and the
shared headless Chromium under /private/tmp/tanahub-interface-20261026/playwright-browsers.
Parent visual QA uses port 1424; isolated agent regressions use 1426 and separate
output directories. Public AniList titles/covers populate an in-memory test
library; no personal data or recognition uploads are involved. Evidence and logs
stay outside source under /private/tmp/tanahub-interface-20261026/.

### Iteration 1 — preserve completed actions during refresh

- Reproduced delayed recognition-history refresh restoring a deleted row or
  losing a newly saved match. Confirmed writes now merge onto the response while
  keeping other newly fetched source rows; failed writes leave the history intact.
- MAL XML Cancel and successful import return keyboard focus to the file control;
  a failed import keeps the confirmation usable and focused for retry.
- Red reproduction: five failures, two passing failure-path guards. All seven new
  cases now pass; existing feature coverage plus regressions passes 29/29. Svelte
  check reports zero errors/warnings. Independent read-only review approved the
  merge/failure handling and visible import-focus target.

### Iteration 2 — establish the library's visual foundation

- Replaced neon surface accents with graphite, paper, a warm bookmark accent and
  restrained sage progress. Kept system typography and existing saved theme values;
  high contrast has its own stronger surfaces and boundaries.
- Navigation now separates primary browsing from Schedule/Recognize and anchors
  Settings below. Compact layouts retain all six labels and at least 44px targets.
  A 320px screenshot exposed adjacent labels nearly touching despite no page
  overflow: a failing spacing regression led to content-sized mobile columns.
- Build passes with zero Svelte errors/warnings; all 53 affected browser scenarios
  pass. All four navigation sizes (320/360/432/768px) pass. Eleven populated
  screenshots include narrow, rail, wide and high-contrast layouts; no page
  overflow or browser errors. Independent review found no actionable defect.
- Recorded the domain, visual intent and compatibility constraints in
  docs/design/interface.md. Screenshots remain in the external QA directory.

### Iteration 3 — scope image paste to the active recognition page

- The retained hidden Recognize page no longer consumes title-page image paste.
  Visibility and request-generation checks protect both clipboard read stages,
  including stale errors that arrive after returning with Back.
- The initial reproduction failed two hidden-page cases. Independent review found
  that a request must stay cancelled after Back; four further read/getType success
  and failure reproductions failed before generation invalidation was added.
- All nine paste regressions now pass; combined feature/recovery/paste coverage
  passes 38/38. Svelte check has zero errors/warnings and the final independent
  review approves the cancellation guards. No real image uploads were performed.

### Broader review after iteration 3

The next interface work should change hierarchy, not only color: Home still puts
six equal analytics cards before the useful titles; library cards and filter rows
have too many enclosing panels; genre labels resemble buttons. Keep every
tracking, filtering and keyboard action while reducing that surface noise.
Review also identified a remaining clipboard intent race: a manual file selection
or Clear can be overwritten by an older pending clipboard request. Reproduce and
fix that separately, then recheck the complete browser suite.

### Iteration 4 — put the next episode or chapter first

- Moved all six Home statistics below recommendations; retained every metric,
  text value, media filter and chart. Replaced metric cards with a semantic
  definition list and restrained bars.
- In-progress titles and their existing actions now lead the page. Flat rows use
  smaller covers, readable captions and a progress bookmark; recent/airing rows
  use quiet separators. Tracking, failure handling and focus logic are unchanged.
- Feature scenarios pass as part of the 46-case concurrent verification batch.
  Build has zero diagnostics. Populated Home views at 320/360/768/1440px show
  current titles first without page overflow; the wider 16-view capture has no
  browser errors. Independent code review found no actionable regression.

### Iteration 5 — clarify the title-page reading hierarchy

- Removed the enclosing header panel and aligned identity consistently with and
  without a banner. The quiet banner remains; the cover still renders at 140px
  on desktop and 100px in compact windows. Header focus outlines are not clipped.
- Genre/format/year fragments now read as metadata rather than buttons. Synopsis
  and characters use rules; facts form a quiet side rail that stacks on mobile.
  Community remains labelled on narrow screens. Editing components and all
  handlers/DOM order remain unchanged.
- Read-only review approved the CSS boundary. Wide, 320/360px and high-contrast
  captures are usable without overflow; build reports zero diagnostics. The full
  browser run passes 191/191, including missing/failed covers, lightbox retry and
  focus, responsive poster dimensions, tracking and preserved journal drafts.

### Iteration 6 — preserve the latest image-source choice

- Reproduced all eight pending-clipboard cases: stale reads/blobs could overwrite
  a newer file or keyboard image paste, repopulate Clear, or show an obsolete
  failure. Accepted file selection and Clear now invalidate the old clipboard
  generation. Cancelling the file picker remains a no-op.
- Eight regressions failed first; all 46 feature/recovery/paste scenarios pass
  after two generation increments. Independent review found no remaining issue
  in the shared input path; Svelte diagnostics are clean.

### Broader review after iteration 6

- The complete browser suite passes 191/191 without retries. Build reports zero
  errors/warnings. Sixteen populated screenshots cover Home, title, collection,
  Discover and Settings in narrow, tablet, wide and high-contrast layouts with no
  browser errors or horizontal page overflow. Screenshot selection explicitly
  distinguishes the anime/manga entries that share a name.
- Home is now action-first and title metadata no longer resembles a filter set.
  The library still has enclosing cards, floating hover motion and several heavy
  filter rows. These are the next two separate visual increments. Preserve the
  mixed tracked/catalog baselines and readable maximum-progress text.
- First three commits are pushed; CI is running its four platform/frontend jobs.
  Published 0.1.4 assets and update feeds remain unchanged. Later native WebKit
  verification should check the complete visual changes before closing the run.

### Iteration 7 — make the collection a shelf

- Removed the enclosing card surface and floating/zooming cover effects. Covers,
  title captions and quick actions remain directly accessible. List view now has
  row separators; size preferences and cover aspect ratios remain unchanged.
- Status is plain text; the progress bookmark and EP/CH position span the full
  caption width. A small footer grid preserves the mixed tracked/catalog geometry
  and lets maximum values wrap without competing with the rating control.
- Actual text-range measurement exposed 10px vertical centering differences in
  short versus two-line button captions. A plain inner text span reserves the
  same two-line box and aligns their first line, preserving accessible names.
- All 71 affected browser scenarios pass, including mixed baselines, unknown/
  maximum progress, cover sizes, grid/list, keyboard focus and narrow layouts.
  Build/16-view capture are clean; independent reviews approve both the grid and
  caption wrapper. Final narrow screenshots confirm the first-line alignment.

### Iteration 8 — lighten collection controls

- Removed promotional header subtitles. Media/status/preset controls use quiet
  tabs with selected rules. Discover presets share a wrapping row with the actions
  when space allows, bringing artwork higher on the page.
- Search has a stronger boundary; sort/options/layout controls have 44px heights.
  Filters, active counts, disclosure state and clear-filter focus remain intact.
  Narrow headers explicitly stretch to preserve left alignment after stacking.
- All 71 affected scenarios pass; the 320px primary toolbar stays below its 135px
  height guard. The same 16 screenshots cover narrow/wide/contrast with no page
  overflow or browser errors. Independent review found no actionable defect.
- First GitHub CI batch now passes all four frontend/macOS/Windows/Linux jobs.

### Iteration 9 — return focus after clearing a search

- Library and Discover removed their focused Clear search button after clearing
  the query. Four pointer/Tab→Enter reproductions all failed at input focus even
  though the result sets restored correctly. Bind and focus the persistent input
  synchronously with preventScroll; preserve its accessible name and query logic.
- All four regression scenarios now pass. No timing workaround or extra network
  request was added. Follow with the full suite and native keyboard verification.

### Broader review after iteration 9

The principal collection/Home/title surfaces now follow the personal-library
direction. First-use, filtered-empty, failure/retry and modal paths need a fresh
rendered pass after these structural changes. Complete that read-only audit,
retain the saved theme wire value while replacing its old display name, align the
native startup color, and validate the actual WebKit app with isolated data.

### Iteration 10 — finish theme and native startup consistency

- Settings now labels the default theme Dark while keeping `Nebula dark` as the
  saved/core wire value. Existing settings remain compatible. The native window
  background matches the new base ink, removing the old purple startup backdrop.
- The full browser suite passes 195/195 without retries, client build has zero
  errors/warnings, transport recovery tests pass, release-version validation
  remains 0.1.4 and diff check is clean. Independent review approves the focus,
  display-name compatibility and native-color boundaries.
- Native app build is in progress for actual WebKit verification. Its fresh
  isolated profile has notifications disabled; no personal library is involved.
  A parallel read-only journey audit covers empty/add/continue, filtered-empty,
  failed-load retry and rating dismissal. Review remaining pages before choosing
  the next useful iteration instead of stopping automatically at ten.

### Native checkpoint after iteration 10

- The local macOS `.app` builds and passes deep/strict signature verification.
  Tested an immutable copy of a7d65b2 with a fresh isolated profile: empty library
  → Discover → title → Add → first episode correctly enters Watching.
- Home records episode 2 and keeps action focus. Search Tab→Enter Clear returns
  to the input in actual WebKit. Original cover loads; Escape returns to Enlarge;
  Back returns to the original Home cover. All six real character photos load.
- Saved a Unicode note, quit cleanly (exit 0), then read through the exact bundled
  core: progress 2, Watching and the note persist. Recorded 37 cached-image
  inode/size pairs for later comparison. Native runtime log is empty. Screenshots
  and reports stay in the external QA folder; no user profile was opened.
- A read-only journey audit passes 9/9 scenarios with 22 screenshots and no
  runtime/console/request errors. It covers empty/add/continue, both filtered-empty
  clears, three retries and rating Cancel/Escape at desktop and 320px. Initial
  offscreen-image assertions were corrected to scroll before judging lazy loads.

### Iteration 11 — keep a clear focus target while retrying

- The audit confirmed first-page Retry lost focus when its error panel vanished.
  Library/Discover now use their persistent heading; Home binds its own heading.
  Transfer happens synchronously before requesting, without an asynchronous
  restoration that could steal a later field or navigation choice.
- All seven new click/Enter and delayed-response cases failed first, then passed.
  Eighteen existing Home/filter/third-page retry scenarios pass; next-page retry
  behavior is unchanged. Svelte diagnostics are clean; independent review approves.

### Iteration 12 — read the schedule as a timetable

- Flat ruled rows align time, cover and title within each day. Library status is
  plain text; date ranges use 44px tabs and a selected rule. Removed the generic
  header subtitle. Range/timezone/notification and detail-return logic are intact.
- Range and collection status scrollers now reserve 5px for the shared 2px focus
  outline plus its 3px offset, including the first/last tab.
- Complete browser coverage passes 202/202; client build has zero diagnostics.
  Populated timetable screenshots at 320/1440px and keyboard tab focus are clean.
  Static independent review approves the CSS, header and focus-space adjustments.

### Broader review after iteration 12 and updated feature direction

- Twenty-five controlled screenshots cover the full interface including selected
  recognition images, matches and history; no page overflow or browser errors.
  Second pushed CI batch a7d65b2 passes all four platform/frontend jobs.
- The user asked to continue useful feature work on themes, notifications,
  statistics placement and updates, while avoiding excess. Queue: Statistics as
  #home/statistics with the six sidebar destinations preserved; Light/System themes
  preserving legacy values; last attempt/result/success in updater Settings.
- A source audit found Windows notification delivery is a no-op but still counted
  as sent. Address delivery honesty/retry as a separate verified notification task.
  Avoid adding a notification center or forcing platform permission prompts.

### Iteration 13 — make recognition's first action clear

- The image/drop area remains the main enclosure. Service tabs, smaller upload
  icon and unboxed empty Matches clarify where to begin. Results and saved history
  use separated rows; similarity is a neutral value rather than always green.
- Kept explicit provider/upload instructions, image/clipboard/history generation
  guards and every action. Cover sizes remain 80/64px; mobile actions are larger.
- Full 202-case suite and build pass. Captures cover empty, chosen image, explicit
  mocked search, saved match and mobile results without overflow/errors. No image
  was uploaded to a real recognition service. Independent review is clean.

### Iteration 14 — use direct state copy and aligned Home captions

- Replaced the generic empty-library slogan with Your library is empty and direct
  Discover/import guidance. Home's empty/error wording uses its actual page name
  and explains the next action. Existing no-account/local-data guidance stays.
- Applied the proven inner-caption wrapper to Home's title buttons so short and
  long names start on the same line; reserved height and accessible names remain.
- Full 202-case run, build and populated responsive captures pass. Independent
  review confirms this changes copy/presentation without changing actions or data.

### Iteration 15 — remove the remaining tinted modal backdrops

- Actual native cover viewing exposed four remaining hard-coded purple scrims.
  Cover, rating, detail and update dialogs now share semantic neutral RGB channels
  while retaining their individual opacity levels. High contrast uses black.
- Client build, all 202 browser scenarios and 25-view QA remain clean; independent
  review approves the shared token/consumers. Final native verification will cover
  these overlays together with the new theme feature.

### Broader review after iteration 15

The main visual queue is complete and tested. Continue with the user's bounded
feature direction: Statistics under Home without a seventh navigation item;
Light/System with explicit saved preference versus resolved palette; check status
in updater Settings while startup failures remain quiet. Do not expand into a
notification center. Separately correct Windows's false notification-delivery
count and expose truthful behavior. Current app source is unreleased, version
0.1.4; publishing installers/update feeds remains outside this request.

### Iteration 16 — give statistics its own place

- Added #home/statistics with an explicit Home entry and Back, preserving all six
  navigation destinations. Home stays mounted hidden/inert and pauses remote work;
  the new page reads only local home.get and shares the anime/manga filter.
- Direct links, reload, Back/Forward, heading/return focus, stale responses, Retry,
  empty/large counters and title-detail context are covered. No recommendations,
  schedule or notification request runs on a direct Statistics entry.
- All 13 new scenarios failed before the feature. The 27-case affected batch is
  green; five initial metric assertions were corrected to locate native dt text,
  without changing production semantics. Independent routing/focus review is clean.
- Combined feature validation passes 237 browser scenarios and all 372 .NET tests;
  build has zero diagnostics. Thirty-nine dark/light/contrast views include Stats
  at 320/1440px and have no page overflow or browser errors.

Next: commit verified themes and updater history, then notification delivery
honesty and final native/CI checks. Continue ordinary choices autonomously.

## 0.1.4 validation — 22 October 2026

Published [0.1.4 Preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.4)
at 2026-10-21 21:59:10 UTC with the artwork recovery fixes and installers for
Apple Silicon macOS, Windows x64 and Linux x64. Tag v0.1.4 resolves to tested
commit 570e5255ce8ecfdbe414de92ace3d8bcf648dd7b; earlier tags and assets are intact.

- [Source CI](https://github.com/Komortes/TanaHub/actions/runs/39679828505)
  passes all four jobs. [Release](https://github.com/Komortes/TanaHub/actions/runs/39679840748)
  passes all eight jobs: four checks, three packages and draft creation. Each
  native platform passes 363 .NET tests, six Rust tests and its self-contained
  core checks. All 166 release browser scenarios pass without retries; frontend
  diagnostics report zero errors and warnings.
- The release audit found newly reported Vite 8.3.2 development-server advisories.
  Updated only Vite to 8.3.3 and repeated the build, transport and all 166 browser
  scenarios locally. npm audit now reports zero vulnerabilities; the .NET
  dependency audit reports none. Independent review found no actionable defects
  in the patch, version alignment or release documentation.
- All 13 assets match GitHub sizes/SHA-256 digests. Three checksum manifests and
  four version-bound signatures verify independently, including the extra DEB
  signature. latest.json contains the three verified update payloads and the
  exact version-specific notes; CI generated it automatically.
- The downloaded macOS archive passes deep/strict codesign and reports 0.1.4;
  both shell and core are arm64. DMG integrity verification and the exact bundled
  core's real SQLite/RPC mutation, validation and restart checks pass. The app
  opens its original cover, restores focus on Escape and displays six cached
  character photos. All LibraryEntries fields and 30 cache-file inodes/sizes
  remain unchanged. Related titles fall back clearly to saved data while the
  provider is unavailable; the native log is empty.
- Linux DEB metadata reports 0.1.4/amd64 and AppImage is x86_64. Windows packaging
  builds the x64 NSIS payload; its installer bootstrap itself is PE32. Interactive
  Windows/Linux installation remains unverified.
- [Update channel](https://github.com/Komortes/TanaHub/actions/runs/39683089467)
  succeeds. Anonymous requests return HTTP 200 for the release, all four
  installer links and latest.json. Public preview.json matches the verified
  signed packages, notes and publication time. README, installation/update/release
  guides and Changelog now target the published 0.1.4; 34 local document links
  resolve. Documentation-only publication changes use link/command verification
  as specified by CONTRIBUTING.md.
- A copy of published 0.1.3 announces 0.1.4 at startup, downloads and verifies it,
  installs and restarts (PID 75743 -> 75842). Both updated binaries match the
  downloaded CI archive and strict codesign passes. Settings then shows 0.1.4
  and You are up to date without a manual check. Every LibraryEntries field and
  all 30 cache-file inodes/sizes survive; default settings remain unchanged.
  The test app quits cleanly and its native log is empty. Evidence is outside
  source under /private/tmp/tanahub-014-qa/ and ignored artifacts/github-release/v0.1.4/.

No database migration, native permission or signing-key change was introduced.
macOS remains ad-hoc signed/not notarized and Windows installers remain unsigned;
update signatures are verified separately. Real OAuth and recognition uploads
remain preview validation limits.

## New-user journey and artwork recovery — 20 October 2026

The user requested another first-use audit, photo/cover loading fixes and
commits/push. These client fixes shipped in 0.1.4, recorded above.

- Started the published macOS app with an empty isolated profile. Discover loaded
  real AniList covers, Attack on Titan opened with its banner, original cover,
  six character photos and related titles. Adding it, recording episode 1 and a
  Unicode note survived navigation and restart. All 23 previously cached images
  retained their inode/size; Home added seven more. The native log was empty.
- Reproduced hidden collection cards occupying image slots ahead of visible
  details, including zero-width resize choosing originals. Pending hidden work
  now waits until the collection is visible again; running work completes within
  the four-load limit. Back resumes covers. Related-title refresh now retries
  failed images whether their URL stays the same or changes.
- Title headers select preview/thumbnail sizes by rendered width and screen
  density. Enlarging still requests the original. The viewer reserves a usable
  loading frame, explains failures and offers Retry cover. A shared header/image
  failure keeps the viewer open; retry and dismissal retain usable keyboard focus.
- Native slow-network testing exposed a metadata race that closed an open cover
  as full title details arrived. A delayed-detail regression reproduces it; the
  viewer now closes only when the original image URL changes. The corrected
  native release build shows Loading cover, stays open after details arrive,
  displays the original and returns focus to Enlarge on Escape. All six real
  character photos load; every LibraryEntries field remains unchanged.
- Ten new browser scenarios cover queue visibility/resume, related-cover refresh,
  320/1440px pending originals, preview selection, failed/shared originals, retry,
  focus and delayed metadata. Gated image tests wait for DOMContentLoaded so
  navigation does not wait for the intentionally blocked images.
- Frontend build has zero errors/warnings. The macOS arm64 app builds and passes
  deep/strict codesign verification. Independent read-only review found a shared
  header failure dismissing the viewer; it is fixed and covered by two tests.
  Final review of the metadata guard found no actionable defects.
- The final browser run passes 166/166 scenarios in 1.6 minutes without retries.
  An earlier run completed 164 but could not write two teardown traces because
  the disk was full. Removed regenerable Cargo compiler caches, retained the
  verified app/evidence, and repeated the complete suite successfully.
- Fresh native screenshots, controlled browser screenshots, regression logs and
  JSON persistence/cache reports are outside source under
  /private/tmp/tanahub-journey-20261019/. Test profiles are isolated; the temporary
  slow-network proxy affected only the test child and stopped when it quit.

No dependency, permission, backend, database schema or signing-key change was
introduced. Windows/Linux interactive rendering remains unverified locally.

## 0.1.3 publication and native upgrade — 19 October 2026

Published [0.1.3 Preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.3)
at 20:32:26 UTC with startup update checks and installers for Apple Silicon macOS,
Windows x64 and Linux x64. Tag v0.1.3 resolves to the exact tested source commit
0e9852b657417440e69bfa291ca95542e05f7cc8. Earlier releases/tags/assets are intact.

- [Source CI](https://github.com/Komortes/TanaHub/actions/runs/39317211704)
  passes all four jobs. [Release](https://github.com/Komortes/TanaHub/actions/runs/39317223452)
  passes all eight jobs: four checks, three packages and draft creation. Each
  native runner passes 363 .NET tests, six Rust tests, 13 tooling tests, format,
  frontend build and self-contained core tests. All 156 Chromium scenarios pass
  without retries; frontend build has zero errors and warnings.
- All 13 release assets match their GitHub sizes/SHA-256 digests. All three checksum
  manifests and four signatures verify independently, including the additional
  DEB signature. latest.json has the correct version, notes and three signed
  platform packages. The draft workflow generated it automatically this time.
- macOS DMG integrity, deep/strict codesign, version 0.1.3 and arm64 shell/core
  checks pass. The exact packaged core passes real SQLite/RPC mutations, Unicode,
  validation and persistence/restart tests locally with isolated data. Linux DEB
  metadata reports 0.1.3/amd64; the AppImage is x86_64. Windows packaging reports
  the x64 NSIS payload (its bootstrap executable itself is PE32).
- [Update channel](https://github.com/Komortes/TanaHub/actions/runs/39320160240)
  succeeds. Anonymous requests return HTTP 200 for the release and all four README
  installer links. Public preview.json matches the verified three-platform
  metadata and publication time. README and user/update/release guides now target
  0.1.3; 52 local links in the release documents resolve.
- A copy of the actual published 0.1.2 app offered 0.1.3 in Settings, downloaded
  and verified its signature, installed it and relaunched (PID 42764 -> 45901).
  The installed binary matches the published CI binary and strict codesign passes.
  After restart, opening Settings shows 0.1.3 and You are up to date without
  clicking Check for updates: the new startup check has already completed.
- Every logical LibraryEntries field matches the pre-upgrade snapshot, including
  Watching EP 1/28 and the Unicode note. All 25 artwork-cache files retain their
  inode/size (1,380,155 bytes). The native log is empty; the test app quit cleanly.
  Evidence is outside source under /private/tmp/tanahub-013-qa/ and ignored
  artifacts/github-release/v0.1.3/.

Windows/Linux interactive installation, real AniList OAuth and live recognition
uploads remain unverified preview limits. macOS remains ad-hoc signed/not notarized
and Windows OS signing is not configured; update signatures are verified separately.
No database migration, dependency, permission or signing-key change was introduced.

## Startup update checks and notification — 19 October 2026

The user requested automatic update discovery on launch, a clear notification
outside Settings, and commits/push. This iteration changes the client only; the
published 0.1.2 packages retain their existing manual checker until a new release.

- Extracted the updater into one reactive controller owned by the application
  window. Existing five download/signature/install/restart scenarios passed before
  adding startup behavior. Navigation no longer disposes an active download.
- Supported native packages check once per window launch, asynchronously and with
  the existing 20-second timeout. Automatic failures stay quiet; successful
  availability produces an in-app notice. Settings retains explicit checking and
  retry, uses the same resource, and remains usable during background checking.
- The notice shows the version, Download update, progress, separate Install and
  restart, and Later. Later postpones it until the next launch. It never downloads
  or installs automatically. Signature verification and idle-core protection are
  preserved; the installation/restart modal is now owned by the app window.
- The initial startup regression failed first (zero checks instead of one).
  Thirteen updater scenarios now cover foreground/background coordination,
  offline/manual retry, no-update silence, unsupported packages, browser mode,
  dismissal/relaunch, downloads across navigation, explicit installation,
  signature/prepare/install/restart failures, keyboard focus and 320/1440px layout.
- Browser plugin not available; used the repository Playwright workflow with
  Chromium at http://127.0.0.1:1424 and isolated in-memory RPC/native IPC fixtures.
  Full browser suite: 156/156 pass. Frontend build: zero errors and warnings.
  Screenshot evidence (mock version 0.1.3) is outside source under
  /private/tmp/tanahub-startup-updates/. No native package was published in this
  iteration, and startup UI behavior has not been interactively tested in WebKit.
- Reviewed lifecycle cleanup, single-flight guards, platform gating, focus and
  failure paths. No dependencies, permissions, provider APIs, database or signing
  keys changed. Documentation labels this behavior as unreleased/on main.

Follow-up completed: CI is green and the feature shipped in signed 0.1.3 above.

## Artwork and signed updater — 19 October 2026

The user authorized continued optimization, commits/push and prereleases, and
explicitly approved storing the prepared signing key in the Komortes/TanaHub
Actions secret `TAURI_SIGNING_PRIVATE_KEY`. The secret name is verified; private
key material stays out of source and logs. The owner-only local backup is ignored.

- Artwork sizes now flow from AniList through bridge DTOs and offline catalog
  persistence. Cards/list rows select 230px/100px images where screen density
  permits; original covers remain available for enlargement. One real AniList
  sample measured 136,267 bytes original, 40,657 preview and 9,488 thumbnail.
  These are per-image traffic measurements, not a claim about total app speed.
- A shared viewport loader bounds active loads to four and defers distant covers.
  Native artwork caching uses allowed HTTPS provider hosts, raster validation,
  atomic writes, 8 MiB/image and 256 MiB LRU limits. Downloads refresh after 30
  days while stale saved images remain usable on refresh failure.
- A native isolated profile loaded real AniList covers, saved Frieren at Watching
  EP 1/28 with a Unicode note, and retained that state after restart. All 25 cached
  image files retained their inodes across restart (1,380,155 bytes). Evidence is
  outside the repository under /private/tmp/tanahub-optimization-qa/.
- Added official Tauri updater/process plugins with version-bound signatures,
  separate download/install controls, progress, busy-core protection, sidecar
  shutdown and recoverable install/restart failures. Linux DEB explains its
  package-manager update path; AppImage supports in-place updates.
- Release tooling preserves exact signed archive bytes, independently verifies
  signatures for all three platforms and publishes monotonic preview/stable feeds
  on a separate updates branch. Existing published releases remain immutable.
- Local checks so far: 363 .NET tests, six native tests, 13 release-tooling tests,
  format, zero-diagnostic frontend build, transport and real self-contained core
  mutation/restart checks pass. Runtime npm audit reports no known vulnerabilities.
- Two image queue/lifecycle regressions were reproduced and fixed. The broad
  browser run then exposed shared mutable fixture objects: size-selection tests
  leaked mocked provider URLs into later tests. Trace errors confirmed those
  exact leaked URLs. Each mock service now deep-clones its seed records; 30 focused
  repetitions and the final full 148/148 browser suite pass. No diagnostic code
  remains. The first GitHub browser run then passed 147/148: Playwright's uncheck
  postcondition raced with the expected immediate rollback after a failed settings
  save. Use a normal user click and retain explicit error/persisted-value assertions;
  12 focused repetitions pass. Superseded runs 39288729059/39288741456 were stopped
  before packaging. Fresh checks below pass completely.

### 0.1.2 publication and real upgrade verification

- Published [0.1.2 Preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.2)
  at 17:17:33 UTC. Lightweight tag v0.1.2 resolves to the exact tested app source
  bbb885258b005922a0f46cfca94c48689cdb69fe. No earlier release/tag was replaced.
- [Main CI](https://github.com/Komortes/TanaHub/actions/runs/39289544108) passes
  all four jobs. [Release](https://github.com/Komortes/TanaHub/actions/runs/39289543887)
  passes all eight: four checks, three native packages and draft creation.
  Each native runner passes 363 .NET tests, six Rust tests, 13 tooling tests,
  formatting, frontend build and real self-contained core checks. Chromium passes
  148/148 with no retry/flaky cases in the successful Release run.
- All 13 published asset sizes and GitHub SHA-256 digests match the exact downloaded
  files, with all three SHA manifests verified. All three updater payloads pass
  independent Minisign/package/version verification; the additional DEB signature
  also verifies. The macOS DMG passes hdiutil verification; app deep/strict codesign,
  version 0.1.2 and arm64 shell/core checks pass. Its exact CI sidecar also passes
  real SQLite/RPC mutation, validation, Unicode and restart tests locally.
- Tauri additionally emits a DEB signature. The draft job's initial total-signature
  count therefore skipped latest.json. Generated and independently verified that
  manifest locally, uploaded it to the draft before publication, and preserved all
  CI payload bytes/signatures. The workflow now checks the three named updater
  signatures rather than counting unrelated signatures, fixing future releases.
- [Update channel](https://github.com/Komortes/TanaHub/actions/runs/39292855434)
  succeeds. Public preview.json announces 0.1.2 with the verified three-platform
  URLs. Anonymous checks return HTTP 200 for the release page and all 13 assets.
  The four README installer links belong to that published asset set.
- A private 0.1.1 bootstrap containing the new updater/key offered the real published
  0.1.2, downloaded and verified its signature, installed it and automatically
  relaunched (detached PID 21965 -> 21987). This bootstrap is a test harness, not the
  old public 0.1.1 installer. Settings shows 0.1.2 and You are up to date afterward.
  All logical LibraryEntries fields match the pre-update snapshot, including
  Watching EP 1/28 and the saved Unicode note. All 25 artwork-cache files retain
  their inode/size after upgrade. The native log is empty and the test app quit.
- Native screenshots and before/after SQLite/cache snapshots remain outside source
  under /private/tmp/tanahub-optimization-qa/. No personal library/account was used.
  Windows/Linux builds and tests pass; interactive installation/updating on those
  platforms remains unverified. OS signing/notarization limits remain documented.

Next: interactive Windows/AppImage update checks when those environments are
available. Existing public 0.1.0/0.1.1 installations need a one-time 0.1.2 install;
subsequent updates download a full signed package and replace the app in place.

## 0.1.1 preview publication — 19 October 2026

The user authorized a new prerelease with macOS, Windows and Linux installers,
CI verification and updated README download links. All version manifests and the
Settings version fallback now use 0.1.1. The user guide describes the shipped
first-progress, draft-recovery and MAL XML fixes without source-only caveats.

- Published [TanaHub 0.1.1 Preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.1)
  at 13:49:52 UTC on 19 October 2026, with draft=false and prerelease=true.
  Annotated tag v0.1.1 resolves to tested source commit
  d547b768c7e77b89798ff106780e32251b60dee2.
- [Main CI](https://github.com/Komortes/TanaHub/actions/runs/39260271758)
  passed all four jobs before tagging. The tag's
  [Release workflow](https://github.com/Komortes/TanaHub/actions/runs/39261312725)
  passed all eight jobs: four reusable checks, three platform packages and draft
  creation. Each native runner passed 359 .NET tests, version/tooling checks,
  frontend build, real self-contained core and Rust checks; Chromium passed all
  137 scenarios. Package jobs also passed their own real core tests.
- Published five packages: macOS arm64 DMG/app archive, Windows x64 NSIS EXE,
  Linux x64 DEB/AppImage, plus three SHA-256 manifests. Downloaded the exact CI
  artifacts under ignored artifacts/github-release/v0.1.1/ and verified every
  payload checksum, all eight GitHub uploaded digests and file sizes.
- Anonymous HTTP checks returned 200 for the release page and all eight asset
  URLs. All three anonymously downloaded checksum files match the verified local
  files. The four README installer links are members of that published asset set;
  the README fetched from GitHub matches the updated local source.
- The downloaded macOS app reports version 0.1.1, macOS minimum 15.0 and arm64
  executables; deep/strict codesign verification passes with an ad-hoc hardened
  runtime signature. The DMG passes hdiutil verification; its shell, core and
  Info.plist match the tested app archive, and it includes an Applications link.
  The read-only test mount was detached afterward.
- The core from the CI archive passed real SQLite/RPC mutations, boundary checks,
  Unicode/imported metadata persistence and restart checks with disposable data.
  The actual CI application opened an empty isolated profile, loaded Settings,
  showed 0.1.1 before and after a real update check, and correctly explained that
  previews are downloaded through Browse releases. It quit with exit code 0.
  The inspected screenshot is in ignored output/release/v0.1.1/native-settings.jpg.
- Linux DEB metadata reports version 0.1.1 and amd64, includes the shell/core,
  desktop entry and icons; AppImage is x86-64 ELF. Windows packaging logs confirm
  the x64 NSIS target. Interactive Windows/Linux installation remains unverified.
- Previous v0.1.0 release notes, publication state and all eight asset records
  remain unchanged. Neither the old public tag nor its packages were replaced.
- Signing limitations remain explicit: macOS ad-hoc/not notarized and Windows
  unsigned. Live OAuth and recognition uploads remain outside the evidence.
  No personal profile or account was used for these release smoke checks.

## New-user workflow audit and fixes — 19 October 2026

### Additional import walkthrough

The actual macOS file dialogs successfully exported CSV/MAL XML. Reimporting that
XML then exposed two further defects: preview displayed a raw MAL ID, and an
existing AniList entry gained a second library record for the same MAL title.

- The parser now exposes XML title/count metadata while retaining its original
  tracking-only API. Preview uses those names without saving anything; apply
  persists missing metadata before the SQLite merge, so names survive restart
  without remote requests. A failed metadata flush leaves the library unchanged
  and reports a retryable storage error; retry also flushes previously staged data.
- Preview/apply match already tracked entries by known MAL ID and media type.
  Local notes, reviews, tags/lists, artwork and dates remain protected. Duplicate
  XML rows are coalesced; existing duplicate library records are not auto-deleted.
- Fixed singular title/episode/chapter copy in import/export, cards and statistics.
- Four further bridge scenarios cover export/reimport, offline names, failed
  persistence/retry/restart and identical anime/manga MAL numbers. Two original
  defects and the staged-cache retry regression were reproduced by failing tests.
- Final local total: 359 .NET tests and 137 browser scenarios pass; format and
  frontend checks pass. Rebuilt the macOS app and reran the packaged core test,
  including imported Unicode names/counts surviving an actual process restart.
- In a fresh native profile, added Frieren, imported the earlier XML using the
  native picker, confirmed its readable preview and verified exactly one canonical
  title remained with the imported progress. Captures 10–12 are in the local report.

The user authorized a fresh application walkthrough, fixes, commits and push.
Used separate empty browser/native profiles; no personal library, account or
credentials were accessed. Captured and inspected the current interface through
the in-app browser and the actual macOS Tauri/WebKit application.

- First progress previously left a new title in Planning while Home claimed
  nothing was in progress. A zero-to-positive progress action now starts
  Watching/Reading, including a non-conflicting start date. Explicit statuses,
  dates and imported tracking retain their existing semantics. Four bridge
  regressions failed first; SQLite and packaged-process checks cover persistence.
- Notes, reviews, organization, dates and progress drafts now survive navigation
  and reload in the same window. Save/Discard remain explicit. Changed saved
  values invalidate old drafts, removal clears them, and an in-memory fallback
  keeps the latest edit/discard when session storage writes fail. Drafts are not
  a library backup and are not guaranteed after closing the window.
- Catalog add/progress and detail add actions restore keyboard focus to a usable
  control. Final progress and failed writes retain a sensible target; inactive
  originating cards cannot take focus away from a title page.
- Settings explains that update checks use stable releases, reports the absence
  of a stable release accurately and always offers Browse releases for previews.
- Added 12 browser cases. Eight regression cases failed before their fixes;
  the storage-failure case also exposed and corrected stale cached draft recovery.
  A full-suite run exposed an existing request-count race: the navigation test
  now waits for the restored debounced search before asserting a no-op click.
- Final local checks: 137/137 Chromium scenarios, 355/355 .NET tests,
  9/9 tooling tests, format verification, zero Svelte diagnostics, production
  frontend/native macOS builds, actual self-contained core and transport checks.
  All five draft editors also pass at 320px without horizontal overflow.
- Actual macOS walkthrough: empty library → Discover/search → Add → Return to
  record episode 1 → Home shows Watching/In progress → notes draft → Back/open
  restores draft → Save → quit/restart retains status, progress and Unicode note.
  Screenshots and the detailed local report are in ignored
  output/playwright/new-user-oct19/. Windows/Linux interactive UI, live OAuth and
  recognition uploads are outside this run's interactive evidence.
- Updated setup guidance to distinguish current source behavior from the
  immutable published 0.1.0 Preview installers. This iteration does not replace
  the release tag or public installer assets.

## Repository presentation — 19 October 2026

- Reworked the README around the application: crisp existing SVG wordmark,
  preview/CI/license badges, real library screenshot, platform downloads and a
  three-step first-title guide. Developer checks and the repository map are
  collapsible; existing setup commands and documentation anchors are retained.
- Added docs/screenshots/library.png (1440 × 780) and tracking.png (885 × 260,
  a direct element capture). Combined size is approximately 642 KiB. The screenshot
  guide records their provenance and refresh workflow.
- Captured the real Svelte client and .NET/SQLite core on port 1422 with the
  isolated /tmp/tanahub-readme-demo-oct19 profile. Twelve sample titles came from
  AniList; six are visible in the Watching / Reading filter. No RPC mocks, edited
  interface elements, personal library or credentials were used.
- Verified Library → Frieren → +1 episode → Back: progress persisted from 12 to
  13, and collection filters survived. Covers and fonts loaded, both captures
  were inspected, no horizontal overflow occurred and the browser reported no
  application errors or warnings. Used the existing Chromium installation with
  Playwright CLI; the Browser plugin is not available.
- GitHub's Markdown API rendered both screenshot links, the SVG, the preview
  note and all three disclosure sections correctly. Local links, existing
  navigation headings, PNG dimensions and git diff --check passed. CLI logs stay
  ignored alongside raw captures; selected documentation images are versioned.
- This is a documentation/presentation change; the public v0.1.0 installers and
  release tag retain their tested source revision.

## Public preview publication — 19 October 2026

The user explicitly authorized committing and pushing the prepared work, checking
CI and publishing a prerelease with installers. This supersedes the earlier
publication boundary for this release. The remote main branch was fetched and
matched the local base; no existing release or tag needed replacement.

Release preparation included a source/secret inventory, platform download links,
installation guidance and versioned preview notes. Native package builds and CI
passed before publication.

### Published result

- [TanaHub 0.1.0 Preview](https://github.com/Komortes/TanaHub/releases/tag/v0.1.0)
  was published on 19 October 2026 at 11:44 UTC. GitHub confirms draft=false and
  prerelease=true. Public tag v0.1.0 points to tested commit
  5e3216fbaf98d2584b3877c5e8e568091161bc48.
- [Cross-platform CI](https://github.com/Komortes/TanaHub/actions/runs/39244660478)
  passed all four jobs. Each native runner passed the 343 .NET tests, format/build,
  version/tooling checks, frontend build, self-contained core and Rust checks.
  The browser job passed all 125 Chromium scenarios and transport checks.
- [Installer build](https://github.com/Komortes/TanaHub/actions/runs/39244660012)
  passed the same reusable checks and packaged all three platforms. Its exact
  downloaded artifacts were promoted to the release without a rebuild.
- Public assets: Apple Silicon macOS DMG (35.67 MiB) and app archive (35.05 MiB),
  Windows x64 EXE (35.10 MiB), Linux x64 DEB (38.45 MiB) and AppImage (112.51 MiB),
  plus one SHA-256 manifest per platform. Local copies are ignored under
  artifacts/github-release/v0.1.0/.
- All five package hashes and formats passed verification. All eight GitHub
  uploaded asset digests matched the verified local files. Anonymous HEAD requests
  returned HTTP 200 for the published release page and all eight asset URLs.
- The downloaded macOS bundle passed deep/strict codesign verification, reports
  macOS 15.0 minimum and arm64 executables, and its DMG passed hdiutil verify.
  The bundled core passed RPC/SQLite/Unicode/persistence checks. The actual CI app
  also started its frontend and initialized SQLite through native IPC using an
  isolated profile, then only that test process was terminated.
- Preview limitations remain explicit: ad-hoc/non-notarized macOS, unsigned
  Windows, pending interactive Windows/Linux installation, real OAuth, live
  recognition uploads and native file-dialog validation. No user profile or
  account was used for release checks.
- Tag publication triggered a duplicate build
  [39246784411](https://github.com/Komortes/TanaHub/actions/runs/39246784411), which
  was cancelled after the exact same commit/artifacts had already passed the
  complete manual workflow. The publication step now exits successfully without
  changing an already public release, avoiding a misleading failure on reruns.
- All prepared source was committed and pushed in five commits (02d7bc5,
  4cd38b2, 9df1de7, d0b517e, 5e3216f); the final publication record and rerun guard
  are a separate maintenance commit. The repository About description and topics
  now describe the local-first Tauri/Svelte tracker.

The first GitHub run exposed two Windows-specific test-harness failures: redirected
stdin used the Windows console code page for Unicode requests, and moving a cache
file over a directory reports UnauthorizedAccessException on Windows rather than
IOException. The production bridge already reads/writes UTF-8. The test harness
now specifies UTF-8 for all pipes; the cache retry test accepts either documented
filesystem failure and still proves a subsequent flush persists the pending item.
The bridge tests also now include the same coverage collector as the other suites.
No assertions or CI gates were skipped. npm and NuGet vulnerability checks found
no known vulnerable dependencies in the current lockfile/restored solution.

The next Windows run passed all .NET and self-contained core assertions, then
exposed a cleanup race: the extracted SQLite DLL remains locked briefly after
terminating the core. Temporary-profile removal now uses Node's bounded recursive
rm retries for Windows file locks; cleanup errors still fail the check. Its pass
message is printed only after cleanup finishes.

## Usability and source-checkout readiness — 19 October 2026

The user resumed autonomous development with the goal of an understandable app
that people can obtain and use from GitHub. Existing dirty work is preserved;
source publication remains subject to the earlier no-push-without-permission rule.

### Latest verified result

- Completed twelve further cycles (20–31 below), bringing the development run to 31 cycles.
- 125 Chromium browser tests pass: the original 108 plus 17 usability regressions, with no skips, retries or unexpected failures.
- All 343 .NET tests pass. Nine Node release/environment tests pass.
- Client and macOS native builds pass with zero source diagnostics. Transport and
  real self-contained sidecar persistence/validation checks pass in isolated profiles.
- actionlint, YAML syntax, version consistency, local documentation links and
  git diff --check pass.
- Current preview is open in the in-app browser at http://127.0.0.1:1420/#library,
  using TANAHUB_DATA_DIRECTORY=/tmp/tanahub-usability-oct19. Browser console is clear.
  The dev server belongs to this work session; do not point it at the user's profile.
- Rebuilt the .app and .dmg using the standard desktop preparation/build path,
  a fresh self-contained core, CI=true, locked/offline Rust resolution and ad-hoc
  signing. No signing credentials, notarization, installation or publication used.
- Screenshots are in /tmp/tanahub-oct19-*.png; JSON regression report is
  /tmp/tanahub-oct19-final-browser.json. Temporary paths may expire.
- Browser plugin is absent. Visual capture and interaction used the existing in-app
  browser through CUA; automated regression used the repository's Playwright workflow.

- Native controls were also checked in the rebuilt macOS app: first launch,
  import shortcut focus, full-size select fields, system picker opening and Escape
  cancellation. These checks used a separate empty profile; the app was then quit.
- Final local assets are in artifacts/release/osx-arm64/ with refreshed SHA-256
  checksums. Preview signature is ad-hoc; the binary is not notarized.

### Cycles

20. **Make all saved titles visible.** First-use library defaults to All, rather
    than silently hiding manga behind Anime. Mixed status labels name Watching /
    Reading and Rewatching / Rereading. Tests cover manga visibility and counters.
21. **Preserve collection intent.** Library and Discover remember independent
    searches and media types. Clicking the active section no longer clears them or
    triggers another collection request. Regression checks switch both ways.
22. **Keyboard and screen-reader orientation.** Added Skip to content without a
    routing side effect. Section changes focus the heading; document titles name
    the current page/title. Existing title-return focus paths remain covered.
    Review checkpoint: first use also needed an import path and clear account setup.
23. **Connect the empty state to import.** Added no-account/local-storage guidance
    and Import a list, which opens/focuses Your library data. The destination waits
    for delayed settings, and late responses cannot steal focus after navigating away.
24. **Explain real providers.** Settings offer AniList and MangaDex with their
    actual scope. Legacy MyAnimeList values are explained, retained and correctly
    restored after failed saves. Optional AniList setup has an expandable guide.
25. **Make imports explicit.** Renamed Sync library to Import from AniList and
    added a confirmation naming replaced fields. Cancel restores focus; failures
    retain the confirmation for retry. MAL XML previews explain the same merge
    rules. The underlying import ownership contract is unchanged.
    Review checkpoint: source setup and first-run clutter remained obstacles.
26. **Help a new checkout start.** Added npm run doctor and a dependency-free root
    CLI that checks Node, SDK selection, Rust and Cargo, with useful missing-tool /
    timeout guidance. Four tests cover those states. CI runs them. Added a user
    guide covering first title, imports, export limits, complete backup and install
    availability. No public release exists as verified through GitHub CLI today.
27. **Remove controls with no job yet.** Truly empty unfiltered All libraries hide
    status/sort/layout controls. Filters remain available for filtered no-result
    states and return after adding the first title. Keyboard and responsive tests pass.
28. **Keep compact navigation readable.** Tablet and narrow layouts show visible
    section names and targets at least 44×44 pixels. Verified at 320, 432 and 768px,
    including no page overflow. Final visual review also accepted the empty-state
    desktop and narrow screenshots. Broad review of cycles 26–28 found no open
    regressions from this change set.

29. **Correct native select sizing.** The actual macOS app exposed Safari menulist
    chrome that ignored intended field sizing. Single-selects now use CSS appearance
    control and the existing Lucide chevron, retaining HTML select semantics. Added
    its ISC notice and room for the arrow in catalog/schedule controls. Native app
    verification confirmed full-size controls, system menu opening and Escape
    cancellation without changing the saved value. Browser tests cover target
    height and Tab/Shift+Tab; selection saves remain covered by existing tests.

The native first-use smoke test used /tmp/tanahub-native-usability-oct19, launched
from the rebuilt bundle. It confirmed empty-state rendering, settings IPC and
Import a list focusing Your library data. The test app was quit after inspection.
Before/after native screenshots are /tmp/tanahub-oct19-native-settings-*.png.

30. **Preserve connection drafts.** A preference save replaced the typed Client
    ID with its last saved value. The credential editor now keeps that draft
    across unrelated saves and resets its dirty flag only after successful connect.
31. **Require a matching secret.** Editing a saved Client ID no longer permits
    reusing the previous ID's saved secret. The form explains the requirement,
    rejects whitespace-only secrets and retains reconnect for the saved pair.
    Both defects were reproduced before the fix and passed with the feature suite.
    Broad review confirmed no changes to backend import/credential contracts.

### Remaining release work

- P1: review and publish the intended source revision before a fresh GitHub clone
  can include this client. No commit, push, tag or release was created this session.
- P1: Windows/Linux native packages and WebViews need their hosts/CI and smoke checks.
- P1: Developer ID/notarization and Windows signing remain distribution prerequisites.
- P2: real-account OAuth, live recognition uploads and native file-dialog checks
  remain separate from fixture-based regression tests.
- Before actual publication, update unreleased/installer-availability wording and
  rerun the release guide against the exact tagged source revision.

## Repository maintenance — 12 October 2026

User requested repository presentation and release preparation after the development run.

- Updated GitHub About and topics through the authenticated CLI; verified the saved description and topics. No release existed at inspection.
- Reworked README with current pre-release status, setup, checks and architecture. Added CHANGELOG, CONTRIBUTING, SECURITY, a documentation index, 0.1.0 preview notes and the Tauri/sidecar ADR; retained historical notes and decisions.
- Added issue/PR templates and monthly grouped Dependabot updates. Ignored generated captures, coverage reports, local secrets and release artifacts without deleting them.
- CI now covers .NET, Svelte/browser scenarios, transport, prepared sidecar, release tooling and the native shell. Tag releases reuse CI, package three hosts and create a draft prerelease only after all packages succeed. Branch dispatch uploads workflow artifacts only. Permissions are scoped and Actions are pinned to verified commit SHAs.
- Added version/tag and installer checks, stable asset names and SHA-256 files. Five Node release-tooling tests pass, including version drift, invalid tags, missing and duplicate installers, Windows names and hashes.
- Corrected whitespace in three existing test files and removed a UTF-8 BOM in one migration; full dotnet format verification and all 343 .NET tests pass. Client build has zero diagnostics; transport tests pass; actionlint and YAML checks pass; local documentation links resolve.
- Built ad-hoc signed macOS .app (47.77 MiB) and .dmg (35.85 MiB). Noninteractive DMG packaging needs CI=true to skip Finder styling; documented this after a local AppleScript timeout. The failed process exited itself; no process was killed.
- Verified the app signature, DMG internal checksum, archived executable permissions and the bundled sidecar's real SQLite persistence/validation in an isolated profile. Release files and SHA-256 manifest are under artifacts/release/osx-arm64/. No notarization or public distribution was performed.
- Source remains uncommitted, preserving earlier work. No source push, tag or GitHub release was created. The original user's restriction on pushing without permission still requires an explicit publication decision.
- Next: review/commit the intended current source, authorize push and v0.1.0 draft generation, watch CI on Windows/Linux, run native smoke checks and review signing limits before publication.

## Session — 11 October 2026

Continue the existing Tauri/Svelte migration and .NET core. Preserve the extensive
pre-existing working-tree changes. Do not commit, push, deploy, or change real user
data. Browser RPC fixtures and isolated temporary profiles are used for checks.

## Latest verified state — 12 October 2026

- Completed **19 full development iterations**, with broad reviews after 3, 6,
  9, 12, 15 and 18, plus native macOS verification and fixes derived from it.
- Current code: **343 .NET tests**, **108 browser tests** (33 added cases) pass;
  Release solution/client/native builds have zero source diagnostics. Native
  sidecar persistence/validation and transport checks pass.
- Latest macOS `.app` is in
  `src/TanaHub.Client/src-tauri/target/release/bundle/macos/TanaHub.app`.
  Local ad-hoc signature only; not notarized, installed, published or deployed.
- Verified the actual WebView: live title metadata/covers/characters/relations,
  date editing/save/restart persistence, explicit empty-date hints, uppercase
  stored-ID tracking/rating saves, rating dismissal and Back focus. Test app was
  closed; the real user library/account settings were not used.
- Preserved all pre-existing working-tree changes. No commits, branch changes,
  remote writes, force push, database schema changes or dependency changes.
- Confirmed actionable defects in the reviewed backlog are resolved. Continue
  with the prioritized external/product tasks below once their prerequisites exist.

### Next priorities and prerequisites

1. **P1: Windows/Linux native builds and WebViews.** Requires those hosts; the
   current host is macOS. Keep Avalonia until native parity is established.
2. **P1: Live AniList OAuth/import and recognition.** Requires user authorization
   and their chosen account/images; do not upload fixtures or log in autonomously.
3. **P2: Native import/export file dialogs on each platform.** Browser downloads,
   bridge XML exchange and SQLite persistence are tested; OS dialogs need their
   own selected destinations and smoke verification.
4. **P2: Sync conflict ownership and further Library workflows.** Establish which
   local tracking fields remote imports may replace before changing the existing
   sync contract; do not add unrelated bulk operations merely to extend this run.
5. **Release prerequisite:** signing/notarization and deployment need explicit
   user instructions. No production work is included in this autonomous run.

### Reproduce checks and resume

Use `dotnet test TanaHub.sln --configuration Release --no-restore`,
`dotnet build TanaHub.sln --configuration Release --no-restore`, and in the client
`npm run build`, `npm test`, `npm run test:transport`, `npm run test:core`.
Browser tests require a localhost listener/Chromium; sandboxed Vite was denied,
so approved local execution was used. There is no dedicated lint script.

The isolated self-contained sidecar is `/tmp/tanahub-sidecar-check/TanaHub.Bridge`;
select it with `TANAHUB_TEST_BRIDGE_BINARY` for `test:core`. Native build used
`npm run tauri -- build --ci --bundles app --config /tmp/tanahub-native-config.json -- --offline`.
The temporary config embeds the verified sidecar and uses ad-hoc signing;
Apple notarization credentials were removed from this build's environment.
Native UI profile is `/tmp/tanahub-native/profile`, with notifications disabled.
Do not launch testing against the default data directory. Temporary files may
expire; reconstruct from the source/commands rather than treating them as assets.
Session comparison snapshots are `/tmp/tanahub-autonomous-baseline`.

### Main changed areas

- Client App: library-read readiness, failed-query/pagination recovery, filter
  reset, stable case comparison, originating-page context and cross-platform focus.
- `TitleDates`: complete date editor, validation, drafts, retry, empty states.
- Journal: real review/organization limits and lossless Unicode name handling.
- Home/Schedule: request identity, paused inactive loaders, retained filters,
  meaningful error/loading states and progress focus.
- Domain/storage/bridge: progress overflow and range consistency, wide insight
  totals, preserved explicit dates and safe automatic date inference.
- Tests and migration/protocol/README documentation updated. Read the detailed
  iteration records below before revisiting a resolved case.

### Baseline

- Architecture reviewed: Svelte 5 client → Tauri/stdin JSON bridge → existing
  Application/Infrastructure services; Avalonia remains available.
- `npm run build`: passed, zero Svelte errors/warnings.
- `dotnet test TanaHub.sln --configuration Release --no-restore`: passed,
  325 tests (100 Application, 179 Infrastructure, 46 Bridge).
- Browser plugin unavailable; use existing Playwright workflow. Sandboxed Vite
  could not listen on localhost; escalated local browser test execution allowed.
- Existing client source snapshots for modified files are in
  `/tmp/tanahub-autonomous-baseline` for comparing this session's changes.

### Priorities and acceptance criteria

1. Block tracking writes when library status cannot be loaded; retry restores
   controls without affecting catalog-only failures.
2. Align review input with the real 4,000-character storage limit and retain
   drafts on validation/save failures.
3. Complete tracking date editing using the existing bridge, with chronology
   validation, clear, retry, and draft preservation.
4. Recover from empty filtered collections directly and avoid showing old query
   results as if they belong to a failed new query.
5. Preserve collection pagination on transient load-more failure.

Review the broader project after iterations 3 and 6. Reassess priorities after
each completed iteration and continue while useful tasks and execution resources
remain. Live OAuth, uploads, and Windows/Linux native packages require separate
platform/user validation. No dedicated lint script exists; Svelte check/build and
browser runtime checks provide client validation.

### Completed iteration 1 — Library-read failure protection

Analysis: title detail errors currently re-enable tracking and add controls even
when `library.get` failed. Implement an independent library-read readiness flag;
confirmed with browser tests for known and unknown entries, then retry.

- Added independent library-read readiness; tracking, review and add controls
  remain disabled on a failed read. Catalog-only failure still permits tracking.
- Both reproduction tests failed before the fix and passed afterward.
- Checks: build passed with zero diagnostics; 51 affected browser tests passed;
  baseline browser suite also passed (75/75); `git diff --check` passed.
- Reassessment: the review editor accepts 20,000 characters but persistence
  permits 4,000. Address that next; date editing remains third priority.

### Completed iteration 2 — Review validation

Match the stored review limit, show the current length, and retain oversized
existing content for editing while blocking an invalid save.

- Review editor now has a 4,000-character typing limit, visible count and local
  validation for oversized content already present in an entry. No stored text
  is truncated. Save and Enter cannot submit an oversized review.
- Corrected the read-only tracking indicator so a failed read does not display
  an indefinite save spinner or a saved checkmark.
- Both new review tests failed before implementation. Checks afterward: build
  passed with zero diagnostics; 8 relevant browser tests passed; diff check passed.
- Reassessment: existing `library.dates` and frontend API are implemented but
  no editable dates UI exists. Complete that workflow without schema changes.

### Completed iteration 3 — Tracking date editor

Provide collapsible start/completion date editing, local-calendar semantics,
chronology validation, explicit Save/Clear/Discard, retained drafts and retry.
Keep untouched timestamps exactly as received and preserve unrelated tracking.

- Added focused `TitleDates` component connected to the existing API. Editors
  collapse without losing drafts; Clear changes a draft until explicit Save.
  Unchanged timestamps retain their original value, and same-day completion is
  valid even when the start timestamp includes a later time.
- Missing-editor tests failed first. Checks: 40 relevant browser tests passed;
  after fixing a Svelte reactivity warning, build has zero diagnostics and all
  4 date-specific tests pass, including 360px/1440px layout and keyboard focus.
- Visually inspected `/tmp/tanahub-dates-360.png` and
  `/tmp/tanahub-dates-1440.png`; controls fit and match existing journal styling.

### Broader review after iteration 3

Reviewed collection state, Home requests, bridge validation, model boundaries,
CSV export, persistence and unfinished client paths. CSV already protects formula
prefixes and the existing bridge transport is bounded. No architecture replacement
is warranted. Found a higher-priority issue: direct library-service progress
increments wrap at `int.MaxValue`; the bridge guards its own calls but Avalonia
and service callers can still persist a negative value. Also retain these next
tasks: empty filter recovery, failed-query stale results, pagination retry, client
progress/organization limits, and stale Home data after type switches.

### Completed iteration 4 — Progress overflow protection

Reject overflowing increments in the domain and return a validation Result from
both library services. Verify boundary success, overflow failure, invalid amount,
and that failed mutations leave all saved fields unchanged.

- Domain rejects arithmetic overflow before producing an updated record.
  SQLite and in-memory services return validation failures and keep the original
  progress, score, notes and modification timestamp. Invalid in-memory amounts
  now follow the same Result contract as SQLite.
- Seven reproduction cases failed before the fix. All 333 .NET tests pass
  afterward (101 Application, 186 Infrastructure, 46 Bridge); Release solution
  build passes with zero warnings/errors; diff check passes.
- Reassessment: no schema or existing user data repair is needed for prevention.
  Empty-filter recovery remains the next useful UI improvement.

### Completed iteration 5 — Empty filtered collection recovery

Explain that active filters caused the empty result and provide one visible reset
action for search/status/organization or catalog filters. Keep selected library
media type and layout preferences; reset Discover presets to their default.

- Empty screens now distinguish active filters and offer Reset filters even when
  organization controls are collapsed. Library type/layout are retained; catalog
  genre and preset constraints can be cleared directly.
- Both tests failed first; 14 relevant browser tests pass after implementation.
  Fixed a test fixture callback type error found by check; final build has zero
  errors/warnings and diff check passes.
- Minimum five cycles reached; continue to concrete outstanding issues.

### Completed iteration 6 — Collection failure recovery

Store the failed pagination request so retry loads the same page while retaining
previous pages. Clear results when a different query begins so its error cannot
expose cards from the previous filter as current results. Verify library and
catalog failures, counts, page sequence, and existing queued-search behavior.

- Failed Load more requests retain earlier pages and retry the exact page/query.
  First-page query changes clear their displayed results. Error text distinguishes
  pagination; repeated loading actions are guarded.
- All three reproduction tests failed first (107 expected vs 48 on retry;
  old Anime/library/catalog cards remained under failed new queries).
- Checks: full browser suite 88/88 passed; build zero diagnostics; bridge transport
  tests passed, including serialized UTF-8, timeout, EOF and cleanup; diff passed.

### Broader review after iteration 6

Reviewed request identity and error behavior across all six pages. Collection and
Schedule generation guards are sound after these fixes. Home still retains prior
type metrics/cards when the next type fails or is loading; this is the next issue.
Also confirmed client organization fields lack the bridge's 50-name/100-character
validation, and absolute progress has inconsistent integer limits. Prioritize
these bounded fixes, then review the accumulated changes and native sidecar.

### Completed iteration 7 — Home query identity

Clear summary/recommendation/airing state when media type changes, retain useful
same-type data during refresh, and avoid claiming empty lists when summary loading
failed. Verify failure/retry and delayed recommendation responses.

- Type changes now reset dashboard data before loading the new type. Refreshes
  of the same type retain useful existing data. Loading/errors no longer pretend
  that in-progress titles or recent activity are empty.
- Both reproduction tests failed first; 7 Home browser tests pass afterward,
  including delayed requests and responsive checks. Build has zero diagnostics;
  tracked and snapshot-based client whitespace checks pass.
- Session continued into 12 October. Next: organization validation, then progress
  boundary consistency and accumulated change review.

### Completed iteration 8 — Organization editor validation

Match bridge limits (50 tags/lists, 100 UTF-16 characters per name), prevent
invalid button/Enter submissions and preserve entered text. Normalize duplicate
case variants consistently for ordinary names without changing stored data.

- Added local length/count validation and accessible field errors; Enter and
  Save cannot send invalid organization. Drafts remain intact. Ordinary case
  duplicates preserve the first spelling. The raw text capacity now accommodates
  all 50 valid names of 100 characters with separators.
- Reproduction failed before the fix; 6 organization/journal browser tests pass
  afterward. Build has zero diagnostics; diff check passes.

### Completed iteration 9 — Consistent progress boundaries

Extend the existing absolute setter to the domain's full nonnegative Int32 range
(additive contract change, no schema change); retain the 1,000,000 increment-size
limit. Use the same upper bound in client validation and disable +1 at the bound
in collections, Home and details. Document and test large saved/imported counters.

- Absolute setter now accepts the persisted Int32 range; increment-size remains
  bounded. Client +1 controls stop at the maximum and detail keyboard focus moves
  to progress when the just-used increment button becomes disabled.
- Found and reproduced related statistics overflow. Episode/chapter totals and
  watch minutes now use 64-bit accumulation; Avalonia formatting accepts those
  totals. JSON field names and numeric types are unchanged; no storage migration.
- Checks: 339 .NET tests pass; solution/client builds have zero diagnostics;
  13 affected browser tests pass (corrected a test's outdated button label);
  tracked/new-client whitespace checks pass.
- Extended the real sidecar test with maximum progress, overflow refusal, large
  statistics, date persistence and review limits. `prepare:bridge` stalled on
  network restore; stopped only this session's own two processes. Existing RID
  assets are present; offline self-contained publication to
  `/tmp/tanahub-sidecar-check` is running. Test binary override allows verifying
  it without replacing a user's existing bundled executable.

### Broader review after iteration 9

Apply code-review-and-quality across this session's snapshot diffs, including
untracked client files. No dependencies, storage migration, remote operations,
or production changes were introduced. Two narrow correctness issues to address:
date drafts should survive status-triggered updates to persisted dates, and a
direct title route should use the known library metadata when catalog refresh
fails. Native/Windows/Linux/live OAuth checks remain external validation limits.

### Completed iteration 10 — Date draft preservation on status updates

An automatic saved date from a status change must update an untouched date field,
while a user-edited date draft remains intact. Discard must restore the latest
persisted value, not an older baseline.

- Saved dates refresh only untouched date drafts. Edited values survive automatic
  status changes; explicit discard reads the latest saved entry.
- Reproduction showed the draft overwritten (5 October → 11 October) before the
  fix. All 5 date browser tests now pass; build has zero diagnostics.
- Native self-contained publication succeeded after running outside the sandbox
  with `--no-restore --disable-build-servers`. Output is confined to
  `/tmp/tanahub-sidecar-check`; real sidecar regression test is next.

### Completed iteration 11 — Library fallback for direct title routes

When catalog detail loading fails but a library entry is known, render its saved
metadata, correct media type and tracking units. Keep the catalog failure notice
and permit tracking because the library read succeeded; retry should restore
catalog details without losing the entry.

- A failed catalog refresh now falls back to the entry's saved metadata, so direct
  Manga routes show the title, Manga type, chapter controls and saved cover.
  Library-confirmed tracking remains enabled; catalog retry retains edits.
- Reproduction failed first; final full browser suite 94/94 passes; client build
  has zero diagnostics. Native self-contained sidecar regression test passed:
  actual runtime/SQLite, Unicode tracking, restart persistence, maximum progress,
  overflow refusal, wide Home metrics, dates and review validation, all isolated.
- Reassessment found that both services currently overwrite an existing completion
  date on each Completed transition. Automatic start/completion can also violate
  chronology when the other saved date conflicts with now. Fix those next.

### Completed iteration 12 — Preserve explicit tracking dates in status changes

Keep existing start/completion dates when changing status. Only infer an absent
date when the inferred current time agrees with the other saved date; keep null
when no correct date can be inferred. Status/progress remain independently editable.

- Both storage implementations preserve existing completion dates. Automatic
  start/end timestamps are added only when chronology stays valid; existing dates,
  progress and scores are retained. No user records are rewritten by this session.
- Four reproduction tests failed first. All 343 .NET tests now pass (102
  Application, 190 Infrastructure, 51 Bridge); Release build zero warnings/errors;
  diff check passes. Real-sidecar test now also asserts that Completed retains a
  manually chosen completion date; re-publish before the final native check.

### Broader review after iteration 12

Reviewed this session's service/model/UI diffs for data preservation, integer
limits, error identity, asynchronous races, accessible controls, and scope.
The existing architecture and migrations remain intact; all newly found data
integrity cases are covered. One final concrete UX check: Home progress actions
disable the focused control while saving, and the last episode leaves it disabled.
Verify focus recovery and ensure delayed saves never steal focus after navigation.
Remaining platform/account validation needs native Windows/Linux hosts and the
user's live integration actions; do not simulate or claim those checks.

### Completed iteration 13 — Home progress keyboard focus

Keep the progress trigger focused after a save/error when still usable, and move
focus to its title opener when reaching the total. Respect deliberate navigation
while a request is pending. Validate in the real browser, then reassess.

- Confirmed focus loss after the last episode with a failing keyboard test.
  Save/error now restores the usable action; reaching a known total or integer
  limit restores the title opener. Disconnected cards and deliberate navigation
  do not receive focus from a late response.
- Final browser suite 97/97 passes (22 new regression cases); client build has
  zero diagnostics. Updated self-contained sidecar and transport checks pass,
  including preserving explicit completion dates on status changes.
- Reassessment: ordinary confirmed defects in the reviewed backlog are addressed.
  Next available verification is the actual macOS Tauri package/WebView with an
  isolated data directory; Windows/Linux and live integrations remain external.

### Completed verification — macOS native client

Inspect local Tauri CLI/package outputs; build with the already verified sidecar
and an isolated output/profile. Do not replace an installed application or touch
the user's library. Inspect the native UI if computer-use access is available.

- Offline Rust/Tauri release build and 47.77 MiB `.app` bundling succeeded with
  local ad-hoc signing. No installer, notarization or installed-app replacement.
  Temporary config uses the verified sidecar from `/tmp/tanahub-native`.
- Launched the exact build with `/tmp/tanahub-native/profile`; one synthetic
  Frieren entry, notifications disabled. Native AX/screenshot showed successful
  native IPC, covers, live AniList metadata/characters/relations and tracking.
- Edited start date through WebKit's keyboard date control, saved via native IPC,
  observed Dates saved, disabled Save, and restored focus. Quit only this test app;
  process exited cleanly, and no TanaHub/sidecar process remained.
- SQLite read after quit confirms progress 3, score 9 and the edited start date;
  completion is null. First CUA initialization was slow; subsequent actions worked.
- Found a native UX discrepancy: empty WebKit date fields show today's date as a
  visual placeholder even though the bound value and saved date are absent.
  Add an explicit empty-state hint rather than replacing native date controls.

### Completed iteration 14 — Explicit empty dates in native WebView

Display and describe No start/completion date recorded below an empty input.
Remove the hint when a draft date is selected. Keep labels, native keyboard
controls, saved null semantics and the existing compact style; verify browser and
the updated native package.

- Empty date inputs now show explicit start/completion absence, described by ARIA
  without changing their labels. Hints disappear only after a full draft date is
  selected; saving start alone still keeps completion null.
- Reproduction failed first; all 98 browser tests pass; native Release build and
  ad-hoc `.app` packaging pass with zero Svelte diagnostics. Updated WebKit AX and
  screenshot explicitly show No completion date recorded. Native restart displays
  the previously saved start date, confirming persistence; test app quit cleanly.
- Reassessment found one cross-language boundary case introduced by frontend
  organization normalization: full Unicode lowercasing merges `Straße`/`STRAẞE`,
  while a real isolated core RPC probe preserves both. Protect that next.

### Completed iteration 15 — Lossless Unicode organization editing

Restrict frontend case coalescing to ASCII, where it agrees with ordinal core
comparison; leave Unicode spelling comparison to the existing backend contract.
Ensure editing a custom list retains distinct existing Unicode tag names.

- Frontend coalesces ASCII case variants only; Unicode spellings survive to the
  existing ordinal backend comparer. First spelling and order are retained.
- Real isolated RPC preserved both `Straße` and `STRAẞE`; browser reproduction
  lost the latter before the fix. All 99 browser tests pass; client/native build
  zero Svelte diagnostics; updated macOS `.app` successfully bundled.

### Broader review after iteration 15

Rechecked identifier and text boundaries. MAL metadata lookup intentionally
preserves its original ID, so no alias rewrite is needed. Found a separate valid
contract mismatch: core lookups are case-insensitive and preserve stored ID case,
while client entry/busy caches use exact strings and catalog IDs are canonical.
Legacy uppercase IDs can appear untracked and removal can leave a stale card.
Normalize ASCII comparison keys in the client while keeping actual IDs intact.
Also verify Back focus when refreshed provider metadata changes a title caption.

### Completed iteration 16 — Consistent client media identity

Use safe ASCII-normalized comparison keys for caches, busy state, result
deduplication and membership checks. Preserve original identifiers in entries,
API requests and persisted records. Verify rating/detail/removal for case variants.

- Entry and busy caches, pagination deduplication and list membership use safe
  comparison keys. Raw IDs are retained; no stored identifiers are rewritten.
- Reproduction showed a tracked uppercase-ID title without its rating control.
  Rating, details and confirmed removal now work with the canonical catalog ID;
  counts and stale-card cleanup are correct. Full browser suite 100/100 passed;
  client build zero diagnostics; diff check passed.
- Reassessment: conditional title rendering also unmounts the originating page.
  Back can lose Home/Schedule filters and recognition results; refreshed captions
  break focus restoration because the original element was destroyed. Preserve
  the inactive originating page while the title is displayed, then resume loading.

### Completed iteration 17 — Preserve the originating page context

Keep the current page mounted but hidden/inert behind title details. Pause Home
and Schedule loaders while inactive; preserve filters, results, image previews
and connected focus targets. Genuine navigation to another section still replaces
the old page. Test metadata-caption changes and each affected return workflow.

- Originating page remains mounted but hidden/inert; Home/Schedule pause requests
  and resume without resetting selected filters. Recognition retains its preview
  and matches, so returning never uploads the same image again automatically.
- Metadata-renamed opener, Home filter/focus and recognition tests failed first.
  Fixed a Schedule test's button name to include its episode; its round trip also
  passes. Full browser suite 104/104 passes; native build/bundle zero diagnostics.
- Reassessment: after confirmed removal, a retained Home card can briefly be
  stale. Clear its return-focus intent and use the collection search or heading
  so later refresh cannot remove the focused element.

### Completed iteration 18 — Focus after title removal

When the originally opened title is removed, discard its old focus references.
Return to collection search or page heading; retain focus on the original title
when only a different related title was removed. Verify Library and Home paths.

- Removed-title focus references are cleared; search/heading remains focused even
  when Home data refreshes. Related-title removal retains the original opener.
  Svelte element keys use the same logical ID comparison so casing alone cannot
  destroy a connected opener or detail component.
- Both focus tests failed first. Full run passed 105/106, with one incorrect test
  expecting the removed title to disappear from recommendations as well. Scoped
  that assertion to tracking; both targeted cases pass. Build/native bundle have
  zero diagnostics; all other cases passed on unchanged application code.

### Broader review after iteration 18

Reviewed code/test changes against this session's snapshots, including untracked
client files. All confirmed currently actionable defects in this backlog are
handled. No dependency, database schema, provider-account or production change
is required. Preserve the existing AniList import policy (remote tracking fields,
local review/organization); changing sync conflict ownership needs product rules.

### Final verification checkpoint

- Run the final complete browser suite after correcting the test assertion.
- Verify latest native package with an uppercase stored ID in the isolated
  profile; confirm rating controls, details and Back context/focus.
- Update migration/protocol documentation and the latest-state summary.
- Remaining external work: Windows/Linux native packaging and WebViews; live
  OAuth and recognition upload must be performed by the user. No release,
  notarization, deployment, commit or push is authorized in this run.

### Native checkpoint after iteration 18

- Full browser suite passed 106/106 after the assertion correction.
- Updated native app recognized the uppercase stored ID and saved a keyboard
  rating draft (9 → 8) through IPC; SQLite retained the uppercase ID, progress and
  date. Native Back/rating dismissal exposed a WebKit focus difference: button
  activation did not establish DOM focus, so the restored target was the page.
- This is a confirmed native-only discrepancy; fix before finishing validation.

### Completed iteration 19 — Native activation focus

Establish focus on an enabled clicked button before its handler opens a modal or
title. Apply one scoped document listener with cleanup, preserving normal keyboard
and pointer behavior. Reproduce WebKit's no-pointer-focus behavior in browser
tests and verify actual native rating dismissal and Back afterward.

- Enabled clicked buttons establish focus before their handlers; one document
  listener is registered/cleaned up with the App. This handles WebKit/native
  accessibility activation without changing button or keyboard contracts.
- Both platform-behavior reproduction tests failed before the fix. Full browser
  suite 108/108 passes; native Release build/bundle zero Svelte diagnostics.
- Actual macOS AX verification now reports the Rate trigger focused after Cancel
  and the original Open title trigger focused after Back. The test app quit with
  exit code 0 and no native runtime errors.
- Final reassessment: no further confirmed code defect remains in this reviewed
  queue. Remaining valid work needs platform/account access or explicit product
  policy; preserve the completed source and verification evidence for continuation.
