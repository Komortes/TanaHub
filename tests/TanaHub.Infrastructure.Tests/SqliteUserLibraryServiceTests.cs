using Microsoft.EntityFrameworkCore;
using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Library;

namespace TanaHub.Infrastructure.Tests;

public sealed class SqliteUserLibraryServiceTests
{
    [Fact]
    public async Task UpsertEntryAsync_PersistsEntryToDisk()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);

        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current)
        {
            Progress = 4,
            Score = 8,
            PosterUri = new Uri("https://example.test/poster.jpg")
        });

        var reloaded = CreateService(dbPath);
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
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath, [
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
        ]);

        await service.UpsertEntryAsync(new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current));
        await service.RemoveEntryAsync("anilist:1");

        var reloaded = CreateService(dbPath);
        var result = await reloaded.GetEntriesAsync(new UserLibraryQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
    }

    [Fact]
    public async Task UpsertEntryAsync_PersistsTagsAndCustomLists()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);

        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current)
        {
            Tags = ["rewatch", "comfort"],
            CustomLists = ["Friday queue", "Short episodes"]
        });

        var reloaded = CreateService(dbPath);
        var result = await reloaded.GetEntriesAsync(new UserLibraryQuery());

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Value!.Items);
        Assert.Equal(["rewatch", "comfort"], entry.Tags);
        Assert.Equal(["Friday queue", "Short episodes"], entry.CustomLists);
    }

    [Fact]
    public async Task GetEntriesAsync_FiltersByTagAndCustomList()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath, [
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
    public async Task GetEntryAsync_ReturnsPersistedEntryCaseInsensitively()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);
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
    public async Task UpsertEntryAsync_WithDifferentCasingUpdatesExistingRowInstadOfDuplicating()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);
        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current) { Progress = 1 });

        await service.UpsertEntryAsync(new UserMediaEntry("ANILIST:20", MediaType.Anime, MediaListStatus.Current) { Progress = 5 });

        var result = await service.GetEntriesAsync(new UserLibraryQuery());
        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Value!.Items);
        Assert.Equal("anilist:20", entry.MediaId);
        Assert.Equal(5, entry.Progress);
    }

    [Fact]
    public async Task UpdateOrganizationAsync_PersistsNormalizedTagsAndCustomLists()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);
        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current));

        var updated = await service.UpdateOrganizationAsync(
            "anilist:20",
            [" rewatch ", "comfort", "REWATCH", ""],
            ["Friday queue", " friday queue ", "Short episodes"]);

        var reloaded = CreateService(dbPath);
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
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);
        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current));

        var updated = await service.UpdateReviewAsync("anilist:20", "A focused rewatch.");
        var reloaded = CreateService(dbPath);
        var entry = await reloaded.GetEntryAsync("anilist:20");

        Assert.True(updated.IsSuccess);
        Assert.True(entry.IsSuccess);
        Assert.Equal("A focused rewatch.", entry.Value!.Review);
    }

    [Fact]
    public async Task UpdateReviewAsync_RejectsReviewsOverFourThousandCharacters()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);
        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current));

        var result = await service.UpdateReviewAsync("anilist:20", new string('a', 4001));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpdateScoreAsync_RejectsScoreOutsideZeroToTen()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);
        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current));

        var result = await service.UpdateScoreAsync("anilist:20", 11);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetEntriesAsync_RejectsNonPositivePageOrPageSize()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);

        var result = await service.GetEntriesAsync(new UserLibraryQuery { Page = 0 });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RemoveEntryAsync_ReturnsFalseWhenEntryIsMissing()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);

        var result = await service.RemoveEntryAsync("anilist:404");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task MergeEntriesAsync_PreservesLocalOnlyFieldsAndPersistsIncomingProgress()
    {
        var dbPath = CreateDbPath();
        var service = CreateService(dbPath);
        await service.UpsertEntryAsync(new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Current)
        {
            Progress = 3,
            Notes = "Old remote note",
            Review = "Keep this review",
            Tags = ["favorite"],
            CustomLists = ["Weekend"]
        });

        var merged = await service.MergeEntriesAsync([
            new UserMediaEntry("anilist:20", MediaType.Anime, MediaListStatus.Completed)
            {
                Progress = 12,
                Score = 9,
                Notes = "Updated remote note"
            }
        ]);

        var reloaded = await CreateService(dbPath).GetEntryAsync("anilist:20");

        Assert.True(merged.IsSuccess);
        Assert.Equal(1, merged.Value);
        Assert.True(reloaded.IsSuccess);
        Assert.Equal(MediaListStatus.Completed, reloaded.Value!.Status);
        Assert.Equal(12, reloaded.Value.Progress);
        Assert.Equal(9, reloaded.Value.Score);
        Assert.Equal("Updated remote note", reloaded.Value.Notes);
        Assert.Equal("Keep this review", reloaded.Value.Review);
        Assert.Equal(["favorite"], reloaded.Value.Tags);
        Assert.Equal(["Weekend"], reloaded.Value.CustomLists);
    }

    [Fact]
    public async Task ImportFromJsonIfNeededAsync_ImportsLegacyEntriesAndBacksUpOriginalFile()
    {
        var dbPath = CreateDbPath();
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var jsonPath = Path.Combine(Path.GetDirectoryName(dbPath)!, "library.json");
        await File.WriteAllTextAsync(jsonPath, """
            [{"MediaId":"anilist:20","MediaType":"Anime","Status":"Current","Progress":4,"Score":8,"PosterUri":null,"Notes":null,"Review":null,"Tags":[],"CustomLists":[],"StartedAt":null,"CompletedAt":null,"UpdatedAt":"2026-01-01T00:00:00+00:00"}]
            """);
        var options = new DbContextOptionsBuilder<LibraryDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using (var db = new LibraryDbContext(options))
        {
            await db.Database.MigrateAsync();
            await SqliteUserLibraryService.ImportFromJsonIfNeededAsync(db, jsonPath);
        }

        var service = CreateService(dbPath);
        var result = await service.GetEntryAsync("anilist:20");

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Progress);
        Assert.False(File.Exists(jsonPath));
        Assert.True(File.Exists(jsonPath + ".bak"));
    }

    [Fact]
    public async Task ImportFromJsonIfNeededAsync_DoesNothingWhenDatabaseAlreadyHasRows()
    {
        var dbPath = CreateDbPath();
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var jsonPath = Path.Combine(Path.GetDirectoryName(dbPath)!, "library.json");
        await File.WriteAllTextAsync(jsonPath, """[{"MediaId":"anilist:99","MediaType":"Anime","Status":"Current","Progress":1,"Score":null,"PosterUri":null,"Notes":null,"Review":null,"Tags":[],"CustomLists":[],"StartedAt":null,"CompletedAt":null,"UpdatedAt":"2026-01-01T00:00:00+00:00"}]""");
        var options = new DbContextOptionsBuilder<LibraryDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using (var db = new LibraryDbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Entries.Add(new UserMediaEntryEntity { MediaId = "anilist:1", MediaType = "Anime", Status = "Current", UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            await SqliteUserLibraryService.ImportFromJsonIfNeededAsync(db, jsonPath);
        }

        Assert.True(File.Exists(jsonPath));
    }

    private static SqliteUserLibraryService CreateService(string dbPath, IEnumerable<UserMediaEntry>? seed = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var factory = new TestDbContextFactory(options);
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        var service = new SqliteUserLibraryService(factory);
        if (seed is not null)
        {
            foreach (var entry in seed)
                service.UpsertEntryAsync(entry).GetAwaiter().GetResult();
        }
        return service;
    }

    private static string CreateDbPath() =>
        Path.Combine(Path.GetTempPath(), "TanaHub.Tests", $"{Guid.NewGuid():N}", "library.db");

    private sealed class TestDbContextFactory(DbContextOptions<LibraryDbContext> options)
        : IDbContextFactory<LibraryDbContext>
    {
        public LibraryDbContext CreateDbContext() => new(options);

        public Task<LibraryDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
