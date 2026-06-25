using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Library;

namespace TanaHub.Infrastructure.Tests;

public sealed class FileUserLibraryServiceTests
{
    [Fact]
    public async Task UpsertEntryAsync_PersistsEntryToDisk()
    {
        var storagePath = CreateStoragePath();
        var service = new FileUserLibraryService(storagePath);

        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current)
        {
            Progress = 4,
            Score = 8,
            PosterUri = new Uri("https://example.test/poster.jpg")
        });

        var reloaded = new FileUserLibraryService(storagePath);
        var result = await reloaded.GetEntriesAsync(new UserLibraryQuery());

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Value!.Items);
        Assert.Equal("anilist:20", entry.MediaId);
        Assert.Equal(4, entry.Progress);
        Assert.Equal(8, entry.Score);
        Assert.Equal(new Uri("https://example.test/poster.jpg"), entry.PosterUri);
    }

    [Fact]
    public async Task RemoveEntryAsync_PersistsRemoval()
    {
        var storagePath = CreateStoragePath();
        var service = new FileUserLibraryService(storagePath, [
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
        ]);

        await service.UpsertEntryAsync(new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current));
        await service.RemoveEntryAsync("anilist:1");

        var reloaded = new FileUserLibraryService(storagePath);
        var result = await reloaded.GetEntriesAsync(new UserLibraryQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
    }

    [Fact]
    public async Task UpsertEntryAsync_PersistsTagsAndCustomLists()
    {
        var storagePath = CreateStoragePath();
        var service = new FileUserLibraryService(storagePath);

        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current)
        {
            Tags = ["rewatch", "comfort"],
            CustomLists = ["Friday queue", "Short episodes"]
        });

        var reloaded = new FileUserLibraryService(storagePath);
        var result = await reloaded.GetEntriesAsync(new UserLibraryQuery());

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Value!.Items);
        Assert.Equal(["rewatch", "comfort"], entry.Tags);
        Assert.Equal(["Friday queue", "Short episodes"], entry.CustomLists);
    }

    [Fact]
    public async Task GetEntriesAsync_FiltersByTagAndCustomList()
    {
        var storagePath = CreateStoragePath();
        var service = new FileUserLibraryService(storagePath, [
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
            {
                Tags = ["rewatch"],
                CustomLists = ["Friday queue"]
            },
            new UserMediaEntry("anilist:2", MediaType.Anime, MediaListStatus.Current)
            {
                Tags = ["seasonal"],
                CustomLists = ["Backlog"]
            }
        ]);

        var byTag = await service.GetEntriesAsync(new UserLibraryQuery { Tag = "REWATCH" });
        var byList = await service.GetEntriesAsync(new UserLibraryQuery { CustomList = "friday queue" });

        Assert.True(byTag.IsSuccess);
        Assert.Equal("anilist:1", Assert.Single(byTag.Value!.Items).MediaId);
        Assert.True(byList.IsSuccess);
        Assert.Equal("anilist:1", Assert.Single(byList.Value!.Items).MediaId);
    }

    [Fact]
    public async Task GetEntryAsync_ReturnsPersistedEntryWithoutApplyingListFilters()
    {
        var storagePath = CreateStoragePath();
        var service = new FileUserLibraryService(storagePath);
        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Completed)
        {
            Progress = 12,
            Score = 9,
            Notes = "Kept after status changes"
        });

        var entry = await service.GetEntryAsync("ANILIST:20");

        Assert.True(entry.IsSuccess);
        Assert.Equal(MediaListStatus.Completed, entry.Value!.Status);
        Assert.Equal(12, entry.Value.Progress);
        Assert.Equal(9, entry.Value.Score);
        Assert.Equal("Kept after status changes", entry.Value.Notes);
    }

    [Fact]
    public async Task UpdateOrganizationAsync_PersistsNormalizedTagsAndCustomLists()
    {
        var storagePath = CreateStoragePath();
        var service = new FileUserLibraryService(storagePath);
        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current));

        var updated = await service.UpdateOrganizationAsync(
            "anilist:20",
            [" rewatch ", "comfort", "REWATCH", ""],
            ["Friday queue", " friday queue ", "Short episodes"]);

        var reloaded = new FileUserLibraryService(storagePath);
        var entry = await reloaded.GetEntryAsync("anilist:20");

        Assert.True(updated.IsSuccess);
        Assert.Equal(["rewatch", "comfort"], updated.Value!.Tags);
        Assert.Equal(["Friday queue", "Short episodes"], updated.Value.CustomLists);
        Assert.True(entry.IsSuccess);
        Assert.Equal(updated.Value.Tags, entry.Value!.Tags);
        Assert.Equal(updated.Value.CustomLists, entry.Value.CustomLists);
    }

    [Fact]
    public async Task UpdateReviewAsync_PersistsReviewAcrossReload()
    {
        var storagePath = CreateStoragePath();
        var service = new FileUserLibraryService(storagePath);
        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current));

        var updated = await service.UpdateReviewAsync("anilist:20", "A focused rewatch.");
        var reloaded = new FileUserLibraryService(storagePath);
        var entry = await reloaded.GetEntryAsync("anilist:20");

        Assert.True(updated.IsSuccess);
        Assert.True(entry.IsSuccess);
        Assert.Equal("A focused rewatch.", entry.Value!.Review);
    }

    [Fact]
    public async Task GetEntriesAsync_LoadsLegacyJsonWithoutReview()
    {
        var storagePath = CreateStoragePath();
        Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
        await File.WriteAllTextAsync(storagePath, """
            [{"MediaId":"anilist:20","MediaType":"Anime","Status":"Current","Progress":1,"Score":8,"PosterUri":null,"Notes":null,"Tags":[],"CustomLists":[],"StartedAt":null,"CompletedAt":null,"UpdatedAt":"2026-01-01T00:00:00+00:00"}]
            """);

        var result = await new FileUserLibraryService(storagePath).GetEntryAsync("anilist:20");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Review);
    }

    private static string CreateStoragePath()
    {
        return Path.Combine(Path.GetTempPath(), "TanaHub.Tests", $"{Guid.NewGuid():N}", "library.json");
    }
}
