# MAL Import/Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add local MAL-style XML import/export for the user's library.

**Architecture:** Implement XML conversion in the Application layer so it is testable without Avalonia. Reuse existing `LibraryExportItem` for export and map import results into `UserMediaEntry`. Wire file save/open commands in the UI.

**Tech Stack:** .NET 10, C#, System.Xml.Linq, Avalonia MVVM, xUnit.

---

### Task 1: MAL XML exchange helper

**Files:**
- Create: `src/TanaHub.Application/Export/MalXmlLibraryExchange.cs`
- Test: `tests/TanaHub.Application.Tests/MalXmlLibraryExchangeTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests for exporting an anime entry, importing anime and manga entries, and returning validation errors for invalid XML.

- [ ] **Step 2: Run RED**

Run `dotnet test tests/TanaHub.Application.Tests/TanaHub.Application.Tests.csproj --no-restore --filter MalXmlLibraryExchangeTests /m:1 /nr:false -v:minimal`.

- [ ] **Step 3: Implement exchange helper**

Use `XDocument` to build and parse `myanimelist` XML. Do not add external dependencies.

- [ ] **Step 4: Run GREEN**

Run the same filtered test command.

### Task 2: UI import/export wiring

**Files:**
- Modify: `src/TanaHub.UI/Services/IFileOpenService.cs`
- Modify: `src/TanaHub.Desktop/Services/AvaloniaFileOpenService.cs`
- Modify: `src/TanaHub.UI/ViewModels/MainWindowViewModel.cs`
- Modify: `src/TanaHub.UI/Views/Pages/SettingsView.axaml`
- Modify: `README.md`

- [ ] **Step 1: Add XML file open service**

Add `PickTextAsync` to `IFileOpenService` and implement it for XML files in the Avalonia desktop service.

- [ ] **Step 2: Add commands**

Add `ExportMalXmlAsync` and `ImportMalXmlAsync` commands. Export uses existing library rows; import reads XML, upserts entries, reloads library, and updates status text.

- [ ] **Step 3: Add settings buttons**

Render `Import MAL XML` and `Export MAL XML` next to the existing CSV export.

- [ ] **Step 4: Mark roadmap complete**

Mark `MAL (MyAnimeList) import/export` as complete in `README.md`.

- [ ] **Step 5: Full verification**

Run `dotnet build TanaHub.sln --no-restore /m:1 /nr:false -v:minimal`, `dotnet test TanaHub.sln --no-build /m:1 /nr:false -v:minimal`, and `git diff --check`.
