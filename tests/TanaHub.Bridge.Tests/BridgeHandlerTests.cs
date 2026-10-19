using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Bridge;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Catalog;
using TanaHub.Infrastructure.Library;

namespace TanaHub.Bridge.Tests;

public sealed class BridgeHandlerTests
{
    private static readonly Anime Violet = new("anilist:21827", new("Violet Evergarden"), MediaFormat.Tv, MediaReleaseStatus.Finished)
    {
        EpisodeCount = 13,
        AverageScore = 87,
        Images = new(new Uri("https://example.test/catalog.jpg"))
    };

    [Fact]
    public async Task LibraryListUsesCachedTitlesFiltersBeforePaginationAndKeepsPersistedPoster()
    {
        var library = new InMemoryUserLibraryService([
            new("anilist:21827", MediaType.Anime, MediaListStatus.Current)
            {
                Progress = 4, Score = 8, PosterUri = new("https://example.test/saved.jpg")
            },
            new("missing", MediaType.Manga, MediaListStatus.Planning)
        ]);
        var handler = new BridgeHandler(new OfflineCatalog(), library, id => id == Violet.Id ? Violet : null);

        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"list-1","method":"library.list","params":{"search":"violet","pageSize":1}}
            """));

        var result = response.RootElement.GetProperty("result");
        Assert.Equal(1, result.GetProperty("totalCount").GetInt32());
        Assert.False(result.GetProperty("hasNextPage").GetBoolean());
        var entry = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("Watching", entry.GetProperty("status").GetString());
        Assert.Equal(8, entry.GetProperty("score").GetInt32());
        Assert.Equal("Violet Evergarden", entry.GetProperty("media").GetProperty("title").GetString());
        Assert.Equal("https://example.test/saved.jpg", entry.GetProperty("media").GetProperty("posterUrl").GetString());
    }

    [Fact]
    public async Task LibraryListKeepsAnUncachedEntryAvailableOffline()
    {
        var library = new InMemoryUserLibraryService([
            new("unknown", MediaType.Manga, MediaListStatus.Repeating) { PosterUri = new("https://example.test/saved.jpg") }
        ]);
        var handler = new BridgeHandler(new OfflineCatalog(), library);

        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"list","method":"library.list","params":{}}
            """));

        var entry = Assert.Single(response.RootElement.GetProperty("result").GetProperty("items").EnumerateArray());
        Assert.Equal("Repeating", entry.GetProperty("status").GetString());
        Assert.Equal("unknown", entry.GetProperty("media").GetProperty("title").GetString());
        Assert.Equal("Manga", entry.GetProperty("media").GetProperty("type").GetString());
    }

    [Fact]
    public async Task CatalogResultsRemainAvailableToLibraryWithoutFurtherNetworkCalls()
    {
        var library = new InMemoryUserLibraryService([new(Violet.Id, MediaType.Anime, MediaListStatus.Current)]);
        var handler = new BridgeHandler(new InMemoryMediaCatalogService([Violet]), library);
        await handler.HandleAsync("""{"id":"search","method":"catalog.search","params":{}}""");

        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"list","method":"library.list","params":{}}
            """));

        var entry = Assert.Single(response.RootElement.GetProperty("result").GetProperty("items").EnumerateArray());
        Assert.Equal("Violet Evergarden", entry.GetProperty("media").GetProperty("title").GetString());
    }

    [Fact]
    public async Task ExplicitDetailRefreshesIncompleteLocalMetadataFromCatalog()
    {
        var fresh = Violet with { Description = "Full remote synopsis", Images = new(new Uri("https://example.test/fresh.jpg")) };
        var seed = Violet with { Description = null, Images = new() };
        var handler = new BridgeHandler(new InMemoryMediaCatalogService([fresh]),
            new InMemoryUserLibraryService(), localCatalog: new InMemoryMediaCatalogService([seed]));
        // Listing a local entry may populate the session with incomplete fallback metadata.
        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"detail","method":"media.detail","params":{"mediaId":"anilist:21827"}}
            """));

        var result = response.RootElement.GetProperty("result");
        Assert.Equal("Full remote synopsis", result.GetProperty("description").GetString());
        Assert.Equal("https://example.test/fresh.jpg", result.GetProperty("posterUrl").GetString());
    }

    [Fact]
    public async Task ExplicitDetailFallsBackToCachedMetadataAndPersistedPosterWhenOffline()
    {
        var library = new InMemoryUserLibraryService([
            new(Violet.Id, MediaType.Anime, MediaListStatus.Current) { PosterUri = new("https://example.test/saved.jpg") }
        ]);
        var cached = Violet with { Images = new() };
        var handler = new BridgeHandler(new UnavailableCatalog(), library, id => id == Violet.Id ? cached : null);

        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"detail","method":"media.detail","params":{"mediaId":"anilist:21827"}}
            """));

        var result = response.RootElement.GetProperty("result");
        Assert.Equal("Violet Evergarden", result.GetProperty("title").GetString());
        Assert.Equal("https://example.test/saved.jpg", result.GetProperty("posterUrl").GetString());
    }

    [Fact]
    public async Task ExplicitDetailKeepsPersistedEntryAvailableWhenRemoteDataIsMalformed()
    {
        var library = new InMemoryUserLibraryService([
            new("unknown", MediaType.Manga, MediaListStatus.Current) { PosterUri = new("https://example.test/saved.jpg") }
        ]);
        var handler = new BridgeHandler(new MalformedCatalog(), library);

        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"detail","method":"media.detail","params":{"mediaId":"unknown"}}
            """));

        var result = response.RootElement.GetProperty("result");
        Assert.Equal("unknown", result.GetProperty("title").GetString());
        Assert.Equal("Manga", result.GetProperty("type").GetString());
        Assert.Equal("https://example.test/saved.jpg", result.GetProperty("posterUrl").GetString());
    }

    [Fact]
    public async Task MalformedCatalogSearchIsReportedAsAnExternalFailure()
    {
        var handler = new BridgeHandler(new MalformedCatalog(), new InMemoryUserLibraryService());
        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"search","method":"catalog.search","params":{}}
            """));

        Assert.Equal("external_service_error", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("private details", response.RootElement.GetRawText());
    }

    [Fact]
    public async Task ProgressIncrementCannotOverflowOrChangeTheSavedValue()
    {
        var library = new InMemoryUserLibraryService([
            new(Violet.Id, MediaType.Anime, MediaListStatus.Current) { Progress = int.MaxValue }
        ]);
        var handler = new BridgeHandler(new OfflineCatalog(), library);
        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"overflow","method":"library.progress","params":{"mediaId":"anilist:21827"}}
            """));

        Assert.Equal("validation_error", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(int.MaxValue, (await library.GetEntryAsync(Violet.Id)).Value!.Progress);
    }

    [Fact]
    public async Task MutationsPreserveDataAndAcceptClearingAScore()
    {
        var library = new InMemoryUserLibraryService();
        var handler = new BridgeHandler(new InMemoryMediaCatalogService([Violet]), library);
        await handler.HandleAsync("""{"id":"add","method":"library.add","params":{"mediaId":"anilist:21827"}}""");
        await handler.HandleAsync("""{"id":"status","method":"library.status","params":{"mediaId":"anilist:21827","status":"Watching"}}""");
        await handler.HandleAsync("""{"id":"progress","method":"library.progress","params":{"mediaId":"anilist:21827","amount":3}}""");
        await handler.HandleAsync("""{"id":"score","method":"library.score","params":{"mediaId":"anilist:21827","score":9}}""");
        await handler.HandleAsync("""{"id":"notes","method":"library.notes","params":{"mediaId":"anilist:21827","notes":"Keep watching"}}""");
        // Adding an existing title must not erase progress, status, rating or notes.
        await handler.HandleAsync("""{"id":"add-again","method":"library.add","params":{"mediaId":"anilist:21827"}}""");

        var entry = (await library.GetEntryAsync(Violet.Id)).Value!;
        Assert.Equal(MediaListStatus.Current, entry.Status);
        Assert.Equal(3, entry.Progress);
        Assert.Equal(9, entry.Score);
        Assert.Equal("Keep watching", entry.Notes);
        Assert.Equal(Violet.Images.PosterUri, entry.PosterUri);

        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"clear","method":"library.score","params":{"mediaId":"anilist:21827","score":null}}
            """));
        Assert.Equal(JsonValueKind.Null, response.RootElement.GetProperty("result").GetProperty("score").ValueKind);
    }

    [Theory]
    [InlineData("library.score", "\"mediaId\":\"x\",\"score\":11")]
    [InlineData("library.score", "\"mediaId\":\"x\",\"score\":-1")]
    [InlineData("library.score", "\"mediaId\":\"x\",\"score\":7.5")]
    [InlineData("library.score", "\"mediaId\":\"x\"")]
    [InlineData("library.progress", "\"mediaId\":\"x\",\"amount\":0")]
    [InlineData("library.progress", "\"mediaId\":\"x\",\"amount\":2147483647")]
    [InlineData("library.status", "\"mediaId\":\"x\",\"status\":\"999\"")]
    [InlineData("library.list", "\"page\":0")]
    [InlineData("library.list", "\"pageSize\":101")]
    [InlineData("catalog.search", "\"sort\":\"Unknown\"")]
    [InlineData("media.detail", "\"mediaId\":\"  \"")]
    public async Task InvalidInputReturnsValidationErrorWithoutChangingLibrary(string method, string parameters)
    {
        var library = new InMemoryUserLibraryService([new("x", MediaType.Anime, MediaListStatus.Planning)]);
        var handler = new BridgeHandler(new OfflineCatalog(), library);

        using var response = JsonDocument.Parse(await handler.HandleAsync(
            "{\"id\":\"invalid\",\"method\":\"" + method + "\",\"params\":{" + parameters + "}}"));

        Assert.Equal("invalid", response.RootElement.GetProperty("id").GetString());
        Assert.Equal("validation_error", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, (await library.GetEntryAsync("x")).Value!.Progress);
    }

    [Fact]
    public async Task MalformedRequestDoesNotPreventNextValidRequest()
    {
        var handler = new BridgeHandler(new OfflineCatalog(), new InMemoryUserLibraryService());
        var input = new StringReader("broken\n{\"id\":\"ok\",\"method\":\"library.list\",\"params\":{}}\n");
        var output = new StringWriter();
        await BridgeProtocol.RunAsync(handler, input, output);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var bad = JsonDocument.Parse(lines[0]);
        using var good = JsonDocument.Parse(lines[1]);
        Assert.Equal("invalid_request", bad.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("ok", good.RootElement.GetProperty("id").GetString());
        Assert.Empty(good.RootElement.GetProperty("result").GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task PrivateExceptionDetailsAreNotSentToFrontend()
    {
        var handler = new BridgeHandler(new ExplodingCatalog(), new InMemoryUserLibraryService());
        using var response = JsonDocument.Parse(await handler.HandleAsync("""
            {"id":"failure","method":"catalog.search","params":{}}
            """));

        Assert.Equal("internal_error", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("private-token", response.RootElement.GetRawText());
    }

    private sealed class OfflineCatalog : IMediaCatalogService
    {
        public Task<Result<PagedResult<MediaItem>>> SearchAsync(MediaSearchQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Library listing must stay local and work without network.");

        public Task<Result<MediaItem>> GetByIdAsync(string mediaId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Library listing must stay local and work without network.");
    }

    private sealed class ExplodingCatalog : IMediaCatalogService
    {
        public Task<Result<PagedResult<MediaItem>>> SearchAsync(MediaSearchQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("private-token /Users/private/library.db");

        public Task<Result<MediaItem>> GetByIdAsync(string mediaId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("private-token");
    }

    private sealed class UnavailableCatalog : IMediaCatalogService
    {
        public Task<Result<PagedResult<MediaItem>>> SearchAsync(MediaSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<PagedResult<MediaItem>>.Failure(ApplicationError.ExternalService("Offline")));

        public Task<Result<MediaItem>> GetByIdAsync(string mediaId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<MediaItem>.Failure(ApplicationError.ExternalService("Offline")));
    }

    private sealed class MalformedCatalog : IMediaCatalogService
    {
        public Task<Result<PagedResult<MediaItem>>> SearchAsync(MediaSearchQuery query, CancellationToken cancellationToken = default)
            => throw new JsonException("Invalid remote payload with private details");

        public Task<Result<MediaItem>> GetByIdAsync(string mediaId, CancellationToken cancellationToken = default)
            => throw new JsonException("Invalid remote payload with private details");
    }
}
