using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Tests;

public sealed class DomainModelTests
{
    [Fact]
    public void MediaTitle_UsesEnglishTitleWhenAvailable()
    {
        var title = new MediaTitle("Sousou no Frieren", "Frieren: Beyond Journey's End", "葬送のフリーレン");

        Assert.Equal("Frieren: Beyond Journey's End", title.DisplayTitle);
    }

    [Fact]
    public void UserMediaEntry_IncrementProgress_ReturnsUpdatedEntry()
    {
        var entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current);

        var updated = entry.IncrementProgress();

        Assert.Equal(0, entry.Progress);
        Assert.Equal(1, updated.Progress);
        Assert.Equal(entry.MediaId, updated.MediaId);
    }

    [Fact]
    public void Anime_SetsMediaType()
    {
        var anime = new Anime(
            "anilist:1",
            new MediaTitle("Cowboy Bebop"),
            MediaFormat.Tv,
            MediaReleaseStatus.Finished);

        Assert.Equal(MediaType.Anime, anime.Type);
    }
}
