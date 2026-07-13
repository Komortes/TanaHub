using System.Text.Json;
using System.Text.Json.Serialization;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Common;

namespace TanaHub.Infrastructure.Library;

public sealed class FileUserLibraryService : IUserLibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string storagePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, UserMediaEntry> entries;

    public FileUserLibraryService(
        string storagePath,
        IEnumerable<UserMediaEntry>? seedEntries = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        this.storagePath = storagePath;
        entries = LoadEntries(storagePath, seedEntries ?? []);
    }

    public async Task<Result<PagedResult<UserMediaEntry>>> GetEntriesAsync(
        UserLibraryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
        {
            return Failure<PagedResult<UserMediaEntry>>("Page must be greater than zero.");
        }

        if (query.PageSize < 1)
        {
            return Failure<PagedResult<UserMediaEntry>>("Page size must be greater than zero.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            IEnumerable<UserMediaEntry> filtered = entries.Values;

            if (query.Type is not null)
            {
                filtered = filtered.Where(entry => entry.MediaType == query.Type.Value);
            }

            if (query.Status is not null)
            {
                filtered = filtered.Where(entry => entry.Status == query.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Tag))
            {
                filtered = filtered.Where(entry => entry.Tags.Any(tag =>
                    tag.Equals(query.Tag, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(query.CustomList))
            {
                filtered = filtered.Where(entry => entry.CustomLists.Any(list =>
                    list.Equals(query.CustomList, StringComparison.OrdinalIgnoreCase)));
            }

            var totalCount = filtered.Count();
            var pageItems = filtered
                .OrderByDescending(entry => entry.UpdatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();

            return Result<PagedResult<UserMediaEntry>>.Success(new PagedResult<UserMediaEntry>(
                pageItems,
                query.Page,
                query.PageSize,
                totalCount));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<UserMediaEntry>> GetEntryAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return Failure<UserMediaEntry>("Media id is required.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            return entries.TryGetValue(mediaId, out var entry)
                ? Result<UserMediaEntry>.Success(entry)
                : Result<UserMediaEntry>.Failure(
                    ApplicationError.NotFound($"Library entry '{mediaId}' was not found."));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<UserMediaEntry>> UpsertEntryAsync(
        UserMediaEntry entry,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var updated = entry with
            {
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var candidate = CloneEntries();
            candidate[entry.MediaId] = updated;
            return await CommitAsync(candidate, updated, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<int>> MergeEntriesAsync(
        IReadOnlyCollection<UserMediaEntry> incomingEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incomingEntries);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var candidate = new Dictionary<string, UserMediaEntry>(entries, StringComparer.OrdinalIgnoreCase);
            var updatedAt = DateTimeOffset.UtcNow;

            foreach (var incoming in incomingEntries)
            {
                candidate[incoming.MediaId] = candidate.TryGetValue(incoming.MediaId, out var existing)
                    ? incoming with
                    {
                        Review = existing.Review,
                        Tags = existing.Tags,
                        CustomLists = existing.CustomLists,
                        UpdatedAt = updatedAt
                    }
                    : incoming with { UpdatedAt = updatedAt };
            }

            return await CommitAsync(candidate, incomingEntries.Count, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<UserMediaEntry>> IncrementProgressAsync(
        string mediaId,
        int amount = 1,
        CancellationToken cancellationToken = default)
    {
        if (amount < 1)
        {
            return Failure<UserMediaEntry>("Amount must be greater than zero.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!entries.TryGetValue(mediaId, out var entry))
            {
                return Result<UserMediaEntry>.Failure(
                    ApplicationError.NotFound($"Library entry '{mediaId}' was not found."));
            }

            var updated = entry.IncrementProgress(amount);
            var candidate = CloneEntries();
            candidate[mediaId] = updated;
            return await CommitAsync(candidate, updated, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<UserMediaEntry>> UpdateStatusAsync(
        string mediaId,
        MediaListStatus status,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!entries.TryGetValue(mediaId, out var entry))
            {
                return Result<UserMediaEntry>.Failure(
                    ApplicationError.NotFound($"Library entry '{mediaId}' was not found."));
            }

            var updated = entry with
            {
                Status = status,
                StartedAt = status == MediaListStatus.Current && entry.StartedAt is null ? DateTimeOffset.UtcNow : entry.StartedAt,
                CompletedAt = status == MediaListStatus.Completed ? DateTimeOffset.UtcNow : entry.CompletedAt,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var candidate = CloneEntries();
            candidate[mediaId] = updated;
            return await CommitAsync(candidate, updated, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<UserMediaEntry>> UpdateScoreAsync(
        string mediaId,
        int? score,
        CancellationToken cancellationToken = default)
    {
        if (score is < 0 or > 10)
        {
            return Failure<UserMediaEntry>("Score must be between 0 and 10.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!entries.TryGetValue(mediaId, out var entry))
            {
                return Result<UserMediaEntry>.Failure(
                    ApplicationError.NotFound($"Library entry '{mediaId}' was not found."));
            }

            var updated = entry with
            {
                Score = score,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var candidate = CloneEntries();
            candidate[mediaId] = updated;
            return await CommitAsync(candidate, updated, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<UserMediaEntry>> UpdateNotesAsync(
        string mediaId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!entries.TryGetValue(mediaId, out var entry))
            {
                return Result<UserMediaEntry>.Failure(
                    ApplicationError.NotFound($"Library entry '{mediaId}' was not found."));
            }

            var updated = entry with { Notes = notes, UpdatedAt = DateTimeOffset.UtcNow };
            var candidate = CloneEntries();
            candidate[mediaId] = updated;
            return await CommitAsync(candidate, updated, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<UserMediaEntry>> UpdateReviewAsync(
        string mediaId,
        string? review,
        CancellationToken cancellationToken = default)
    {
        var normalizedReview = string.IsNullOrWhiteSpace(review) ? null : review.Trim();
        if (normalizedReview?.Length > 4000)
        {
            return Failure<UserMediaEntry>("Review must be 4,000 characters or fewer.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!entries.TryGetValue(mediaId, out var entry))
            {
                return Result<UserMediaEntry>.Failure(
                    ApplicationError.NotFound($"Library entry '{mediaId}' was not found."));
            }

            var updated = entry with { Review = normalizedReview, UpdatedAt = DateTimeOffset.UtcNow };
            var candidate = CloneEntries();
            candidate[mediaId] = updated;
            return await CommitAsync(candidate, updated, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<UserMediaEntry>> UpdateOrganizationAsync(
        string mediaId,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> customLists,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!entries.TryGetValue(mediaId, out var entry))
            {
                return Result<UserMediaEntry>.Failure(
                    ApplicationError.NotFound($"Library entry '{mediaId}' was not found."));
            }

            var updated = entry with
            {
                Tags = NormalizeValues(tags),
                CustomLists = NormalizeValues(customLists),
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var candidate = CloneEntries();
            candidate[mediaId] = updated;
            return await CommitAsync(candidate, updated, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<bool>> RemoveEntryAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var candidate = CloneEntries();
            var removed = candidate.Remove(mediaId);
            if (!removed)
            {
                return Result<bool>.Success(false);
            }

            return await CommitAsync(candidate, true, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static Dictionary<string, UserMediaEntry> LoadEntries(
        string storagePath,
        IEnumerable<UserMediaEntry> seedEntries)
    {
        if (!File.Exists(storagePath))
        {
            return seedEntries.ToDictionary(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(storagePath);
            var dtos = JsonSerializer.Deserialize<List<UserMediaEntryDto>>(json, JsonOptions) ?? [];

            return dtos
                .Select(ToDomain)
                .ToDictionary(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            var backupPath = storagePath + ".bak";
            try { File.Copy(storagePath, backupPath, overwrite: true); } catch { /* best-effort */ }
            return seedEntries.ToDictionary(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveAsync(
        IEnumerable<UserMediaEntry> snapshot,
        CancellationToken cancellationToken)
    {
        var dtos = snapshot
            .OrderBy(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase)
            .Select(UserMediaEntryDto.FromDomain)
            .ToArray();

        await AtomicFileWriter.WriteJsonAsync(storagePath, dtos, JsonOptions, cancellationToken);
    }

    private Dictionary<string, UserMediaEntry> CloneEntries()
    {
        return new Dictionary<string, UserMediaEntry>(entries, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Result<T>> CommitAsync<T>(
        Dictionary<string, UserMediaEntry> candidate,
        T value,
        CancellationToken cancellationToken)
    {
        try
        {
            await SaveAsync(candidate.Values, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Result<T>.Failure(ApplicationError.Storage("Library could not be saved."));
        }

        entries.Clear();
        foreach (var pair in candidate)
        {
            entries[pair.Key] = pair.Value;
        }

        return Result<T>.Success(value);
    }

    private static UserMediaEntry ToDomain(UserMediaEntryDto dto)
    {
        return new UserMediaEntry(dto.MediaId, dto.MediaType, dto.Status)
        {
            Progress = dto.Progress,
            Score = dto.Score,
            PosterUri = Uri.TryCreate(dto.PosterUri, UriKind.Absolute, out var posterUri) ? posterUri : null,
            Notes = dto.Notes,
            Review = dto.Review,
            Tags = dto.Tags ?? [],
            CustomLists = dto.CustomLists ?? [],
            StartedAt = dto.StartedAt,
            CompletedAt = dto.CompletedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    private static Result<T> Failure<T>(string message)
    {
        return Result<T>.Failure(ApplicationError.Validation(message));
    }

    private static IReadOnlyList<string> NormalizeValues(IReadOnlyList<string> values)
    {
        return values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record UserMediaEntryDto(
        string MediaId,
        MediaType MediaType,
        MediaListStatus Status,
        int Progress,
        int? Score,
        string? PosterUri,
        string? Notes,
        string? Review,
        IReadOnlyList<string>? Tags,
        IReadOnlyList<string>? CustomLists,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        DateTimeOffset UpdatedAt)
    {
        public static UserMediaEntryDto FromDomain(UserMediaEntry entry)
        {
            return new UserMediaEntryDto(
                entry.MediaId,
                entry.MediaType,
                entry.Status,
                entry.Progress,
                entry.Score,
                entry.PosterUri?.ToString(),
                entry.Notes,
                entry.Review,
                entry.Tags,
                entry.CustomLists,
                entry.StartedAt,
                entry.CompletedAt,
                entry.UpdatedAt);
        }
    }
}
