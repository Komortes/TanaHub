using System.Diagnostics;
using System.Net;
using System.Text;
using TanaHub.Infrastructure.Catalog;

namespace TanaHub.Infrastructure.Tests;

public sealed class MangaDexCurlHandlerTests
{
    private const string MetadataUrl = "https://api.mangadex.org/manga/304ceac3-8cdb-4fe7-acf7-2b6ff7a60613/relation?includes[]=manga";

    [Fact]
    public async Task PublicMetadataGetUsesFixedExecutableAndIndependentArguments()
    {
        ProcessStartInfo? captured = null;
        using var client = new HttpClient(new MangaDexCurlHandler((start, _) =>
        {
            captured = start;
            return Task.FromResult(new MangaDexCurlHandler.ProcessResult(0, Encoding.UTF8.GetBytes("{\"data\":[]}\n200")));
        }));

        using var response = await client.GetAsync(MetadataUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"data\":[]}", await response.Content.ReadAsStringAsync());
        Assert.NotNull(captured);
        Assert.Equal("/usr/bin/curl", captured.FileName);
        Assert.False(captured.UseShellExecute);
        Assert.Empty(captured.Arguments);
        Assert.Equal("--disable", captured.ArgumentList[0]);
        Assert.Equal("--", captured.ArgumentList[^2]);
        Assert.Equal(MetadataUrl, captured.ArgumentList[^1]);
        Assert.Contains("=https", captured.ArgumentList);
        Assert.DoesNotContain("--insecure", captured.ArgumentList);
        Assert.DoesNotContain("--location", captured.ArgumentList);
    }

    [Theory]
    [InlineData("http://api.mangadex.org/manga")]
    [InlineData("https://api.mangadex.org.evil.test/manga")]
    [InlineData("https://user:password@api.mangadex.org/manga")]
    [InlineData("https://api.mangadex.org:444/manga")]
    [InlineData("https://api.mangadex.org/manga#fragment")]
    [InlineData("https://api.mangadex.org/auth/login")]
    [InlineData("https://api.mangadex.org/manga/not-a-guid")]
    [InlineData("https://api.mangadex.org/manga/304ceac3-8cdb-4fe7-acf7-2b6ff7a60613/feed")]
    public async Task NonMetadataAndNonPublicUrlsAreRejectedBeforeStartingProcess(string url)
    {
        var started = false;
        using var client = new HttpClient(new MangaDexCurlHandler((_, _) =>
        {
            started = true;
            return Task.FromResult(new MangaDexCurlHandler.ProcessResult(0, Encoding.UTF8.GetBytes("{}\n200")));
        }));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(url));

        Assert.False(started);
    }

    [Fact]
    public async Task RequestsWithCredentialsOrBodyAreRejected()
    {
        using var client = new HttpClient(new MangaDexCurlHandler((_, _) => throw new InvalidOperationException("Should not start.")));
        using var authenticated = new HttpRequestMessage(HttpMethod.Get, MetadataUrl);
        authenticated.Headers.Authorization = new("Bearer", "test-secret");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(authenticated));
        using var cookie = new HttpRequestMessage(HttpMethod.Get, MetadataUrl);
        cookie.Headers.Add("Cookie", "session=test-secret");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(cookie));
        using var post = new HttpRequestMessage(HttpMethod.Post, MetadataUrl) { Content = new StringContent("test") };
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(post));
    }

    [Theory]
    [InlineData(302)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task HttpStatusIsPreservedInsteadOfRedirectedOrConvertedToSuccess(int status)
    {
        using var client = new HttpClient(new MangaDexCurlHandler((_, _) => Task.FromResult(
            new MangaDexCurlHandler.ProcessResult(0, Encoding.UTF8.GetBytes($"{{\"error\":true}}\n{status}")))));

        using var response = await client.GetAsync(MetadataUrl);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(35, "private TLS diagnostic\n000")]
    [InlineData(0, "missing trailer")]
    [InlineData(0, "{}\n000")]
    public async Task TransportErrorsAndMalformedTrailersHaveSafeMessages(int exitCode, string output)
    {
        using var client = new HttpClient(new MangaDexCurlHandler((_, _) => Task.FromResult(
            new MangaDexCurlHandler.ProcessResult(exitCode, Encoding.UTF8.GetBytes(output)))));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(MetadataUrl));

        Assert.DoesNotContain("private", exception.Message);
    }

    [Fact]
    public async Task CancellationReachesTheTransport()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new MangaDexCurlHandler(async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Canceled work should not finish.");
        }));
        using var cancellation = new CancellationTokenSource();
        var request = client.GetAsync(MetadataUrl, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Fact]
    public async Task NativeProcessOutputIsBounded()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var start = StartInfo("/usr/bin/head", "-c", (MangaDexCurlHandler.MaxResponseBytes + 5).ToString(), "/dev/zero");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            MangaDexCurlHandler.RunProcessAsync(start, CancellationToken.None));

        Assert.Contains("too large", exception.Message);
    }

    [Fact]
    public async Task NativeProcessIsKilledOnCancellation()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var directory = Path.Combine(Path.GetTempPath(), "TanaHub.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var pidFile = Path.Combine(directory, "process-id");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var start = StartInfo("/bin/sh", "-c", "echo $$ > \"$1\"; exec /bin/sleep 30", "test", pidFile);
        var running = MangaDexCurlHandler.RunProcessAsync(start, cancellation.Token);
        try
        {
            int pid;
            while (!File.Exists(pidFile) || !int.TryParse(await File.ReadAllTextAsync(pidFile, cancellation.Token), out pid))
                await Task.Delay(20, cancellation.Token);

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

            try
            {
                using var child = Process.GetProcessById(pid);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                Assert.True(child.HasExited);
            }
            catch (ArgumentException)
            {
                // The OS already reaped the child.
            }
        }
        finally
        {
            cancellation.Cancel();
            try { await running; } catch (OperationCanceledException) { }
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ProcessStartInfo StartInfo(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }
}
