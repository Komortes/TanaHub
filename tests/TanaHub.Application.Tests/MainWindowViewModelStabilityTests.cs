using System.Collections.Specialized;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Tests;

public sealed partial class MainWindowViewModelRecognitionTests
{
    [Fact]
    public async Task LibrarySave_PreservesExistingCardsAndCommandsWithoutCollectionResets()
    {
        var library = new FakeUserLibraryService
        {
            Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current) { Score = 5 }
        };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), library: library);
        await vm.Initialization;
        var card = Assert.Single(vm.LibraryEntries);
        var increment = card.IncrementCommand;
        var resets = 0;
        vm.LibraryEntries.CollectionChanged += Observe;
        vm.ContinueItems.CollectionChanged += Observe;
        vm.RecentLibraryEntries.CollectionChanged += Observe;
        var changed = new List<string?>();
        // The card must notify the existing view when the score changes.
        if ((object)card is System.ComponentModel.INotifyPropertyChanged observable)
            observable.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await card.IncreaseScoreCommand.ExecuteAsync(null);
        await vm.LibraryRefresh;

        Assert.Same(card, Assert.Single(vm.LibraryEntries));
        Assert.Same(card, Assert.Single(vm.ContinueItems));
        Assert.Same(card, Assert.Single(vm.RecentLibraryEntries));
        Assert.Same(increment, card.IncrementCommand);
        Assert.Equal("6", card.Score);
        Assert.Contains(nameof(card.Score), changed);
        Assert.Equal(0, resets);

        void Observe(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) resets++;
        }
    }

    [Fact]
    public async Task LibraryRefresh_RemovesMissingEntriesWithoutResettingCollections()
    {
        var library = new FakeUserLibraryService
        { Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current) };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), library: library);
        await vm.Initialization;
        var changes = new List<NotifyCollectionChangedAction>();
        vm.LibraryEntries.CollectionChanged += (_, e) => changes.Add(e.Action);
        library.Entry = null;

        await vm.RefreshLibraryCommand.ExecuteAsync(null);

        Assert.Empty(vm.LibraryEntries);
        Assert.Empty(vm.ContinueItems);
        Assert.Empty(vm.RecentLibraryEntries);
        Assert.Contains(NotifyCollectionChangedAction.Remove, changes);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, changes);
    }

    [Fact]
    public async Task Discover_AppendKeepsLoadMoreVisibleAndExistingCardsWhilePending()
    {
        var catalog = new DeferredSearchCatalog();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), catalog);
        await vm.Initialization;
        catalog.Defer = true;
        var first = vm.SelectDiscoverTypeCommand.ExecuteAsync("Anime");
        catalog.Complete(0, "first", 30);
        await first;
        var card = Assert.Single(vm.SearchResults);
        var initialLoaderAppeared = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsInitialSearchLoading) && vm.IsInitialSearchLoading)
                initialLoaderAppeared = true;
        };

        var append = vm.LoadMoreSearchResultsCommand.ExecuteAsync(null);

        Assert.False(append.IsCompleted);
        Assert.True(vm.HasMoreSearchResults);
        Assert.True(vm.IsLoadingMoreSearchResults);
        Assert.False(vm.IsInitialSearchLoading);
        Assert.False(initialLoaderAppeared);
        Assert.Same(card, Assert.Single(vm.SearchResults));
        catalog.Complete(1, "second", 30);
        await append;
        Assert.Same(card, vm.SearchResults[0]);
        Assert.False(vm.IsLoadingMoreSearchResults);
    }

    [Fact]
    public async Task LibraryActions_QueueStatusBehindProgressSaveWithoutLosingEitherChange()
    {
        var library = new DeferredMutationLibrary();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), library: library);
        await vm.Initialization;
        await vm.RecentLibraryEntries[0].OpenDetailCommand.ExecuteAsync(null);
        library.DeferProgress = true;
        var progress = vm.SelectedMedia!.SetProgressCommand.ExecuteAsync("4");
        await library.ProgressSaveStarted.Task;

        var status = vm.SelectedMedia.MarkCompletedCommand.ExecuteAsync(null);
        Assert.False(status.IsCompleted);
        Assert.False(library.StatusSaved);
        library.AllowProgressSave.SetResult();
        await Task.WhenAll(progress, status);
        await vm.LibraryRefresh;

        Assert.Equal(4, library.Entry.Progress);
        Assert.Equal(MediaListStatus.Completed, library.Entry.Status);
        Assert.Equal("4", vm.SelectedMedia.LibraryCurrentProgress);
        Assert.True(vm.SelectedMedia.IsStatusCompleted);
    }

    private sealed class DeferredMutationLibrary : IUserLibraryService
    {
        public UserMediaEntry Entry { get; private set; } =
            new("anilist:1", MediaType.Anime, MediaListStatus.Current);
        public bool DeferProgress { get; set; }
        public bool StatusSaved { get; private set; }
        public TaskCompletionSource ProgressSaveStarted { get; } = new();
        public TaskCompletionSource AllowProgressSave { get; } = new();

        public Task<Result<PagedResult<UserMediaEntry>>> GetEntriesAsync(UserLibraryQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<PagedResult<UserMediaEntry>>.Success(new([Entry], query.Page, query.PageSize, 1)));
        public Task<Result<UserMediaEntry>> GetEntryAsync(string mediaId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<UserMediaEntry>.Success(Entry));
        public async Task<Result<UserMediaEntry>> UpsertEntryAsync(UserMediaEntry entry, CancellationToken cancellationToken = default)
        {
            if (DeferProgress)
            {
                ProgressSaveStarted.SetResult();
                await AllowProgressSave.Task;
            }
            Entry = entry;
            return Result<UserMediaEntry>.Success(Entry);
        }
        public Task<Result<UserMediaEntry>> UpdateStatusAsync(string mediaId, MediaListStatus status, CancellationToken cancellationToken = default)
        {
            StatusSaved = true;
            Entry = Entry with { Status = status };
            return GetEntryAsync(mediaId, cancellationToken);
        }
        public Task<Result<int>> MergeEntriesAsync(IReadOnlyCollection<UserMediaEntry> entries, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<int>.Success(entries.Count));
        public Task<Result<UserMediaEntry>> IncrementProgressAsync(string mediaId, int amount = 1, CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);
        public Task<Result<UserMediaEntry>> UpdateScoreAsync(string mediaId, int? score, CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);
        public Task<Result<UserMediaEntry>> UpdateNotesAsync(string mediaId, string? notes, CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);
        public Task<Result<UserMediaEntry>> UpdateReviewAsync(string mediaId, string? review, CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);
        public Task<Result<UserMediaEntry>> UpdateOrganizationAsync(string mediaId, IReadOnlyList<string> tags, IReadOnlyList<string> customLists, CancellationToken cancellationToken = default) =>
            GetEntryAsync(mediaId, cancellationToken);
        public Task<Result<bool>> RemoveEntryAsync(string mediaId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<bool>.Success(true));
    }
}
