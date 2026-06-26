# Library Statistics Charts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add compact dashboard charts for library statistics while reusing the existing insight calculator and watch-time metric.

**Architecture:** Add small UI view-model records for dashboard chart cards and segments. Populate an observable chart collection from `LibraryInsightsSummary` in `MainWindowViewModel`, then bind HomeView to it with dependency-free Avalonia progress bars.

**Tech Stack:** .NET 10, C#, Avalonia MVVM, xUnit.

---

### Task 1: Chart view-model contract

**Files:**
- Create: `src/TanaHub.UI/ViewModels/DashboardChartSegmentViewModel.cs`
- Create: `src/TanaHub.UI/ViewModels/DashboardChartViewModel.cs`
- Test: `tests/TanaHub.Application.Tests/DashboardChartViewModelTests.cs`

- [ ] **Step 1: Write failing tests**

Test that a segment with count `2` of total `3` formats as `67%`, and an empty chart exposes `HasSegments == false`.

- [ ] **Step 2: Run RED**

Run `dotnet test tests/TanaHub.Application.Tests/TanaHub.Application.Tests.csproj --no-restore --filter DashboardChartViewModelTests /m:1 /nr:false -v:minimal`.

- [ ] **Step 3: Implement records**

Create immutable records with `Percent`, `PercentText`, `HasSegments`, and factory methods from `LibraryInsightSegment`.

- [ ] **Step 4: Run GREEN**

Run the same filtered test command.

### Task 2: Dashboard wiring and UI

**Files:**
- Modify: `src/TanaHub.UI/ViewModels/MainWindowViewModel.cs`
- Modify: `src/TanaHub.UI/Views/Pages/HomeView.axaml`
- Modify: `README.md`

- [ ] **Step 1: Wire chart collection**

Add `DashboardCharts` to `MainWindowViewModel`, initialize it, and refresh it alongside `DashboardMetrics`.

- [ ] **Step 2: Render charts**

Add an `ItemsControl` under the stats metric cards in `HomeView.axaml`. Each chart renders segment rows with label, count, percentage, and `ProgressBar`.

- [ ] **Step 3: Mark roadmap complete**

Mark `Library statistics and watch-time charts` as complete in `README.md`.

- [ ] **Step 4: Full verification**

Run `dotnet build TanaHub.sln --no-restore /m:1 /nr:false -v:minimal`, `dotnet test TanaHub.sln --no-build /m:1 /nr:false -v:minimal`, and `git diff --check`.
