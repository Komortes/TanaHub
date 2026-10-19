using System.Globalization;
using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Catalog;

/// <summary>Direct MangaDex relations, enriched through explicitly supplied manga identifiers.</summary>
internal sealed class MangaDexMediaRelationsService(HttpClient httpClient, AniListMediaRelationsService aniList)
    : IMediaRelationsService
{
    private const string BaseUrl = "https://api.mangadex.org/manga";

    public async Task<Result<RelatedTitlesResult>> GetRelatedTitlesAsync(
        string mediaId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(mediaId) || !mediaId.StartsWith("mangadex:", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(mediaId["mangadex:".Length..], out var id))
            return Result<RelatedTitlesResult>.Failure(ApplicationError.Validation("Invalid MangaDex title identifier."));

        var mangaId = id.ToString();
        var root = await ReadDataAsync($"{BaseUrl}/{mangaId}", JsonValueKind.Object, cancellationToken);
        var relations = await ReadDataAsync($"{BaseUrl}/{mangaId}/relation?includes[]=manga", JsonValueKind.Array, cancellationToken);
        var notices = new List<string>();
        var titles = new List<RelatedTitle>();
        var excludedAniListIds = new HashSet<int>();
        var excludedMalIds = new HashSet<int>();
        var attributes = default(JsonElement);
        if (root.IsSuccess && Guid.TryParse(Text(root.Value, "id"), out var responseId) && responseId == id &&
            Text(root.Value, "type") == "manga" && Property(root.Value, "attributes").ValueKind == JsonValueKind.Object)
            attributes = Property(root.Value, "attributes");
        else
            notices.Add("MangaDex cross-catalog links could not be loaded.");

        var links = Property(attributes, "links");
        var rootAniListId = ExternalId(links, "al");
        var rootMalId = ExternalId(links, "mal");
        var rootIdentity = new RelatedTitle($"mangadex:{mangaId}", string.Empty, MediaType.Manga,
            MediaFormat.Manga, MediaRelationKind.Other, "MangaDex", string.Empty)
        {
            MangaDexId = mangaId,
            AniListId = rootAniListId,
            MalId = rootMalId
        };

        if (relations.IsSuccess)
        {
            var incompleteTargets = false;
            foreach (var relation in relations.Value.EnumerateArray())
            {
                var rawKind = Text(Property(relation, "attributes"), "relation") ?? "other";
                var relationships = Property(relation, "relationships");
                if (relationships.ValueKind != JsonValueKind.Array)
                    incompleteTargets = true;
                foreach (var target in Array(relationships))
                {
                    if (Text(target, "type") != "manga") continue;
                    var targetAttributes = Property(target, "attributes");
                    if (targetAttributes.ValueKind != JsonValueKind.Object)
                    {
                        incompleteTargets = true;
                        continue;
                    }
                    if (Text(targetAttributes, "contentRating") is not ("safe" or "suggestive"))
                    {
                        // A conflicting provider rating must not bypass MangaDex's catalog filter.
                        var targetLinks = Property(targetAttributes, "links");
                        if (ExternalId(targetLinks, "al") is int excludedAniListId)
                            excludedAniListIds.Add(excludedAniListId);
                        if (ExternalId(targetLinks, "mal") is int excludedMalId)
                            excludedMalIds.Add(excludedMalId);
                        continue;
                    }
                    var title = ParseTarget(target, rawKind);
                    if (title is not null && !SameTitle(title, rootIdentity))
                        Merge(titles, title);
                }
            }
            if (incompleteTargets)
                notices.Add("Some MangaDex related-title details could not be loaded.");
        }

        Result<RelatedTitlesResult>? bridge = null;
        var mappedId = rootAniListId is int al ? $"anilist:{al}" : rootMalId is int mal ? $"mal:manga:{mal}" : null;
        if (mappedId is not null)
        {
            bridge = await aniList.GetMangaRelatedTitlesAsync(mappedId, cancellationToken);
            if (bridge.IsSuccess)
            {
                foreach (var title in bridge.Value!.Titles)
                    if (!SameTitle(title, rootIdentity) &&
                        !(title.MediaType == MediaType.Manga &&
                          (title.AniListId is int linkedAniListId && excludedAniListIds.Contains(linkedAniListId) ||
                           title.MalId is int linkedMalId && excludedMalIds.Contains(linkedMalId))))
                        Merge(titles, title);
                if (!string.IsNullOrWhiteSpace(bridge.Value.Notice))
                    notices.Add(bridge.Value.Notice);
            }
            else
                notices.Add("AniList adaptations and related titles are temporarily unavailable.");
        }

        if (relations.IsFailure)
        {
            // An unavailable endpoint must not look like a verified empty franchise.
            if (bridge is not { IsSuccess: true } || titles.Count == 0)
                return Result<RelatedTitlesResult>.Failure(relations.Error);
            notices.Add("MangaDex relations are temporarily unavailable. Showing available AniList titles.");
        }

        return Result<RelatedTitlesResult>.Success(new RelatedTitlesResult(titles,
            notices.Count == 0 ? null : string.Join(" ", notices)));
    }

    private async Task<Result<JsonElement>> ReadDataAsync(string url, JsonValueKind expectedKind, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var data = Property(document.RootElement, "data");
            if (Text(document.RootElement, "result") == "error" || data.ValueKind != expectedKind)
                return Failure<JsonElement>("MangaDex returned an invalid relations response.");
            return Result<JsonElement>.Success(data.Clone());
        }
        catch (HttpRequestException)
        {
            return Failure<JsonElement>("MangaDex relations are temporarily unavailable.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure<JsonElement>("MangaDex relations request timed out.");
        }
        catch (JsonException)
        {
            return Failure<JsonElement>("MangaDex returned an invalid relations response.");
        }
        catch (IOException)
        {
            return Failure<JsonElement>("The MangaDex response could not be read. Please try again.");
        }
    }

    private static RelatedTitle? ParseTarget(JsonElement target, string rawKind)
    {
        if (Text(target, "type") != "manga" || !Guid.TryParse(Text(target, "id"), out var id))
            return null;
        var attributes = Property(target, "attributes");
        if (Text(attributes, "contentRating") is not ("safe" or "suggestive"))
            return null;

        var mangaId = id.ToString();
        var links = Property(attributes, "links");
        var title = LocalizedTitle(Property(attributes, "title")) ?? $"MangaDex {mangaId}";
        var format = Text(attributes, "originalLanguage") switch
        {
            "ko" => MediaFormat.Manhwa,
            "zh" or "zh-hk" => MediaFormat.Manhua,
            _ => MediaFormat.Manga
        };
        if (Array(Property(attributes, "tags")).Any(tag =>
                string.Equals(Text(Property(Property(tag, "attributes"), "name"), "en"), "Oneshot", StringComparison.OrdinalIgnoreCase)))
            format = MediaFormat.OneShot;

        Uri? poster = null;
        foreach (var cover in Array(Property(target, "relationships")))
        {
            if (Text(cover, "type") != "cover_art") continue;
            var fileName = Text(Property(cover, "attributes"), "fileName");
            if (!string.IsNullOrWhiteSpace(fileName))
                poster = new Uri($"https://uploads.mangadex.org/covers/{mangaId}/{Uri.EscapeDataString(fileName)}.256.jpg");
            break;
        }

        return new RelatedTitle($"mangadex:{mangaId}", title, MediaType.Manga, format,
            ToRelation(rawKind), "MangaDex", rawKind)
        {
            MangaDexId = mangaId,
            AniListId = ExternalId(links, "al"),
            MalId = ExternalId(links, "mal"),
            Year = Number(attributes, "year"),
            PosterUri = poster
        };
    }

    private static MediaRelationKind ToRelation(string rawKind) => rawKind switch
    {
        "prequel" => MediaRelationKind.Prequel,
        "sequel" => MediaRelationKind.Sequel,
        "adapted_from" or "based_on" => MediaRelationKind.Source,
        "main_story" => MediaRelationKind.Parent,
        "side_story" => MediaRelationKind.SideStory,
        "spin_off" => MediaRelationKind.SpinOff,
        "alternate_story" or "alternate_version" => MediaRelationKind.Alternative,
        "shared_universe" => MediaRelationKind.SameUniverse,
        "colored" => MediaRelationKind.Colored,
        "monochrome" => MediaRelationKind.Monochrome,
        "doujinshi" => MediaRelationKind.Doujinshi,
        "serialization" => MediaRelationKind.Serialization,
        "preserialization" => MediaRelationKind.PreSerialization,
        _ => MediaRelationKind.Other
    };

    private static bool SameTitle(RelatedTitle left, RelatedTitle right)
    {
        if (left.MediaType != right.MediaType) return false;
        // Editions such as colored manga may deliberately share an AniList/MAL entry.
        if (left.MangaDexId is not null && right.MangaDexId is not null)
            return string.Equals(left.MangaDexId, right.MangaDexId, StringComparison.OrdinalIgnoreCase);
        return string.Equals(left.MediaId, right.MediaId, StringComparison.OrdinalIgnoreCase) ||
               left.AniListId is > 0 && left.AniListId == right.AniListId ||
               left.MalId is > 0 && left.MalId == right.MalId;
    }

    private static void Merge(List<RelatedTitle> titles, RelatedTitle candidate)
    {
        var index = titles.FindIndex(existing => SameTitle(existing, candidate));
        if (index < 0)
        {
            titles.Add(candidate);
            return;
        }
        var merged = Enrich(titles[index], candidate);
        // Merge cross-catalog aliases while retaining distinct MangaDex editions.
        bool removed;
        do
        {
            removed = false;
            for (var other = titles.Count - 1; other >= 0; other--)
            {
                if (other == index || !SameTitle(titles[other], merged)) continue;
                merged = Enrich(merged, titles[other]);
                titles.RemoveAt(other);
                if (other < index) index--;
                removed = true;
            }
        } while (removed);
        titles[index] = merged;
    }

    private static RelatedTitle Enrich(RelatedTitle existing, RelatedTitle candidate)
        => existing with
        {
            AniListId = existing.AniListId ?? candidate.AniListId,
            MalId = existing.MalId ?? candidate.MalId,
            MangaDexId = existing.MangaDexId ?? candidate.MangaDexId,
            Year = existing.Year ?? candidate.Year,
            EpisodeCount = existing.EpisodeCount ?? candidate.EpisodeCount,
            ChapterCount = existing.ChapterCount ?? candidate.ChapterCount,
            VolumeCount = existing.VolumeCount ?? candidate.VolumeCount,
            PosterUri = existing.PosterUri ?? candidate.PosterUri
        };

    private static JsonElement Property(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static string? Text(JsonElement element, string name)
        => Property(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static int? Number(JsonElement element, string name)
        => Property(element, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number) && number > 0 ? number : null;

    private static int? ExternalId(JsonElement links, string name)
        => int.TryParse(Text(links, name), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    private static IEnumerable<JsonElement> Array(JsonElement element)
        => element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : [];

    private static string? LocalizedTitle(JsonElement titles)
    {
        if (titles.ValueKind != JsonValueKind.Object) return null;
        var preferred = Text(titles, "en") ?? Text(titles, "ja-ro");
        if (!string.IsNullOrWhiteSpace(preferred)) return preferred;
        return titles.EnumerateObject().Select(value => value.Value)
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static Result<T> Failure<T>(string message) => Result<T>.Failure(ApplicationError.ExternalService(message));
}
