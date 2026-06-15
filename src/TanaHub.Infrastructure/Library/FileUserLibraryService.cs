using System.Text.Json;
using System.Text.Json.Serialization;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

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

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                filtered = filtered.Where(entry => entry.MediaId.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase));
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

            entries[entry.MediaId] = updated;
            await SaveAsync(cancellationToken);
            return Result<UserMediaEntry>.Success(updated);
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
            entries[mediaId] = updated;
            await SaveAsync(cancellationToken);
            return Result<UserMediaEntry>.Success(updated);
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

            entries[mediaId] = updated;
            await SaveAsync(cancellationToken);
            return Result<UserMediaEntry>.Success(updated);
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

            entries[mediaId] = updated;
            await SaveAsync(cancellationToken);
            return Result<UserMediaEntry>.Success(updated);
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
            entries[mediaId] = updated;
            await SaveAsync(cancellationToken);
            return Result<UserMediaEntry>.Success(updated);
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
            var removed = entries.Remove(mediaId);
            if (removed)
            {
                await SaveAsync(cancellationToken);
            }

            return Result<bool>.Success(removed);
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
        catch
        {
            return seedEntries.ToDictionary(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var dtos = entries.Values
            .OrderBy(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase)
            .Select(UserMediaEntryDto.FromDomain)
            .ToArray();

        await using var stream = File.Create(storagePath);
        await JsonSerializer.SerializeAsync(stream, dtos, JsonOptions, cancellationToken);
    }

    private static UserMediaEntry ToDomain(UserMediaEntryDto dto)
    {
        return new UserMediaEntry(dto.MediaId, dto.MediaType, dto.Status)
        {
            Progress = dto.Progress,
            Score = dto.Score,
            PosterUri = Uri.TryCreate(dto.PosterUri, UriKind.Absolute, out var posterUri) ? posterUri : null,
            Notes = dto.Notes,
            StartedAt = dto.StartedAt,
            CompletedAt = dto.CompletedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    private static Result<T> Failure<T>(string message)
    {
        return Result<T>.Failure(ApplicationError.Validation(message));
    }

    private sealed record UserMediaEntryDto(
        string MediaId,
        MediaType MediaType,
        MediaListStatus Status,
        int Progress,
        int? Score,
        string? PosterUri,
        string? Notes,
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
                entry.StartedAt,
                entry.CompletedAt,
                entry.UpdatedAt);
        }
    }
}
