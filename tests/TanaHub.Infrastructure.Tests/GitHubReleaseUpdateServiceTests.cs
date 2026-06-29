using System.Net;
using TanaHub.Infrastructure.Updates;

namespace TanaHub.Infrastructure.Tests;

public sealed class GitHubReleaseUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsLatestReleaseFromGitHubResponse()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(
            HttpStatusCode.OK,
            """
            {
              "tag_name": "v1.2.4",
              "name": "TanaHub 1.2.4",
              "html_url": "https://github.com/Komortes/TanaHub/releases/tag/v1.2.4",
              "published_at": "2026-06-29T10:00:00Z"
            }
            """))
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var service = new GitHubReleaseUpdateService(httpClient, "Komortes", "TanaHub");

        var result = await service.CheckForUpdatesAsync(Version.Parse("1.2.3"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsUpdateAvailable);
        Assert.Equal(Version.Parse("1.2.4"), result.Value.LatestRelease!.Version);
        Assert.Equal("TanaHub 1.2.4", result.Value.LatestRelease.Name);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_TreatsMissingReleaseAsNoUpdate()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(HttpStatusCode.NotFound, string.Empty))
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var service = new GitHubReleaseUpdateService(httpClient, "Komortes", "TanaHub");

        var result = await service.CheckForUpdatesAsync(Version.Parse("1.2.3"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsUpdateAvailable);
        Assert.Null(result.Value.LatestRelease);
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string content;

        public StaticResponseHandler(HttpStatusCode statusCode, string content)
        {
            this.statusCode = statusCode;
            this.content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
        }
    }
}
