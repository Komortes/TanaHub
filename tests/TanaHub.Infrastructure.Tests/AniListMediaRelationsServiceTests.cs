using System.Net;
using System.Text;
using System.Text.Json;
using TanaHub.Domain.Enums;
using TanaHub.Infrastructure.Catalog;

namespace TanaHub.Infrastructure.Tests;

public sealed class AniListMediaRelationsServiceTests
{
    [Fact]
    public async Task GetRelatedTitlesAsync_MapsAnimeSourceAndSequelWithMetadata()
    {
        var handler = new CapturingHandler("""
            {"data":{"Media":{"id":16498,"idMal":16498,"type":"ANIME","isAdult":false,"relations":{"edges":[
              {"relationType":"SOURCE","node":{"id":53390,"idMal":23390,"type":"MANGA","isAdult":false,"title":{"romaji":"Shingeki no Kyojin","english":"Attack on Titan"},"format":"MANGA","chapters":141,"volumes":34,"startDate":{"year":2009},"coverImage":{"large":"https://example.com/manga.jpg"}}},
              {"relationType":"SEQUEL","node":{"id":20958,"idMal":25777,"type":"ANIME","isAdult":false,"title":{"romaji":"Shingeki no Kyojin 2"},"format":"TV","episodes":12,"startDate":{"year":2017}}}
            ]}}}}
            """);
        var result = await CreateService(handler).GetRelatedTitlesAsync("anilist:16498");

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value!.Titles,
            title =>
            {
                Assert.Equal("anilist:53390", title.MediaId);
                Assert.Equal("Attack on Titan", title.Title);
                Assert.Equal(MediaRelationKind.Source, title.Relation);
                Assert.Equal(MediaType.Manga, title.MediaType);
                Assert.Equal(MediaFormat.Manga, title.Format);
                Assert.Equal(53390, title.AniListId);
                Assert.Equal(23390, title.MalId);
                Assert.Equal(141, title.ChapterCount);
                Assert.Equal(34, title.VolumeCount);
                Assert.Null(title.EpisodeCount);
                Assert.Equal(2009, title.Year);
                Assert.Equal("https://example.com/manga.jpg", title.PosterUri!.AbsoluteUri);
                Assert.Equal("AniList", title.Source);
                Assert.Equal("SOURCE", title.SourceRelation);
            },
            title =>
            {
                Assert.Equal(MediaRelationKind.Sequel, title.Relation);
                Assert.Equal(MediaType.Anime, title.MediaType);
                Assert.Equal(12, title.EpisodeCount);
                Assert.Null(title.ChapterCount);
            });
        Assert.Contains("relationType(version: 2)", handler.RequestBody);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("mal:anime:7", "ANIME", "ANIME")]
    [InlineData("mal:manga:7", "MANGA", "MANGA")]
    [InlineData("ANILIST:1", "MANGA", null)]
    public async Task GetRelatedTitlesAsync_ResolvesTypedAliases(string mediaId, string rootType, string? expectedType)
    {
        var handler = new CapturingHandler("""
            {"data":{"Media":{"id":1,"idMal":7,"type":"ROOT_TYPE","isAdult":false,"relations":{"edges":[]}}}}
            """.Replace("ROOT_TYPE", rootType));

        var result = await CreateService(handler).GetRelatedTitlesAsync(mediaId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Titles);
        Assert.Null(result.Value.Notice);
        using var body = JsonDocument.Parse(handler.RequestBody);
        var variables = body.RootElement.GetProperty("variables");
        if (expectedType is not null)
        {
            Assert.Equal(expectedType, variables.GetProperty("type").GetString());
            Assert.Equal(7, variables.GetProperty("idMal").GetInt32());
            Assert.False(variables.TryGetProperty("id", out _));
        }
        else
        {
            Assert.Equal(1, variables.GetProperty("id").GetInt32());
            Assert.False(variables.TryGetProperty("idMal", out _));
            Assert.False(variables.TryGetProperty("type", out _));
        }
    }

    [Fact]
    public async Task GetMangaRelatedTitlesAsync_ConstrainsRootAndReturnsAnimeAdaptation()
    {
        var handler = new CapturingHandler("""
            {"data":{"Media":{"id":53390,"type":"MANGA","isAdult":false,"relations":{"edges":[
              {"relationType":"ADAPTATION","node":{"id":16498,"type":"ANIME","isAdult":false,"title":{"english":"Attack on Titan"},"format":"TV","episodes":25}}
            ]}}}}
            """);

        var result = await CreateService(handler).GetMangaRelatedTitlesAsync("anilist:53390");

        Assert.True(result.IsSuccess);
        var title = Assert.Single(result.Value!.Titles);
        Assert.Equal(MediaRelationKind.Adaptation, title.Relation);
        Assert.Equal("Attack on Titan", title.Title);
        Assert.Equal(25, title.EpisodeCount);
        Assert.Contains("\"type\":\"MANGA\"", handler.RequestBody);
        using var body = JsonDocument.Parse(handler.RequestBody);
        Assert.False(body.RootElement.GetProperty("variables").TryGetProperty("idMal", out _));
    }

    [Theory]
    [InlineData("{\"data\":{\"Media\":null}}")]
    [InlineData("{\"data\":{\"Media\":{\"id\":1,\"type\":\"ANIME\",\"isAdult\":false,\"relations\":null}}}")]
    [InlineData("{\"data\":{\"Media\":{\"id\":2,\"type\":\"ANIME\",\"isAdult\":false,\"relations\":{\"edges\":[]}}}}")]
    [InlineData("{\"data\":{\"Media\":{\"id\":1,\"type\":\"UNKNOWN\",\"isAdult\":false,\"relations\":{\"edges\":[]}}}}")]
    [InlineData("{\"errors\":[{\"message\":\"private upstream diagnostic\"}],\"data\":{\"Media\":{\"id\":1,\"type\":\"ANIME\",\"isAdult\":false,\"relations\":{\"edges\":[]}}}}")]
    [InlineData("not json")]
    [InlineData("null")]
    public async Task GetRelatedTitlesAsync_InvalidResponsesFailInsteadOfAppearingEmpty(string response)
    {
        var result = await CreateService(new CapturingHandler(response)).GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsFailure);
        Assert.DoesNotContain("private upstream diagnostic", result.Error.Message);
    }

    [Fact]
    public async Task GetMangaRelatedTitlesAsync_RejectsWrongRootType()
    {
        var result = await CreateService(new CapturingHandler("""
            {"data":{"Media":{"id":1,"type":"ANIME","isAdult":false,"relations":{"edges":[]}}}}
            """)).GetMangaRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetRelatedTitlesAsync_SkipsMalformedSelfAndDuplicateEdges()
    {
        var result = await CreateService(new CapturingHandler("""
            {"data":{"Media":{"id":1,"type":"ANIME","isAdult":false,"relations":{"edges":[
              null, 12, {}, {"relationType":"SEQUEL","node":null},
              {"relationType":"SEQUEL","node":{"id":1,"type":"ANIME","isAdult":false,"title":{"romaji":"Self"}}},
              {"relationType":"SEQUEL","node":{"id":2,"type":"INVALID","isAdult":false,"title":{"romaji":"Bad type"}}},
              {"relationType":"SEQUEL","node":{"id":3,"type":"ANIME","isAdult":false,"title":{}}},
              {"relationType":"SEQUEL","node":{"id":"oops","type":"ANIME","isAdult":false,"title":{"romaji":"Bad id"}}},
              {"relationType":"NEW_KIND","node":{"id":4,"type":"MANGA","isAdult":false,"title":{"romaji":"  ","native":"本"},"chapters":-2,"volumes":0,"coverImage":{"large":"file:///private/test"}}},
              {"relationType":"NEW_KIND","node":{"id":4,"type":"MANGA","isAdult":false,"title":{"native":"本"}}}
            ]}}}}
            """)).GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsSuccess);
        var title = Assert.Single(result.Value!.Titles);
        Assert.Equal("本", title.Title);
        Assert.Equal(MediaRelationKind.Other, title.Relation);
        Assert.Equal("NEW_KIND", title.SourceRelation);
        Assert.Null(title.ChapterCount);
        Assert.Null(title.VolumeCount);
        Assert.Null(title.PosterUri);
        Assert.NotNull(result.Value.Notice);
    }

    [Theory]
    [InlineData("""{"relationType":"SEQUEL","node":{"type":"ANIME","isAdult":false,"title":{"romaji":"Missing ID"}}}""")]
    [InlineData("""{"relationType":"SEQUEL","node":{"id":3,"isAdult":false,"title":{"romaji":"Missing type"}}}""")]
    [InlineData("""{"relationType":"SEQUEL","node":{"id":3,"type":"ANIME","isAdult":false}}""")]
    [InlineData("""{"node":{"id":3,"type":"ANIME","isAdult":false,"title":{"romaji":"Missing relation"}}}""")]
    [InlineData("""{"relationType":"SEQUEL","node":{"id":3,"type":"ANIME","title":{"romaji":"Missing classification"}}}""")]
    [InlineData("""{"relationType":"SEQUEL","node":{"id":3,"type":"ANIME","isAdult":"false","title":{"romaji":"Invalid classification"}}}""")]
    public async Task GetRelatedTitlesAsync_MalformedOmissionsPreserveGoodTitlesAndReportPartialData(string malformedEdge)
    {
        var result = await CreateService(new CapturingHandler("""
            {"data":{"Media":{"id":1,"type":"ANIME","isAdult":false,"relations":{"edges":[
              {"relationType":"SEQUEL","node":{"id":2,"type":"ANIME","isAdult":false,"title":{"romaji":"Valid sequel"}}},
              MALFORMED_EDGE
            ]}}}}
            """.Replace("MALFORMED_EDGE", malformedEdge))).GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsSuccess);
        Assert.Equal("anilist:2", Assert.Single(result.Value!.Titles).MediaId);
        Assert.Contains("incomplete", result.Value.Notice);
    }

    [Fact]
    public async Task GetRelatedTitlesAsync_IntentionalExclusionsRemainComplete()
    {
        var result = await CreateService(new CapturingHandler("""
            {"data":{"Media":{"id":1,"type":"ANIME","isAdult":false,"relations":{"edges":[
              {"relationType":"SEQUEL","node":{"id":1,"type":"ANIME","isAdult":false,"title":{"romaji":"Self"}}},
              {"relationType":"SEQUEL","node":{"id":2,"type":"ANIME","isAdult":false,"title":{"romaji":"Sequel"}}},
              {"relationType":"SEQUEL","node":{"id":2,"type":"ANIME","isAdult":false,"title":{"romaji":"Sequel"}}},
              {"relationType":"SIDE_STORY","node":{"id":3,"type":"ANIME","isAdult":true,"title":{"romaji":"Excluded"}}}
            ]}}}}
            """)).GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsSuccess);
        Assert.Equal("anilist:2", Assert.Single(result.Value!.Titles).MediaId);
        Assert.Null(result.Value.Notice);
    }

    [Theory]
    [InlineData("")]
    [InlineData("anilist:0")]
    [InlineData("anilist:-5")]
    [InlineData("anilist:1:2")]
    [InlineData("mal:1")]
    [InlineData("mal:novel:1")]
    [InlineData("mangadex:abc")]
    public async Task GetRelatedTitlesAsync_RejectsInvalidIdsBeforeRequest(string mediaId)
    {
        var handler = new CapturingHandler("{}");
        var result = await CreateService(handler).GetRelatedTitlesAsync(mediaId);

        Assert.True(result.IsFailure);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetRelatedTitlesAsync_HttpFailuresAreSafeResults(HttpStatusCode status)
    {
        var result = await CreateService(new CapturingHandler("private upstream diagnostic", status))
            .GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsFailure);
        Assert.DoesNotContain("private upstream diagnostic", result.Error.Message);
    }

    [Fact]
    public async Task GetRelatedTitlesAsync_CallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(new CapturingHandler("{}"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetRelatedTitlesAsync("anilist:1", cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task GetRelatedTitlesAsync_NetworkFailureIsSafeResult()
    {
        var result = await CreateService(new ThrowingHandler(new HttpRequestException("private connection detail")))
            .GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsFailure);
        Assert.DoesNotContain("private connection detail", result.Error.Message);
    }

    [Fact]
    public async Task GetRelatedTitlesAsync_TimeoutIsSafeResult()
    {
        var result = await CreateService(new ThrowingHandler(new TaskCanceledException()))
            .GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsFailure);
        Assert.Contains("timed out", result.Error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RelatedTitles_ExcludeAdultAndUnclassifiedNodes_ForDirectAndMangaBridge(bool bridge)
    {
        var handler = new CapturingHandler("""
            {"data":{"Media":{"id":1,"type":"MANGA","isAdult":false,"relations":{"edges":[
              {"relationType":"ADAPTATION","node":{"id":2,"type":"ANIME","isAdult":false,"title":{"romaji":"Allowed"}}},
              {"relationType":"ADAPTATION","node":{"id":3,"type":"ANIME","isAdult":true,"title":{"romaji":"Adult"}}},
              {"relationType":"ADAPTATION","node":{"id":4,"type":"ANIME","title":{"romaji":"Unclassified"}}},
              {"relationType":"ADAPTATION","node":{"id":5,"type":"ANIME","isAdult":"false","title":{"romaji":"Malformed"}}},
              {"relationType":"ADAPTATION","node":{"id":6,"type":"ANIME","isAdult":null,"title":{"romaji":"Null flag"}}}
            ]}}}}
            """);
        var service = CreateService(handler);

        var result = bridge
            ? await service.GetMangaRelatedTitlesAsync("anilist:1")
            : await service.GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsSuccess);
        Assert.Equal("anilist:2", Assert.Single(result.Value!.Titles).MediaId);
        Assert.Contains("isAdult: false", handler.RequestBody);
        Assert.Contains("id idMal type isAdult", handler.RequestBody);
    }

    [Theory]
    [InlineData("true", false)]
    [InlineData("null", false)]
    [InlineData("\"false\"", false)]
    [InlineData("true", true)]
    [InlineData("null", true)]
    [InlineData("\"false\"", true)]
    public async Task RelatedTitles_RejectAdultOrUnclassifiedRoots(string flag, bool bridge)
    {
        var service = CreateService(new CapturingHandler("""
            {"data":{"Media":{"id":1,"type":"MANGA","isAdult":ADULT_FLAG,"relations":{"edges":[]}}}}
            """.Replace("ADULT_FLAG", flag)));

        var result = bridge
            ? await service.GetMangaRelatedTitlesAsync("anilist:1")
            : await service.GetRelatedTitlesAsync("anilist:1");

        Assert.True(result.IsFailure);
    }

    private static AniListMediaRelationsService CreateService(HttpMessageHandler handler) => new(new HttpClient(handler));

    private sealed class CapturingHandler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
