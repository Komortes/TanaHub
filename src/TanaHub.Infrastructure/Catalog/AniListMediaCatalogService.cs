using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Catalog;

internal sealed class AniListMediaCatalogService : IMediaCatalogService
{
    private const string GraphQlEndpoint = "https://graphql.anilist.co/";

    private readonly HttpClient httpClient;
    private readonly IMediaCatalogService fallbackCatalog;
    private readonly OfflineCatalogCache? offlineCache;
    private readonly ConcurrentDictionary<string, MediaItem> fetchedItems = new(StringComparer.OrdinalIgnoreCase);

    public AniListMediaCatalogService(
        HttpClient httpClient,
        IMediaCatalogService fallbackCatalog,
        OfflineCatalogCache? offlineCache = null)
    {
        this.httpClient = httpClient;
        this.fallbackCatalog = fallbackCatalog;
        this.offlineCache = offlineCache;
    }

    public async Task<Result<PagedResult<MediaItem>>> SearchAsync(
        MediaSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
        {
            return Failure<PagedResult<MediaItem>>("Page must be greater than zero.");
        }

        if (query.PageSize < 1)
        {
            return Failure<PagedResult<MediaItem>>("Page size must be greater than zero.");
        }

        try
        {
            var request = CreateSearchRequest(query);
            var response = await PostGraphQlAsync<AniListSearchData>(
                request.Query,
                request.Variables,
                cancellationToken);

            if (response.Errors?.Count > 0)
            {
                return await fallbackCatalog.SearchAsync(query, cancellationToken);
            }

            var media = response.Data?.Page?.Media ?? [];
            var items = media.Select(ToMediaItem).ToArray();

            foreach (var item in items)
            {
                fetchedItems[item.Id] = item;
            }

            if (offlineCache is not null)
            {
                offlineCache.PutRange(items);
                _ = offlineCache.FlushAsync(cancellationToken);
            }

            return Result<PagedResult<MediaItem>>.Success(new PagedResult<MediaItem>(
                items,
                query.Page,
                query.PageSize,
                response.Data?.Page?.PageInfo?.Total));
        }
        catch (HttpRequestException)
        {
            return await fallbackCatalog.SearchAsync(query, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await fallbackCatalog.SearchAsync(query, cancellationToken);
        }
    }

    public async Task<Result<MediaItem>> GetByIdAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return Failure<MediaItem>("Media id is required.");
        }

        if (fetchedItems.TryGetValue(mediaId, out var fetched))
        {
            return Result<MediaItem>.Success(fetched);
        }

        if (!TryParseAniListId(mediaId, out var id))
        {
            return await fallbackCatalog.GetByIdAsync(mediaId, cancellationToken);
        }

        try
        {
            var response = await PostGraphQlAsync<AniListMediaData>(
                MediaQuery,
                new { id },
                cancellationToken);

            var media = response.Data?.Media;
            if (media is null)
            {
                return await fallbackCatalog.GetByIdAsync(mediaId, cancellationToken);
            }

            var item = ToMediaItem(media);
            fetchedItems[item.Id] = item;
            if (offlineCache is not null)
            {
                offlineCache.Put(item);
                _ = offlineCache.FlushAsync(cancellationToken);
            }
            return Result<MediaItem>.Success(item);
        }
        catch (HttpRequestException)
        {
            return await TryOfflineOrFallback(mediaId, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await TryOfflineOrFallback(mediaId, cancellationToken);
        }
    }

    private Task<Result<MediaItem>> TryOfflineOrFallback(string mediaId, CancellationToken cancellationToken)
    {
        if (offlineCache is not null && offlineCache.TryGet(mediaId, out var cached))
            return Task.FromResult(Result<MediaItem>.Success(cached));
        return fallbackCatalog.GetByIdAsync(mediaId, cancellationToken);
    }

    private async Task<GraphQlResponse<T>> PostGraphQlAsync<T>(
        string query,
        object variables,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            GraphQlEndpoint,
            new GraphQlRequest(query, variables),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GraphQlResponse<T>>(
            cancellationToken: cancellationToken);

        return payload ?? new GraphQlResponse<T>();
    }

    private static GraphQlRequest CreateSearchRequest(MediaSearchQuery query)
    {
        var variableDefinitions = new List<string>
        {
            "$page: Int!",
            "$perPage: Int!"
        };

        var mediaArguments = new List<string>
        {
            "isAdult: false"
        };

        var variables = new Dictionary<string, object?>
        {
            ["page"] = query.Page,
            ["perPage"] = Math.Min(query.PageSize, 50)
        };

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            variableDefinitions.Add("$search: String");
            mediaArguments.Add("search: $search");
            variables["search"] = query.SearchText;
        }

        var type = ToAniListType(query.Type);
        if (type is not null)
        {
            variableDefinitions.Add("$type: MediaType");
            mediaArguments.Add("type: $type");
            variables["type"] = type;
        }

        var format = ToAniListFormat(query.Format);
        if (format is not null)
        {
            variableDefinitions.Add("$format: MediaFormat");
            mediaArguments.Add("format: $format");
            variables["format"] = format;
        }

        var status = ToAniListStatus(query.ReleaseStatus);
        if (status is not null)
        {
            variableDefinitions.Add("$status: MediaStatus");
            mediaArguments.Add("status: $status");
            variables["status"] = status;
        }

        if (query.SeasonYear is not null)
        {
            variableDefinitions.Add("$seasonYear: Int");
            mediaArguments.Add("seasonYear: $seasonYear");
            variables["seasonYear"] = query.SeasonYear.Value;
        }

        if (query.Genres.Count > 0)
        {
            variableDefinitions.Add("$genres: [String]");
            mediaArguments.Add("genre_in: $genres");
            variables["genres"] = query.Genres;
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            variableDefinitions.Add("$country: CountryCode");
            mediaArguments.Add("countryOfOrigin: $country");
            variables["country"] = query.CountryCode.ToUpperInvariant();
        }

        var sort = ToAniListSort(query.Sort);
        mediaArguments.Add(string.IsNullOrWhiteSpace(query.SearchText)
            ? $"sort: [{sort}]"
            : $"sort: [SEARCH_MATCH, {sort}]");

        var graphQl = $$"""
            query SearchMedia({{string.Join(", ", variableDefinitions)}}) {
              Page(page: $page, perPage: $perPage) {
                pageInfo { total }
                media({{string.Join(", ", mediaArguments)}}) {
                  id
                  type
                  title { romaji english native }
                  format
                  status
                  startDate { year }
                  averageScore
                  description(asHtml: false)
                  genres
                  episodes
                  duration
                  chapters
                  volumes
                  coverImage { extraLarge large medium }
                  bannerImage
                  studios(isMain: true) { nodes { name } }
                }
              }
            }
            """;

        return new GraphQlRequest(graphQl, variables);
    }

    private static MediaItem ToMediaItem(AniListMedia media)
    {
        var title = new MediaTitle(
            media.Title?.Romaji ?? media.Title?.English ?? $"AniList #{media.Id}",
            media.Title?.English,
            media.Title?.Native);

        var format = FromAniListFormat(media.Format);
        var status = FromAniListStatus(media.Status);
        var images = new MediaImages(
            ToUri(media.CoverImage?.ExtraLarge ?? media.CoverImage?.Large ?? media.CoverImage?.Medium),
            ToUri(media.BannerImage),
            ToUri(media.CoverImage?.Medium));

        var characters = media.Characters?.Edges?
            .Where(e => e.Node?.Name?.Full is not null)
            .Select(e => new CharacterInfo(
                e.Node!.Name!.Full!,
                ToUri(e.Node.Image?.Medium),
                e.Role switch { "MAIN" => "Main", "SUPPORTING" => "Supporting", _ => "Background" }))
            .ToList() ?? [];

        if (media.Type == "MANGA")
        {
            return new Manga($"anilist:{media.Id}", title, format, status)
            {
                ChapterCount = media.Chapters,
                VolumeCount = media.Volumes,
                StartYear = media.StartDate?.Year,
                AverageScore = media.AverageScore,
                Description = media.Description,
                Genres = media.Genres ?? [],
                Characters = characters,
                Images = images
            };
        }

        return new Anime($"anilist:{media.Id}", title, format, status)
        {
            EpisodeCount = media.Episodes,
            DurationMinutes = media.Duration,
            StartYear = media.StartDate?.Year,
            AverageScore = media.AverageScore,
            Description = media.Description,
            Genres = media.Genres ?? [],
            Characters = characters,
            Images = images,
            Studio = media.Studios?.Nodes?.FirstOrDefault()?.Name
        };
    }

    private static bool TryParseAniListId(string mediaId, out int id)
    {
        id = 0;
        return mediaId.StartsWith("anilist:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(mediaId["anilist:".Length..], out id);
    }

    private static Uri? ToUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static string? ToAniListType(MediaType? type)
    {
        return type switch
        {
            MediaType.Anime => "ANIME",
            MediaType.Manga => "MANGA",
            _ => null
        };
    }

    private static string? ToAniListFormat(MediaFormat? format)
    {
        return format switch
        {
            MediaFormat.Tv => "TV",
            MediaFormat.TvShort => "TV_SHORT",
            MediaFormat.Movie => "MOVIE",
            MediaFormat.Special => "SPECIAL",
            MediaFormat.Ova => "OVA",
            MediaFormat.Ona => "ONA",
            MediaFormat.Music => "MUSIC",
            MediaFormat.Manga => "MANGA",
            MediaFormat.Novel => "NOVEL",
            MediaFormat.OneShot => "ONE_SHOT",
            _ => null
        };
    }

    private static string? ToAniListStatus(MediaReleaseStatus? status)
    {
        return status switch
        {
            MediaReleaseStatus.Releasing => "RELEASING",
            MediaReleaseStatus.Finished => "FINISHED",
            MediaReleaseStatus.NotYetReleased => "NOT_YET_RELEASED",
            MediaReleaseStatus.Cancelled => "CANCELLED",
            MediaReleaseStatus.Hiatus => "HIATUS",
            _ => null
        };
    }

    private static string ToAniListSort(MediaSearchSort sort)
    {
        return sort switch
        {
            MediaSearchSort.Score => "SCORE_DESC",
            MediaSearchSort.Trending => "TRENDING_DESC",
            MediaSearchSort.Newest => "START_DATE_DESC",
            _ => "POPULARITY_DESC"
        };
    }

    private static MediaFormat FromAniListFormat(string? format)
    {
        return format switch
        {
            "TV" => MediaFormat.Tv,
            "TV_SHORT" => MediaFormat.TvShort,
            "MOVIE" => MediaFormat.Movie,
            "SPECIAL" => MediaFormat.Special,
            "OVA" => MediaFormat.Ova,
            "ONA" => MediaFormat.Ona,
            "MUSIC" => MediaFormat.Music,
            "MANGA" => MediaFormat.Manga,
            "NOVEL" => MediaFormat.Novel,
            "ONE_SHOT" => MediaFormat.OneShot,
            _ => MediaFormat.Unknown
        };
    }

    private static MediaReleaseStatus FromAniListStatus(string? status)
    {
        return status switch
        {
            "RELEASING" => MediaReleaseStatus.Releasing,
            "FINISHED" => MediaReleaseStatus.Finished,
            "NOT_YET_RELEASED" => MediaReleaseStatus.NotYetReleased,
            "CANCELLED" => MediaReleaseStatus.Cancelled,
            "HIATUS" => MediaReleaseStatus.Hiatus,
            _ => MediaReleaseStatus.Unknown
        };
    }

    private static Result<T> Failure<T>(string message)
    {
        return Result<T>.Failure(ApplicationError.Validation(message));
    }

    private const string MediaQuery = """
        query MediaById($id: Int!) {
          Media(id: $id, isAdult: false) {
            id
            type
            title { romaji english native }
            format
            status
            startDate { year }
            averageScore
            description(asHtml: false)
            genres
            episodes
            duration
            chapters
            volumes
            coverImage { extraLarge large medium }
            bannerImage
            studios(isMain: true) { nodes { name } }
            characters(sort: ROLE, perPage: 6) {
              edges {
                role
                node { name { full } image { medium } }
              }
            }
          }
        }
        """;

    private sealed record GraphQlRequest(string Query, object Variables);

    private sealed record GraphQlResponse<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; init; }

        [JsonPropertyName("errors")]
        public IReadOnlyList<GraphQlError>? Errors { get; init; }
    }

    private sealed record GraphQlError
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    private sealed record AniListSearchData
    {
        [JsonPropertyName("Page")]
        public AniListPage? Page { get; init; }
    }

    private sealed record AniListMediaData
    {
        [JsonPropertyName("Media")]
        public AniListMedia? Media { get; init; }
    }

    private sealed record AniListPage
    {
        [JsonPropertyName("pageInfo")]
        public AniListPageInfo? PageInfo { get; init; }

        [JsonPropertyName("media")]
        public IReadOnlyList<AniListMedia>? Media { get; init; }
    }

    private sealed record AniListPageInfo
    {
        [JsonPropertyName("total")]
        public int? Total { get; init; }
    }

    private sealed record AniListMedia
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("title")]
        public AniListTitle? Title { get; init; }

        [JsonPropertyName("format")]
        public string? Format { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("startDate")]
        public AniListDate? StartDate { get; init; }

        [JsonPropertyName("averageScore")]
        public int? AverageScore { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("genres")]
        public IReadOnlyList<string>? Genres { get; init; }

        [JsonPropertyName("episodes")]
        public int? Episodes { get; init; }

        [JsonPropertyName("duration")]
        public int? Duration { get; init; }

        [JsonPropertyName("chapters")]
        public int? Chapters { get; init; }

        [JsonPropertyName("volumes")]
        public int? Volumes { get; init; }

        [JsonPropertyName("coverImage")]
        public AniListCoverImage? CoverImage { get; init; }

        [JsonPropertyName("bannerImage")]
        public string? BannerImage { get; init; }

        [JsonPropertyName("studios")]
        public AniListStudioConnection? Studios { get; init; }

        [JsonPropertyName("characters")]
        public AniListCharacterConnection? Characters { get; init; }
    }

    private sealed record AniListTitle
    {
        [JsonPropertyName("romaji")]
        public string? Romaji { get; init; }

        [JsonPropertyName("english")]
        public string? English { get; init; }

        [JsonPropertyName("native")]
        public string? Native { get; init; }
    }

    private sealed record AniListDate
    {
        [JsonPropertyName("year")]
        public int? Year { get; init; }
    }

    private sealed record AniListCoverImage
    {
        [JsonPropertyName("extraLarge")]
        public string? ExtraLarge { get; init; }

        [JsonPropertyName("large")]
        public string? Large { get; init; }

        [JsonPropertyName("medium")]
        public string? Medium { get; init; }
    }

    private sealed record AniListStudioConnection
    {
        [JsonPropertyName("nodes")]
        public IReadOnlyList<AniListStudio>? Nodes { get; init; }
    }

    private sealed record AniListStudio
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed record AniListCharacterConnection
    {
        [JsonPropertyName("edges")]
        public IReadOnlyList<AniListCharacterEdge>? Edges { get; init; }
    }

    private sealed record AniListCharacterEdge
    {
        [JsonPropertyName("role")]
        public string? Role { get; init; }

        [JsonPropertyName("node")]
        public AniListCharacterNode? Node { get; init; }
    }

    private sealed record AniListCharacterNode
    {
        [JsonPropertyName("name")]
        public AniListCharacterName? Name { get; init; }

        [JsonPropertyName("image")]
        public AniListCharacterImage? Image { get; init; }
    }

    private sealed record AniListCharacterName
    {
        [JsonPropertyName("full")]
        public string? Full { get; init; }
    }

    private sealed record AniListCharacterImage
    {
        [JsonPropertyName("medium")]
        public string? Medium { get; init; }
    }
}
