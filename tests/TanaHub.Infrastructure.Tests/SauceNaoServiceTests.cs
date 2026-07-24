using System.Net;
using System.Text;
using TanaHub.Infrastructure.Recognition;

namespace TanaHub.Infrastructure.Tests;

public sealed class SauceNaoServiceTests
{
    [Fact]
    public async Task RecognizeAsync_MapsResultsAboveSimilarityThreshold()
    {
        var handler = new CapturingHandler("""
            {"header":{"status":0},"results":[
              {"header":{"similarity":"87.87","thumbnail":"https://img1.saucenao.com/res/thumb.jpg"},
               "data":{"title":"Sample Art","member_name":"Some Artist","ext_urls":["https://www.pixiv.net/artworks/123"]}},
              {"header":{"similarity":"40.10","thumbnail":"https://img1.saucenao.com/res/low.jpg"},
               "data":{"title":"Low Match","ext_urls":["https://www.pixiv.net/artworks/456"]}}
            ]}
            """);
        var service = new SauceNaoService(new HttpClient(handler));
        using var stream = new MemoryStream([1, 2, 3]);

        var result = await service.RecognizeAsync(stream, "image/png", apiKey: string.Empty);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Value!);
        Assert.Equal("Sample Art", match.Title);
        Assert.Equal("Some Artist", match.Author);
        Assert.Equal(0.8787, match.Similarity, 4);
        Assert.Equal("https://www.pixiv.net/artworks/123", match.SourceUri!.ToString());
        Assert.Equal("https://img1.saucenao.com/res/thumb.jpg", match.ThumbnailUri!.ToString());
    }

    [Fact]
    public async Task RecognizeAsync_OmitsApiKeyFromRequestWhenNotProvided()
    {
        var handler = new CapturingHandler("""{"header":{"status":0},"results":[]}""");
        var service = new SauceNaoService(new HttpClient(handler));
        using var stream = new MemoryStream([1, 2, 3]);

        await service.RecognizeAsync(stream, "image/png", apiKey: string.Empty);

        Assert.DoesNotContain("api_key", handler.RequestUri!.Query);
    }

    [Fact]
    public async Task RecognizeAsync_IncludesApiKeyInRequestWhenProvided()
    {
        var handler = new CapturingHandler("""{"header":{"status":0},"results":[]}""");
        var service = new SauceNaoService(new HttpClient(handler));
        using var stream = new MemoryStream([1, 2, 3]);

        await service.RecognizeAsync(stream, "image/png", apiKey: "secret-key");

        Assert.Contains("api_key=secret-key", handler.RequestUri!.Query);
    }

    [Fact]
    public async Task RecognizeAsync_ReturnsFailureWhenHeaderStatusIsNegative()
    {
        var handler = new CapturingHandler(
            """{"header":{"status":-2,"message":"Daily Search Limit Exceeded."},"results":[]}""");
        var service = new SauceNaoService(new HttpClient(handler));
        using var stream = new MemoryStream([1, 2, 3]);

        var result = await service.RecognizeAsync(stream, "image/png", apiKey: string.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal("Daily Search Limit Exceeded.", result.Error.Message);
    }

    [Fact]
    public async Task RecognizeAsync_ReturnsFailureOnHttpError()
    {
        var handler = new CapturingHandler("""{}""", HttpStatusCode.ServiceUnavailable);
        var service = new SauceNaoService(new HttpClient(handler));
        using var stream = new MemoryStream([1, 2, 3]);

        var result = await service.RecognizeAsync(stream, "image/png", apiKey: string.Empty);

        Assert.True(result.IsFailure);
    }

    private sealed class CapturingHandler(
        string responseJson,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
