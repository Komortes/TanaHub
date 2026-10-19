using System.Xml.Linq;
using TanaHub.Application.Common;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Export;

public static class MalXmlLibraryExchange
{
    public static string Export(IEnumerable<LibraryExportItem> items)
    {
        return CreateExport(items).Xml;
    }

    public static MalXmlExportDocument CreateExport(IEnumerable<LibraryExportItem> items)
    {
        var root = new XElement("myanimelist");
        var exportedCount = 0;
        var skippedCount = 0;

        foreach (var item in items)
        {
            var type = ParseMediaType(item.Type);
            var malId = item.MalId ?? ExtractMalId(item.MediaId, type);
            if (malId <= 0)
            {
                skippedCount++;
                continue;
            }

            if (type == MediaType.Manga)
            {
                root.Add(new XElement("manga",
                    new XElement("manga_mangadb_id", malId),
                    new XElement("manga_title", item.Title),
                    new XElement("my_read_chapters", Math.Max(0, item.Progress)),
                    new XElement("my_score", item.Score ?? 0),
                    new XElement("my_status", ToMalStatus(item.Status, MediaType.Manga))));
            }
            else
            {
                root.Add(new XElement("anime",
                    new XElement("series_animedb_id", malId),
                    new XElement("series_title", item.Title),
                    new XElement("my_watched_episodes", Math.Max(0, item.Progress)),
                    new XElement("my_score", item.Score ?? 0),
                    new XElement("my_status", ToMalStatus(item.Status, MediaType.Anime))));
            }

            exportedCount++;
        }

        return new MalXmlExportDocument(new XDocument(root).ToString(), exportedCount, skippedCount);
    }

    public static Result<IReadOnlyList<UserMediaEntry>> Import(string xml)
    {
        var document = ImportDocument(xml);
        return document.IsSuccess
            ? Result<IReadOnlyList<UserMediaEntry>>.Success(document.Value!.Entries)
            : Result<IReadOnlyList<UserMediaEntry>>.Failure(document.Error);
    }

    public static Result<MalXmlImportDocument> ImportDocument(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Result<MalXmlImportDocument>.Failure(
                ApplicationError.Validation("MAL XML is empty."));
        }

        try
        {
            var document = XDocument.Parse(xml);
            var entries = new List<UserMediaEntry>();
            var media = new List<MediaItem>();

            foreach (var anime in document.Descendants("anime"))
            {
                if (ReadInt(anime, "series_animedb_id") is not { } id || id <= 0)
                {
                    continue;
                }

                media.Add(new Anime($"mal:anime:{id}", new MediaTitle(ReadTitle(anime, "series_title", $"MyAnimeList anime #{id}")), MediaFormat.Unknown, MediaReleaseStatus.Unknown)
                {
                    MalId = id,
                    EpisodeCount = Positive(ReadInt(anime, "series_episodes"))
                });
                entries.Add(new UserMediaEntry($"mal:anime:{id}", MediaType.Anime, FromMalStatus(ReadText(anime, "my_status")))
                {
                    Progress = Math.Max(0, ReadInt(anime, "my_watched_episodes") ?? 0),
                    Score = NormalizeScore(ReadInt(anime, "my_score"))
                });
            }

            foreach (var manga in document.Descendants("manga"))
            {
                var id = ReadInt(manga, "manga_mangadb_id")
                    ?? ReadInt(manga, "series_mangadb_id");
                if (id is null or <= 0)
                {
                    continue;
                }

                media.Add(new Manga($"mal:manga:{id}", new MediaTitle(ReadTitle(manga, "manga_title", $"MyAnimeList manga #{id}")), MediaFormat.Manga, MediaReleaseStatus.Unknown)
                {
                    MalId = id,
                    ChapterCount = Positive(ReadInt(manga, "manga_chapters")),
                    VolumeCount = Positive(ReadInt(manga, "manga_volumes"))
                });
                entries.Add(new UserMediaEntry($"mal:manga:{id}", MediaType.Manga, FromMalStatus(ReadText(manga, "my_status")))
                {
                    Progress = Math.Max(0, ReadInt(manga, "my_read_chapters") ?? 0),
                    Score = NormalizeScore(ReadInt(manga, "my_score"))
                });
            }

            return Result<MalXmlImportDocument>.Success(new(entries, media));
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException or InvalidOperationException)
        {
            return Result<MalXmlImportDocument>.Failure(
                ApplicationError.Validation("MAL XML could not be parsed."));
        }
    }

    private static MediaType ParseMediaType(string type)
    {
        return Enum.TryParse<MediaType>(type, ignoreCase: true, out var mediaType)
            ? mediaType
            : MediaType.Anime;
    }

    private static int ExtractMalId(string mediaId, MediaType type)
    {
        if (!mediaId.StartsWith($"mal:{type.ToString().ToLowerInvariant()}:", StringComparison.OrdinalIgnoreCase)
            && !mediaId.StartsWith("mal:", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var lastSegment = mediaId.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return int.TryParse(lastSegment, out var id) ? id : 0;
    }

    private static string ToMalStatus(string status, MediaType type)
    {
        return Enum.TryParse<MediaListStatus>(status, ignoreCase: true, out var listStatus)
            ? ToMalStatus(listStatus, type)
            : type == MediaType.Anime ? "Plan to Watch" : "Plan to Read";
    }

    private static string ToMalStatus(MediaListStatus status, MediaType type)
    {
        return status switch
        {
            MediaListStatus.Current => type == MediaType.Anime ? "Watching" : "Reading",
            MediaListStatus.Completed => "Completed",
            MediaListStatus.Paused => "On-Hold",
            MediaListStatus.Dropped => "Dropped",
            MediaListStatus.Repeating => type == MediaType.Anime ? "Watching" : "Reading",
            _ => type == MediaType.Anime ? "Plan to Watch" : "Plan to Read"
        };
    }

    private static MediaListStatus FromMalStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "watching" or "reading" => MediaListStatus.Current,
            "completed" => MediaListStatus.Completed,
            "on-hold" or "on hold" => MediaListStatus.Paused,
            "dropped" => MediaListStatus.Dropped,
            _ => MediaListStatus.Planning
        };
    }

    private static int? Positive(int? value) => value > 0 ? value : null;

    private static string ReadTitle(XContainer element, string name, string fallback)
    {
        var title = ReadText(element, name)?.Trim();
        return string.IsNullOrEmpty(title) ? fallback : title[..Math.Min(title.Length, 1000)];
    }

    private static string? ReadText(XContainer element, string name)
    {
        return element.Element(name)?.Value;
    }

    private static int? ReadInt(XContainer element, string name)
    {
        return int.TryParse(ReadText(element, name), out var value) ? value : null;
    }

    private static int? NormalizeScore(int? score)
    {
        return score is > 0 and <= 10 ? score : null;
    }
}

public sealed record MalXmlExportDocument(string Xml, int ExportedCount, int SkippedCount);

public sealed record MalXmlImportDocument(IReadOnlyList<UserMediaEntry> Entries, IReadOnlyList<MediaItem> Media);
