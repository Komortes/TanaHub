using TanaHub.Application.Common;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Tests;

public sealed partial class MainWindowViewModelRecognitionTests
{
    [Fact]
    public async Task SelectingCurrentSection_ClosesItsTitleDetail()
    {
        var library = new FakeUserLibraryService
        { Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current) };
        using var vm = CreateViewModel(new FakeFileOpenService(new()), library: library);
        await vm.Initialization;
        vm.NavigateToLibraryCommand.Execute(null);
        await vm.LibraryEntries[0].OpenDetailCommand.ExecuteAsync(null);
        Assert.True(vm.IsDetailVisible);

        vm.NavigateToLibraryCommand.Execute(null);

        Assert.False(vm.IsDetailVisible);
        Assert.Null(vm.SelectedMedia);
        Assert.True(vm.IsLibraryVisible);
        Assert.Equal("Library", vm.CurrentPageTitle);
    }

    [Fact]
    public async Task SelectingCurrentSection_CancelsPendingTitleNavigation()
    {
        var library = new FakeUserLibraryService
        { Entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current) };
        var catalog = new TwoTitleCatalog();
        using var vm = CreateViewModel(new FakeFileOpenService(new()), catalog, library);
        await vm.Initialization;
        vm.NavigateToLibraryCommand.Execute(null);
        catalog.PendingDetail = new TaskCompletionSource<Result<MediaItem>>();
        var opening = vm.LibraryEntries[0].OpenDetailCommand.ExecuteAsync(null);

        vm.NavigateToLibraryCommand.Execute(null);
        catalog.PendingDetail.SetResult(Result<MediaItem>.Success(
            new Anime("anilist:1", new MediaTitle("One"), MediaFormat.Tv, MediaReleaseStatus.Finished)));
        await opening;

        Assert.True(vm.IsLibraryVisible);
        Assert.False(vm.IsDetailVisible);
        Assert.Null(vm.SelectedMedia);
    }
}
