using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Tests;

public sealed partial class MainWindowViewModelRecognitionTests
{
    [Fact]
    public async Task RepeatingQuickScore_UsesSavedScoreWhileLibraryRefreshIsPending()
    {
        var library = new FakeUserLibraryService
        { Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current) { Score = 5 } };
        var catalog = new TwoTitleCatalog();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), catalog, library);
        await vm.Initialization;
        var command = vm.LibraryEntries[0].IncreaseScoreCommand;
        catalog.PendingDetail = new TaskCompletionSource<Result<MediaItem>>();

        await command.ExecuteAsync(null);
        await command.ExecuteAsync(null);

        Assert.Equal(7, library.Entry!.Score);
        catalog.PendingDetail.SetResult(Result<MediaItem>.Success(
            new Anime("anilist:1", new MediaTitle("One"), MediaFormat.Tv, MediaReleaseStatus.Finished)));
        await vm.LibraryRefresh;
    }

    [Fact]
    public async Task SavingSettings_ShowsTemporaryFeedbackWithoutChangingSearchSummary()
    {
        using var vm = CreateViewModel(new FakeFileOpenService(new()));
        await vm.Initialization;
        vm.SearchStatus = "Showing 12 results.";
        await vm.ToggleNotificationsCommand.ExecuteAsync(null);
        Assert.Equal("Settings saved.", vm.Notification.Message);
        Assert.Equal("Showing 12 results.", vm.SearchStatus);
        vm.NavigateToDiscoverCommand.Execute(null);
        Assert.False(vm.Notification.IsVisible);
    }

    [Fact]
    public async Task SavingProgress_KeepsTheOpenDetailAndItsCommands()
    {
        var library = new FakeUserLibraryService
        {
            Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
            { Progress = 3, Notes = "Personal notes" }
        };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), library: library);
        await vm.Initialization;
        await vm.RecentLibraryEntries[0].OpenDetailCommand.ExecuteAsync(null);
        var detail = vm.SelectedMedia!;
        detail.Notes = "Unsaved notes";
        detail.Review = "Unsaved review";
        detail.TagsText = "unsaved tag";
        detail.CustomListsText = "unsaved list";

        await detail.SetProgressCommand.ExecuteAsync("4");

        Assert.Same(detail, vm.SelectedMedia);
        Assert.Equal("4", detail.LibraryCurrentProgress);
        Assert.Equal("Unsaved notes", detail.Notes);
        Assert.Equal("Unsaved review", detail.Review);
        Assert.Equal("unsaved tag", detail.TagsText);
        Assert.Equal("unsaved list", detail.CustomListsText);
        Assert.True(vm.IsDetailVisible);
    }

    [Theory]
    [InlineData("back")]
    [InlineData("navigation")]
    [InlineData("title")]
    public async Task LeavingTitle_ClearsItsFeedback(string destination)
    {
        var library = new FakeUserLibraryService
        { Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current) };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), new TwoTitleCatalog(), library);
        await vm.Initialization;
        await vm.TrendingItems[0].OpenDetailCommand.ExecuteAsync(null);
        await vm.SelectedMedia!.SetProgressCommand.ExecuteAsync("invalid");
        Assert.True(vm.Notification.IsVisible);
        Assert.True(vm.Notification.IsError);

        if (destination == "back") vm.GoBackCommand.Execute(null);
        else if (destination == "navigation") vm.NavigateToLibraryCommand.Execute(null);
        else await vm.TrendingItems[1].OpenDetailCommand.ExecuteAsync(null);

        Assert.False(vm.Notification.IsVisible);
    }

    [Fact]
    public async Task SaveCompletingAfterAnotherTitleOpens_DoesNotShowOldFeedbackOrReplaceDetail()
    {
        var library = new FakeUserLibraryService
        {
            Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current),
            PendingSave = new TaskCompletionSource()
        };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), new TwoTitleCatalog(), library);
        await vm.Initialization;
        await vm.TrendingItems[0].OpenDetailCommand.ExecuteAsync(null);
        var save = vm.SelectedMedia!.SetProgressCommand.ExecuteAsync("4");
        Assert.False(save.IsCompleted);
        await vm.TrendingItems[1].OpenDetailCommand.ExecuteAsync(null);
        var newDetail = vm.SelectedMedia;
        library.PendingSave.SetResult();
        await save;

        Assert.Equal("anilist:2", vm.SelectedMedia!.Id);
        Assert.Same(newDetail, vm.SelectedMedia);
        Assert.False(vm.Notification.IsVisible);
    }

    [Fact]
    public async Task SavingProgress_ReenablesTheButtonWhileCatalogRefreshIsStillPending()
    {
        var library = new FakeUserLibraryService
        { Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current) };
        var catalog = new TwoTitleCatalog();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), catalog, library);
        await vm.Initialization;
        await vm.TrendingItems[0].OpenDetailCommand.ExecuteAsync(null);
        catalog.PendingDetail = new TaskCompletionSource<Result<MediaItem>>();
        var detail = vm.SelectedMedia!;

        await detail.SetProgressCommand.ExecuteAsync("7");

        Assert.Equal("7", detail.LibraryCurrentProgress);
        Assert.False(detail.SetProgressCommand.IsRunning);
        Assert.True(detail.SetProgressCommand.CanExecute("8"));
        Assert.False(vm.LibraryRefresh.IsCompleted);
        catalog.PendingDetail.SetResult(Result<MediaItem>.Success(
            new Anime("anilist:1", new MediaTitle("One"), MediaFormat.Tv, MediaReleaseStatus.Finished)));
        await vm.LibraryRefresh;
    }

    [Fact]
    public async Task BackgroundSearch_CannotOverwriteTitleFeedback()
    {
        var catalog = new TwoTitleCatalog();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), catalog);
        await vm.Initialization;
        catalog.PendingSearch = new TaskCompletionSource<Result<PagedResult<MediaItem>>>();
        var search = vm.SelectDiscoverTypeCommand.ExecuteAsync("Anime");
        await vm.TrendingItems[0].OpenDetailCommand.ExecuteAsync(null);
        await vm.SelectedMedia!.SetProgressCommand.ExecuteAsync("invalid");
        var message = vm.Notification.Message;
        catalog.PendingSearch.SetResult(Result<PagedResult<MediaItem>>.Success(new([], 1, 12, 0)));
        await search;

        Assert.Equal(message, vm.Notification.Message);
        Assert.True(vm.Notification.IsError);
    }

    private sealed class TwoTitleCatalog : IMediaCatalogService
    {
        public TaskCompletionSource<Result<PagedResult<MediaItem>>>? PendingSearch { get; set; }
        public TaskCompletionSource<Result<MediaItem>>? PendingDetail { get; set; }

        public Task<Result<PagedResult<MediaItem>>> SearchAsync(MediaSearchQuery query, CancellationToken cancellationToken = default) =>
            PendingSearch?.Task ?? Task.FromResult(Result<PagedResult<MediaItem>>.Success(
                new([Title("anilist:1"), Title("anilist:2")], 1, 12, 2)));

        public Task<Result<MediaItem>> GetByIdAsync(string mediaId, CancellationToken cancellationToken = default) =>
            PendingDetail?.Task ?? Task.FromResult(Result<MediaItem>.Success(Title(mediaId)));

        private static Anime Title(string id) => new(id, new MediaTitle(id), MediaFormat.Tv, MediaReleaseStatus.Finished);
    }
}
