using System.Globalization;
using System.Text.Json;
using TanaHub.Application.Export;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;

namespace TanaHub.Bridge;

public sealed partial class BridgeHandler
{
    private T Service<T>() where T : class => services?.GetService(typeof(T)) as T
        ?? throw new BridgeException("unavailable", "This service is unavailable in the current session.");

    private async Task<IReadOnlyList<UserMediaEntry>> AllEntriesAsync(CancellationToken ct) =>
        RequireSuccess(await library.GetEntriesAsync(new UserLibraryQuery { PageSize = int.MaxValue }, ct)).Items;

    private async Task<object> LookupLibraryAsync(JsonElement parameters, CancellationToken ct)
    {
        if (!parameters.TryGetProperty("mediaIds", out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 50)
            throw Validation("mediaIds must be an array of at most 50 strings.");
        var ids = new List<string>();
        var requested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) throw Validation("Each mediaIds value must be a string.");
            var id = item.GetString()!;
            if (id.Length > 200 || string.IsNullOrWhiteSpace(id) || id.Any(char.IsControl))
                throw Validation("Each mediaIds value must be nonempty, at most 200 characters, and contain no control characters.");
            id = id.Trim();
            if (requested.Add(id)) ids.Add(id);
        }
        var items = new List<LibraryEntryDto>(ids.Count);
        if (ids.Count == 0) return new { items };
        var matching = new Dictionary<string, UserMediaEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in await AllEntriesAsync(ct))
            if (requested.Contains(entry.MediaId)) matching.TryAdd(entry.MediaId, entry);
        foreach (var id in ids)
            if (matching.TryGetValue(id, out var entry)) items.Add(await MapEntryAsync(entry, ct));
        return new { items };
    }

    private async Task<LibraryEntryDto> SetProgressAsync(JsonElement parameters, CancellationToken ct)
    {
        var id = MediaId(parameters);
        var progress = RequiredInteger(parameters, "progress", 0, int.MaxValue);
        var entry = RequireSuccess(await library.GetEntryAsync(id, ct));
        return await MapEntryAsync(RequireSuccess(await library.UpsertEntryAsync(entry.SetProgress(progress), ct)), ct);
    }

    private async Task<LibraryEntryDto> SetReviewAsync(JsonElement parameters, CancellationToken ct)
    {
        if (!parameters.TryGetProperty("review", out _)) throw Validation("review is required.");
        return await MapEntryAsync(RequireSuccess(await library.UpdateReviewAsync(
            MediaId(parameters), OptionalString(parameters, "review", 50_000), ct)), ct);
    }

    private async Task<LibraryEntryDto> SetOrganizationAsync(JsonElement parameters, CancellationToken ct)
    {
        var id = MediaId(parameters);
        var current = RequireSuccess(await library.GetEntryAsync(id, ct));
        return await MapEntryAsync(RequireSuccess(await library.UpdateOrganizationAsync(id,
            StringList(parameters, "tags", current.Tags), StringList(parameters, "customLists", current.CustomLists), ct)), ct);
    }

    private async Task<LibraryEntryDto> SetDatesAsync(JsonElement parameters, CancellationToken ct)
    {
        var current = RequireSuccess(await library.GetEntryAsync(MediaId(parameters), ct));
        var updated = current with
        {
            StartedAt = parameters.TryGetProperty("startedAt", out _) ? OptionalDate(parameters, "startedAt") : current.StartedAt,
            CompletedAt = parameters.TryGetProperty("completedAt", out _) ? OptionalDate(parameters, "completedAt") : current.CompletedAt
        };
        if (updated.StartedAt > updated.CompletedAt) throw Validation("The completion date cannot be before the start date.");
        return await MapEntryAsync(RequireSuccess(await library.UpsertEntryAsync(updated, ct)), ct);
    }

    private async Task<object> LibraryFiltersAsync(CancellationToken ct)
    {
        var entries = await AllEntriesAsync(ct);
        static string[] Options(IEnumerable<string> values) => values.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        return new { tags = Options(entries.SelectMany(entry => entry.Tags)), customLists = Options(entries.SelectMany(entry => entry.CustomLists)) };
    }

    private async Task<object> ExportAsync(JsonElement parameters, CancellationToken ct)
    {
        var format = RequiredString(parameters, "format", 20);
        if (format is not ("csv" or "malXml")) throw Validation("format must be csv or malXml.");
        var rows = new List<LibraryExportItem>();
        foreach (var entry in await AllEntriesAsync(ct))
        {
            var media = await FindLocalMediaAsync(entry.MediaId, ct);
            rows.Add(new(entry.MediaId, media?.Title.DisplayTitle ?? entry.MediaId,
                entry.MediaType.ToString(), entry.Status.ToString(), entry.Progress, entry.Score)
            {
                Tags = entry.Tags,
                CustomLists = entry.CustomLists,
                MalId = media?.MalId
            });
        }
        if (format == "csv") return new
        {
            text = LibraryCsvExporter.Export(rows),
            fileName = "tanahub-library.csv",
            mimeType = "text/csv",
            exportedCount = rows.Count,
            skippedCount = 0
        };
        var xml = MalXmlLibraryExchange.CreateExport(rows);
        return new { text = xml.Xml, fileName = "tanahub-library.xml", mimeType = "application/xml", xml.ExportedCount, xml.SkippedCount };
    }

    private async Task<object> PreviewImportAsync(JsonElement parameters, CancellationToken ct)
    {
        var parsed = RequireSuccess(MalXmlLibraryExchange.Import(RequiredText(parameters, "xml", 4 * 1024 * 1024)));
        var items = new List<LibraryEntryDto>();
        foreach (var entry in parsed.Take(100)) items.Add(await MapEntryAsync(entry, ct));
        return new { items, count = parsed.Count };
    }

    private async Task<object> ApplyImportAsync(JsonElement parameters, CancellationToken ct)
    {
        var parsed = RequireSuccess(MalXmlLibraryExchange.Import(RequiredText(parameters, "xml", 4 * 1024 * 1024)));
        // MAL import supplies tracking fields only. Preserve local notes, artwork and dates absent from the XML.
        var existing = (await AllEntriesAsync(ct)).ToDictionary(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase);
        var merged = parsed.Select(entry => existing.TryGetValue(entry.MediaId, out var saved)
            ? entry with { Notes = saved.Notes, PosterUri = saved.PosterUri, StartedAt = saved.StartedAt, CompletedAt = saved.CompletedAt }
            : entry).ToArray();
        return new { importedCount = RequireSuccess(await library.MergeEntriesAsync(merged, ct)) };
    }

    private static IReadOnlyList<string> StringList(JsonElement parameters, string name, IReadOnlyList<string> fallback)
    {
        if (!parameters.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 50) throw Validation($"{name} must be an array of at most 50 strings.");
        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString()!.Length > 100) throw Validation($"Each {name} value must be a string of at most 100 characters.");
            var text = item.GetString()!.Trim();
            if (text.Length > 0) items.Add(text);
        }
        return items.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static int RequiredInteger(JsonElement parameters, string name, int minimum, int maximum)
    {
        if (!parameters.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var number) || number < minimum || number > maximum)
            throw Validation($"{name} must be an integer from {minimum} to {maximum}.");
        return number;
    }

    private static string RequiredText(JsonElement parameters, string name, int maximum)
    {
        var value = OptionalString(parameters, name, maximum);
        return string.IsNullOrWhiteSpace(value) ? throw Validation($"{name} is required.") : value;
    }

    private static int? OptionalYear(JsonElement parameters) => parameters.TryGetProperty("seasonYear", out var value) && value.ValueKind != JsonValueKind.Null
        ? RequiredInteger(parameters, "seasonYear", 1900, 2200) : null;

    private static bool Boolean(JsonElement parameters, string name, bool fallback)
    {
        if (!parameters.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Validation($"{name} must be a boolean.");
        return value.GetBoolean();
    }

    private static DateTimeOffset? OptionalDate(JsonElement parameters, string name)
    {
        var value = OptionalString(parameters, name, 50);
        if (value is null) return null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || !value.Contains('T') || !(value.EndsWith('Z') || value.Length >= 6 && value[^6] is '+' or '-'))
            throw Validation($"{name} must be an ISO date/time with an explicit timezone or null.");
        return date;
    }
}
