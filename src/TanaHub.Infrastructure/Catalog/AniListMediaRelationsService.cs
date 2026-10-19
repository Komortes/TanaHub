using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Catalog;

internal sealed class AniListMediaRelationsService(HttpClient httpClient) : IMediaRelationsService
{
    public Task<Result<RelatedTitlesResult>> GetRelatedTitlesAsync(
        string mediaId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default) =>
        FetchAsync(mediaId, requireManga: false, cancellationToken);

    /// <summary>Resolves an exact MangaDex mapping without accepting an anime ID as its manga source.</summary>
    public Task<Result<RelatedTitlesResult>> GetMangaRelatedTitlesAsync(
        string mediaId,
        CancellationToken cancellationToken = default) =>
        FetchAsync(mediaId, requireManga: true, cancellationToken);

    private async Task<Result<RelatedTitlesResult>> FetchAsync(
        string mediaId,
        bool requireManga,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseId(mediaId, out var id, out var malId, out var type) || (requireManga && type == "ANIME"))
            return Result<RelatedTitlesResult>.Failure(ApplicationError.Validation("Invalid AniList or MyAnimeList title ID."));

        if (requireManga)
            type = "MANGA";

        // AniList applies explicit null lookup variables as filters instead of omitting them.
        var variables = new Dictionary<string, object>();
        if (id.HasValue) variables["id"] = id.Value;
        if (malId.HasValue) variables["idMal"] = malId.Value;
        if (type is not null) variables["type"] = type;

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "https://graphql.anilist.co/",
                new { query = RelationsQuery, variables },
                cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            var payload = document.RootElement;
            var errors = Property(payload, "errors");
            if (errors.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null) &&
                (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() > 0))
                return Failure("AniList could not load related titles. Please try again.");

            var media = Property(Property(payload, "data"), "Media");
            var rootId = PositiveInteger(media, "id");
            var rootType = String(media, "type");
            if (rootId is null || rootType is not ("ANIME" or "MANGA") ||
                Property(media, "isAdult").ValueKind != JsonValueKind.False ||
                (id.HasValue && rootId != id) ||
                (malId.HasValue && PositiveInteger(media, "idMal") != malId) ||
                (type is not null && rootType != type))
                return Failure("AniList could not find the requested title's relations.");

            var edges = Property(Property(media, "relations"), "edges");
            if (edges.ValueKind != JsonValueKind.Array)
                return Failure("AniList returned incomplete related-title data. Please try again.");

            var titles = new List<RelatedTitle>();
            var seen = new HashSet<(int Id, string Relation)>();
            var hasIncompleteEdges = false;
            foreach (var edge in edges.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var node = Property(edge, "node");
                var nodeId = PositiveInteger(node, "id");
                var nodeType = String(node, "type");
                var relation = String(edge, "relationType");
                var adultFlag = Property(node, "isAdult").ValueKind;
                if (nodeId == rootId || adultFlag == JsonValueKind.True ||
                    (nodeId.HasValue && relation is not null && seen.Contains((nodeId.Value, relation))))
                    continue;

                var names = Property(node, "title");
                var title = String(names, "english") ?? String(names, "romaji") ?? String(names, "native");
                if (nodeId is null || nodeType is not ("ANIME" or "MANGA") ||
                    adultFlag != JsonValueKind.False || relation is null || title is null)
                {
                    hasIncompleteEdges = true;
                    continue;
                }

                seen.Add((nodeId.Value, relation));
                var mediaType = nodeType == "MANGA" ? MediaType.Manga : MediaType.Anime;
                titles.Add(new RelatedTitle(
                    $"anilist:{nodeId.Value}", title, mediaType,
                    ParseFormat(String(node, "format")), ParseRelation(relation), "AniList", relation)
                {
                    AniListId = nodeId,
                    MalId = PositiveInteger(node, "idMal"),
                    Year = PositiveInteger(Property(node, "startDate"), "year"),
                    EpisodeCount = mediaType == MediaType.Anime ? PositiveInteger(node, "episodes") : null,
                    ChapterCount = mediaType == MediaType.Manga ? PositiveInteger(node, "chapters") : null,
                    VolumeCount = mediaType == MediaType.Manga ? PositiveInteger(node, "volumes") : null,
                    PosterUri = PosterUri(String(Property(node, "coverImage"), "large"))
                });
            }

            return Result<RelatedTitlesResult>.Success(new RelatedTitlesResult(titles,
                hasIncompleteEdges ? "Some AniList related-title data was incomplete. Try again to load the missing titles." : null));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("The AniList request timed out. Please try again.");
        }
        catch (HttpRequestException)
        {
            return Failure("AniList is unavailable. Please try again later.");
        }
        catch (JsonException)
        {
            return Failure("AniList returned invalid related-title data. Please try again.");
        }
        catch (IOException)
        {
            return Failure("The AniList response could not be read. Please try again.");
        }
    }

    private static bool TryParseId(string mediaId, out int? id, out int? malId, out string? type)
    {
        id = null;
        malId = null;
        type = null;
        if (string.IsNullOrWhiteSpace(mediaId))
            return false;

        var parts = mediaId.Split(':');
        if (parts.Length == 2 && parts[0].Equals("anilist", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var aniListId) && aniListId > 0)
        {
            id = aniListId;
            return true;
        }

        if (parts.Length != 3 || !parts[0].Equals("mal", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedMalId) || parsedMalId <= 0)
            return false;

        type = parts[1].ToUpperInvariant() switch { "ANIME" => "ANIME", "MANGA" => "MANGA", _ => null };
        malId = parsedMalId;
        return type is not null;
    }

    private static JsonElement Property(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) ? value : default;

    private static string? String(JsonElement element, string property)
    {
        var value = Property(element, property);
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static int? PositiveInteger(JsonElement element, string property)
    {
        var value = Property(element, property);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0 ? number : null;
    }

    private static Uri? PosterUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? uri : null;

    private static MediaRelationKind ParseRelation(string relation) => relation switch
    {
        "PREQUEL" => MediaRelationKind.Prequel,
        "SEQUEL" => MediaRelationKind.Sequel,
        "SOURCE" => MediaRelationKind.Source,
        "ADAPTATION" => MediaRelationKind.Adaptation,
        "PARENT" => MediaRelationKind.Parent,
        "SIDE_STORY" => MediaRelationKind.SideStory,
        "SPIN_OFF" => MediaRelationKind.SpinOff,
        "ALTERNATIVE" => MediaRelationKind.Alternative,
        "SUMMARY" => MediaRelationKind.Summary,
        "COMPILATION" => MediaRelationKind.Compilation,
        "CONTAINS" => MediaRelationKind.Contains,
        "SAME_UNIVERSE" => MediaRelationKind.SameUniverse,
        "CHARACTER" => MediaRelationKind.Character,
        _ => MediaRelationKind.Other
    };

    private static MediaFormat ParseFormat(string? format) => format switch
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

    private static Result<RelatedTitlesResult> Failure(string message) =>
        Result<RelatedTitlesResult>.Failure(ApplicationError.ExternalService(message));

    private const string RelationsQuery = """
        query ($id: Int, $idMal: Int, $type: MediaType) {
          Media(id: $id, idMal: $idMal, type: $type, isAdult: false) {
            id idMal type isAdult
            relations {
              edges {
                relationType(version: 2)
                node {
                  id idMal type isAdult format
                  title { romaji english native }
                  startDate { year }
                  episodes chapters volumes
                  coverImage { large }
                }
              }
            }
          }
        }
        """;
}
