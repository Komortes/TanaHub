using CommunityToolkit.Mvvm.Input;
using TanaHub.UI.ViewModels;

namespace TanaHub.Application.Tests;

public sealed class LibraryEntryViewModelTests
{
    [Theory]
    [InlineData("Anime", "Current", "Watching", "3/12 ep.", "+1 Episode")]
    [InlineData("Manga", "Current", "Reading", "3/12 ch.", "+1 Chapter")]
    [InlineData("Anime", "Repeating", "Rewatching", "3/12 ep.", "+1 Episode")]
    [InlineData("Manga", "Repeating", "Rereading", "3/12 ch.", "+1 Chapter")]
    [InlineData("Manga", "Completed", "Completed", "3/12 ch.", "+1 Chapter")]
    public void TrackingLanguage_MatchesMediaTypeWithoutChangingStoredStatus(
        string type, string status, string label, string progress, string increment)
    {
        var command = new AsyncRelayCommand(() => Task.CompletedTask);
        var model = new LibraryEntryViewModel(
            "anilist:1", "Title", type, status, "3/12", "-", null,
            command, command, command, command, command, command);

        Assert.Equal(label, model.StatusLabel);
        Assert.Equal(progress, model.ProgressLabel);
        Assert.Equal(increment, model.IncrementLabel);
        Assert.Equal(status, model.Status);
    }
}
