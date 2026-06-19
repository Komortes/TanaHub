using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Recognition;

namespace TanaHub.Infrastructure.Tests;

public sealed class FileRecognitionInboxServiceTests
{
    [Fact]
    public async Task SaveAsync_PersistsAttemptToDisk()
    {
        var storagePath = CreateStoragePath();
        var service = new FileRecognitionInboxService(storagePath);

        var attempt = new RecognitionAttempt
        {
            Id = "attempt-1",
            CreatedAt = new DateTimeOffset(2026, 6, 19, 12, 30, 0, TimeSpan.Zero),
            SourceName = "frame.png",
            SourcePath = "/tmp/frame.png",
            AniListId = 1,
            RomajiTitle = "Cowboy Bebop",
            EnglishTitle = "Cowboy Bebop",
            Episode = "5",
            Similarity = 0.94,
            ThumbnailUri = new Uri("https://example.test/thumb.jpg")
        };

        var saved = await service.SaveAsync(attempt);
        var reloaded = new FileRecognitionInboxService(storagePath);
        var recent = await reloaded.GetRecentAsync();

        Assert.True(saved.IsSuccess);
        Assert.True(recent.IsSuccess);
        var stored = Assert.Single(recent.Value!);
        Assert.Equal("attempt-1", stored.Id);
        Assert.Equal("anilist:1", stored.MediaId);
        Assert.Equal("frame.png", stored.SourceName);
        Assert.Equal("/tmp/frame.png", stored.SourcePath);
        Assert.Equal("Cowboy Bebop", stored.RomajiTitle);
        Assert.Equal("5", stored.Episode);
        Assert.Equal(0.94, stored.Similarity);
        Assert.Equal(new Uri("https://example.test/thumb.jpg"), stored.ThumbnailUri);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestAttemptsFirstAndHonorsLimit()
    {
        var storagePath = CreateStoragePath();
        var service = new FileRecognitionInboxService(storagePath);

        await service.SaveAsync(new RecognitionAttempt
        {
            Id = "old",
            CreatedAt = new DateTimeOffset(2026, 6, 19, 10, 0, 0, TimeSpan.Zero),
            SourceName = "old.png"
        });
        await service.SaveAsync(new RecognitionAttempt
        {
            Id = "new",
            CreatedAt = new DateTimeOffset(2026, 6, 19, 11, 0, 0, TimeSpan.Zero),
            SourceName = "new.png"
        });

        var recent = await service.GetRecentAsync(limit: 1);

        Assert.True(recent.IsSuccess);
        var attempt = Assert.Single(recent.Value!);
        Assert.Equal("new", attempt.Id);
    }

    [Fact]
    public async Task RemoveAsync_PersistsRemoval()
    {
        var storagePath = CreateStoragePath();
        var service = new FileRecognitionInboxService(storagePath);

        await service.SaveAsync(new RecognitionAttempt
        {
            Id = "attempt-1",
            SourceName = "frame.png"
        });

        var removed = await service.RemoveAsync("attempt-1");
        var reloaded = new FileRecognitionInboxService(storagePath);
        var recent = await reloaded.GetRecentAsync();

        Assert.True(removed.IsSuccess);
        Assert.True(removed.Value);
        Assert.True(recent.IsSuccess);
        Assert.Empty(recent.Value!);
    }

    [Fact]
    public async Task GetRecentAsync_RejectsInvalidLimit()
    {
        var service = new FileRecognitionInboxService(CreateStoragePath());

        var result = await service.GetRecentAsync(limit: 0);

        Assert.True(result.IsFailure);
    }

    private static string CreateStoragePath()
    {
        return Path.Combine(Path.GetTempPath(), "TanaHub.Tests", $"{Guid.NewGuid():N}", "recognition_inbox.json");
    }
}
