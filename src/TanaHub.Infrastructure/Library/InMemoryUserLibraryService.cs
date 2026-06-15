using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Library;

public sealed class InMemoryUserLibraryService : IUserLibraryService
{
    private readonly Dictionary<string, UserMediaEntry> entries;

    public InMemoryUserLibraryService()
        : this([])
    {
    }

    public InMemoryUserLibraryService(IEnumerable<UserMediaEntry> seedEntries)
    {
        entries = seedEntries.ToDictionary(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase);
    }

    public Task<Result<PagedResult<UserMediaEntry>>> GetEntriesAsync(
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

        var page = new PagedResult<UserMediaEntry>(pageItems, query.Page, query.PageSize, totalCount);
        return Task.FromResult(Result<PagedResult<UserMediaEntry>>.Success(page));
    }

    public Task<Result<UserMediaEntry>> UpsertEntryAsync(
        UserMediaEntry entry,
        CancellationToken cancellationToken = default)
    {
        entries[entry.MediaId] = entry with
        {
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(Result<UserMediaEntry>.Success(entries[entry.MediaId]));
    }

    public Task<Result<UserMediaEntry>> IncrementProgressAsync(
        string mediaId,
        int amount = 1,
        CancellationToken cancellationToken = default)
    {
        if (!entries.TryGetValue(mediaId, out var entry))
        {
            return Task.FromResult(Result<UserMediaEntry>.Failure(
                ApplicationError.NotFound($"Library entry '{mediaId}' was not found.")));
        }

        var updated = entry.IncrementProgress(amount);
        entries[mediaId] = updated;
        return Task.FromResult(Result<UserMediaEntry>.Success(updated));
    }

    public Task<Result<UserMediaEntry>> UpdateStatusAsync(
        string mediaId,
        MediaListStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!entries.TryGetValue(mediaId, out var entry))
        {
            return Task.FromResult(Result<UserMediaEntry>.Failure(
                ApplicationError.NotFound($"Library entry '{mediaId}' was not found.")));
        }

        var updated = entry with
        {
            Status = status,
            StartedAt = status == MediaListStatus.Current && entry.StartedAt is null ? DateTimeOffset.UtcNow : entry.StartedAt,
            CompletedAt = status == MediaListStatus.Completed ? DateTimeOffset.UtcNow : entry.CompletedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        entries[mediaId] = updated;
        return Task.FromResult(Result<UserMediaEntry>.Success(updated));
    }

    public Task<Result<UserMediaEntry>> UpdateScoreAsync(
        string mediaId,
        int? score,
        CancellationToken cancellationToken = default)
    {
        if (score is < 0 or > 10)
        {
            return Failure<UserMediaEntry>("Score must be between 0 and 10.");
        }

        if (!entries.TryGetValue(mediaId, out var entry))
        {
            return Task.FromResult(Result<UserMediaEntry>.Failure(
                ApplicationError.NotFound($"Library entry '{mediaId}' was not found.")));
        }

        var updated = entry with
        {
            Score = score,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        entries[mediaId] = updated;
        return Task.FromResult(Result<UserMediaEntry>.Success(updated));
    }

    public Task<Result<UserMediaEntry>> UpdateNotesAsync(
        string mediaId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (!entries.TryGetValue(mediaId, out var entry))
        {
            return Task.FromResult(Result<UserMediaEntry>.Failure(
                ApplicationError.NotFound($"Library entry '{mediaId}' was not found.")));
        }

        var updated = entry with { Notes = notes, UpdatedAt = DateTimeOffset.UtcNow };
        entries[mediaId] = updated;
        return Task.FromResult(Result<UserMediaEntry>.Success(updated));
    }

    public Task<Result<bool>> RemoveEntryAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        var removed = entries.Remove(mediaId);
        return Task.FromResult(Result<bool>.Success(removed));
    }

    private static Task<Result<T>> Failure<T>(string message)
    {
        return Task.FromResult(Result<T>.Failure(ApplicationError.Validation(message)));
    }
}
