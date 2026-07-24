# 0002. Native UI via Avalonia instead of an Electron-style web wrapper

## Status

Accepted

## Context

Existing anime/manga trackers in this category (Taiga, Shiru) are Electron-based — a bundled Chromium plus a Node runtime rendering the UI as a web page. `PRODUCT.md` names these explicitly as anti-references: "web-in-a-box tells — slow transitions, inconsistent component vocabulary, browser-default form controls, shadows that look like CSS box-shadow." The product's stated purpose is to demonstrate that a native .NET app can be lighter, faster, and more native-feeling than that alternative.

## Decision

Build the UI with Avalonia (Skia-rendered, XAML/MVVM, via `CommunityToolkit.Mvvm` for view models) instead of an Electron/WebView wrapper. This is a real rendering pipeline, not a browser embed: no bundled Chromium, no Node runtime, no JS bridge between UI and app logic.

Trade-off accepted knowingly: Avalonia's component and tooling ecosystem is smaller than the web stack's (fewer pre-built components, fewer accessibility auto-checks out of the box than mature browser engines provide). The project compensates for the accessibility gap directly — `tests/TanaHub.Application.Tests/XamlAccessibilityAuditTests.cs` audits the XAML views in CI rather than relying on inherited browser accessibility behavior.

## Consequences

Easier: smaller memory footprint and faster startup than an Electron app (no bundled browser engine to boot), UI and application code share one process and one language, no IPC/bridge layer between "web view" and "backend."

Harder: fewer off-the-shelf components than web UI frameworks provide, more manual work for things like accessibility that a browser engine would otherwise cover — mitigated by the XAML accessibility audit test rather than left unaddressed.
