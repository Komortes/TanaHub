# Library Statistics and Watch-Time Charts Design

## Goal

Finish the roadmap item for library statistics and watch-time charts by turning the existing library insight metrics into compact visual dashboard charts.

## Current State

`LibraryInsightsCalculator` already computes totals, completion rate, dropped ratio, average score, episodes watched, chapters read, estimated watch minutes, and segment summaries by media type and list status. `HomeView` already renders metric cards, including watch time.

## Design

- Keep the existing metric cards.
- Add two compact chart cards on Home:
  - Library mix: segments from `LibraryInsightsSummary.ByMediaType`.
  - Status breakdown: segments from `LibraryInsightsSummary.ByStatus`.
- Each chart segment shows label, count, and percentage of the total library.
- Empty libraries show an empty-state chart detail instead of fake percentages.
- Keep charts dependency-free using Avalonia `ProgressBar`; do not add a charting package.

## Data Flow

`LoadLibraryAsync` calculates `LibraryInsightsSummary`, then `RefreshDashboardMetrics` updates both metric cards and chart cards. Chart card view-models normalize `LibraryInsightSegment.Count / TotalEntries` into 0-100 percentage values for binding.

## Testing

Unit-test chart segment percentage formatting and empty-library behavior through UI view-model records. Full build/test verification covers Avalonia binding compilation and existing insight calculations.
