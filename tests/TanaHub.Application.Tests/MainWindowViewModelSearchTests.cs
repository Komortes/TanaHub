using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Tests;

public sealed partial class MainWindowViewModelRecognitionTests
{
    [Fact]
    public async Task Discover_IgnoresOlderResponseAfterFiltersChange()
    {
        var catalog = new DeferredSearchCatalog();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), catalog);
        await vm.Initialization;
        catalog.Defer = true;

        var older = vm.SelectDiscoverTypeCommand.ExecuteAsync("Anime");
        var newer = vm.SelectDiscoverSortCommand.ExecuteAsync("Score");
        Assert.True(catalog.Requests[0].Token.IsCancellationRequested);
        catalog.Complete(1, "newer");
        await newer;
        catalog.Complete(0, "older");
        await older;

        Assert.Equal("newer", Assert.Single(vm.SearchResults).Id);
        Assert.False(vm.IsSearching);
    }

    [Fact]
    public async Task Discover_ClearDuringSearchDoesNotReopenResults()
    {
        var catalog = new DeferredSearchCatalog();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), catalog);
        await vm.Initialization;
        catalog.Defer = true;
        var search = vm.SelectDiscoverTypeCommand.ExecuteAsync("Anime");
        vm.ClearDiscoverSearchCommand.Execute(null);
        catalog.Complete(0, "late");
        await search;

        Assert.Empty(vm.SearchResults);
        Assert.False(vm.IsDiscoverSearchActive);
        Assert.False(vm.IsSearching);
        Assert.False(vm.HasMoreSearchResults);
    }

    [Fact]
    public async Task AddExistingTitle_PreservesProgressAndPersonalFields()
    {
        var entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Completed)
        { Progress = 26, Score = 9, Notes = "Keep notes", Review = "Keep review", Tags = ["favorite"] };
        var library = new FakeUserLibraryService { Entry = entry };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), library: library);
        await vm.Initialization;
        await vm.RecentLibraryEntries[0].OpenDetailCommand.ExecuteAsync(null);
        await vm.SelectedMedia!.AddToLibraryCommand.ExecuteAsync(null);

        Assert.Same(entry, library.Entry);
        Assert.Equal(0, library.UpsertCalls);
    }

    [Fact]
    public async Task HomeRecentEntries_AreIndependentOfLibraryFilters()
    {
        var library = new FakeUserLibraryService
        { Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Completed) };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), library: library);
        await vm.Initialization;
        vm.SelectedLibraryStatus = "Planning";
        Assert.Empty(vm.LibraryEntries);
        Assert.Single(vm.RecentLibraryEntries);
        Assert.True(vm.HasRecentLibraryEntries);
    }

    [Theory]
    [InlineData("not a number")]
    [InlineData("-3")]
    [InlineData("99999999999999999999")]
    public async Task InvalidProgressInput_DoesNotResetExistingProgress(string input)
    {
        var entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current) { Progress = 9 };
        var library = new FakeUserLibraryService { Entry = entry };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), library: library);
        await vm.Initialization;
        await vm.RecentLibraryEntries[0].OpenDetailCommand.ExecuteAsync(null);
        await vm.SelectedMedia!.SetProgressCommand.ExecuteAsync(input);
        Assert.Same(entry, library.Entry);
        Assert.Equal(0, library.UpsertCalls);
    }

    [Fact]
    public async Task Discover_NewSearchCanPaginateWhileOldProviderStillRuns()
    {
        var catalog = new DeferredSearchCatalog();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), catalog);
        await vm.Initialization;
        catalog.Defer = true;
        var first = vm.SelectDiscoverTypeCommand.ExecuteAsync("Anime");
        catalog.Complete(0, "first", 30);
        await first;
        var stalePage = vm.LoadMoreSearchResultsCommand.ExecuteAsync(null);
        var replacement = vm.SelectDiscoverSortCommand.ExecuteAsync("Score");
        catalog.Complete(2, "replacement", 30);
        await replacement;
        await stalePage;
        Assert.False(vm.IsLoadingMoreSearchResults);
        var nextPage = vm.LoadMoreSearchResultsCommand.ExecuteAsync(null);
        Assert.Equal(4, catalog.Requests.Count);
        catalog.Complete(3, "second page");
        await nextPage;
        catalog.Complete(1, "stale page");
        Assert.Equal(["replacement", "second page"], vm.SearchResults.Select(item => item.Id));
    }

    private sealed class DeferredSearchCatalog : IMediaCatalogService
    {
        public bool Defer { get; set; }
        public List<(TaskCompletionSource<Result<PagedResult<MediaItem>>> Completion, CancellationToken Token)> Requests { get; } = [];

        public Task<Result<PagedResult<MediaItem>>> SearchAsync(MediaSearchQuery query, CancellationToken cancellationToken = default)
        {
            if (!Defer) return Task.FromResult(Result<PagedResult<MediaItem>>.Success(new([], 1, 12, 0)));
            var completion = new TaskCompletionSource<Result<PagedResult<MediaItem>>>();
            Requests.Add((completion, cancellationToken));
            return completion.Task;
        }

        public void Complete(int index, string id, int total = 1) => Requests[index].Completion.SetResult(
            Result<PagedResult<MediaItem>>.Success(new(
                [new Anime(id, new MediaTitle(id), MediaFormat.Tv, MediaReleaseStatus.Finished)], 1, 12, total)));

        public Task<Result<MediaItem>> GetByIdAsync(string mediaId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<MediaItem>.Failure(ApplicationError.NotFound("Not found")));
    }
}
