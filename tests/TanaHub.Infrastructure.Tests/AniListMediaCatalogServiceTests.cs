using System.Net;
using System.Text;
using System.Text.Json;
using TanaHub.Infrastructure.Catalog;

namespace TanaHub.Infrastructure.Tests;

public sealed class AniListMediaCatalogServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ResolvesImportedMalIdAndKeepsLibraryKey()
    {
        var handler = new CapturingHandler("""
            {"data":{"Media":{"id":1,"idMal":1,"type":"ANIME","title":{"romaji":"Cowboy Bebop"},"format":"TV","status":"FINISHED","genres":[]}}}
            """);
        var service = new AniListMediaCatalogService(
            new HttpClient(handler),
            new InMemoryMediaCatalogService());

        var result = await service.GetByIdAsync("mal:anime:1");

        Assert.True(result.IsSuccess);
        Assert.Equal("mal:anime:1", result.Value!.Id);
        Assert.Equal(1, result.Value.MalId);
        Assert.Contains("\"idMal\":1", handler.RequestBody);
        Assert.Contains("\"type\":\"ANIME\"", handler.RequestBody);
        using var body = JsonDocument.Parse(handler.RequestBody);
        Assert.False(body.RootElement.GetProperty("variables").TryGetProperty("id", out _));
    }

    [Theory]
    [InlineData("anilist:1", "ANIME", null)]
    [InlineData("mal:manga:1", "MANGA", "MANGA")]
    public async Task GetByIdAsync_OmitsUnusedLookupVariables(string mediaId, string mediaType, string? expectedType)
    {
        var handler = new CapturingHandler("""
            {"data":{"Media":{"id":1,"idMal":1,"type":"MEDIA_TYPE","title":{"romaji":"Title"},"format":"MANGA","status":"FINISHED","genres":[]}}}
            """.Replace("MEDIA_TYPE", mediaType));
        var service = new AniListMediaCatalogService(new HttpClient(handler), new InMemoryMediaCatalogService([]));

        var result = await service.GetByIdAsync(mediaId);

        Assert.True(result.IsSuccess);
        Assert.Equal(mediaId, result.Value!.Id);
        using var body = JsonDocument.Parse(handler.RequestBody);
        var variables = body.RootElement.GetProperty("variables");
        if (expectedType is null)
        {
            Assert.Equal(1, variables.GetProperty("id").GetInt32());
            Assert.False(variables.TryGetProperty("idMal", out _));
            Assert.False(variables.TryGetProperty("type", out _));
        }
        else
        {
            Assert.Equal(1, variables.GetProperty("idMal").GetInt32());
            Assert.Equal(expectedType, variables.GetProperty("type").GetString());
            Assert.False(variables.TryGetProperty("id", out _));
        }
    }

    [Fact]
    public async Task GetByIdAsync_DoesNotUseOfflineCacheWhenDisabled()
    {
        var cache = new OfflineCatalogCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        cache.Put(new Domain.Models.Anime(
            "anilist:1",
            new Domain.Models.MediaTitle("Cached title"),
            Domain.Enums.MediaFormat.Tv,
            Domain.Enums.MediaReleaseStatus.Finished));
        var service = new AniListMediaCatalogService(
            new HttpClient(new CapturingHandler("{}", HttpStatusCode.ServiceUnavailable)),
            new InMemoryMediaCatalogService([]),
            cache)
        {
            OfflineCacheEnabled = false
        };

        var result = await service.GetByIdAsync("anilist:1");

        Assert.True(result.IsFailure);
    }

    private sealed class CapturingHandler(
        string responseJson,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
