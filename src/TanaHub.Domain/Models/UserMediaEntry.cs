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

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> CustomLists { get; init; } = [];

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public UserMediaEntry IncrementProgress(int amount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        return this with
        {
            Progress = Progress + amount,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
