using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TanaHub.Application.Common;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Sync;

public sealed class AniListSyncService : IAniListSyncService
{
    private const string GraphQlUrl = "https://graphql.anilist.co";

    private const string ListQueryText = """
        query ($userId: Int, $type: MediaType) {
          MediaListCollection(userId: $userId, type: $type) {
            lists {
              entries {
                mediaId
                status
                progress
                score(format: POINT_10)
                notes
                startedAt { year month day }
                completedAt { year month day }
                media { coverImage { large } }
              }
            }
          }
        }
        """;

    private readonly HttpClient httpClient;

    public AniListSyncService(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<Result<int>> SyncAsync(
        string accessToken,
        int userId,
        IUserLibraryService libraryService,
        CancellationToken cancellationToken = default)
    {
        var imported = 0;

        foreach (var (typeName, mediaType) in new[] { ("ANIME", MediaType.Anime), ("MANGA", MediaType.Manga) })
        {
            var entries = await FetchListAsync(accessToken, userId, typeName, cancellationToken);
            if (entries is null)
                return Result<int>.Failure(ApplicationError.Validation($"Failed to fetch {typeName} list from AniList."));

            foreach (var entry in entries)
            {
                var upsertResult = await libraryService.UpsertEntryAsync(entry, cancellationToken);
                if (upsertResult.IsSuccess) imported++;
            }
        }

        return Result<int>.Success(imported);
    }

    private async Task<List<UserMediaEntry>?> FetchListAsync(
        string accessToken, int userId, string type, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = JsonContent.Create(new
        {
            query = ListQueryText,
            variables = new { userId, type }
        });

        try
        {
            var response = await httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return null;

            var payload = await response.Content.ReadFromJsonAsync<AniListCollectionResponse>(
                cancellationToken: ct);

            var lists = payload?.Data?.MediaListCollection?.Lists;
            if (lists is null) return null;

            var mediaTypeDomain = type == "ANIME" ? MediaType.Anime : MediaType.Manga;
            var results = new List<UserMediaEntry>();

            foreach (var list in lists)
                foreach (var e in list.Entries ?? [])
                {
                    if (e.MediaId <= 0) continue;

                    Uri? posterUri = null;
                    if (e.Media?.CoverImage?.Large is { } large)
                        Uri.TryCreate(large, UriKind.Absolute, out posterUri);

                    results.Add(new UserMediaEntry($"anilist:{e.MediaId}", mediaTypeDomain, MapStatus(e.Status))
                    {
                        Progress = e.Progress,
                        Score = e.Score > 0 ? e.Score : null,
                        Notes = string.IsNullOrWhiteSpace(e.Notes) ? null : e.Notes,
                        PosterUri = posterUri,
                        StartedAt = ParseDate(e.StartedAt),
                        CompletedAt = ParseDate(e.CompletedAt),
                    });
                }

            return results;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private static MediaListStatus MapStatus(string? status) => status switch
    {
        "CURRENT" => MediaListStatus.Current,
        "REPEATING" => MediaListStatus.Current,
        "COMPLETED" => MediaListStatus.Completed,
        "PLANNING" => MediaListStatus.Planning,
        "PAUSED" => MediaListStatus.Paused,
        "DROPPED" => MediaListStatus.Dropped,
        _ => MediaListStatus.Planning,
    };

    private static DateTimeOffset? ParseDate(AniListDate? date)
    {
        if (date?.Year is not { } year) return null;
        try
        {
            return new DateTimeOffset(year, date.Month ?? 1, date.Day ?? 1, 0, 0, 0, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private sealed class AniListCollectionResponse
    {
        [JsonPropertyName("data")] public AniListData? Data { get; init; }
    }

    private sealed class AniListData
    {
        [JsonPropertyName("MediaListCollection")] public AniListMediaListCollection? MediaListCollection { get; init; }
    }

    private sealed class AniListMediaListCollection
    {
        [JsonPropertyName("lists")] public List<AniListList>? Lists { get; init; }
    }

    private sealed class AniListList
    {
        [JsonPropertyName("entries")] public List<AniListEntry>? Entries { get; init; }
    }

    private sealed class AniListEntry
    {
        [JsonPropertyName("mediaId")] public int MediaId { get; init; }
        [JsonPropertyName("status")] public string? Status { get; init; }
        [JsonPropertyName("progress")] public int Progress { get; init; }
        [JsonPropertyName("score")] public int Score { get; init; }
        [JsonPropertyName("notes")] public string? Notes { get; init; }
        [JsonPropertyName("startedAt")] public AniListDate? StartedAt { get; init; }
        [JsonPropertyName("completedAt")] public AniListDate? CompletedAt { get; init; }
        [JsonPropertyName("media")] public AniListMedia? Media { get; init; }
    }

    private sealed class AniListDate
    {
        [JsonPropertyName("year")] public int? Year { get; init; }
        [JsonPropertyName("month")] public int? Month { get; init; }
        [JsonPropertyName("day")] public int? Day { get; init; }
    }

    private sealed class AniListMedia
    {
        [JsonPropertyName("coverImage")] public AniListCoverImage? CoverImage { get; init; }
    }

    private sealed class AniListCoverImage
    {
        [JsonPropertyName("large")] public string? Large { get; init; }
    }
}
