using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Insights;

public static class LibraryInsightsCalculator
{
    private const int FallbackAnimeEpisodeMinutes = 24;

    public static LibraryInsightsSummary Calculate(
        IEnumerable<UserMediaEntry> entries,
        IReadOnlyDictionary<string, MediaItem>? mediaById = null)
    {
        var items = entries.ToArray();
        var totalEntries = items.Length;
        var completedEntries = items.Count(entry => entry.Status == MediaListStatus.Completed);
        var droppedEntries = items.Count(entry => entry.Status == MediaListStatus.Dropped);
        var scoredEntries = items.Where(entry => entry.Score is not null).ToArray();

        var episodesWatched = items
            .Where(entry => entry.MediaType == MediaType.Anime)
            .Sum(entry => entry.Progress);

        var chaptersRead = items
            .Where(entry => entry.MediaType == MediaType.Manga)
            .Sum(entry => entry.Progress);

        var estimatedWatchMinutes = items
            .Where(entry => entry.MediaType == MediaType.Anime)
            .Sum(entry => entry.Progress * GetEpisodeMinutes(entry, mediaById));

        return new LibraryInsightsSummary(
            totalEntries,
            completedEntries,
            items.Count(entry => entry.Status == MediaListStatus.Planning),
            droppedEntries,
            Ratio(completedEntries, totalEntries),
            Ratio(droppedEntries, totalEntries),
            scoredEntries.Length == 0 ? null : scoredEntries.Average(entry => entry.Score!.Value),
            episodesWatched,
            chaptersRead,
            estimatedWatchMinutes,
            SegmentBy(items, entry => entry.MediaType.ToString()),
            SegmentBy(items, entry => entry.Status.ToString()));
    }

    private static int GetEpisodeMinutes(
        UserMediaEntry entry,
        IReadOnlyDictionary<string, MediaItem>? mediaById)
    {
        if (mediaById?.TryGetValue(entry.MediaId, out var media) == true
            && media is Anime { DurationMinutes: > 0 } anime)
        {
            return anime.DurationMinutes.Value;
        }

        return FallbackAnimeEpisodeMinutes;
    }

    private static IReadOnlyList<LibraryInsightSegment> SegmentBy(
        IReadOnlyList<UserMediaEntry> entries,
        Func<UserMediaEntry, string> keySelector)
    {
        return entries
            .GroupBy(keySelector)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var groupItems = group.ToArray();
                var completed = groupItems.Count(entry => entry.Status == MediaListStatus.Completed);
                var scored = groupItems.Where(entry => entry.Score is not null).ToArray();
                return new LibraryInsightSegment(
                    group.Key,
                    groupItems.Length,
                    completed,
                    Ratio(completed, groupItems.Length),
                    scored.Length == 0 ? null : scored.Average(entry => entry.Score!.Value));
            })
            .ToArray();
    }

    private static double Ratio(int numerator, int denominator)
    {
        return denominator == 0 ? 0 : (double)numerator / denominator;
    }
}
