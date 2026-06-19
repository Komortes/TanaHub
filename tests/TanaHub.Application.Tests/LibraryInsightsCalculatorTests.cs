using TanaHub.Application.Insights;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Tests;

public sealed class LibraryInsightsCalculatorTests
{
    [Fact]
    public void Calculate_ComputesCoreMetricsForMixedLibrary()
    {
        var anime = new Anime(
            "anilist:1",
            new MediaTitle("Cowboy Bebop"),
            MediaFormat.Tv,
            MediaReleaseStatus.Finished)
        {
            DurationMinutes = 25
        };

        var entries = new[]
        {
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Completed)
            {
                Progress = 26,
                Score = 10
            },
            new UserMediaEntry("mangadex:1", MediaType.Manga, MediaListStatus.Planning)
            {
                Progress = 12,
                Score = 8
            },
            new UserMediaEntry("anilist:2", MediaType.Anime, MediaListStatus.Dropped)
            {
                Progress = 3
            }
        };

        var summary = LibraryInsightsCalculator.Calculate(
            entries,
            new Dictionary<string, MediaItem> { [anime.Id] = anime });

        Assert.Equal(3, summary.TotalEntries);
        Assert.Equal(1, summary.CompletedEntries);
        Assert.Equal(1, summary.BacklogEntries);
        Assert.Equal(1, summary.DroppedEntries);
        Assert.Equal(1.0 / 3.0, summary.CompletionRate);
        Assert.Equal(1.0 / 3.0, summary.DroppedRatio);
        Assert.Equal(9, summary.AverageScore);
        Assert.Equal(29, summary.EpisodesWatched);
        Assert.Equal(12, summary.ChaptersRead);
        Assert.Equal(26 * 25 + 3 * 24, summary.EstimatedWatchMinutes);

        var animeSegment = Assert.Single(summary.ByMediaType, segment => segment.Key == "Anime");
        Assert.Equal(2, animeSegment.Count);
        Assert.Equal(1, animeSegment.CompletedCount);

        var completedSegment = Assert.Single(summary.ByStatus, segment => segment.Key == "Completed");
        Assert.Equal(1, completedSegment.Count);
        Assert.Equal(1, completedSegment.CompletedCount);
    }

    [Fact]
    public void Calculate_ReturnsZerosForEmptyLibrary()
    {
        var summary = LibraryInsightsCalculator.Calculate([]);

        Assert.Equal(0, summary.TotalEntries);
        Assert.Equal(0, summary.CompletionRate);
        Assert.Equal(0, summary.DroppedRatio);
        Assert.Null(summary.AverageScore);
        Assert.Empty(summary.ByMediaType);
        Assert.Empty(summary.ByStatus);
    }
}
