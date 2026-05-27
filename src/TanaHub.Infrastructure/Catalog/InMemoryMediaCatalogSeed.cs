using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Catalog;

internal static class InMemoryMediaCatalogSeed
{
    public static IReadOnlyList<MediaItem> Create()
    {
        return
        [
            new Anime(
                "anilist:1",
                new MediaTitle("Cowboy Bebop"),
                MediaFormat.Tv,
                MediaReleaseStatus.Finished)
            {
                EpisodeCount = 26,
                DurationMinutes = 24,
                StartYear = 1998,
                AverageScore = 86,
                Studio = "Sunrise",
                Genres = ["Action", "Adventure", "Drama", "Sci-Fi"]
            },
            new Anime(
                "anilist:21",
                new MediaTitle("One Piece"),
                MediaFormat.Tv,
                MediaReleaseStatus.Releasing)
            {
                StartYear = 1999,
                AverageScore = 88,
                Studio = "Toei Animation",
                Genres = ["Action", "Adventure", "Comedy", "Fantasy"]
            },
            new Anime(
                "anilist:154587",
                new MediaTitle("Sousou no Frieren", "Frieren: Beyond Journey's End", "葬送のフリーレン"),
                MediaFormat.Tv,
                MediaReleaseStatus.Finished)
            {
                EpisodeCount = 28,
                DurationMinutes = 24,
                StartYear = 2023,
                AverageScore = 91,
                Studio = "Madhouse",
                Genres = ["Adventure", "Drama", "Fantasy"]
            },
            new Manga(
                "mangadex:berserk",
                new MediaTitle("Berserk", null, "ベルセルク"),
                MediaFormat.Manga,
                MediaReleaseStatus.Hiatus)
            {
                StartYear = 1989,
                AverageScore = 93,
                Genres = ["Action", "Adventure", "Drama", "Fantasy"]
            },
            new Manga(
                "mangadex:solo-leveling",
                new MediaTitle("Solo Leveling", null, "나 혼자만 레벨업"),
                MediaFormat.Manhwa,
                MediaReleaseStatus.Finished)
            {
                ChapterCount = 200,
                StartYear = 2018,
                AverageScore = 82,
                Genres = ["Action", "Adventure", "Fantasy"]
            }
        ];
    }
}
