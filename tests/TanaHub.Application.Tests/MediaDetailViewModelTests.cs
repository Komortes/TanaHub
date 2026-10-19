using CommunityToolkit.Mvvm.Input;
using TanaHub.UI.ViewModels;

namespace TanaHub.Application.Tests;

public sealed class MediaDetailViewModelTests
{
    [Fact]
    public void Constructor_ExposesSavedReview()
    {
        var model = CreateDetailViewModel();

        Assert.Equal("A memorable story.", model.Review);
    }

    [Theory]
    [InlineData("Anime", "Watching", "+1 Episode", "Episodes", "12")]
    [InlineData("Manga", "Reading", "+1 Chapter", "Chapters", "108")]
    public void TrackingLanguage_MatchesMediaType(string mediaType, string currentLabel, string increment, string unit, string total)
    {
        var model = CreateDetailViewModel(mediaType);

        Assert.Equal(currentLabel, model.CurrentStatusLabel);
        Assert.Equal(increment, model.IncrementLabel);
        Assert.Equal(unit, model.ProgressLabel);
        Assert.Equal(total, model.ProgressValue);
    }

    private static MediaDetailViewModel CreateDetailViewModel(string mediaType = "Anime")
    {
        return new MediaDetailViewModel(
            "anilist:1",
            "Title",
            string.Empty,
            string.Empty,
            mediaType,
            "TV",
            "Finished",
            "2026",
            "90",
            string.Empty,
            [],
            null,
            null,
            "12",
            "24",
            "Studio",
            "108",
            "12",
            "Current",
            "1/12",
            "8",
            true,
            Command(),
            Command(),
            Command(),
            Command(),
            Command(),
            Command(),
            Command(),
            Command(),
            StringCommand(),
            StringCommand(),
            Command(),
            Command(),
            Command(),
            "Personal note",
            "A memorable story.",
            "tag",
            "list",
            StringCommand(),
            StringCommand(),
            [],
            StringCommand(),
            StringCommand());
    }

    private static IAsyncRelayCommand Command() => new AsyncRelayCommand(() => Task.CompletedTask);

    private static IAsyncRelayCommand<string?> StringCommand() =>
        new AsyncRelayCommand<string?>(_ => Task.CompletedTask);
}
