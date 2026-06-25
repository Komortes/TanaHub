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

    private static MediaDetailViewModel CreateDetailViewModel()
    {
        return new MediaDetailViewModel(
            "anilist:1",
            "Title",
            string.Empty,
            string.Empty,
            "Anime",
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
            "—",
            "—",
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
