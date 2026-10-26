using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Bridge;

/// <summary>Dispatches the newline JSON protocol without exposing service internals to the webview.</summary>
public sealed partial class BridgeHandler(
    IMediaCatalogService catalog,
    IUserLibraryService library,
    Func<string, MediaItem?>? cachedMedia = null,
    IMediaCatalogService? localCatalog = null,
    TextWriter? diagnostics = null,
    IServiceProvider? services = null,
    Func<IReadOnlyCollection<MediaItem>, CancellationToken, Task>? persistImportedMedia = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, MediaItem> sessionMedia = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> HandleAsync(string line, CancellationToken cancellationToken = default)
    {
        string? id = null;
        try
        {
            if (line.Length > 20 * 1024 * 1024)
                throw new BridgeException("invalid_request", "Request is too large.");
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new BridgeException("invalid_request", "Request must be an object.");
            id = RequiredString(root, "id", 128);
            var method = RequiredString(root, "method", 80);
            var parameters = root.TryGetProperty("params", out var value) ? value : default;
            if (parameters.ValueKind is not JsonValueKind.Object)
                throw new BridgeException("invalid_request", "params must be an object.");
            object? result = method switch
            {
                "library.list" => await ListLibraryAsync(parameters, cancellationToken),
                "library.get" => await GetLibraryEntryAsync(parameters, cancellationToken),
                "library.lookup" => await LookupLibraryAsync(parameters, cancellationToken),
                "catalog.search" => await SearchCatalogAsync(parameters, cancellationToken),
                "media.detail" => await GetMediaDetailAsync(parameters, cancellationToken),
                "library.add" => await AddLibraryEntryAsync(parameters, cancellationToken),
                "library.progress" => await IncrementProgressAsync(parameters, cancellationToken),
                "library.score" => await UpdateScoreAsync(parameters, cancellationToken),
                "library.status" => await UpdateStatusAsync(parameters, cancellationToken),
                "library.notes" => await UpdateNotesAsync(parameters, cancellationToken),
                "library.remove" => await RemoveLibraryEntryAsync(parameters, cancellationToken),
                "library.progress.set" => await SetProgressAsync(parameters, cancellationToken),
                "library.review" => await SetReviewAsync(parameters, cancellationToken),
                "library.organization" => await SetOrganizationAsync(parameters, cancellationToken),
                "library.dates" => await SetDatesAsync(parameters, cancellationToken),
                "library.filters" => await LibraryFiltersAsync(cancellationToken),
                "home.get" => await HomeAsync(parameters, cancellationToken),
                "home.recommendations" => await RecommendationsAsync(parameters, cancellationToken),
                "schedule.list" => await ScheduleAsync(parameters, cancellationToken),
                "schedule.notify" => await NotifyScheduleAsync(parameters, cancellationToken),
                "notifications.support" => Service<INotificationService>().Support,
                "media.related" => await RelatedAsync(parameters, cancellationToken),
                "settings.get" => SettingsDto.From(await SettingsAsync(cancellationToken)),
                "settings.save" => await SaveSettingsAsync(parameters, cancellationToken),
                "anilist.connect" => await ConnectAsync(parameters, cancellationToken),
                "anilist.sync" => await SyncAsync(cancellationToken),
                "anilist.disconnect" => await DisconnectAsync(cancellationToken),
                "recognition.run" => await RecognizeAsync(parameters, cancellationToken),
                "recognition.history" => await RecognitionHistoryAsync(parameters, cancellationToken),
                "recognition.save" => await SaveRecognitionAsync(parameters, cancellationToken),
                "recognition.remove" => new { removed = RequireSuccess(await Service<IRecognitionInboxService>().RemoveAsync(RequiredString(parameters, "id", 128), cancellationToken)) },
                "library.export" => await ExportAsync(parameters, cancellationToken),
                "library.import.preview" => await PreviewImportAsync(parameters, cancellationToken),
                "library.import.apply" => await ApplyImportAsync(parameters, cancellationToken),
                "updates.check" => await UpdatesAsync(cancellationToken),
                _ => throw new BridgeException("method_not_found", "Unknown bridge method.")
            };
            return JsonSerializer.Serialize(new { id, result }, JsonOptions);
        }
        catch (JsonException)
        {
            return ErrorResponse(id, "invalid_request", "Request must contain valid JSON.");
        }
        catch (BridgeException exception)
        {
            return ErrorResponse(id, exception.Code, exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ErrorResponse(id, "cancelled", "Request was cancelled.");
        }
        catch (Exception exception)
        {
            if (diagnostics is not null)
                await diagnostics.WriteLineAsync($"Bridge request failed: {exception.GetType().Name}");
            return ErrorResponse(id, "internal_error", "The request could not be completed.");
        }
    }

    private async Task<PageDto<LibraryEntryDto>> ListLibraryAsync(JsonElement parameters, CancellationToken ct)
    {
        var page = Pagination(parameters, "page", 1, 100_000);
        var pageSize = Pagination(parameters, "pageSize", 48, 100);
        var search = OptionalString(parameters, "search", 500)?.Trim() ?? string.Empty;
        var type = OptionalEnum<MediaType>(parameters, "type");
        var status = OptionalStatus(parameters);
        var sort = OptionalString(parameters, "sort", 30) ?? "Updated";
        if (sort is not ("Updated" or "Title" or "Score" or "Progress")) throw Validation("sort is not a supported value.");
        var needsAll = search.Length > 0 || sort != "Updated";
        // The existing library service filters type/status; title search is applied to local catalog metadata here.
        // It never calls the network for each card, including when an entry has no cached title.
        var source = RequireSuccess(await library.GetEntriesAsync(new UserLibraryQuery
        {
            Type = type,
            Status = status,
            Tag = OptionalString(parameters, "tag", 100),
            CustomList = OptionalString(parameters, "customList", 100),
            Page = needsAll ? 1 : page,
            PageSize = needsAll ? int.MaxValue : pageSize
        }, ct));
        var entries = new List<LibraryEntryDto>(source.Items.Count);
        foreach (var entry in source.Items)
            entries.Add(await MapEntryAsync(entry, ct));

        if (!needsAll)
            return new(entries, page, pageSize, source.TotalCount, HasNextPage(page, pageSize, source.TotalCount));
        IEnumerable<LibraryEntryDto> matching = entries.Where(entry => search.Length == 0
            || entry.Media.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Media.Romaji.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.MediaId.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Notes?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
            || entry.Tags.Concat(entry.CustomLists).Any(value => value.Contains(search, StringComparison.OrdinalIgnoreCase)));
        matching = sort switch
        {
            "Title" => matching.OrderBy(entry => entry.Media.Title, StringComparer.OrdinalIgnoreCase),
            "Score" => matching.OrderByDescending(entry => entry.Score ?? -1),
            "Progress" => matching.OrderByDescending(entry => entry.Progress),
            _ => matching
        };
        var sorted = matching.ToArray();
        return new(sorted.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            page, pageSize, sorted.Length, HasNextPage(page, pageSize, sorted.Length));
    }

    private async Task<LibraryEntryDto?> GetLibraryEntryAsync(JsonElement parameters, CancellationToken ct)
    {
        var result = await library.GetEntryAsync(MediaId(parameters), ct);
        if (result.IsFailure && result.Error.Code == "not_found") return null;
        return await MapEntryAsync(RequireSuccess(result), ct);
    }

    private async Task<PageDto<MediaDto>> SearchCatalogAsync(JsonElement parameters, CancellationToken ct)
    {
        var query = new MediaSearchQuery
        {
            Page = Pagination(parameters, "page", 1, 100_000),
            PageSize = Pagination(parameters, "pageSize", 48, 100),
            Type = OptionalEnum<MediaType>(parameters, "type"),
            SearchText = OptionalString(parameters, "search", 500)?.Trim() ?? string.Empty,
            Sort = OptionalEnum<MediaSearchSort>(parameters, "sort") ?? MediaSearchSort.Popularity,
            Genres = StringList(parameters, "genres", []),
            SeasonYear = OptionalYear(parameters),
            Format = OptionalEnum<MediaFormat>(parameters, "format"),
            ReleaseStatus = OptionalEnum<MediaReleaseStatus>(parameters, "releaseStatus"),
            CountryCode = OptionalString(parameters, "countryCode", 2)
        };
        var source = RequireSuccess(await ReadCatalogAsync(() => catalog.SearchAsync(query, ct), ct));
        foreach (var item in source.Items) sessionMedia[item.Id] = item;
        return new(source.Items.Select(item => MediaDto.From(item)).ToArray(), source.Page, source.PageSize,
            source.TotalCount, HasNextPage(source.Page, source.PageSize, source.TotalCount));
    }

    private async Task<MediaDto> GetMediaDetailAsync(JsonElement parameters, CancellationToken ct)
    {
        var mediaId = MediaId(parameters);
        // An explicit detail request refreshes sparse local seeds and search metadata.
        // List hydration stays local; a failed detail refresh falls back to that same local metadata.
        var result = await ReadCatalogAsync(() => catalog.GetByIdAsync(mediaId, ct), ct);
        var entryResult = await library.GetEntryAsync(mediaId, ct);
        if (result.IsSuccess)
        {
            sessionMedia[mediaId] = result.Value!;
            return DetailDto(result.Value!, entryResult.IsSuccess ? entryResult.Value : null);
        }
        var local = await FindLocalMediaAsync(mediaId, ct);
        if (local is not null) return DetailDto(local, entryResult.IsSuccess ? entryResult.Value : null);
        if (entryResult.IsSuccess) return MediaDto.Missing(entryResult.Value!);
        return MediaDto.From(RequireSuccess(result));
    }

    private static MediaDto DetailDto(MediaItem media, UserMediaEntry? entry) =>
        MediaDto.From(media, media.Images.PosterUri is null ? entry?.PosterUri : null);

    private async Task<Result<T>> ReadCatalogAsync<T>(Func<Task<Result<T>>> fetch, CancellationToken ct)
    {
        try
        {
            return await fetch();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is OperationCanceledException && !ct.IsCancellationRequested)
        {
            if (diagnostics is not null)
                await diagnostics.WriteLineAsync($"Catalog request failed: {exception.GetType().Name}");
            return Result<T>.Failure(ApplicationError.ExternalService("The catalog is temporarily unavailable."));
        }
    }

    private async Task<LibraryEntryDto> AddLibraryEntryAsync(JsonElement parameters, CancellationToken ct)
    {
        var mediaId = MediaId(parameters);
        var status = OptionalStatus(parameters) ?? MediaListStatus.Planning;
        var existing = await library.GetEntryAsync(mediaId, ct);
        if (existing.IsSuccess) return await MapEntryAsync(existing.Value!, ct);
        if (existing.Error.Code != "not_found") RequireSuccess(existing);
        var media = await FindLocalMediaAsync(mediaId, ct)
            ?? RequireSuccess(await ReadCatalogAsync(() => catalog.GetByIdAsync(mediaId, ct), ct));
        sessionMedia[mediaId] = media;
        var entry = new UserMediaEntry(media.Id, media.Type, status)
        {
            PosterUri = media.Images.PosterUri,
            StartedAt = status is MediaListStatus.Current or MediaListStatus.Repeating ? DateTimeOffset.UtcNow : null,
            CompletedAt = status == MediaListStatus.Completed ? DateTimeOffset.UtcNow : null
        };
        return await MapEntryAsync(RequireSuccess(await library.UpsertEntryAsync(entry, ct)), ct);
    }

    private async Task<LibraryEntryDto> IncrementProgressAsync(JsonElement parameters, CancellationToken ct)
    {
        var mediaId = MediaId(parameters);
        var amount = Pagination(parameters, "amount", 1, 1_000_000);
        var entry = RequireSuccess(await library.GetEntryAsync(mediaId, ct));
        if (entry.Progress > int.MaxValue - amount)
            throw Validation("Progress exceeds the supported range.");
        return await MapEntryAsync(RequireSuccess(await library.IncrementProgressAsync(mediaId, amount, ct)), ct);
    }

    private async Task<LibraryEntryDto> UpdateScoreAsync(JsonElement parameters, CancellationToken ct)
    {
        var mediaId = MediaId(parameters);
        if (!parameters.TryGetProperty("score", out var scoreElement))
            throw Validation("score is required; use null to clear the rating.");
        int? score = null;
        if (scoreElement.ValueKind != JsonValueKind.Null)
        {
            if (scoreElement.ValueKind != JsonValueKind.Number || !scoreElement.TryGetInt32(out var number)
                || number is < 0 or > 10)
                throw Validation("score must be an integer from 0 to 10 or null.");
            score = number;
        }
        return await MapEntryAsync(RequireSuccess(await library.UpdateScoreAsync(mediaId, score, ct)), ct);
    }

    private async Task<LibraryEntryDto> UpdateStatusAsync(JsonElement parameters, CancellationToken ct)
    {
        var mediaId = MediaId(parameters);
        var status = OptionalStatus(parameters) ?? throw Validation("status is required.");
        return await MapEntryAsync(RequireSuccess(await library.UpdateStatusAsync(mediaId, status, ct)), ct);
    }

    private async Task<LibraryEntryDto> UpdateNotesAsync(JsonElement parameters, CancellationToken ct)
    {
        var mediaId = MediaId(parameters);
        if (!parameters.TryGetProperty("notes", out _)) throw Validation("notes is required.");
        var notes = OptionalString(parameters, "notes", 20_000);
        return await MapEntryAsync(RequireSuccess(await library.UpdateNotesAsync(mediaId, notes, ct)), ct);
    }

    private async Task<object> RemoveLibraryEntryAsync(JsonElement parameters, CancellationToken ct)
    {
        var mediaId = MediaId(parameters);
        var removed = RequireSuccess(await library.RemoveEntryAsync(mediaId, ct));
        return new { mediaId, removed };
    }

    private async Task<LibraryEntryDto> MapEntryAsync(UserMediaEntry entry, CancellationToken ct)
    {
        var media = await FindLocalMediaAsync(entry.MediaId, ct);
        return new(entry.MediaId, entry.MediaType.ToString(),
            entry.Status == MediaListStatus.Current ? "Watching" : entry.Status.ToString(),
            entry.Progress, entry.Score, entry.Notes, entry.Review, entry.UpdatedAt,
            media is null ? MediaDto.Missing(entry) : MediaDto.From(media, entry.PosterUri))
        {
            Tags = entry.Tags,
            CustomLists = entry.CustomLists,
            StartedAt = entry.StartedAt,
            CompletedAt = entry.CompletedAt
        };
    }

    private async Task<MediaItem?> FindLocalMediaAsync(string id, CancellationToken ct)
    {
        if (sessionMedia.TryGetValue(id, out var item)) return item;
        item = cachedMedia?.Invoke(id);
        if (item is not null) return item;
        if (localCatalog is null) return null;
        var result = await localCatalog.GetByIdAsync(id, ct);
        if (result.IsFailure) return null;
        sessionMedia[id] = result.Value!;
        return result.Value;
    }

    private static int Pagination(JsonElement parameters, string name, int defaultValue, int maximum)
    {
        if (!parameters.TryGetProperty(name, out var value)) return defaultValue;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number)
            || number < 1 || number > maximum)
            throw Validation($"{name} must be an integer from 1 to {maximum}.");
        return number;
    }

    private static string MediaId(JsonElement parameters) => RequiredString(parameters, "mediaId", 200);

    private static string RequiredString(JsonElement parent, string name, int maximumLength)
    {
        var value = OptionalString(parent, name, maximumLength);
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw Validation($"{name} must be a nonempty string without control characters.");
        return value.Trim();
    }

    private static string? OptionalString(JsonElement parent, string name, int maximumLength)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String || value.GetString()!.Length > maximumLength)
            throw Validation($"{name} must be a string with at most {maximumLength} characters.");
        return value.GetString();
    }

    private static T? OptionalEnum<T>(JsonElement parameters, string name) where T : struct, Enum
    {
        var value = OptionalString(parameters, name, 50);
        if (value is null) return null;
        var match = Enum.GetNames<T>().FirstOrDefault(option => option.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (match is null) throw Validation($"{name} is not a supported value.");
        return Enum.Parse<T>(match);
    }

    private static MediaListStatus? OptionalStatus(JsonElement parameters)
    {
        var value = OptionalString(parameters, "status", 50);
        if (value?.Equals("Watching", StringComparison.OrdinalIgnoreCase) == true) return MediaListStatus.Current;
        return OptionalEnum<MediaListStatus>(parameters, "status");
    }

    private static bool HasNextPage(int page, int pageSize, int? total) => total.HasValue && (long)page * pageSize < total.Value;

    private static T RequireSuccess<T>(Result<T> result)
    {
        if (result.IsSuccess) return result.Value!;
        var message = result.Error.Code switch
        {
            "validation_error" => result.Error.Message,
            "not_found" => "The requested title or library entry was not found.",
            "external_service_error" => "The catalog is temporarily unavailable.",
            "storage_error" => "The library could not be saved or loaded.",
            _ => "The request could not be completed."
        };
        var code = result.Error.Code is "validation_error" or "not_found" or "external_service_error" or "storage_error"
            ? result.Error.Code : "internal_error";
        throw new BridgeException(code, message);
    }

    private static BridgeException Validation(string message) => new("validation_error", message);
    private static string ErrorResponse(string? id, string code, string message) =>
        JsonSerializer.Serialize(new { id, error = new BridgeError(code, message) }, JsonOptions);

    private sealed class BridgeException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
