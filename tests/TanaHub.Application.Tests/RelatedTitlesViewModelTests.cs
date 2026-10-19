using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.UI.ViewModels;

namespace TanaHub.Application.Tests;

public sealed class RelatedTitlesViewModelTests
{
    [Fact]
    public async Task MangaAlias_OpensExistingLibraryKeyAndUsesReadingUnits()
    {
        var title = Title(MediaRelationKind.Source) with
        { MediaId = "anilist:53390", MediaType = MediaType.Manga, Format = MediaFormat.Manga, MalId = 23390, ChapterCount = 139 };
        string? opened = null;
        var card = new RelatedTitleViewModel(title,
            [new("mal:anime:23390", MediaType.Anime, MediaListStatus.Completed),
             new("mal:manga:23390", MediaType.Manga, MediaListStatus.Current) { Progress = 20 }],
            id => { opened = id; return Task.CompletedTask; });

        await card.OpenCommand.ExecuteAsync(null);

        Assert.Equal("mal:manga:23390", opened);
        Assert.Equal("Reading · 20 ch.", card.LibraryLabel);
        Assert.Contains("139 ch.", card.Metadata);
    }

    [Fact]
    public async Task DifferentMangaEdition_DoesNotOpenTheSharedAniListLibraryEntry()
    {
        var title = Title(MediaRelationKind.Colored) with
        {
            MediaId = "mangadex:edition",
            MangaDexId = "edition",
            AniListId = 2,
            MediaType = MediaType.Manga,
            Format = MediaFormat.Manga
        };
        string? opened = null;
        var card = new RelatedTitleViewModel(title,
            [new("anilist:2", MediaType.Manga, MediaListStatus.Completed)],
            id => { opened = id; return Task.CompletedTask; });
        await card.OpenCommand.ExecuteAsync(null);
        Assert.Equal("mangadex:edition", opened);
        Assert.Empty(card.LibraryLabel);
    }

    [Fact]
    public void Groups_KeepMainConnectionsVisibleAndFanWorksCollapsed()
    {
        var vm = new RelatedTitlesViewModel(() => Task.CompletedTask);
        vm.Apply(new RelatedTitlesResult([
            Title(MediaRelationKind.Doujinshi), Title(MediaRelationKind.Adaptation),
            Title(MediaRelationKind.Character), Title(MediaRelationKind.Sequel), Title(MediaRelationKind.Prequel)]),
            [], _ => Task.CompletedTask);

        Assert.Equal("Previous & next parts", vm.Groups[0].Title);
        Assert.Equal(MediaRelationKind.Prequel, vm.Groups[0].Titles[0].Kind);
        Assert.True(vm.Groups[0].IsExpanded);
        Assert.False(vm.Groups[^1].IsExpanded);
        Assert.Equal("Fan works", vm.Groups[^1].Title);
    }

    [Fact]
    public void EmptySuccess_IsDifferentFromAnErrorAndRetryKeepsExistingCards()
    {
        var vm = new RelatedTitlesViewModel(() => Task.CompletedTask);
        vm.Apply(new RelatedTitlesResult([]), [], _ => Task.CompletedTask);
        Assert.Contains("No related titles", vm.StatusMessage);
        Assert.False(vm.CanRetry);
        vm.Apply(new RelatedTitlesResult([Title(MediaRelationKind.Sequel)], "Saved data", true), [], _ => Task.CompletedTask);
        Assert.True(vm.CanRetry);
        vm.BeginLoading();
        Assert.Single(vm.Groups);
        Assert.True(vm.IsLoading);
        Assert.True(vm.CanRetry);
        vm.ShowError("Temporarily unavailable");
        Assert.Single(vm.Groups);
        Assert.True(vm.CanRetry);
        Assert.False(vm.IsLoading);
    }

    private static RelatedTitle Title(MediaRelationKind kind) =>
        new("anilist:2", "Related", MediaType.Anime, MediaFormat.Tv, kind, "AniList", kind.ToString());
}
