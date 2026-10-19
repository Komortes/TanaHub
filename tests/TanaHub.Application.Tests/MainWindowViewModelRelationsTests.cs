using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Tests;

public sealed partial class MainWindowViewModelRecognitionTests
{
    [Fact]
    public async Task RelatedTitles_LoadWithoutBlockingDetail_AndIgnoreLatePreviousResponse()
    {
        var relations = new ControlledRelationsService();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), new TwoTitleCatalog(), relations: relations);
        await vm.Initialization;
        await vm.TrendingItems[0].OpenDetailCommand.ExecuteAsync(null);
        var first = vm.SelectedMedia!;
        var firstLoad = vm.RelatedTitlesLoad;
        Assert.True(vm.IsDetailVisible);
        Assert.True(first.RelatedTitles!.IsLoading);
        Assert.False(firstLoad.IsCompleted);

        await vm.TrendingItems[1].OpenDetailCommand.ExecuteAsync(null);
        var second = vm.SelectedMedia!;
        Assert.True(relations.Requests[0].Token.IsCancellationRequested);
        relations.Complete(1, "anilist:3");
        await vm.RelatedTitlesLoad;
        relations.Complete(0, "anilist:99");
        await firstLoad;

        Assert.Same(second, vm.SelectedMedia);
        Assert.Equal("anilist:3", second.RelatedTitles!.Groups.Single().Titles.Single().MediaId);
        Assert.Empty(first.RelatedTitles.Groups);
        Assert.False(vm.Notification.IsVisible);
    }

    [Fact]
    public async Task FailedTitleNavigation_RestartsCancelledRelationsAndKeepsRetryWorking()
    {
        var relations = new ControlledRelationsService();
        var catalog = new TwoTitleCatalog();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), catalog, relations: relations);
        await vm.Initialization;
        await vm.TrendingItems[0].OpenDetailCommand.ExecuteAsync(null);
        var oldLoad = vm.RelatedTitlesLoad;
        catalog.PendingDetail = new();
        var navigate = vm.TrendingItems[1].OpenDetailCommand.ExecuteAsync(null);
        catalog.PendingDetail.SetResult(Result<MediaItem>.Failure(new("unavailable", "Unavailable")));
        await navigate;

        Assert.Equal("anilist:1", vm.SelectedMedia!.Id);
        Assert.Equal(2, relations.Requests.Count);
        relations.Requests[1].Completion.SetResult(Result<RelatedTitlesResult>.Failure(new("unavailable", "Try again")));
        await vm.RelatedTitlesLoad;
        var retry = vm.SelectedMedia.RelatedTitles!.RetryCommand.ExecuteAsync(null);
        Assert.Equal(3, relations.Requests.Count);
        relations.Complete(2, "anilist:3");
        await retry;
        Assert.Single(vm.SelectedMedia.RelatedTitles.Groups);
        relations.Complete(0, "anilist:99");
        await oldLoad;
        Assert.Equal("anilist:3", vm.SelectedMedia.RelatedTitles.Groups[0].Titles[0].MediaId);
    }

    [Fact]
    public async Task RelatedTitleNavigation_BackReturnsToPreviousTitle()
    {
        var relations = new ControlledRelationsService { CompleteImmediately = true };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), new TwoTitleCatalog(), relations: relations);
        await vm.Initialization;
        await vm.TrendingItems[0].OpenDetailCommand.ExecuteAsync(null);
        await vm.RelatedTitlesLoad;

        await vm.SelectedMedia!.RelatedTitles!.Groups[0].Titles[0].OpenCommand.ExecuteAsync(null);
        Assert.Equal("anilist:2", vm.SelectedMedia!.Id);

        await vm.GoBackCommand.ExecuteAsync(null);
        Assert.Equal("anilist:1", vm.SelectedMedia!.Id);
        await vm.GoBackCommand.ExecuteAsync(null);
        Assert.False(vm.IsDetailVisible);
    }

    [Fact]
    public async Task RelatedTitles_ErrorStaysInItsSection_AndRetryBypassesCache()
    {
        var relations = new ControlledRelationsService();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), new TwoTitleCatalog(), relations: relations);
        await vm.Initialization;
        await vm.TrendingItems[0].OpenDetailCommand.ExecuteAsync(null);
        relations.Requests[0].Completion.SetResult(Result<RelatedTitlesResult>.Failure(new("unavailable", "Try later")));
        await vm.RelatedTitlesLoad;
        var section = vm.SelectedMedia!.RelatedTitles!;
        Assert.True(section.CanRetry);
        Assert.False(vm.Notification.IsVisible);
        var retry = section.RetryCommand.ExecuteAsync(null);
        Assert.True(relations.Requests[1].ForceRefresh);
        relations.Complete(1, "anilist:2");
        await retry;
        Assert.Single(section.Groups);
        Assert.False(section.CanRetry);
    }

    [Fact]
    public async Task LeavingDetail_CancelsRelatedLoad_AndLateErrorIsIgnored()
    {
        var relations = new ControlledRelationsService();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), new TwoTitleCatalog(), relations: relations);
        await vm.Initialization;
        await vm.TrendingItems[0].OpenDetailCommand.ExecuteAsync(null);
        var section = vm.SelectedMedia!.RelatedTitles!;
        var load = vm.RelatedTitlesLoad;
        vm.NavigateToLibraryCommand.Execute(null);
        Assert.True(relations.Requests[0].Token.IsCancellationRequested);
        relations.Requests[0].Completion.SetException(new HttpRequestException());
        await load;
        Assert.Null(vm.SelectedMedia);
        Assert.False(section.CanRetry);
        Assert.False(vm.Notification.IsVisible);
    }

    private sealed class ControlledRelationsService : IMediaRelationsService
    {
        public bool CompleteImmediately { get; init; }
        public List<RelationRequest> Requests { get; } = [];

        public Task<Result<RelatedTitlesResult>> GetRelatedTitlesAsync(string mediaId, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            var request = new RelationRequest(forceRefresh, cancellationToken, new());
            Requests.Add(request);
            if (CompleteImmediately) Complete(Requests.Count - 1, "anilist:2");
            return request.Completion.Task;
        }

        public void Complete(int index, string targetId) => Requests[index].Completion.SetResult(
            Result<RelatedTitlesResult>.Success(new([
                new(targetId, "Next title", MediaType.Anime, MediaFormat.Tv, MediaRelationKind.Sequel, "AniList", "SEQUEL")])));
    }

    private sealed record RelationRequest(bool ForceRefresh, CancellationToken Token,
        TaskCompletionSource<Result<RelatedTitlesResult>> Completion);
}
