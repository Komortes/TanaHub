namespace TanaHub.Domain.Models;

public sealed record RecognitionAttempt
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Provider { get; init; } = "trace.moe";

    public string SourceName { get; init; } = string.Empty;

    public string? SourcePath { get; init; }

    public int? AniListId { get; init; }

    public string? MediaId => AniListId is null ? null : $"anilist:{AniListId}";

    public string? RomajiTitle { get; init; }

    public string? EnglishTitle { get; init; }

    public string? NativeTitle { get; init; }

    public string? Episode { get; init; }

    public double? Similarity { get; init; }

    public Uri? ThumbnailUri { get; init; }
}
