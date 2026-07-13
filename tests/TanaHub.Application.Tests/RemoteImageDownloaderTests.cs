using System.Net;
using TanaHub.UI.Views.Controls;

namespace TanaHub.Application.Tests;

public sealed class RemoteImageDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_RejectsResponseLargerThanLimit()
    {
        var content = new ByteArrayContent(new byte[32]);
        content.Headers.ContentType = new("image/png");
        var client = new HttpClient(new StaticHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() => RemoteImageDownloader.DownloadAsync(
            client,
            new Uri("https://example.test/poster.png"),
            maxBytes: 16,
            CancellationToken.None));
    }

    [Fact]
    public async Task DownloadAsync_RejectsNonHttpSchemeAndNonImageContent()
    {
        var content = new StringContent("not an image");
        content.Headers.ContentType = new("text/plain");
        var client = new HttpClient(new StaticHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() => RemoteImageDownloader.DownloadAsync(
            client,
            new Uri("https://example.test/not-image"),
            maxBytes: 1024,
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => RemoteImageDownloader.DownloadAsync(
            client,
            new Uri("ftp://example.test/poster.png"),
            maxBytes: 1024,
            CancellationToken.None));
    }

    private sealed class StaticHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
