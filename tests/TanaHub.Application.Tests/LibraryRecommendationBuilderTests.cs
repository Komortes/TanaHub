using TanaHub.Application.Recommendations;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Tests;

public sealed class LibraryRecommendationBuilderTests
{
    [Fact]
    public void BuildGenreProfile_WeightsCompletedAndScoredGenres()
    {
        var entries = new[]
        {
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Completed) { Score = 10 },
            new UserMediaEntry("anilist:2", MediaType.Anime, MediaListStatus.Planning) { Score = 6 },
            new UserMediaEntry("anilist:3", MediaType.Anime, MediaListStatus.Dropped) { Score = 10 }
        };
        var mediaById = new Dictionary<string, MediaItem>
        {
            ["anilist:1"] = Anime("anilist:1", "Favorite", ["Action", "Drama"], 90),
            ["anilist:2"] = Anime("anilist:2", "Maybe", ["Comedy"], 70),
            ["anilist:3"] = Anime("anilist:3", "Dropped", ["Horror"], 80)
        };

        var profile = LibraryRecommendationBuilder.BuildGenreProfile(entries, mediaById, maxGenres: 3);

        Assert.Equal("Action", profile[0].Name);
        Assert.Contains(profile, genre => genre.Name == "Drama");
        Assert.DoesNotContain(profile, genre => genre.Name == "Horror");
    }

    [Fact]
    public void RankCandidates_ExcludesAlreadyTrackedTitles()
    {
        var entries = new[]
        {
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Completed) { Score = 10 }
        };
        var profile = new[]
        {
            new LibraryRecommendationGenre("Action", 10)
        };
        var candidates = new[]
        {
            Anime("anilist:1", "Already tracked", ["Action"], 100),
            Anime("anilist:2", "New action", ["Action"], 80)
        };

        var ranked = LibraryRecommendationBuilder.RankCandidates(entries, profile, candidates, maxItems: 10);

        var recommendation = Assert.Single(ranked);
        Assert.Equal("anilist:2", recommendation.Id);
    }

    [Fact]
    public void RankCandidates_RanksMatchingGenresAboveUnrelatedCandidates()
    {
        var entries = new[]
        {
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Completed) { Score = 10 }
        };
        var profile = new[]
        {
            new LibraryRecommendationGenre("Action", 10),
            new LibraryRecommendationGenre("Drama", 5)
        };
        var candidates = new[]
        {
            Anime("anilist:2", "Unrelated high score", ["Comedy"], 99),
            Anime("anilist:3", "Matching lower score", ["Action"], 80)
        };

        var ranked = LibraryRecommendationBuilder.RankCandidates(entries, profile, candidates, maxItems: 10);

        Assert.Equal("anilist:3", ranked[0].Id);
    }

    private static Anime Anime(string id, string title, IReadOnlyList<string> genres, int score)
    {
        return new Anime(
            id,
            new MediaTitle(title),
            MediaFormat.Tv,
            MediaReleaseStatus.Finished)
        {
            Genres = genres,
            AverageScore = score
        };
    }
}
