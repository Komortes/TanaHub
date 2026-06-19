namespace TanaHub.Application.Insights;

public sealed record LibraryInsightsSummary(
    int TotalEntries,
    int CompletedEntries,
    int BacklogEntries,
    int DroppedEntries,
    double CompletionRate,
    double DroppedRatio,
    double? AverageScore,
    int EpisodesWatched,
    int ChaptersRead,
    int EstimatedWatchMinutes,
    IReadOnlyList<LibraryInsightSegment> ByMediaType,
    IReadOnlyList<LibraryInsightSegment> ByStatus);
