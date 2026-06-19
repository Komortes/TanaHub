namespace TanaHub.Application.Insights;

public sealed record LibraryInsightSegment(
    string Key,
    int Count,
    int CompletedCount,
    double CompletionRate,
    double? AverageScore);
