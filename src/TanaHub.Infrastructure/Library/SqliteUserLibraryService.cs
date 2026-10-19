using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Library;

public sealed class SqliteUserLibraryService : IUserLibraryService
{
    private const string MediaIdCollation = "ORDINAL_IGNORE_CASE";
    private readonly IDbContextFactory<LibraryDbContext> dbContextFactory;

    public SqliteUserLibraryService(IDbContextFactory<LibraryDbContext> dbContextFactory)
    {
        this.dbContextFactory = dbContextFactory;
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

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IEnumerable<UserMediaEntry> filtered = (await db.Entries.AsNoTracking().ToListAsync(cancellationToken))
            .Select(ToDomain);

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

        var materialized = filtered.ToArray();
        var totalCount = materialized.Length;
        var pageItems = materialized
            .OrderByDescending(entry => entry.UpdatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        return Result<PagedResult<UserMediaEntry>>.Success(new PagedResult<UserMediaEntry>(
            pageItems, query.Page, query.PageSize, totalCount));
    }

    public async Task<Result<UserMediaEntry>> GetEntryAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return Failure<UserMediaEntry>("Media id is required.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindByIdIgnoreCase(db, mediaId).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return entity is not null
            ? Result<UserMediaEntry>.Success(ToDomain(entity))
            : Result<UserMediaEntry>.Failure(ApplicationError.NotFound($"Library entry '{mediaId}' was not found."));
    }

    public async Task<Result<UserMediaEntry>> UpsertEntryAsync(
        UserMediaEntry entry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await FindByIdIgnoreCase(db, entry.MediaId).FirstOrDefaultAsync(cancellationToken);
            var updatedAt = DateTimeOffset.UtcNow;

            if (existing is not null)
            {
                ApplyDomainFields(existing, entry, existing.MediaId, updatedAt);
            }
            else
            {
                existing = ToEntity(entry with { UpdatedAt = updatedAt });
                db.Entries.Add(existing);
            }

            await db.SaveChangesAsync(cancellationToken);
            return Result<UserMediaEntry>.Success(ToDomain(existing));
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            return Result<UserMediaEntry>.Failure(ApplicationError.Storage("Library could not be saved."));
        }
    }

    public async Task<Result<int>> MergeEntriesAsync(
        IReadOnlyCollection<UserMediaEntry> incomingEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incomingEntries);
        if (incomingEntries.Count == 0)
        {
            return Result<int>.Success(0);
        }

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            ConfigureMediaIdCollation(db);
            var rows = new Dictionary<string, UserMediaEntryEntity>(StringComparer.OrdinalIgnoreCase);
            // Bound parameter counts for large imports and only materialize entries being merged.
            foreach (var ids in incomingEntries.Select(entry => entry.MediaId)
                         .Distinct(StringComparer.OrdinalIgnoreCase).Chunk(500))
            {
                var matching = await db.Entries
                    .Where(row => ids.Contains(EF.Functions.Collate(row.MediaId, MediaIdCollation)))
                    .ToListAsync(cancellationToken);
                foreach (var row in matching)
                {
                    rows.TryAdd(row.MediaId, row);
                }
            }
            var updatedAt = DateTimeOffset.UtcNow;

            foreach (var incoming in incomingEntries)
            {
                if (rows.TryGetValue(incoming.MediaId, out var existing))
                {
                    var merged = incoming with
                    {
                        Review = existing.Review,
                        Tags = existing.Tags,
                        CustomLists = existing.CustomLists,
                        UpdatedAt = updatedAt
                    };
                    ApplyDomainFields(existing, merged, existing.MediaId, updatedAt);
                }
                else
                {
                    var created = ToEntity(incoming with { UpdatedAt = updatedAt });
                    db.Entries.Add(created);
                    rows.Add(created.MediaId, created);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(incomingEntries.Count);
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            return Result<int>.Failure(ApplicationError.Storage("Library could not be saved."));
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

        try
        {
            return await MutateAsync(mediaId, (entry, _) => entry.IncrementProgress(amount), cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == nameof(amount))
        {
            return Failure<UserMediaEntry>("Progress cannot exceed 2,147,483,647.");
        }
    }

    public async Task<Result<UserMediaEntry>> UpdateStatusAsync(
        string mediaId,
        MediaListStatus status,
        CancellationToken cancellationToken = default)
    {
        return await MutateAsync(mediaId, (entry, updatedAt) => entry with
        {
            Status = status,
            StartedAt = status == MediaListStatus.Current && entry.StartedAt is null
                && (entry.CompletedAt is null || entry.CompletedAt >= updatedAt) ? updatedAt : entry.StartedAt,
            CompletedAt = status == MediaListStatus.Completed && entry.CompletedAt is null
                && (entry.StartedAt is null || entry.StartedAt <= updatedAt) ? updatedAt : entry.CompletedAt
        }, cancellationToken);
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

        return await MutateAsync(mediaId, (entry, _) => entry with { Score = score }, cancellationToken);
    }

    public async Task<Result<UserMediaEntry>> UpdateNotesAsync(
        string mediaId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        return await MutateAsync(mediaId, (entry, _) => entry with { Notes = notes }, cancellationToken);
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

        return await MutateAsync(mediaId, (entry, _) => entry with { Review = normalizedReview }, cancellationToken);
    }

    public async Task<Result<UserMediaEntry>> UpdateOrganizationAsync(
        string mediaId,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> customLists,
        CancellationToken cancellationToken = default)
    {
        return await MutateAsync(mediaId, (entry, _) => entry with
        {
            Tags = NormalizeValues(tags),
            CustomLists = NormalizeValues(customLists)
        }, cancellationToken);
    }

    public async Task<Result<bool>> RemoveEntryAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await FindByIdIgnoreCase(db, mediaId).FirstOrDefaultAsync(cancellationToken);
            if (existing is null)
            {
                return Result<bool>.Success(false);
            }

            db.Entries.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            return Result<bool>.Failure(ApplicationError.Storage("Library could not be saved."));
        }
    }

    public static async Task ImportFromJsonIfNeededAsync(
        LibraryDbContext db, string jsonPath, CancellationToken cancellationToken = default)
    {
        if (await db.Entries.AnyAsync(cancellationToken).ConfigureAwait(false)) return;
        if (!File.Exists(jsonPath)) return;

        try
        {
            // Startup bridges this import synchronously; never queue continuations on the UI thread.
            var json = await File.ReadAllTextAsync(jsonPath, cancellationToken).ConfigureAwait(false);
            var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
            var dtos = JsonSerializer.Deserialize<List<LegacyLibraryEntryDto>>(json, options) ?? [];

            foreach (var dto in dtos)
            {
                db.Entries.Add(new UserMediaEntryEntity
                {
                    MediaId = dto.MediaId,
                    MediaType = dto.MediaType,
                    Status = dto.Status,
                    Progress = dto.Progress,
                    Score = dto.Score,
                    PosterUri = dto.PosterUri,
                    Notes = dto.Notes,
                    Review = dto.Review,
                    Tags = dto.Tags ?? [],
                    CustomLists = dto.CustomLists ?? [],
                    StartedAt = dto.StartedAt,
                    CompletedAt = dto.CompletedAt,
                    UpdatedAt = dto.UpdatedAt
                });
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            File.Move(jsonPath, jsonPath + ".bak", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            try { File.Copy(jsonPath, jsonPath + ".bak", overwrite: true); } catch { /* best-effort */ }
        }
    }

    private async Task<Result<UserMediaEntry>> MutateAsync(
        string mediaId,
        Func<UserMediaEntry, DateTimeOffset, UserMediaEntry> mutate,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await FindByIdIgnoreCase(db, mediaId).FirstOrDefaultAsync(cancellationToken);
            if (existing is null)
            {
                return Result<UserMediaEntry>.Failure(
                    ApplicationError.NotFound($"Library entry '{mediaId}' was not found."));
            }

            var updatedAt = DateTimeOffset.UtcNow;
            var mutated = mutate(ToDomain(existing), updatedAt) with { UpdatedAt = updatedAt };
            ApplyDomainFields(existing, mutated, existing.MediaId, updatedAt);

            await db.SaveChangesAsync(cancellationToken);
            return Result<UserMediaEntry>.Success(ToDomain(existing));
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            return Result<UserMediaEntry>.Failure(ApplicationError.Storage("Library could not be saved."));
        }
    }

    private static void ApplyDomainFields(
        UserMediaEntryEntity target, UserMediaEntry source, string preservedMediaId, DateTimeOffset updatedAt)
    {
        target.MediaId = preservedMediaId;
        target.MediaType = source.MediaType.ToString();
        target.Status = source.Status.ToString();
        target.Progress = source.Progress;
        target.Score = source.Score;
        target.PosterUri = source.PosterUri?.ToString();
        target.Notes = source.Notes;
        target.Review = source.Review;
        target.Tags = source.Tags.ToList();
        target.CustomLists = source.CustomLists.ToList();
        target.StartedAt = source.StartedAt;
        target.CompletedAt = source.CompletedAt;
        target.UpdatedAt = updatedAt;
    }

    private static IQueryable<UserMediaEntryEntity> FindByIdIgnoreCase(LibraryDbContext db, string mediaId)
    {
        ConfigureMediaIdCollation(db);
        return db.Entries.Where(row => EF.Functions.Collate(row.MediaId, MediaIdCollation) == mediaId);
    }

    private static void ConfigureMediaIdCollation(LibraryDbContext db)
    {
        // SQLite's built-in NOCASE only handles ASCII; IDs previously used .NET ordinal comparison.
        ((SqliteConnection)db.Database.GetDbConnection()).CreateCollation(
            MediaIdCollation, StringComparer.OrdinalIgnoreCase.Compare);
    }

    private static UserMediaEntry ToDomain(UserMediaEntryEntity entity) => new(
        entity.MediaId,
        Enum.Parse<MediaType>(entity.MediaType),
        Enum.Parse<MediaListStatus>(entity.Status))
    {
        Progress = entity.Progress,
        Score = entity.Score,
        PosterUri = Uri.TryCreate(entity.PosterUri, UriKind.Absolute, out var uri) ? uri : null,
        Notes = entity.Notes,
        Review = entity.Review,
        Tags = entity.Tags,
        CustomLists = entity.CustomLists,
        StartedAt = entity.StartedAt,
        CompletedAt = entity.CompletedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static UserMediaEntryEntity ToEntity(UserMediaEntry entry) => new()
    {
        MediaId = entry.MediaId,
        MediaType = entry.MediaType.ToString(),
        Status = entry.Status.ToString(),
        Progress = entry.Progress,
        Score = entry.Score,
        PosterUri = entry.PosterUri?.ToString(),
        Notes = entry.Notes,
        Review = entry.Review,
        Tags = entry.Tags.ToList(),
        CustomLists = entry.CustomLists.ToList(),
        StartedAt = entry.StartedAt,
        CompletedAt = entry.CompletedAt,
        UpdatedAt = entry.UpdatedAt
    };

    private static Result<T> Failure<T>(string message) =>
        Result<T>.Failure(ApplicationError.Validation(message));

    private static IReadOnlyList<string> NormalizeValues(IReadOnlyList<string> values)
    {
        return values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record LegacyLibraryEntryDto(
        string MediaId, string MediaType, string Status, int Progress, int? Score,
        string? PosterUri, string? Notes, string? Review,
        List<string>? Tags, List<string>? CustomLists,
        DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, DateTimeOffset UpdatedAt);
}
