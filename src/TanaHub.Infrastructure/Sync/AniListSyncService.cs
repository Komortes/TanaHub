using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Sync;

public sealed class AniListSyncService : IAniListSyncService
{
    private const string GraphQlUrl = "https://graphql.anilist.co";

    private static readonly string ListQuery = """
        {
          "query": "query ($userId: Int, $type: MediaType) { MediaListCollection(userId: $userId, type: $type) { lists { entries { mediaId status progress score(format: POINT_10) notes startedAt { year month day } completedAt { year month day } media { coverImage { large } } } } } }",
          "variables": { "userId": {0}, "type": "{1}" }
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
        var body = ListQuery.Replace("{0}", userId.ToString()).Replace("{1}", type);
        var req = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var mediaTypeDomain = type == "ANIME" ? MediaType.Anime : MediaType.Manga;
            var results = new List<UserMediaEntry>();

            var lists = doc.RootElement
                .GetProperty("data")
                .GetProperty("MediaListCollection")
                .GetProperty("lists");

            foreach (var list in lists.EnumerateArray())
            {
                foreach (var e in list.GetProperty("entries").EnumerateArray())
                {
                    var mediaId = $"anilist:{e.GetProperty("mediaId").GetInt32()}";
                    var status = MapStatus(e.GetProperty("status").GetString());
                    var progress = e.GetProperty("progress").GetInt32();
                    var score = e.GetProperty("score").GetInt32();
                    var notes = e.TryGetProperty("notes", out var n) ? n.GetString() : null;

                    Uri? posterUri = null;
                    if (e.TryGetProperty("media", out var media) &&
                        media.TryGetProperty("coverImage", out var cover) &&
                        cover.TryGetProperty("large", out var large) &&
                        large.ValueKind == JsonValueKind.String)
                    {
                        Uri.TryCreate(large.GetString(), UriKind.Absolute, out posterUri);
                    }

                    results.Add(new UserMediaEntry(mediaId, mediaTypeDomain, status)
                    {
                        Progress = progress,
                        Score = score > 0 ? score : null,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                        PosterUri = posterUri,
                        StartedAt = ParseDate(e, "startedAt"),
                        CompletedAt = ParseDate(e, "completedAt"),
                    });
                }
            }

            return results;
        }
        catch
        {
            return null;
        }
    }

    private static MediaListStatus MapStatus(string? status) => status switch
    {
        "CURRENT"   => MediaListStatus.Current,
        "REPEATING" => MediaListStatus.Current,
        "COMPLETED" => MediaListStatus.Completed,
        "PLANNING"  => MediaListStatus.Planning,
        "PAUSED"    => MediaListStatus.Paused,
        "DROPPED"   => MediaListStatus.Dropped,
        _           => MediaListStatus.Planning,
    };

    private static DateTimeOffset? ParseDate(JsonElement entry, string field)
    {
        if (!entry.TryGetProperty(field, out var date)) return null;
        if (!date.TryGetProperty("year", out var y) || y.ValueKind == JsonValueKind.Null) return null;
        var year = y.GetInt32();
        var month = date.TryGetProperty("month", out var m) && m.ValueKind != JsonValueKind.Null ? m.GetInt32() : 1;
        var day = date.TryGetProperty("day", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetInt32() : 1;
        try { return new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero); }
        catch { return null; }
    }
}
