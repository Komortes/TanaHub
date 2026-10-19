using TanaHub.Domain.Models;

namespace TanaHub.Bridge;

public sealed record SettingsDto(
    string Theme,
    bool NotificationsEnabled,
    bool OfflineCacheEnabled,
    bool RecognitionServicesEnabled,
    string PreferredSyncSource,
    string AniListClientId,
    bool HasAniListClientSecret,
    bool HasSauceNaoApiKey,
    bool IsAniListConnected,
    string AniListUsername,
    int AniListUserId,
    DateTimeOffset? AniListLastSyncAt)
{
    public static SettingsDto From(AppSettings settings) => new(
        settings.Theme, settings.NotificationsEnabled, settings.OfflineCacheEnabled,
        settings.RecognitionServicesEnabled, settings.PreferredSyncSource, settings.AniListClientId,
        !string.IsNullOrWhiteSpace(settings.AniListClientSecret), !string.IsNullOrWhiteSpace(settings.SauceNaoApiKey),
        !string.IsNullOrWhiteSpace(settings.AniListAccessToken) && settings.AniListUserId > 0,
        settings.AniListUsername, settings.AniListUserId, settings.AniListLastSyncAt);
}

public sealed record RecognitionResultDto(
    string? MediaId, string Title, string? Romaji, string? NativeTitle, string? Episode,
    double Similarity, string? ThumbnailUrl, string? SourceUrl, string? Author);

public sealed record RecognitionHistoryDto(
    string Id, DateTimeOffset CreatedAt, string Provider, string SourceName, string? MediaId,
    string Title, string? RomajiTitle, string? NativeTitle, string? Episode,
    double? Similarity, string? ThumbnailUrl)
{
    internal static RecognitionHistoryDto From(RecognitionAttempt attempt) => new(
        attempt.Id, attempt.CreatedAt, attempt.Provider, attempt.SourceName, attempt.MediaId,
        attempt.EnglishTitle ?? attempt.RomajiTitle ?? attempt.NativeTitle ?? "Unknown title",
        attempt.RomajiTitle, attempt.NativeTitle, attempt.Episode, attempt.Similarity,
        attempt.ThumbnailUri?.Scheme is "http" or "https" ? attempt.ThumbnailUri.ToString() : null);
}
