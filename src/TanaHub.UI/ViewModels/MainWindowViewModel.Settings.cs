using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TanaHub.Application.Export;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed partial class MainWindowViewModel
{
    private static readonly Version CurrentAppVersion = GetCurrentAppVersion();

    [ObservableProperty]
    private string settingsTheme = "Nebula dark";

    [ObservableProperty]
    private bool notificationsEnabled;

    [ObservableProperty]
    private bool offlineCacheEnabled;

    [ObservableProperty]
    private bool recognitionServicesEnabled;

    [ObservableProperty]
    private string preferredSyncSource = "AniList";

    [ObservableProperty]
    private string settingsStorageDetail = "Loading settings...";

    [ObservableProperty]
    private string libraryExportStatus = "Import or export your library as CSV or MAL XML.";

    [ObservableProperty]
    private string aniListClientId = string.Empty;

    [ObservableProperty]
    private string aniListClientSecret = string.Empty;

    [ObservableProperty]
    private string aniListConnectionStatus = "Not connected";

    [ObservableProperty]
    private bool isAniListConnected;

    [ObservableProperty]
    private string aniListSyncStatus = string.Empty;

    [ObservableProperty]
    private string currentAppVersionLabel = $"v{CurrentAppVersion}";

    [ObservableProperty]
    private string updateCheckStatus = "Checking for updates after startup...";

    [ObservableProperty]
    private string latestUpdateUrl = string.Empty;

    [ObservableProperty]
    private bool isUpdateAvailable;

    [ObservableProperty]
    private bool isCheckingForUpdates;

    public bool IsNebulaThemeSelected => SettingsTheme == "Nebula dark";
    public bool IsHighContrastThemeSelected => SettingsTheme == "High contrast";
    public bool IsAniListSyncSelected => PreferredSyncSource == "AniList";
    public bool IsMyAnimeListSyncSelected => PreferredSyncSource == "MyAnimeList";
    public bool IsMangaDexSyncSelected => PreferredSyncSource == "MangaDex";
    public bool HasLatestUpdateUrl => !string.IsNullOrWhiteSpace(LatestUpdateUrl);

    [RelayCommand]
    private async Task SelectThemeAsync(string theme)
    {
        await SaveSettingsAsync(appSettings with
        {
            Theme = string.IsNullOrWhiteSpace(theme) ? "Nebula dark" : theme
        });
    }

    [RelayCommand]
    private async Task SelectSyncSourceAsync(string source)
    {
        var resolved = string.IsNullOrWhiteSpace(source) ? "AniList" : source;
        catalogSourceSelector.SetSource(resolved);
        await SaveSettingsAsync(appSettings with { PreferredSyncSource = resolved });
    }

    [RelayCommand]
    private async Task ToggleNotificationsAsync()
    {
        await SaveSettingsAsync(appSettings with { NotificationsEnabled = !NotificationsEnabled });
    }

    [RelayCommand]
    private async Task ToggleOfflineCacheAsync()
    {
        await SaveSettingsAsync(appSettings with { OfflineCacheEnabled = !OfflineCacheEnabled });
    }

    [RelayCommand]
    private async Task ToggleRecognitionServicesAsync()
    {
        await SaveSettingsAsync(appSettings with { RecognitionServicesEnabled = !RecognitionServicesEnabled });
    }

    [RelayCommand]
    private async Task ConnectAniListAsync()
    {
        AniListConnectionStatus = "Opening browser…";
        var result = await aniListAuthService.AuthorizeAsync(AniListClientId, AniListClientSecret);
        if (result.IsFailure) { AniListConnectionStatus = result.Error.Message; return; }

        await SaveSettingsAsync(appSettings with
        {
            AniListClientId = AniListClientId,
            AniListClientSecret = AniListClientSecret,
            AniListAccessToken = result.Value!.AccessToken,
            AniListUsername = result.Value.Username,
            AniListUserId = result.Value.UserId,
        });
    }

    [RelayCommand]
    private async Task SyncFromAniListAsync()
    {
        if (!IsAniListConnected) return;
        AniListSyncStatus = "Syncing…";
        SetAppConnectionStatus("Online", "Syncing AniList...");

        var result = await aniListSyncService.SyncAsync(
            appSettings.AniListAccessToken, appSettings.AniListUserId, userLibraryService);

        if (result.IsFailure)
        {
            AniListSyncStatus = result.Error.Message;
            SetAppConnectionStatus("Offline", "AniList sync failed");
            return;
        }

        await SaveSettingsAsync(appSettings with { AniListLastSyncAt = DateTimeOffset.UtcNow });
        await LoadLibraryAsync();
        AniListSyncStatus = $"Imported {result.Value} entries · {DateTimeOffset.Now:HH:mm}";
    }

    [RelayCommand]
    private async Task DisconnectAniListAsync()
    {
        await SaveSettingsAsync(appSettings with
        {
            AniListAccessToken = string.Empty,
            AniListUsername = string.Empty,
            AniListUserId = 0,
            AniListLastSyncAt = null,
        });
    }

    [RelayCommand]
    private async Task ExportLibraryAsync()
    {
        var rows = await BuildExportRowsAsync();
        if (rows is null) return;

        var saved = await fileSaveService.SaveTextAsync(
            $"tanahub-library-{DateTimeOffset.Now:yyyy-MM-dd}.csv",
            LibraryCsvExporter.Export(rows), "csv", "text/csv");

        LibraryExportStatus = saved
            ? $"Exported {rows.Count} library item(s)."
            : "Export canceled.";
    }

    [RelayCommand]
    private async Task ExportMalXmlAsync()
    {
        var rows = await BuildExportRowsAsync();
        if (rows is null) return;

        var saved = await fileSaveService.SaveTextAsync(
            $"tanahub-mal-library-{DateTimeOffset.Now:yyyy-MM-dd}.xml",
            MalXmlLibraryExchange.Export(rows), "xml", "application/xml");

        LibraryExportStatus = saved
            ? $"Exported {rows.Count} MAL XML item(s)."
            : "MAL XML export canceled.";
    }

    [RelayCommand]
    private async Task ImportMalXmlAsync()
    {
        var picked = await fileOpenService.PickTextAsync();
        if (picked.Stream is null) { LibraryExportStatus = "MAL XML import canceled."; return; }

        await using var stream = picked.Stream;
        using var reader = new StreamReader(stream);
        var importResult = MalXmlLibraryExchange.Import(await reader.ReadToEndAsync());
        if (importResult.IsFailure) { LibraryExportStatus = importResult.Error.Message; return; }

        var imported = 0;
        foreach (var entry in importResult.Value!)
        {
            var upsert = await userLibraryService.UpsertEntryAsync(entry);
            if (upsert.IsSuccess) imported++;
        }

        await LoadLibraryAsync();
        LibraryExportStatus = imported == 0
            ? "No MAL XML entries found."
            : $"Imported {imported} MAL XML item(s).";
    }

    [RelayCommand]
    private Task CheckForUpdatesAsync()
    {
        return RefreshUpdateStatusAsync(isManualCheck: true);
    }

    private async Task RefreshUpdateStatusAsync(bool isManualCheck)
    {
        if (IsCheckingForUpdates) return;

        IsCheckingForUpdates = true;
        UpdateCheckStatus = isManualCheck
            ? "Checking GitHub releases..."
            : "Checking for updates...";

        var result = await appUpdateService.CheckForUpdatesAsync(CurrentAppVersion);
        IsCheckingForUpdates = false;

        if (result.IsFailure)
        {
            UpdateCheckStatus = result.Error.Message;
            SetAppConnectionStatus("Offline", "Update check failed");
            return;
        }

        var check = result.Value!;
        IsUpdateAvailable = check.IsUpdateAvailable;

        if (check.IsUpdateAvailable)
        {
            var latest = check.LatestRelease!;
            LatestUpdateUrl = latest.ReleaseUri.ToString();
            UpdateCheckStatus = $"Update {latest.TagName} is available: {latest.Name}.";
            SetAppConnectionStatus("Online", "Update available");
            OnPropertyChanged(nameof(HasLatestUpdateUrl));
            return;
        }

        LatestUpdateUrl = check.LatestRelease?.ReleaseUri.ToString() ?? string.Empty;
        UpdateCheckStatus = check.LatestRelease is null
            ? "No published GitHub release found yet."
            : $"You're up to date on {CurrentAppVersionLabel}.";
        OnPropertyChanged(nameof(HasLatestUpdateUrl));
    }

    private async Task<List<LibraryExportItem>?> BuildExportRowsAsync()
    {
        var result = await userLibraryService.GetEntriesAsync(new Application.Queries.UserLibraryQuery
        {
            PageSize = 500
        });

        if (result.IsFailure) { LibraryExportStatus = result.Error.Message; return null; }

        var rows = new List<LibraryExportItem>();
        foreach (var entry in result.Value!.Items)
        {
            var media = await mediaCatalogService.GetByIdAsync(entry.MediaId);
            rows.Add(new LibraryExportItem(
                entry.MediaId,
                media.IsSuccess ? media.Value!.Title.DisplayTitle : entry.MediaId,
                entry.MediaType.ToString(), entry.Status.ToString(),
                entry.Progress, entry.Score)
            {
                Tags = entry.Tags,
                CustomLists = entry.CustomLists
            });
        }

        return rows;
    }

    private async Task LoadSettingsAsync()
    {
        var result = await appSettingsService.GetAsync();
        if (result.IsFailure)
        {
            SettingsStorageDetail = result.Error.Message;
            SetAppConnectionStatus("Offline", "Settings unavailable");
            return;
        }

        ApplySettingsToUi(result.Value!);
    }

    private async Task SaveSettingsAsync(AppSettings settings)
    {
        var result = await appSettingsService.SaveAsync(settings);
        if (result.IsFailure)
        {
            SettingsStorageDetail = result.Error.Message;
            SetAppConnectionStatus("Offline", "Settings save failed");
            return;
        }

        ApplySettingsToUi(result.Value!);
        SearchStatus = "Settings saved.";
    }

    private void ApplySettingsToUi(AppSettings settings)
    {
        appSettings = settings;
        SettingsTheme = appSettings.Theme;
        appThemeService.Apply(SettingsTheme);
        OnPropertyChanged(nameof(IsNebulaThemeSelected));
        OnPropertyChanged(nameof(IsHighContrastThemeSelected));
        NotificationsEnabled = appSettings.NotificationsEnabled;
        OfflineCacheEnabled = appSettings.OfflineCacheEnabled;
        RecognitionServicesEnabled = appSettings.RecognitionServicesEnabled;
        PreferredSyncSource = appSettings.PreferredSyncSource;
        catalogSourceSelector.SetSource(PreferredSyncSource);
        NotifySyncSourceSelectionChanged();
        SettingsStorageDetail = $"Settings saved {appSettings.UpdatedAt:yyyy-MM-dd HH:mm}";
        AniListClientId = appSettings.AniListClientId;
        AniListClientSecret = appSettings.AniListClientSecret;
        IsAniListConnected = !string.IsNullOrWhiteSpace(appSettings.AniListAccessToken);
        AniListConnectionStatus = IsAniListConnected
            ? $"Connected as {appSettings.AniListUsername}"
            : "Not connected";
        AniListSyncStatus = appSettings.AniListLastSyncAt.HasValue
            ? $"Last sync: {appSettings.AniListLastSyncAt:yyyy-MM-dd HH:mm}"
            : string.Empty;
        RefreshAppConnectionStatus();
    }

    private void RefreshAppConnectionStatus()
    {
        if (!OfflineCacheEnabled)
        {
            SetAppConnectionStatus("Online", $"{PreferredSyncSource} live mode");
            return;
        }

        if (appSettings.AniListLastSyncAt is { } lastSyncAt)
        {
            SetAppConnectionStatus("Cached", $"Last sync {lastSyncAt.ToLocalTime():MMM d, HH:mm}");
            return;
        }

        SetAppConnectionStatus("Cached", "Local cache ready");
    }

    private void NotifySyncSourceSelectionChanged()
    {
        OnPropertyChanged(nameof(IsAniListSyncSelected));
        OnPropertyChanged(nameof(IsMyAnimeListSyncSelected));
        OnPropertyChanged(nameof(IsMangaDexSyncSelected));
    }

    private static Version GetCurrentAppVersion()
    {
        var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
        if (version is null) return new Version(0, 1, 0);

        return new Version(
            Math.Max(version.Major, 0),
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0));
    }
}
