using System.Xml.Linq;
using TanaHub.Application.Common;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Export;

public static class MalXmlLibraryExchange
{
    public static string Export(IEnumerable<LibraryExportItem> items)
    {
        var root = new XElement("myanimelist");

        foreach (var item in items)
        {
            var type = ParseMediaType(item.Type);
            if (type == MediaType.Manga)
            {
                root.Add(new XElement("manga",
                    new XElement("manga_mangadb_id", ExtractMalId(item.MediaId, MediaType.Manga)),
                    new XElement("manga_title", item.Title),
                    new XElement("my_read_chapters", Math.Max(0, item.Progress)),
                    new XElement("my_score", item.Score ?? 0),
                    new XElement("my_status", ToMalStatus(item.Status, MediaType.Manga))));
            }
            else
            {
                root.Add(new XElement("anime",
                    new XElement("series_animedb_id", ExtractMalId(item.MediaId, MediaType.Anime)),
                    new XElement("series_title", item.Title),
                    new XElement("my_watched_episodes", Math.Max(0, item.Progress)),
                    new XElement("my_score", item.Score ?? 0),
                    new XElement("my_status", ToMalStatus(item.Status, MediaType.Anime))));
            }
        }

        return new XDocument(root).ToString();
    }

    public static Result<IReadOnlyList<UserMediaEntry>> Import(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Result<IReadOnlyList<UserMediaEntry>>.Failure(
                ApplicationError.Validation("MAL XML is empty."));
        }

        try
        {
            var document = XDocument.Parse(xml);
            var entries = new List<UserMediaEntry>();

            foreach (var anime in document.Descendants("anime"))
            {
                if (ReadInt(anime, "series_animedb_id") is not { } id || id <= 0)
                {
                    continue;
                }

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

                entries.Add(new UserMediaEntry($"mal:manga:{id}", MediaType.Manga, FromMalStatus(ReadText(manga, "my_status")))
                {
                    Progress = Math.Max(0, ReadInt(manga, "my_read_chapters") ?? 0),
                    Score = NormalizeScore(ReadInt(manga, "my_score"))
                });
            }

            return Result<IReadOnlyList<UserMediaEntry>>.Success(entries);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException or InvalidOperationException)
        {
            return Result<IReadOnlyList<UserMediaEntry>>.Failure(
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
