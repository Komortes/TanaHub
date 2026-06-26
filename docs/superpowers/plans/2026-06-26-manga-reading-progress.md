# Manga Reading Progress Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make manga reading progress behave as a simple chapter counter, using catalog totals only when known.

**Architecture:** Reuse the existing library `Progress` field. Centralize progress display formatting in the UI view-model layer so detail and library surfaces do not show `/?` when a manga total is unknown.

**Tech Stack:** .NET 10, C#, Avalonia MVVM, xUnit.

---

### Task 1: Progress display formatter

**Files:**
- Modify: `src/TanaHub.UI/ViewModels/MainWindowViewModel.cs`
- Test: `tests/TanaHub.Application.Tests/MediaDetailViewModelTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving manga unknown totals display as a plain chapter number, while known totals still display `current/total`.

- [ ] **Step 2: Run tests to verify RED**

Run `dotnet test tests/TanaHub.Application.Tests/TanaHub.Application.Tests.csproj --no-restore --filter MediaDetailViewModelTests /m:1 /nr:false -v:minimal`.

- [ ] **Step 3: Implement minimal formatter**

Add a small helper in `MainWindowViewModel` that returns `progress/total` only when total is a known integer. Use it in detail and library entry construction.

- [ ] **Step 4: Run tests to verify GREEN**

Run the same filtered test command.

### Task 2: Roadmap and full verification

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Mark roadmap item complete**

Mark `Reading progress tracking for manga (chapter/volume)` as complete because chapter progress is implemented and volumes remain visible metadata.

- [ ] **Step 2: Run full verification**

Run `dotnet build TanaHub.sln --no-restore /m:1 /nr:false -v:minimal`, `dotnet test TanaHub.sln --no-build /m:1 /nr:false -v:minimal`, and `git diff --check`.
