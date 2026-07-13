using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Common;

namespace TanaHub.Infrastructure.Recognition;

public sealed class FileRecognitionInboxService : IRecognitionInboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string storagePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, RecognitionAttempt> attempts;

    public FileRecognitionInboxService(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        this.storagePath = storagePath;
        attempts = LoadAttempts(storagePath);
    }

    public async Task<Result<IReadOnlyList<RecognitionAttempt>>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1)
        {
            return Failure<IReadOnlyList<RecognitionAttempt>>("Limit must be greater than zero.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var items = attempts.Values
                .OrderByDescending(attempt => attempt.CreatedAt)
                .Take(limit)
                .ToArray();

            return Result<IReadOnlyList<RecognitionAttempt>>.Success(items);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<RecognitionAttempt>> SaveAsync(
        RecognitionAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(attempt.Id))
        {
            return Failure<RecognitionAttempt>("Recognition attempt id is required.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var saved = attempt with
            {
                CreatedAt = attempt.CreatedAt == default ? DateTimeOffset.UtcNow : attempt.CreatedAt
            };

            var candidate = CloneAttempts();
            candidate[saved.Id] = saved;
            candidate = candidate.Values
                .OrderByDescending(item => item.CreatedAt)
                .Take(200)
                .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

            return await CommitAsync(candidate, saved, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<bool>> RemoveAsync(
        string attemptId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(attemptId))
        {
            return Failure<bool>("Recognition attempt id is required.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var candidate = CloneAttempts();
            var removed = candidate.Remove(attemptId);
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

    private static Dictionary<string, RecognitionAttempt> LoadAttempts(string storagePath)
    {
        if (!File.Exists(storagePath))
        {
            return new Dictionary<string, RecognitionAttempt>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(storagePath);
            var dtos = JsonSerializer.Deserialize<List<RecognitionAttemptDto>>(json, JsonOptions) ?? [];

            return dtos
                .Select(ToDomain)
                .Where(attempt => !string.IsNullOrWhiteSpace(attempt.Id))
                .ToDictionary(attempt => attempt.Id, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            return new Dictionary<string, RecognitionAttempt>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveFileAsync(
        IEnumerable<RecognitionAttempt> snapshot,
        CancellationToken cancellationToken)
    {
        var dtos = snapshot
            .OrderByDescending(attempt => attempt.CreatedAt)
            .Take(200)
            .Select(RecognitionAttemptDto.FromDomain)
            .ToArray();

        await AtomicFileWriter.WriteJsonAsync(storagePath, dtos, JsonOptions, cancellationToken);
    }

    private Dictionary<string, RecognitionAttempt> CloneAttempts()
    {
        return new Dictionary<string, RecognitionAttempt>(attempts, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Result<T>> CommitAsync<T>(
        Dictionary<string, RecognitionAttempt> candidate,
        T value,
        CancellationToken cancellationToken)
    {
        try
        {
            await SaveFileAsync(candidate.Values, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Result<T>.Failure(ApplicationError.Storage("Recognition history could not be saved."));
        }

        attempts.Clear();
        foreach (var pair in candidate)
        {
            attempts[pair.Key] = pair.Value;
        }

        return Result<T>.Success(value);
    }

    private static RecognitionAttempt ToDomain(RecognitionAttemptDto dto)
    {
        return new RecognitionAttempt
        {
            Id = dto.Id,
            CreatedAt = dto.CreatedAt,
            Provider = dto.Provider,
            SourceName = dto.SourceName,
            SourcePath = dto.SourcePath,
            AniListId = dto.AniListId,
            RomajiTitle = dto.RomajiTitle,
            EnglishTitle = dto.EnglishTitle,
            NativeTitle = dto.NativeTitle,
            Episode = dto.Episode,
            Similarity = dto.Similarity,
            ThumbnailUri = Uri.TryCreate(dto.ThumbnailUri, UriKind.Absolute, out var thumbnailUri)
                ? thumbnailUri
                : null
        };
    }

    private static Result<T> Failure<T>(string message)
    {
        return Result<T>.Failure(ApplicationError.Validation(message));
    }

    private sealed record RecognitionAttemptDto(
        string Id,
        DateTimeOffset CreatedAt,
        string Provider,
        string SourceName,
        string? SourcePath,
        int? AniListId,
        string? RomajiTitle,
        string? EnglishTitle,
        string? NativeTitle,
        string? Episode,
        double? Similarity,
        string? ThumbnailUri)
    {
        public static RecognitionAttemptDto FromDomain(RecognitionAttempt attempt)
        {
            return new RecognitionAttemptDto(
                attempt.Id,
                attempt.CreatedAt,
                attempt.Provider,
                attempt.SourceName,
                attempt.SourcePath,
                attempt.AniListId,
                attempt.RomajiTitle,
                attempt.EnglishTitle,
                attempt.NativeTitle,
                attempt.Episode,
                attempt.Similarity,
                attempt.ThumbnailUri?.ToString());
        }
    }
}
