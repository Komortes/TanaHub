# TanaHub v1.0 Release Readiness Plan

**Goal:** Make the existing desktop tracker reliable and easy to evaluate before creating release artifacts and moving the project to another machine for real-world use.

**Scope rule:** Stabilize and polish existing user flows first. Manga recognition through additional providers and full sync conflict resolution remain post-v1.0 work. Recommendations and automatic update checks are now implemented.

## Phase 1: Core Flow Stability

### Task 1: Make Detail Library State Independent of UI Filters

**Description:** Ensure the title detail page always reads its library entry from the library service, never from a filtered UI collection. This prevents add/progress/status/score actions from making a title appear removed.

**Acceptance criteria:**
- [x] Detail state is resolved from the persisted library, regardless of the active Library filters.
- [x] Add, increment, status, score, notes, and remove refresh the detail state correctly.
- [x] Regression tests cover direct retrieval and persistence of a library entry.

**Verification:**
- [x] `dotnet test TanaHub.sln`
- [ ] Manual flow: add a title, use every action on its detail page, then return to Library.

### Task 2: Harden Recognition Feedback and History

**Description:** Make recognition outcomes unambiguous: clear empty/error states, best result before variants, source image preview, and a history section that expands predictably.

**Acceptance criteria:**
- [x] No-match, unsupported clipboard, service error, and successful match states are distinct.
- [x] Each recognition attempt creates at most one history entry.
- [x] History opens and closes predictably; empty history appears only when expanded.

**Verification:**
- [x] `dotnet test TanaHub.sln`
- [ ] Manual flow: file, clipboard, no-match, best-match, history expand/collapse.

### Task 3: Complete Local Organization Controls

**Description:** Turn existing tags/custom list storage and filters into user-facing management controls so a user can create, assign, remove, and filter local organization metadata.

**Acceptance criteria:**
- [x] Users can assign/remove tags and custom lists from a title detail page.
- [x] Users can create a new tag/list inline without leaving the flow.
- [x] Library filters refresh after an assignment changes.

**Verification:**
- [x] `dotnet test TanaHub.sln`
- [ ] Manual flow: create tag/list, assign it, filter by it, restart app, export CSV.

### Checkpoint: Core Flows
- [ ] Build succeeds without warnings.
- [ ] Tests pass.
- [ ] Manual smoke checklist passes on a clean local data profile.

## Phase 2: Quality and Polish

### Task 4: Add a Release Smoke Checklist and Recovery Guidance

**Description:** Document the core flows and failure recovery expected before every release. Include cache/settings/library file locations and how to reset demo state without deleting source code.

**Acceptance criteria:**
- [x] A concise manual smoke checklist exists in `docs/`.
- [x] README points to the checklist and troubleshooting notes.
- [x] The checklist covers Discover, Library, Detail, Schedule, Recognition, Sync, and Export.

### Task 5: Enforce a Clean Build Baseline

**Description:** Promote compiler warnings to errors and eliminate any remaining warnings so a release build cannot silently degrade.

**Acceptance criteria:**
- [x] `TreatWarningsAsErrors` is enabled for CI/release builds.
- [x] Solution builds without warnings.
- [x] Test projects remain compatible with the stricter setting.

### Task 6: Accessibility and Keyboard Polish

**Description:** Verify keyboard paths, focus visibility, labels/tooltips, and contrast on the main workflows. Fix concrete gaps found during the audit.

**Acceptance criteria:**
- [x] Primary actions are keyboard reachable.
- [x] Icon-only buttons have tooltips.
- [x] Dynamic states have readable text, not color-only meaning.

**Verification:**
- [x] `dotnet test TanaHub.sln`
- [ ] Manual flow: navigate sidebar, search, detail actions, settings, and history controls with keyboard only.

### Checkpoint: Product Ready
- [ ] Smoke checklist passes on a fresh profile.
- [ ] No P1/P2 issues remain in the tracked release list.
- [ ] README accurately describes current features and limitations.

## Phase 3: Release Artifacts

### Task 7: Cross-Platform CI

**Description:** Add GitHub Actions build/test coverage for macOS, Windows, and Linux.

**Acceptance criteria:**
- [ ] CI restores, builds, and tests the solution on all three OSes.
- [ ] Warnings fail CI.
- [ ] README displays the workflow status badge.

### Task 8: Publishable Desktop Packages

**Description:** Produce versioned self-contained publish artifacts for macOS arm64, Windows x64, and Linux x64.

**Acceptance criteria:**
- [ ] One documented command creates release artifacts per runtime.
- [ ] Artifacts include a short release verification checklist.
- [ ] macOS signing/notarization is documented as a limitation if credentials are unavailable.

### Task 9: Portfolio Documentation and Demo Assets

**Description:** Update README with current screenshots, a short architecture diagram, demo flow, and clear setup/release instructions.

**Acceptance criteria:**
- [ ] README has current screenshots and feature list.
- [ ] README links to ADRs and the smoke checklist.
- [ ] A reviewer can build and understand the project in under five minutes.

## Post-v1.0 Backlog
- Manga image recognition through a separate provider abstraction (SauceNAO/OCR), not trace.moe.
- AniList sync conflict resolution and pending-change tracking.
