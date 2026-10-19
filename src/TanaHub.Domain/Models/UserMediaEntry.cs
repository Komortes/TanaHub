using TanaHub.Domain.Enums;

namespace TanaHub.Domain.Models;

public sealed record UserMediaEntry
{
    public UserMediaEntry(
        string mediaId,
        MediaType mediaType,
        MediaListStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaId);

        MediaId = mediaId;
        MediaType = mediaType;
        Status = status;
    }

    public string MediaId { get; }

    public MediaType MediaType { get; }

    public MediaListStatus Status { get; init; }

    public int Progress { get; init; }

    public int? Score { get; init; }

    public Uri? PosterUri { get; init; }

    public string? Notes { get; init; }

    public string? Review { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> CustomLists { get; init; } = [];

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public UserMediaEntry IncrementProgress(int amount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (Progress > int.MaxValue - amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Progress cannot exceed 2,147,483,647.");
        }

        return SetProgress(Progress + amount);
    }

    public UserMediaEntry SetProgress(int progress)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(progress);
        var now = DateTimeOffset.UtcNow;
        var starting = Status == MediaListStatus.Planning && Progress == 0 && progress > 0;
        return this with
        {
            Progress = progress,
            Status = starting ? MediaListStatus.Current : Status,
            StartedAt = starting && StartedAt is null && (CompletedAt is null || CompletedAt >= now) ? now : StartedAt,
            UpdatedAt = now
        };
    }
}
