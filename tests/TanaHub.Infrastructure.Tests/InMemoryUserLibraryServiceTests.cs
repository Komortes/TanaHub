using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Library;

namespace TanaHub.Infrastructure.Tests;

public sealed class InMemoryUserLibraryServiceTests
{
    [Fact]
    public async Task UpsertEntryAsync_AddsEntryToLibrary()
    {
        var service = new InMemoryUserLibraryService();
        var entry = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current);

        await service.UpsertEntryAsync(entry);
        var result = await service.GetEntriesAsync(new UserLibraryQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
    }

    [Fact]
    public async Task IncrementProgressAsync_UpdatesExistingEntry()
    {
        var service = new InMemoryUserLibraryService([
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
        ]);

        var result = await service.IncrementProgressAsync("anilist:1");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Progress);
    }

    [Fact]
    public async Task IncrementProgressAsync_ReturnsNotFoundForMissingEntry()
    {
        var service = new InMemoryUserLibraryService();

        var result = await service.IncrementProgressAsync("missing");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task UpdateStatusAsync_UpdatesExistingEntry()
    {
        var service = new InMemoryUserLibraryService([
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
        ]);

        var result = await service.UpdateStatusAsync("anilist:1", MediaListStatus.Completed);

        Assert.True(result.IsSuccess);
        Assert.Equal(MediaListStatus.Completed, result.Value!.Status);
        Assert.NotNull(result.Value.CompletedAt);
    }

    [Fact]
    public async Task UpdateScoreAsync_ValidatesScoreRange()
    {
        var service = new InMemoryUserLibraryService([
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
        ]);

        var result = await service.UpdateScoreAsync("anilist:1", 11);

        Assert.True(result.IsFailure);
        Assert.Equal("validation_error", result.Error.Code);
    }
}
