using System.Net;
using TanaHub.UI.Views.Controls;

namespace TanaHub.Application.Tests;

public sealed class RemoteImageStreamTests
{
    [Fact]
    public async Task DownloadAsync_RejectsOversizedBodyWithoutContentLength()
    {
        using var content = new StreamContent(new NonSeekableStream(new byte[33]));
        content.Headers.ContentType = new("image/png");
        using var client = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content })));

        Assert.Null(content.Headers.ContentLength);
        await Assert.ThrowsAsync<InvalidDataException>(() => RemoteImageDownloader.DownloadAsync(
            client, new Uri("https://example.test/image.png"), 32, CancellationToken.None));
    }

    [Fact]
    public async Task ReadLimitedAsync_AcceptsExactlyTheLimit()
    {
        byte[] expected = [1, 2, 3, 4];
        using var stream = new NonSeekableStream(expected);

        var actual = await RemoteImageDownloader.ReadLimitedAsync(stream, expected.Length, CancellationToken.None);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DownloadAsync_CancelsAnInFlightRequest()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new DelegateHandler(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var cancellation = new CancellationTokenSource();
        var download = RemoteImageDownloader.DownloadAsync(
            client, new Uri("https://example.test/image.png"), 32, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task DownloadAsync_RejectsRelativeUriBeforeSendingRequest()
    {
        var requests = 0;
        using var client = new HttpClient(new DelegateHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() => RemoteImageDownloader.DownloadAsync(
            client, new Uri("image.png", UriKind.Relative), 32, CancellationToken.None));
        Assert.Equal(0, requests);
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
