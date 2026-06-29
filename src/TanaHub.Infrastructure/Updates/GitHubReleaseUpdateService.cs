using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using TanaHub.Application.Common;
using TanaHub.Application.Services;
using TanaHub.Application.Updates;

namespace TanaHub.Infrastructure.Updates;

public sealed class GitHubReleaseUpdateService : IAppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;
    private readonly string owner;
    private readonly string repository;

    public GitHubReleaseUpdateService(HttpClient httpClient, string owner, string repository)
    {
        this.httpClient = httpClient;
        this.owner = owner;
        this.repository = repository;
    }

    public async Task<Result<AppUpdateCheckResult>> CheckForUpdatesAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"repos/{owner}/{repository}/releases/latest",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result<AppUpdateCheckResult>.Success(new AppUpdateCheckResult(currentVersion, null));
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result<AppUpdateCheckResult>.Failure(ApplicationError.ExternalService(
                    $"Update check failed: GitHub returned {(int)response.StatusCode}."));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(
                stream,
                JsonOptions,
                cancellationToken);

            if (release is null ||
                !AppUpdateVersionComparer.TryParseReleaseVersion(release.TagName, out var latestVersion))
            {
                return Result<AppUpdateCheckResult>.Success(new AppUpdateCheckResult(currentVersion, null));
            }

            var releaseUri = Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var parsedUri)
                ? parsedUri
                : new Uri($"https://github.com/{owner}/{repository}/releases/tag/{release.TagName}");

            var info = new AppUpdateInfo(
                latestVersion!,
                release.TagName,
                string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
                releaseUri,
                release.PublishedAt);

            return Result<AppUpdateCheckResult>.Success(new AppUpdateCheckResult(currentVersion, info));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Result<AppUpdateCheckResult>.Failure(ApplicationError.ExternalService(
                $"Update check failed: {exception.Message}"));
        }
    }

    private sealed record GitHubReleaseDto(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt);
}
