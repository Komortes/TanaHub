using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Catalog;

namespace TanaHub.Infrastructure.Tests;

public sealed class CachedMediaRelationsServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "TanaHub.Tests", Guid.NewGuid().ToString("N"));
    private string CachePath => Path.Combine(directory, "relations_cache.json");

    [Theory]
    [InlineData("anilist:1", false)]
    [InlineData("mal:anime:1", false)]
    [InlineData("mal:manga:1", false)]
    [InlineData("MangaDex:some-id", true)]
    public async Task RoutesByMediaIdRegardlessOfCatalogPreference(string mediaId, bool expectedMangaDex)
    {
        var service = new CachedMediaRelationsService(new FakeProvider("AniList"), new FakeProvider("MangaDex"), CachePath);

        var result = await service.GetRelatedTitlesAsync(mediaId);

        Assert.Equal(expectedMangaDex ? "MangaDex" : "AniList", Assert.Single(result.Value!.Titles).Source);
    }

    [Fact]
    public async Task CompleteEmptyResultsSurviveRestartAndAvoidUnnecessaryRequests()
    {
        var provider = new FakeProvider { Response = Result<RelatedTitlesResult>.Success(new([])) };
        var service = Create(provider);
        Assert.Empty((await service.GetRelatedTitlesAsync("anilist:1")).Value!.Titles);

        var offline = new FakeProvider { Response = Failure() };
        var reloaded = Create(offline);
        var result = await reloaded.GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Titles);
        Assert.False(result.Value.IsStale);
        Assert.Equal(0, offline.RequestCount);
    }

    [Fact]
    public async Task RefreshReplacesFreshCachedRelations()
    {
        var provider = new FakeProvider("Original");
        var service = Create(provider);
        await service.GetRelatedTitlesAsync("anilist:1");
        provider.Response = Success("Updated");

        var cached = await service.GetRelatedTitlesAsync("anilist:1");
        var refreshed = await service.GetRelatedTitlesAsync("anilist:1", forceRefresh: true);

        Assert.Equal("Original", Assert.Single(cached.Value!.Titles).Source);
        Assert.Equal("Updated", Assert.Single(refreshed.Value!.Titles).Source);
        Assert.True(provider.LastForceRefresh);
        Assert.Equal(2, provider.RequestCount);
    }

    [Fact]
    public async Task ExpiredRelationsFallBackWithNoticeWhenProviderIsOffline()
    {
        var clock = new TestClock();
        var provider = new FakeProvider("Saved");
        var service = Create(provider, clock);
        await service.GetRelatedTitlesAsync("anilist:1");
        clock.Advance(TimeSpan.FromDays(2));
        provider.Response = Failure();

        var result = await service.GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsStale);
        Assert.Contains("saved", result.Value.Notice!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Saved", Assert.Single(result.Value.Titles).Source);
        Assert.Equal(2, provider.RequestCount);
    }

    [Fact]
    public async Task FailuresAreRetriedAndDoNotBecomeEmptySuccess()
    {
        var provider = new FakeProvider { Response = Failure() };
        var service = Create(provider);
        Assert.True((await service.GetRelatedTitlesAsync("anilist:1")).IsFailure);
        provider.Response = Success("Recovered");

        var result = await service.GetRelatedTitlesAsync("anilist:1");

        Assert.Equal("Recovered", Assert.Single(result.Value!.Titles).Source);
        Assert.Equal(2, provider.RequestCount);
    }

    [Fact]
    public async Task PartialResultsDoNotOverwriteCompleteCachedRelations()
    {
        var provider = new FakeProvider("Complete");
        var service = Create(provider);
        await service.GetRelatedTitlesAsync("anilist:1");
        provider.Response = Result<RelatedTitlesResult>.Success(new([], "An additional source could not be loaded."));
        var partial = await service.GetRelatedTitlesAsync("anilist:1", forceRefresh: true);
        Assert.NotNull(partial.Value!.Notice);

        var offline = new FakeProvider { Response = Failure() };
        var reloaded = Create(offline);
        var saved = await reloaded.GetRelatedTitlesAsync("anilist:1");

        Assert.Equal("Complete", Assert.Single(saved.Value!.Titles).Source);
        Assert.Equal(0, offline.RequestCount);
    }

    [Fact]
    public async Task PartialEmptyResultsAreRetriedUntilComplete()
    {
        var provider = new FakeProvider { Response = Result<RelatedTitlesResult>.Success(new([], "Additional source unavailable.")) };
        var service = Create(provider);
        await service.GetRelatedTitlesAsync("anilist:1");
        provider.Response = Success("Recovered");

        var result = await service.GetRelatedTitlesAsync("anilist:1");

        Assert.Equal("Recovered", Assert.Single(result.Value!.Titles).Source);
        Assert.Equal(2, provider.RequestCount);
    }

    [Fact]
    public async Task DefinitivelyMissingTitleDoesNotFallBackToStaleRelations()
    {
        var provider = new FakeProvider("Saved");
        var service = Create(provider);
        await service.GetRelatedTitlesAsync("anilist:1");
        provider.Response = Result<RelatedTitlesResult>.Failure(ApplicationError.NotFound("Title removed."));

        var result = await service.GetRelatedTitlesAsync("anilist:1", forceRefresh: true);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task DisabledCacheNeitherUsesSavedDataNorWritesNewData()
    {
        var provider = new FakeProvider("Saved");
        await Create(provider).GetRelatedTitlesAsync("anilist:1");
        var savedContent = await File.ReadAllTextAsync(CachePath);
        provider.Response = Failure();
        var service = Create(provider);
        service.SetOfflineCacheEnabled(false);
        Assert.True((await service.GetRelatedTitlesAsync("anilist:1")).IsFailure);
        provider.Response = Success("Unsaved");
        await service.GetRelatedTitlesAsync("anilist:2");

        Assert.Equal(savedContent, await File.ReadAllTextAsync(CachePath));
        service.SetOfflineCacheEnabled(true);
        Assert.Equal("Saved", Assert.Single((await service.GetRelatedTitlesAsync("anilist:1")).Value!.Titles).Source);
    }

    [Fact]
    public async Task CorruptCacheDoesNotPreventLoadingAndSavingRelations()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(CachePath, "{malformed");
        var result = await Create(new FakeProvider("Recovered")).GetRelatedTitlesAsync("anilist:1");
        var reloaded = Create(new FakeProvider { Response = Failure() });

        Assert.Equal("Recovered", Assert.Single(result.Value!.Titles).Source);
        Assert.True((await reloaded.GetRelatedTitlesAsync("anilist:1")).IsSuccess);
    }

    [Fact]
    public async Task UnwritableCacheDoesNotMaskSuccessfulProviderResponse()
    {
        Directory.CreateDirectory(CachePath);

        var result = await Create(new FakeProvider("Live")).GetRelatedTitlesAsync("anilist:1");

        Assert.Equal("Live", Assert.Single(result.Value!.Titles).Source);
    }

    [Fact]
    public async Task ConcurrentRequestsPersistEverySuccessfulResult()
    {
        var service = Create(new FakeProvider("Saved"));
        await Task.WhenAll(Enumerable.Range(1, 20).Select(id => service.GetRelatedTitlesAsync($"anilist:{id}")));
        var offline = new FakeProvider { Response = Failure() };
        var reloaded = Create(offline);

        var results = await Task.WhenAll(Enumerable.Range(1, 20).Select(id => reloaded.GetRelatedTitlesAsync($"anilist:{id}")));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(0, offline.RequestCount);
    }

    [Fact]
    public async Task CacheEvictsOldestEntriesBeyondItsBound()
    {
        var clock = new TestClock();
        var service = Create(new FakeProvider("Saved"), clock);
        for (var index = 0; index <= CachedMediaRelationsService.MaxCacheEntries; index++)
        {
            await service.GetRelatedTitlesAsync($"anilist:{index}");
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        var offline = Create(new FakeProvider { Response = Failure() }, clock);

        Assert.True((await offline.GetRelatedTitlesAsync("anilist:0")).IsFailure);
        Assert.True((await offline.GetRelatedTitlesAsync($"anilist:{CachedMediaRelationsService.MaxCacheEntries}")).IsSuccess);
    }

    [Fact]
    public async Task CancellationIsPropagatedInsteadOfReturningSavedData()
    {
        var provider = new FakeProvider("Saved");
        var service = Create(provider);
        await service.GetRelatedTitlesAsync("anilist:1");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetRelatedTitlesAsync("anilist:1", cancellationToken: cancellation.Token));
    }

    private CachedMediaRelationsService Create(FakeProvider provider, TimeProvider? clock = null)
        => new(provider, new FakeProvider("MangaDex"), CachePath, clock);

    private static Result<RelatedTitlesResult> Success(string source) => Result<RelatedTitlesResult>.Success(new([
        new("anilist:2", "A sequel", MediaType.Anime, MediaFormat.Tv, MediaRelationKind.Sequel, source, "SEQUEL")
        { EpisodeCount = 12, PosterUri = new Uri("https://example.org/poster.jpg") }
    ]));

    private static Result<RelatedTitlesResult> Failure()
        => Result<RelatedTitlesResult>.Failure(ApplicationError.ExternalService("Provider is unavailable."));

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private sealed class FakeProvider(string source = "AniList") : IMediaRelationsService
    {
        public Result<RelatedTitlesResult> Response { get; set; } = Success(source);
        public int RequestCount { get; private set; }
        public bool LastForceRefresh { get; private set; }

        public Task<Result<RelatedTitlesResult>> GetRelatedTitlesAsync(string mediaId, bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            LastForceRefresh = forceRefresh;
            return Task.FromResult(Response);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
