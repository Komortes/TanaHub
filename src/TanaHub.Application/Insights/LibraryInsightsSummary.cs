namespace TanaHub.Application.Insights;

public sealed record LibraryInsightsSummary(
    int TotalEntries,
    int CompletedEntries,
    int BacklogEntries,
    int DroppedEntries,
    double CompletionRate,
    double DroppedRatio,
    double? AverageScore,
    long EpisodesWatched,
    long ChaptersRead,
    long EstimatedWatchMinutes,
    IReadOnlyList<LibraryInsightSegment> ByMediaType,
    IReadOnlyList<LibraryInsightSegment> ByStatus);
