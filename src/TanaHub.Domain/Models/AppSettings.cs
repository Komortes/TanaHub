namespace TanaHub.Domain.Models;

public sealed record AppSettings
{
    public string Theme { get; init; } = "Nebula dark";

    public bool NotificationsEnabled { get; init; } = true;

    public bool OfflineCacheEnabled { get; init; } = true;

    public bool RecognitionServicesEnabled { get; init; }

    public string PreferredSyncSource { get; init; } = "AniList";

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
