using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Catalog;

internal sealed class MangaDexMediaCatalogService : IMediaCatalogService
{
    private const string BaseUrl = "https://api.mangadex.org";
    private const string CoverBase = "https://uploads.mangadex.org/covers";

    private readonly HttpClient httpClient;

    public MangaDexMediaCatalogService(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<Result<PagedResult<MediaItem>>> SearchAsync(
        MediaSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
            return Failure<PagedResult<MediaItem>>("Page must be greater than zero.");
        if (query.PageSize < 1)
            return Failure<PagedResult<MediaItem>>("Page size must be greater than zero.");

        var limit  = Math.Min(query.PageSize, 100);
        var offset = (query.Page - 1) * limit;

        var url = $"{BaseUrl}/manga?limit={limit}&offset={offset}&includes[]=cover_art&contentRating[]=safe&contentRating[]=suggestive";

        if (!string.IsNullOrWhiteSpace(query.SearchText))
            url += $"&title={Uri.EscapeDataString(query.SearchText)}";

        var status = ToMangaDexStatus(query.ReleaseStatus);
        if (status is not null)
            url += $"&status[]={status}";

        url += ToMangaDexOrder(query.Sort);

        try
        {
            var response = await httpClient.GetFromJsonAsync<MangaDexListResponse>(url, cancellationToken);
            if (response is null)
                return Failure<PagedResult<MediaItem>>("Empty response from MangaDex.");

            var items = response.Data.Select(ToMediaItem).ToArray();
            return Result<PagedResult<MediaItem>>.Success(
                new PagedResult<MediaItem>(items, query.Page, limit, response.Total));
        }
        catch (HttpRequestException ex)
        {
            return Failure<PagedResult<MediaItem>>($"MangaDex unavailable: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure<PagedResult<MediaItem>>("MangaDex request timed out.");
        }
    }

    public async Task<Result<MediaItem>> GetByIdAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseId(mediaId, out var uuid))
            return Failure<MediaItem>($"Invalid MangaDex id: {mediaId}");

        var url = $"{BaseUrl}/manga/{uuid}?includes[]=cover_art";
        try
        {
            var response = await httpClient.GetFromJsonAsync<MangaDexSingleResponse>(url, cancellationToken);
            if (response?.Data is null)
                return Failure<MediaItem>("Not found on MangaDex.");

            return Result<MediaItem>.Success(ToMediaItem(response.Data));
        }
        catch (HttpRequestException ex)
        {
            return Failure<MediaItem>($"MangaDex unavailable: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure<MediaItem>("MangaDex request timed out.");
        }
    }

    private static Manga ToMediaItem(MangaDexManga manga)
    {
        var attrs = manga.Attributes;
        var title = new MediaTitle(
            attrs.Title?.GetValueOrDefault("ja-ro") ?? attrs.Title?.GetValueOrDefault("en") ?? attrs.Title?.Values.FirstOrDefault() ?? $"mangadex:{manga.Id}",
            attrs.Title?.GetValueOrDefault("en"),
            attrs.Title?.GetValueOrDefault("ja"));

        var format = attrs.Type switch
        {
            "novel"     => MediaFormat.Novel,
            "oneshot"   => MediaFormat.OneShot,
            "doujinshi" => MediaFormat.Special,
            _           => MediaFormat.Manga,
        };

        var status = attrs.Status switch
        {
            "ongoing"    => MediaReleaseStatus.Releasing,
            "completed"  => MediaReleaseStatus.Finished,
            "cancelled"  => MediaReleaseStatus.Cancelled,
            "hiatus"     => MediaReleaseStatus.Hiatus,
            _            => MediaReleaseStatus.Unknown,
        };

        Uri? poster = null;
        var cover = manga.Relationships?.FirstOrDefault(r => r.Type == "cover_art");
        if (cover?.Attributes?.FileName is string fileName)
            poster = new Uri($"{CoverBase}/{manga.Id}/{fileName}.256.jpg");

        var desc = attrs.Description?.GetValueOrDefault("en");
        var genres = attrs.Tags?
            .Where(t => t.Attributes?.Group == "genre")
            .Select(t => t.Attributes?.Name?.GetValueOrDefault("en") ?? string.Empty)
            .Where(g => g.Length > 0)
            .ToArray() ?? [];

        return new Manga($"mangadex:{manga.Id}", title, format, status)
        {
            StartYear    = attrs.Year,
            Description  = desc,
            Genres       = genres,
            Images       = new MediaImages(poster),
        };
    }

    private static bool TryParseId(string mediaId, out string uuid)
    {
        uuid = string.Empty;
        if (!mediaId.StartsWith("mangadex:", StringComparison.OrdinalIgnoreCase)) return false;
        uuid = mediaId["mangadex:".Length..];
        return !string.IsNullOrWhiteSpace(uuid);
    }

    private static string? ToMangaDexStatus(MediaReleaseStatus? s) => s switch
    {
        MediaReleaseStatus.Releasing        => "ongoing",
        MediaReleaseStatus.Finished         => "completed",
        MediaReleaseStatus.Cancelled        => "cancelled",
        MediaReleaseStatus.Hiatus           => "hiatus",
        _                                   => null,
    };

    private static string ToMangaDexOrder(MediaSearchSort sort) => sort switch
    {
        MediaSearchSort.Newest  => "&order[createdAt]=desc",
        MediaSearchSort.Score   => "&order[rating]=desc",
        _                       => "&order[followedCount]=desc",
    };

    private static Result<T> Failure<T>(string message)
        => Result<T>.Failure(ApplicationError.Validation(message));

    // ── JSON DTOs ──────────────────────────────────────────────────────────────

    private sealed class MangaDexListResponse
    {
        [JsonPropertyName("data")]  public List<MangaDexManga> Data  { get; init; } = [];
        [JsonPropertyName("total")] public int Total { get; init; }
    }

    private sealed class MangaDexSingleResponse
    {
        [JsonPropertyName("data")] public MangaDexManga? Data { get; init; }
    }

    private sealed class MangaDexManga
    {
        [JsonPropertyName("id")]            public string Id { get; init; } = string.Empty;
        [JsonPropertyName("attributes")]    public MangaDexMangaAttrs Attributes { get; init; } = new();
        [JsonPropertyName("relationships")] public List<MangaDexRelationship>? Relationships { get; init; }
    }

    private sealed class MangaDexMangaAttrs
    {
        [JsonPropertyName("title")]       public Dictionary<string, string>? Title { get; init; }
        [JsonPropertyName("description")] public Dictionary<string, string>? Description { get; init; }
        [JsonPropertyName("status")]      public string? Status { get; init; }
        [JsonPropertyName("year")]        public int? Year { get; init; }
        [JsonPropertyName("publicationDemographic")] public string? Type { get; init; }
        [JsonPropertyName("tags")]        public List<MangaDexTag>? Tags { get; init; }
    }

    private sealed class MangaDexTag
    {
        [JsonPropertyName("attributes")] public MangaDexTagAttrs? Attributes { get; init; }
    }

    private sealed class MangaDexTagAttrs
    {
        [JsonPropertyName("name")]  public Dictionary<string, string>? Name { get; init; }
        [JsonPropertyName("group")] public string? Group { get; init; }
    }

    private sealed class MangaDexRelationship
    {
        [JsonPropertyName("type")]       public string? Type { get; init; }
        [JsonPropertyName("attributes")] public MangaDexRelAttrs? Attributes { get; init; }
    }

    private sealed class MangaDexRelAttrs
    {
        [JsonPropertyName("fileName")] public string? FileName { get; init; }
    }
}
