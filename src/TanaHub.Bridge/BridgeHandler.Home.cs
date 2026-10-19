using System.Text.Json;
using TanaHub.Application.Insights;
using TanaHub.Application.Queries;
using TanaHub.Application.Recommendations;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Bridge;

public sealed partial class BridgeHandler
{
    private IReadOnlyList<AiringScheduleItem> lastSchedule = [];
    private readonly HashSet<string> notifiedEpisodes = new(StringComparer.OrdinalIgnoreCase);

    private async Task<object> HomeAsync(JsonElement parameters, CancellationToken ct)
    {
        var type = OptionalEnum<MediaType>(parameters, "type");
        var entries = (await AllEntriesAsync(ct)).Where(entry => type is null || entry.MediaType == type).ToArray();
        var media = await LocalMediaDictionaryAsync(entries, ct);
        var insights = LibraryInsightsCalculator.Calculate(entries, media);
        insights = insights with
        {
            ByStatus = insights.ByStatus.Select(segment => segment.Key == "Current" ? segment with { Key = "Watching" } : segment).ToArray()
        };
        var continued = new List<LibraryEntryDto>();
        foreach (var entry in entries.Where(entry => entry.Status is MediaListStatus.Current or MediaListStatus.Paused or MediaListStatus.Repeating).Take(12))
            continued.Add(await MapEntryAsync(entry, ct));
        var recent = new List<LibraryEntryDto>();
        foreach (var entry in entries.Take(6)) recent.Add(await MapEntryAsync(entry, ct));
        return new { insights, continueItems = continued, recentItems = recent };
    }

    private async Task<object> RecommendationsAsync(JsonElement parameters, CancellationToken ct)
    {
        var limit = Pagination(parameters, "limit", 8, 24);
        var type = OptionalEnum<MediaType>(parameters, "type");
        var allEntries = await AllEntriesAsync(ct);
        var profileEntries = allEntries.Where(entry => type is null || entry.MediaType == type).ToArray();
        var profile = LibraryRecommendationBuilder.BuildGenreProfile(profileEntries, await LocalMediaDictionaryAsync(profileEntries, ct));
        var candidates = new List<MediaItem>();
        var unavailable = false;
        foreach (var genre in profile)
        {
            var result = await ReadCatalogAsync(() => catalog.SearchAsync(new MediaSearchQuery
            {
                Type = type,
                Genres = [genre.Name],
                Sort = MediaSearchSort.Score,
                PageSize = 30
            }, ct), ct);
            if (result.IsFailure) { unavailable = true; continue; }
            candidates.AddRange(result.Value!.Items);
        }
        var ranked = LibraryRecommendationBuilder.RankCandidates(allEntries, profile,
            candidates.DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase), limit);
        foreach (var item in ranked) sessionMedia[item.Id] = item;
        return new
        {
            items = ranked.Select(item => MediaDto.From(item)).ToArray(),
            genres = profile,
            notice = profile.Count == 0 ? "Add titles and ratings to build recommendations."
                : unavailable ? "Some recommendations are unavailable while the catalog is offline." : null
        };
    }

    private async Task<Dictionary<string, MediaItem>> LocalMediaDictionaryAsync(IEnumerable<UserMediaEntry> entries, CancellationToken ct)
    {
        var media = new Dictionary<string, MediaItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var item = await FindLocalMediaAsync(entry.MediaId, ct);
            if (item is not null) media[entry.MediaId] = item;
        }
        return media;
    }

    private async Task<object> ScheduleAsync(JsonElement parameters, CancellationToken ct)
    {
        var (from, to) = ScheduleWindow(parameters);
        var libraryOnly = Boolean(parameters, "libraryOnly", false);
        var schedule = RequireSuccess(await ReadCatalogAsync(() => Service<IAiringScheduleService>().GetUpcomingAsync(from, to, 100, ct), ct));
        lastSchedule = schedule;
        var entries = (await AllEntriesAsync(ct)).ToDictionary(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase);
        var items = schedule.Where(item => item.AiringAt >= from && item.AiringAt < to && (!libraryOnly || entries.ContainsKey(item.MediaId)))
            .OrderBy(item => item.AiringAt).Select(item => new
            {
                item.MediaId,
                item.Title,
                item.Episode,
                item.AiringAt,
                item.Format,
                item.ReleaseStatus,
                posterUrl = item.PosterUri?.ToString(),
                inLibrary = entries.ContainsKey(item.MediaId),
                status = entries.TryGetValue(item.MediaId, out var entry) ? WireStatus(entry.Status) : null
            }).ToArray();
        return new { items, from, to };
    }

    private async Task<object> NotifyScheduleAsync(JsonElement parameters, CancellationToken ct)
    {
        var (from, to) = ScheduleWindow(parameters);
        if (to - from > TimeSpan.FromHours(26) || DateTimeOffset.UtcNow < from || DateTimeOffset.UtcNow >= to)
            throw Validation("Notifications require today's schedule window.");
        var settings = await SettingsAsync(ct);
        var notifiedCount = 0;
        if (!settings.NotificationsEnabled) return new { notifiedCount };
        var ids = (await AllEntriesAsync(ct)).Select(entry => entry.MediaId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in lastSchedule.Where(item => item.AiringAt >= from && item.AiringAt < to && ids.Contains(item.MediaId)))
        {
            var key = $"{item.MediaId}:{item.Episode}";
            if (notifiedEpisodes.Contains(key)) continue;
            await Service<INotificationService>().NotifyAsync("TanaHub — Airing today", $"EP {item.Episode} of {item.Title} airs today.", ct);
            notifiedEpisodes.Add(key);
            notifiedCount++;
        }
        return new { notifiedCount };
    }

    private static (DateTimeOffset From, DateTimeOffset To) ScheduleWindow(JsonElement parameters)
    {
        var from = OptionalDate(parameters, "from") ?? throw Validation("from is required.");
        var to = OptionalDate(parameters, "to") ?? throw Validation("to is required.");
        if (to <= from || to - from > TimeSpan.FromDays(31)) throw Validation("The schedule range must be positive and at most 31 days.");
        return (from, to);
    }

    private async Task<object> RelatedAsync(JsonElement parameters, CancellationToken ct)
    {
        var result = RequireSuccess(await ReadCatalogAsync(() => Service<IMediaRelationsService>()
            .GetRelatedTitlesAsync(MediaId(parameters), Boolean(parameters, "forceRefresh", false), ct), ct));
        return new
        {
            titles = result.Titles.Select(title => new
            {
                title.MediaId,
                title.Title,
                mediaType = title.MediaType.ToString(),
                format = title.Format.ToString(),
                relation = title.Relation.ToString(),
                title.Source,
                title.SourceRelation,
                title.AniListId,
                title.MalId,
                title.MangaDexId,
                title.Year,
                title.EpisodeCount,
                title.ChapterCount,
                title.VolumeCount,
                posterUrl = title.PosterUri?.ToString()
            }).ToArray(),
            result.Notice,
            result.IsStale
        };
    }

    private static string WireStatus(MediaListStatus status) => status == MediaListStatus.Current ? "Watching" : status.ToString();
}
