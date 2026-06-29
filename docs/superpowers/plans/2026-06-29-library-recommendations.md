# Library Recommendations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Home recommendations strip based on the user's local library genres.

**Architecture:** Keep recommendation scoring in the Application layer as a pure, testable builder. Let the UI use the builder to choose top genres, search the existing catalog service for candidates, rank them, and bind results to Home.

**Tech Stack:** .NET 10, C#, Avalonia MVVM, xUnit.

---

### Task 1: Recommendation builder

**Files:**
- Create: `src/TanaHub.Application/Recommendations/LibraryRecommendationBuilder.cs`
- Test: `tests/TanaHub.Application.Tests/LibraryRecommendationBuilderTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests for top genre extraction, excluding already-library titles, and ranking candidates with matching genres above unrelated titles.

- [ ] **Step 2: Run RED**

Run `dotnet test tests/TanaHub.Application.Tests/TanaHub.Application.Tests.csproj --no-restore --filter LibraryRecommendationBuilderTests /m:1 /nr:false -v:minimal`.

- [ ] **Step 3: Implement builder**

Add `LibraryRecommendationGenre`, `BuildGenreProfile`, and `RankCandidates` methods. Use no external dependencies.

- [ ] **Step 4: Run GREEN**

Run the same filtered test command.

### Task 2: Home UI recommendations

**Files:**
- Modify: `src/TanaHub.UI/ViewModels/MainWindowViewModel.cs`
- Modify: `src/TanaHub.UI/ViewModels/MainWindowViewModel.Library.cs`
- Modify: `src/TanaHub.UI/Views/Pages/HomeView.axaml`
- Modify: `README.md`

- [ ] **Step 1: Add recommendation state**

Add `RecommendedItems`, `HasRecommendedItems`, and `RecommendationSummary` to the main view model.

- [ ] **Step 2: Load recommendations after library load**

When library entries are loaded, use their hydrated media metadata to build top genres, query the catalog for candidates, rank them, and populate `RecommendedItems`.

- [ ] **Step 3: Render Home strip**

Add `RECOMMENDED FOR YOU` under the in-progress strip. Reuse the discover card-style data model.

- [ ] **Step 4: Mark roadmap complete**

Mark `Recommendations based on your library` as complete in `README.md`.

- [ ] **Step 5: Full verification**

Run `dotnet build TanaHub.sln --no-restore /m:1 /nr:false -v:minimal`, `dotnet test TanaHub.sln --no-build /m:1 /nr:false -v:minimal`, and `git diff --check`.
