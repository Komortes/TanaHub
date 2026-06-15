namespace TanaHub.Domain.Models;

public sealed record AppSettings
{
    public string Theme { get; init; } = "Nebula dark";

    public bool NotificationsEnabled { get; init; } = true;

    public bool OfflineCacheEnabled { get; init; } = true;

    public bool RecognitionServicesEnabled { get; init; }

    public string PreferredSyncSource { get; init; } = "AniList";

    public string AniListClientId { get; init; } = string.Empty;

    public string AniListClientSecret { get; init; } = string.Empty;

    public string AniListAccessToken { get; init; } = string.Empty;

    public string AniListUsername { get; init; } = string.Empty;

    public int AniListUserId { get; init; }

    public DateTimeOffset? AniListLastSyncAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
