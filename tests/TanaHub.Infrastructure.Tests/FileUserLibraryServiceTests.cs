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

    private static string CreateStoragePath()
    {
        return Path.Combine(Path.GetTempPath(), "TanaHub.Tests", $"{Guid.NewGuid():N}", "library.json");
    }
}
