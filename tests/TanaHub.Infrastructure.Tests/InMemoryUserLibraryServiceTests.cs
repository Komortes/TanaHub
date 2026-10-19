using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Library;

namespace TanaHub.Infrastructure.Tests;

public sealed class InMemoryUserLibraryServiceTests
{
    [Fact]
    public async Task UpdateStatusAsync_PreservesAnExplicitCompletionDate()
    {
        var completion = DateTimeOffset.UtcNow.AddDays(-20);
        var service = new InMemoryUserLibraryService([new("anilist:1", MediaType.Anime, MediaListStatus.Paused) { CompletedAt = completion, Progress = 3 }]);
        var result = await service.UpdateStatusAsync("anilist:1", MediaListStatus.Completed);
        Assert.True(result.IsSuccess);
        Assert.Equal(completion, result.Value!.CompletedAt);
        Assert.Equal(3, result.Value.Progress);
    }

    [Fact]
    public async Task UpdateStatusAsync_DoesNotInferDatesThatContradictTheSavedDate()
    {
        var future = DateTimeOffset.UtcNow.AddDays(20);
        var past = DateTimeOffset.UtcNow.AddDays(-20);
        var service = new InMemoryUserLibraryService([
            new("future", MediaType.Anime, MediaListStatus.Planning) { StartedAt = future },
            new("past", MediaType.Anime, MediaListStatus.Completed) { CompletedAt = past }
        ]);
        var finished = await service.UpdateStatusAsync("future", MediaListStatus.Completed);
        var started = await service.UpdateStatusAsync("past", MediaListStatus.Current);
        Assert.True(finished.IsSuccess);
        Assert.Null(finished.Value!.CompletedAt);
        Assert.Equal(future, finished.Value.StartedAt);
        Assert.True(started.IsSuccess);
        Assert.Null(started.Value!.StartedAt);
        Assert.Equal(past, started.Value.CompletedAt);
    }

    [Theory]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MaxValue - 1, 2)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    public async Task IncrementProgressAsync_RejectsInvalidAmountsWithoutChangingSavedEntry(int progress, int amount)
    {
        var original = new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current) { Progress = progress, Score = 9, Notes = "Keep notes" };
        var service = new InMemoryUserLibraryService([original]);
        var result = await service.IncrementProgressAsync(original.MediaId, amount);
        Assert.True(result.IsFailure);
        Assert.Equal("validation_error", result.Error.Code);
        Assert.Equal(original, (await service.GetEntryAsync(original.MediaId)).Value);
    }

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

    [Fact]
    public async Task UpdateReviewAsync_SavesTrimmedReview()
    {
        var service = new InMemoryUserLibraryService([
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
        ]);

        var result = await service.UpdateReviewAsync("anilist:1", "  Excellent pacing.  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Excellent pacing.", result.Value!.Review);
    }

    [Fact]
    public async Task UpdateReviewAsync_ClearsWhitespaceOnlyReview()
    {
        var service = new InMemoryUserLibraryService([
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
            {
                Review = "Old review"
            }
        ]);

        var result = await service.UpdateReviewAsync("anilist:1", "  \n ");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Review);
    }

    [Fact]
    public async Task UpdateReviewAsync_RejectsReviewLongerThanFourThousandCharacters()
    {
        var service = new InMemoryUserLibraryService([
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
            {
                Review = "Kept review"
            }
        ]);

        var result = await service.UpdateReviewAsync("anilist:1", new string('a', 4001));
        var entry = await service.GetEntryAsync("anilist:1");

        Assert.True(result.IsFailure);
        Assert.Equal("validation_error", result.Error.Code);
        Assert.Equal("Kept review", entry.Value!.Review);
    }

    [Fact]
    public async Task UpdateReviewAsync_ReturnsNotFoundForMissingEntry()
    {
        var result = await new InMemoryUserLibraryService().UpdateReviewAsync("missing", "Review");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
