using System.Text.Json;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;

namespace TanaHub.Bridge;

public sealed partial class BridgeHandler
{
    private async Task<AppSettings> SettingsAsync(CancellationToken ct) =>
        RequireSuccess(await Service<IAppSettingsService>().GetAsync(ct));

    private async Task<SettingsDto> SaveSettingsAsync(JsonElement parameters, CancellationToken ct)
    {
        var current = await SettingsAsync(ct);
        var theme = OptionalString(parameters, "theme", 50) ?? current.Theme;
        var source = OptionalString(parameters, "preferredSyncSource", 30) ?? current.PreferredSyncSource;
        if (theme is not ("Nebula dark" or "High contrast")) throw Validation("Unsupported theme.");
        if (source is not ("AniList" or "MyAnimeList" or "MangaDex")) throw Validation("Unsupported catalog source.");
        return await PersistSettingsAsync(current with
        {
            Theme = theme,
            PreferredSyncSource = source,
            NotificationsEnabled = Boolean(parameters, "notificationsEnabled", current.NotificationsEnabled),
            OfflineCacheEnabled = Boolean(parameters, "offlineCacheEnabled", current.OfflineCacheEnabled),
            RecognitionServicesEnabled = Boolean(parameters, "recognitionServicesEnabled", current.RecognitionServicesEnabled),
            AniListClientId = OptionalString(parameters, "aniListClientId", 200) ?? current.AniListClientId,
            AniListClientSecret = OptionalString(parameters, "aniListClientSecret", 4096) ?? current.AniListClientSecret,
            SauceNaoApiKey = OptionalString(parameters, "sauceNaoApiKey", 4096) ?? current.SauceNaoApiKey
        }, ct);
    }

    private async Task<SettingsDto> PersistSettingsAsync(AppSettings settings, CancellationToken ct)
    {
        var saved = RequireSuccess(await Service<IAppSettingsService>().SaveAsync(settings, ct));
        if (services?.GetService(typeof(ICatalogSourceSelector)) is ICatalogSourceSelector source)
        {
            source.SetSource(saved.PreferredSyncSource);
            source.SetOfflineCacheEnabled(saved.OfflineCacheEnabled);
        }
        if (services?.GetService(typeof(IMediaRelationsService)) is IMediaRelationsService relations)
            relations.SetOfflineCacheEnabled(saved.OfflineCacheEnabled);
        return SettingsDto.From(saved);
    }

    private async Task<SettingsDto> ConnectAsync(JsonElement parameters, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var clientId = OptionalString(parameters, "clientId", 200) ?? settings.AniListClientId;
        var secret = OptionalString(parameters, "clientSecret", 4096) ?? settings.AniListClientSecret;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret))
            throw Validation("AniList client ID and client secret are required.");
        var result = await Service<IAniListAuthService>().AuthorizeAsync(clientId, secret, ct);
        // Existing OAuth errors may contain HTTP payloads. Keep credentials and remote internals inside the core.
        if (result.IsFailure) throw new BridgeException("authorization_failed", "Could not connect to AniList. Check the app credentials and try again.");
        var auth = result.Value!;
        // The authorization process can run independently while preferences change in the main window.
        var latest = await SettingsAsync(ct);
        return await PersistSettingsAsync(latest with
        {
            AniListClientId = clientId,
            AniListClientSecret = secret,
            AniListAccessToken = auth.AccessToken,
            AniListUsername = auth.Username,
            AniListUserId = auth.UserId
        }, ct);
    }

    private async Task<object> SyncAsync(CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        if (!SettingsDto.From(settings).IsAniListConnected) throw Validation("Connect to AniList before syncing.");
        var result = await Service<IAniListSyncService>().SyncAsync(settings.AniListAccessToken, settings.AniListUserId, library, ct);
        if (result.IsFailure) throw new BridgeException("external_service_error", "AniList sync could not be completed. Try again later.");
        var latest = await SettingsAsync(ct);
        var saved = latest.AniListUserId == settings.AniListUserId && latest.AniListAccessToken == settings.AniListAccessToken
            ? await PersistSettingsAsync(latest with { AniListLastSyncAt = DateTimeOffset.UtcNow }, ct) : SettingsDto.From(latest);
        return new { importedCount = result.Value, settings = saved };
    }

    private async Task<SettingsDto> DisconnectAsync(CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        return await PersistSettingsAsync(settings with
        {
            AniListAccessToken = string.Empty,
            AniListUsername = string.Empty,
            AniListUserId = 0,
            AniListLastSyncAt = null
        }, ct);
    }

    private async Task<object> UpdatesAsync(CancellationToken ct)
    {
        var version = typeof(BridgeHandler).Assembly.GetName().Version ?? new Version(0, 1, 0);
        var check = RequireSuccess(await ReadCatalogAsync(() => Service<IAppUpdateService>().CheckForUpdatesAsync(version, ct), ct));
        var latest = check.LatestRelease;
        return new
        {
            currentVersion = check.CurrentVersion.ToString(3),
            check.IsUpdateAvailable,
            latestRelease = latest is null ? null : new
            {
                version = latest.Version.ToString(),
                latest.TagName,
                latest.Name,
                releaseUrl = latest.ReleaseUri.ToString(),
                latest.PublishedAt
            }
        };
    }
}
