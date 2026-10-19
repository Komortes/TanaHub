# TanaHub UI refinement — 4 October 2026

Latest iteration: the follow-up below replaces the initial inline rating with a
shared modal and removes the redundant recognition-enable setting.

## Aim

Keep the existing dark indigo palette, compact covers, cyan progress and amber
scores. Make everyday tracking easier and prevent changing title lengths or
missing metadata from moving actions between neighbouring cards.

## References and observations

- The attached One-Punch Man screenshot shows a wide rating trigger and five
  equally prominent progress controls. Personal score and progress should be
  editable directly, with secondary operations disclosed separately.
- [Letterboxd title page](https://letterboxd.com/film/spirited-away/) separates
  personal rating/diary actions from film metadata. Borrow that separation,
  retaining TanaHub's existing integer 0–10 scale.
- AniList's live title page was inaccessible to the research tool (403), so do
  not claim a pixel-level comparison with its authenticated list editor.
- Existing grid cards reserve title space but allow metadata to wrap into a
  different number of rows. Missing totals also remove the progress track,
  causing action baselines to drift.

## Proposed implementation

1. **Stable collection geometry.** Reserve two lines for titles and a consistent
   metadata area. Keep score/progress/action baselines aligned for short/long
   names, missing years, unknown totals, manga and all tracking statuses. Full
   titles remain available through accessible names, tooltips and the detail
   page. Check Library, Discover, Home and related/scheduled rows.
2. **Compact tracking panel.** Place status in the heading, use a compact
   decrement / editable count / increment stepper with a clear total. Direct
   numeric edits apply on Enter or a small explicit Apply button, with Escape
   restoring the saved count. Move record-all/reset operations into an optional
   secondary area. Show remaining episodes/chapters, and remaining viewing time
   only when a known episode length makes it reliable. Status and progress remain
   independent: a pre-existing Completed/0 entry is never silently rewritten.
3. **Inline personal rating.** Select any integer 0–10 directly on the title page,
   with a separate Clear action, textual score meaning and visible saving/error
   states. Preserve the difference between zero and unrated. Retain the existing
   rating dialog for quick rating from collection cards. Support keyboard and
   touch access without introducing another UI dependency.
4. **Small title-page conveniences.** Open the cover in a bounded lightbox with
   Escape, outside-click close and focus restoration; no action for a failed or
   absent cover. Copy the title with success/failure feedback. Offer an external
   source link only when a recognised provider ID can form a trusted URL. Fold
   long synopses behind Show more/less, preserving the full text for expansion.
5. **Behaviour and visual polish.** Use short opacity/transform feedback, respect
   reduced motion, prevent actions from overlapping while saving, and retain
   the last saved data after failures. No automatic completion/status change or
   production data migration.

## Review before implementation

A second agent must challenge this plan for usability, excessive scope, error
handling and accessibility. Incorporate its findings before development.

### Accepted critique (before development)

The independent `ui_critic` agent found the plan sound and supplied the following
refinements, all accepted:

| Before | After | Why |
| --- | --- | --- |
| Score buttons might be squeezed into one mobile row | Wrap equal buttons with at least 36px targets; radio semantics and arrow navigation | Readable touch and keyboard use |
| Static Saved locally message | Saved, Saving, Unsaved progress and Could not save states | Accurate persistence feedback |
| Stepper can overwrite a numeric draft | Disable +/- while a dirty draft awaits Apply or Escape | Never silently discard input |
| Reset is one click | Secondary actions plus explicit reset confirmation | Prevent accidental progress loss |
| Variable metadata and absent progress bars | Exactly two title/metadata lines and reserved track space | Stable action baselines |
| New lightbox behaviour | Reuse modal.ts and reset transient state between titles | Focus, background lock and reliable navigation |
| Unbounded remaining-time estimate | Known positive totals/duration only, clamp remaining to zero | Preserve over-total history |
| Layout/hover animations everywhere | Progress scaleX, hover movement only for fine pointers, reduced motion | Keep frequent actions immediate |

## Validation cycles

- Initial implementation: type checking, existing UI regression suite, targeted
  tests for new interactions and fixtures with adversarial title/metadata lengths.
- Cycle 1: independent code/behaviour review, keyboard/focus/retry/zero/null
  checks, desktop and narrow-window inspection; fix identified issues.
- Cycle 2: fresh visual review of rendered Library, Home and title pages at
  320/360/768/1024/1440 px, repeat changed flows, rebuild the native macOS bundle
  and validate its signature/startup in an isolated profile.

Record actual results and remaining limitations here. Do not claim live OAuth,
image recognition uploads or Windows/Linux verification from browser fixtures.

## Implemented and reviewed

The independent `ui_critic` agent reviewed the plan before development and
reviewed the implementation twice. `card_polish` handled collection geometry;
`polish_qa` independently exercised the interactions and responsive layouts.

- Library/Discover cards reserve title, metadata, action and progress areas,
  including mixed tracked/untracked entries. Home, Schedule and related-title
  rows also reserve title space. Compact cover sizing is preserved.
- The title page has direct 0–10 rating, separate Clear, a progress stepper,
  explicit numeric confirmation, remaining counts/time and secondary progress
  actions. Dirty drafts survive unrelated changes and failed saves. Reset
  requires confirmation; score, status and progress remain independent.
- Covers open in an accessible lightbox. Copy title, recognised-provider links
  and expandable long synopses are available. Failed covers are noninteractive.
- Native title copying uses the Tauri clipboard plugin with write-text permission
  only; browser copying uses the browser clipboard API with visible feedback.

### Corrections from the two review cycles

| Before | After | Why |
| --- | --- | --- |
| Apply/Reset removed the focused control | Return focus to the progress input when the completed action removes its control | Keep keyboard navigation usable after saving |
| Details preceded personal tracking in a narrow window | Tracking follows the title header; metadata follows the synopsis | Keep frequent actions near the top |
| Cancel edit and Keep progress removed their focused buttons | Restore focus to the stable progress input | Make cancelling predictable |
| Missing thumbnail repeated a truncated title | Use a quiet image fallback with a complete accessible label | Reduce noise without losing the title |

### Actual validation

- Complete browser regression suite: **68/68 passed**, including 16 new polish
  cases. After adding mixed-catalog geometry assertions, all 16 polish cases
  passed again. The final Keep progress focus correction passed its targeted
  regression test.
- Library, Home, Title and Schedule inspected at **320, 360, 768, 1024 and
  1440px**: no horizontal overflow, browser exceptions or development overlays.
- Checked keyboard rating navigation, zero versus unrated, draft confirmation
  and cancellation, delayed/failed saves and retries, reset confirmation,
  lightbox focus trapping/restoration, absent/failed posters, copy feedback,
  long synopsis expansion and reduced motion. Clipboard tests use a mock rather
  than modifying the user's clipboard.
- Live One-Punch Man detail page rendered at 1440px and 360px against the real
  core and an isolated library: no browser errors or horizontal overflow.
- Final frontend/native build: **zero Svelte errors or warnings**, macOS bundle
  built successfully (47.77 MiB), strict/deep signature verification passed.
  Native application and its bundled child process remained running after launch
  in an isolated profile. The packaged core separately passed three local RPC
  requests (library, settings, lookup), SQLite/runtime loading and clean EOF exit.

[Desktop result](design/tauri-title-polish-2026-10-04.png) ·
[Narrow-window result](design/tauri-title-polish-mobile-2026-10-04.png)

Native clipboard interaction was compiled and packaged but was not exercised
through a macOS UI automation session. This UI pass does not add verification of
live OAuth, recognition uploads or Windows/Linux builds.

## Follow-up: modal rating and compact everyday controls

The follow-up implements the user's preference for a modal score editor and
always-available recognition. An independent `interface_review` agent evaluated
the direction before implementation and reviewed the rendered result twice.

| Before | After | Why |
| --- | --- | --- |
| Inline rating grid occupied the tracking panel | Compact score summary opens the same centered modal from title, library and Home | Keep tracking focused and rating consistent |
| Anchor-positioned rating popover with slider and presets | One slider with +/- precision controls, value/meaning preview, explicit Save/Cancel/Clear | Deliberate edits with fewer competing controls |
| Short windows hid actions or the slider | Persistent footer plus compact layout below 520px height | Main controls remain visible without scrolling |
| Boundary score/Clear actions disabled the focused button | Focus returns to the slider | Preserve keyboard navigation |
| Recognition menu opened a feature blocked by a Settings toggle | Explicit provider search action authorizes each upload | Remove the redundant preference and its dead end |
| One narrow-grid card left half a row empty | Two compact columns at small widths, wider-window sizing preserved | Use available space without oversized covers |
| Empty tag/list filters and cover slider cluttered the toolbar | Secondary options open on demand with an active-filter count | Earlier content and predictable controls |
| Empty journal forms remained expanded | Add/Edit controls, saved content previews and retained drafts | Shorter title pages with clear optional actions |
| Settings feedback was far from the changed control | Feedback in the relevant panel, intrinsic panel heights | Make save results easy to find |

### Behaviour preserved

- Rating is a local draft until Save, including the distinction between score 0
  and no score. Cancel/Escape/backdrop dismiss only when idle; a failed request
  retains the draft for retry. Concurrent submissions are blocked and focus
  returns to the exact opener after closing.
- Closing a journal editor preserves its draft; explicit Discard restores saved
  content. Unrelated status/rating saves do not replace unsaved text or progress.
- Image selection, paste and navigation do not upload. Find matches sends
  `uploadConfirmed: true` with the selected provider. The bridge requires it,
  regardless of the legacy recognition-enable value; the legacy stored setting
  remains unchanged for Avalonia compatibility.

### Follow-up validation

- **75/75 browser tests passed**, including the adapted existing workflows and
  seven additional compact-layout/rating checks. The final short-window CSS and
  focus paths were checked again with two targeted passing tests.
- **46/46 Bridge tests passed**, including legacy-enabled/disabled recognition,
  missing confirmation, invalid images and process boundaries.
- Independent interaction and visual review covered 1440×1000, 320×800,
  320×360 and 768×360; Settings/Recognize also covered 360/768/1024px widths.
  No horizontal overflow or runtime errors. Final review reported no remaining
  issues requiring changes.
- Live One-Punch Man title and rating modal rendered with the real local core
  in an isolated library, with no browser errors. No real images were submitted
  to recognition providers during this verification.
- Final macOS build: zero Svelte errors/warnings, successful native bundle and
  strict/deep signature verification. Packaged core passed five local RPC checks,
  including recognition request validation, and exited cleanly on EOF. The
  updated application was launched with a separate test profile.

[Title page](design/tauri-title-modal-2026-10-04.png) ·
[Rating modal](design/tauri-rating-modal-2026-10-04.png) ·
[Narrow rating modal](design/tauri-rating-modal-mobile-2026-10-04.png)
