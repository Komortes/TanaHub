using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using Anime = TanaHub.Domain.Models.Anime;
using Manga = TanaHub.Domain.Models.Manga;

namespace TanaHub.UI.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IMediaCatalogService mediaCatalogService;
    private readonly IUserLibraryService userLibraryService;
    private readonly IAppSettingsService appSettingsService;
    private readonly IAiringScheduleService airingScheduleService;
    private AppSettings appSettings = new();

    public MainWindowViewModel(
        IMediaCatalogService mediaCatalogService,
        IUserLibraryService userLibraryService,
        IAppSettingsService appSettingsService,
        IAiringScheduleService airingScheduleService)
    {
        this.mediaCatalogService = mediaCatalogService;
        this.userLibraryService = userLibraryService;
        this.appSettingsService = appSettingsService;
        this.airingScheduleService = airingScheduleService;

        NavigationItems =
        [
            new(
                "home",
                "Home",
                "Continue watching, today's releases, recent updates, and quick stats.",
                "Home dashboard content will be connected after the first domain models are in place."),
            new(
                "discover",
                "Discover",
                "Search and filter anime and manga by trend, season, genre, status, and score.",
                "Discover will start as an AniList-backed search and browsing surface."),
            new(
                "library",
                "Library",
                "Track watching, reading, completed, planned, paused, and dropped titles.",
                "Library needs local storage first, then fast list actions can be added."),
            new(
                "schedule",
                "Schedule",
                "See upcoming episodes grouped by day and filtered by your lists.",
                "Schedule will be wired after anime list sync and airing metadata exist."),
            new(
                "settings",
                "Settings",
                "Accounts, sync direction, notifications, theme, language, and cache controls.",
                "Settings will become the home for app preferences and external account setup.")
        ];

        selectedNavigationItem = NavigationItems[0];
        selectedNavigationIndex = 0;
        selectedNavigationOffset = 0;
        currentPageTitle = selectedNavigationItem.Title;
        currentPageSummary = selectedNavigationItem.Summary;
        currentEmptyState = selectedNavigationItem.EmptyState;
        SearchResults = [];
        DashboardMetrics = [];
        ContinueItems = [];
        LibraryEntries = [];
        ScheduleItems = [];
        SetVisiblePage(selectedNavigationItem.Key);

        _ = LoadPageDataAsync();
    }

    public string AppName => "TanaHub";

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public ObservableCollection<MediaSearchResultViewModel> SearchResults { get; }

    public ObservableCollection<DashboardMetricViewModel> DashboardMetrics { get; }

    public ObservableCollection<LibraryEntryViewModel> ContinueItems { get; }

    public ObservableCollection<LibraryEntryViewModel> LibraryEntries { get; }

    public ObservableCollection<ScheduleItemViewModel> ScheduleItems { get; }

    public IReadOnlyList<string> LibraryStatusFilters { get; } =
    [
        "All",
        "Current",
        "Completed",
        "Planning",
        "Paused",
        "Dropped"
    ];

    [ObservableProperty]
    private NavigationItemViewModel? selectedNavigationItem;

    [ObservableProperty]
    private int selectedNavigationIndex;

    [ObservableProperty]
    private double selectedNavigationOffset;

    [ObservableProperty]
    private string currentPageTitle = string.Empty;

    [ObservableProperty]
    private string currentPageSummary = string.Empty;

    [ObservableProperty]
    private string currentEmptyState = string.Empty;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string searchStatus = "Search uses AniList with local fallback.";

    [ObservableProperty]
    private string selectedDiscoverType = "All";

    [ObservableProperty]
    private string selectedLibraryType = "All";

    [ObservableProperty]
    private string selectedLibraryStatus = "All";

    [ObservableProperty]
    private string librarySearchText = string.Empty;

    [ObservableProperty]
    private bool hasSearchResults;

    [ObservableProperty]
    private bool isHomeVisible;

    [ObservableProperty]
    private bool isDiscoverVisible;

    [ObservableProperty]
    private bool isLibraryVisible;

    [ObservableProperty]
    private bool isScheduleVisible;

    [ObservableProperty]
    private bool isSettingsVisible;

    [ObservableProperty]
    private bool isDetailVisible;

    [ObservableProperty]
    private MediaDetailViewModel? selectedMedia;

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

    private string previousPageKey = "home";

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        CurrentPageTitle = value?.Title ?? AppName;
        CurrentPageSummary = value?.Summary ?? string.Empty;
        CurrentEmptyState = value?.EmptyState ?? string.Empty;
        SelectedNavigationIndex = value is null ? 0 : NavigationItems.IndexOf(value);
        SelectedNavigationOffset = Math.Max(0, SelectedNavigationIndex) * 58;
        IsDetailVisible = false;
        SelectedMedia = null;
        SetVisiblePage(value?.Key ?? "home");
    }

    [RelayCommand]
    private void GoBack()
    {
        IsDetailVisible = false;
        SelectedMedia = null;
        SetVisiblePage(previousPageKey);
        CurrentPageTitle = SelectedNavigationItem?.Title ?? AppName;
        CurrentPageSummary = SelectedNavigationItem?.Summary ?? string.Empty;
    }

    private async Task OpenDetailAsync(string mediaId)
    {
        previousPageKey = SelectedNavigationItem?.Key ?? "home";

        var mediaResult = await mediaCatalogService.GetByIdAsync(mediaId);
        if (mediaResult.IsFailure)
        {
            SearchStatus = mediaResult.Error.Message;
            return;
        }

        var item = mediaResult.Value!;
        var libraryVm = LibraryEntries.FirstOrDefault(e => e.MediaId == mediaId);

        var episodes = "—";
        var duration = "—";
        var studio = "—";
        var chapters = "—";
        var volumes = "—";

        if (item is Anime anime)
        {
            episodes = anime.EpisodeCount?.ToString() ?? "?";
            duration = anime.DurationMinutes is not null ? $"{anime.DurationMinutes} min" : "—";
            studio = anime.Studio ?? "Unknown";
        }
        else if (item is Manga manga)
        {
            chapters = manga.ChapterCount?.ToString() ?? "?";
            volumes = manga.VolumeCount?.ToString() ?? "?";
        }

        SelectedMedia = new MediaDetailViewModel(
            item.Id,
            item.Title.DisplayTitle,
            item.Title.Native ?? string.Empty,
            item.Title.Romaji,
            item.Type.ToString(),
            item.Format.ToString(),
            item.ReleaseStatus.ToString(),
            item.StartYear?.ToString() ?? "—",
            item.AverageScore?.ToString() ?? "n/a",
            item.Description ?? string.Empty,
            item.Genres,
            item.Images.PosterUri,
            item.Images.BannerUri,
            episodes,
            duration,
            studio,
            chapters,
            volumes,
            libraryVm?.Status ?? string.Empty,
            libraryVm?.Progress ?? string.Empty,
            libraryVm?.Score ?? string.Empty,
            libraryVm is not null,
            new AsyncRelayCommand(() => AddToLibraryAsync(mediaId)),
            new AsyncRelayCommand(() => IncrementProgressAsync(mediaId)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Current)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Planning)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Paused)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Completed)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Dropped)),
            new AsyncRelayCommand(() => IncreaseScoreAsync(mediaId, ParseLibraryScore(libraryVm?.Score))),
            new AsyncRelayCommand(() => RemoveFromLibraryAsync(mediaId)));

        CurrentPageTitle = item.Title.DisplayTitle;
        CurrentPageSummary = $"{item.Format} · {item.ReleaseStatus}";
        IsDetailVisible = true;
        IsHomeVisible = false;
        IsDiscoverVisible = false;
        IsLibraryVisible = false;
        IsScheduleVisible = false;
        IsSettingsVisible = false;
    }

    private async Task RefreshDetailIfOpenAsync(string mediaId)
    {
        if (IsDetailVisible && SelectedMedia?.Id == mediaId)
        {
            await OpenDetailAsync(mediaId);
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        SelectNavigationItem("discover");
        await LoadSearchResultsAsync();
    }

    private async Task LoadSearchResultsAsync()
    {
        var result = await mediaCatalogService.SearchAsync(new MediaSearchQuery
        {
            SearchText = SearchText,
            Type = ParseMediaTypeFilter(SelectedDiscoverType),
            PageSize = 12
        });

        SearchResults.Clear();

        if (result.IsFailure)
        {
            SearchStatus = result.Error.Message;
            HasSearchResults = false;
            return;
        }

        foreach (var item in result.Value!.Items)
        {
            SearchResults.Add(MediaSearchResultViewModel.FromMediaItem(item, AddToLibraryAsync, OpenDetailAsync));
        }

        HasSearchResults = SearchResults.Count > 0;
        SearchStatus = HasSearchResults
            ? $"Found {SearchResults.Count} result(s)."
            : "No local results found.";
    }

    [RelayCommand]
    private async Task SelectDiscoverTypeAsync(string type)
    {
        SelectedDiscoverType = string.IsNullOrWhiteSpace(type) ? "All" : type;
        await LoadSearchResultsAsync();
    }

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private async Task SelectLibraryTypeAsync(string type)
    {
        SelectedLibraryType = string.IsNullOrWhiteSpace(type) ? "All" : type;
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private async Task SelectLibraryStatusAsync(string status)
    {
        SelectedLibraryStatus = string.IsNullOrWhiteSpace(status) ? "All" : status;
        await LoadLibraryAsync();
    }

    private async Task LoadPageDataAsync()
    {
        await LoadSettingsAsync();
        await LoadSearchResultsAsync();
        await LoadLibraryAsync();
        await LoadScheduleAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var result = await appSettingsService.GetAsync();
        if (result.IsFailure)
        {
            SettingsStorageDetail = result.Error.Message;
            return;
        }

        appSettings = result.Value!;
        SettingsTheme = appSettings.Theme;
        NotificationsEnabled = appSettings.NotificationsEnabled;
        OfflineCacheEnabled = appSettings.OfflineCacheEnabled;
        RecognitionServicesEnabled = appSettings.RecognitionServicesEnabled;
        PreferredSyncSource = appSettings.PreferredSyncSource;
        SettingsStorageDetail = $"Settings saved {appSettings.UpdatedAt:yyyy-MM-dd HH:mm}";
    }

    [RelayCommand]
    private async Task ToggleNotificationsAsync()
    {
        await SaveSettingsAsync(appSettings with
        {
            NotificationsEnabled = !NotificationsEnabled
        });
    }

    [RelayCommand]
    private async Task ToggleOfflineCacheAsync()
    {
        await SaveSettingsAsync(appSettings with
        {
            OfflineCacheEnabled = !OfflineCacheEnabled
        });
    }

    [RelayCommand]
    private async Task ToggleRecognitionServicesAsync()
    {
        await SaveSettingsAsync(appSettings with
        {
            RecognitionServicesEnabled = !RecognitionServicesEnabled
        });
    }

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
        await SaveSettingsAsync(appSettings with
        {
            PreferredSyncSource = string.IsNullOrWhiteSpace(source) ? "AniList" : source
        });
    }

    private async Task SaveSettingsAsync(AppSettings settings)
    {
        var result = await appSettingsService.SaveAsync(settings);
        if (result.IsFailure)
        {
            SettingsStorageDetail = result.Error.Message;
            return;
        }

        appSettings = result.Value!;
        SettingsTheme = appSettings.Theme;
        NotificationsEnabled = appSettings.NotificationsEnabled;
        OfflineCacheEnabled = appSettings.OfflineCacheEnabled;
        RecognitionServicesEnabled = appSettings.RecognitionServicesEnabled;
        PreferredSyncSource = appSettings.PreferredSyncSource;
        SettingsStorageDetail = $"Settings saved {appSettings.UpdatedAt:yyyy-MM-dd HH:mm}";
        SearchStatus = "Settings saved.";
    }

    private async Task LoadLibraryAsync()
    {
        var result = await userLibraryService.GetEntriesAsync(new UserLibraryQuery
        {
            Type = ParseMediaTypeFilter(SelectedLibraryType),
            Status = ParseStatusFilter(SelectedLibraryStatus),
            SearchText = LibrarySearchText,
            PageSize = 20
        });

        if (result.IsFailure)
        {
            return;
        }

        LibraryEntries.Clear();
        ContinueItems.Clear();

        foreach (var entry in result.Value!.Items)
        {
            var viewModel = await CreateLibraryEntryViewModelAsync(entry);
            LibraryEntries.Add(viewModel);

            if (entry.Status is MediaListStatus.Current or MediaListStatus.Paused)
            {
                ContinueItems.Add(viewModel);
            }
        }

        DashboardMetrics.Clear();
        DashboardMetrics.Add(new("Library", LibraryEntries.Count.ToString(), "seeded local entries", MaterialIconKind.Bookshelf));
        DashboardMetrics.Add(new("Watching", ContinueItems.Count.ToString(), "current or paused", MaterialIconKind.PlayCircle));
        DashboardMetrics.Add(new("Search", SearchResults.Count.ToString(), "local catalog results", MaterialIconKind.Magnify));
    }

    private async Task LoadScheduleAsync()
    {
        var now = DateTimeOffset.Now;
        var result = await airingScheduleService.GetUpcomingAsync(now, now.AddDays(7), pageSize: 20);

        ScheduleItems.Clear();

        if (result.IsFailure)
        {
            SearchStatus = result.Error.Message;
            return;
        }

        var libraryIds = LibraryEntries
            .Select(entry => entry.MediaId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in result.Value!)
        {
            ScheduleItems.Add(new ScheduleItemViewModel(
                item.MediaId,
                FormatScheduleDay(item.AiringAt),
                item.AiringAt.ToLocalTime().ToString("HH:mm"),
                item.Title,
                $"{item.Format} · {item.ReleaseStatus}",
                $"EP {item.Episode}",
                item.PosterUri,
                libraryIds.Contains(item.MediaId),
                new AsyncRelayCommand(() => OpenDetailAsync(item.MediaId))));
        }
    }

    private async Task<LibraryEntryViewModel> CreateLibraryEntryViewModelAsync(UserMediaEntry entry)
    {
        var media = await mediaCatalogService.GetByIdAsync(entry.MediaId);
        var title = media.IsSuccess ? media.Value!.Title.DisplayTitle : entry.MediaId;
        var posterUri = entry.PosterUri ?? (media.IsSuccess ? media.Value!.Images.PosterUri : null);
        var total = media.Value switch
        {
            Anime anime when anime.EpisodeCount is not null => anime.EpisodeCount.ToString(),
            Manga manga when manga.ChapterCount is not null => manga.ChapterCount.ToString(),
            _ => "?"
        };

        return new LibraryEntryViewModel(
            entry.MediaId,
            title,
            entry.MediaType.ToString(),
            entry.Status.ToString(),
            $"{entry.Progress}/{total}",
            entry.Score?.ToString() ?? "-",
            posterUri,
            new AsyncRelayCommand(() => IncrementProgressAsync(entry.MediaId)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(entry.MediaId, MediaListStatus.Current)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(entry.MediaId, MediaListStatus.Completed)),
            new AsyncRelayCommand(() => IncreaseScoreAsync(entry.MediaId, entry.Score)),
            new AsyncRelayCommand(() => RemoveFromLibraryAsync(entry.MediaId)),
            new AsyncRelayCommand(() => OpenDetailAsync(entry.MediaId)));
    }

    private async Task AddToLibraryAsync(string mediaId)
    {
        var media = await mediaCatalogService.GetByIdAsync(mediaId);
        if (media.IsFailure)
        {
            SearchStatus = media.Error.Message;
            return;
        }

        var status = media.Value!.Type == MediaType.Anime
            ? MediaListStatus.Current
            : MediaListStatus.Planning;

        var result = await userLibraryService.UpsertEntryAsync(new UserMediaEntry(media.Value.Id, media.Value.Type, status)
        {
            PosterUri = media.Value.Images.PosterUri
        });
        SearchStatus = result.IsSuccess
            ? $"Added {media.Value.Title.DisplayTitle} to local library."
            : result.Error.Message;

        await LoadLibraryAsync();
        await RefreshDetailIfOpenAsync(mediaId);
    }

    private async Task IncrementProgressAsync(string mediaId)
    {
        var result = await userLibraryService.IncrementProgressAsync(mediaId);
        SearchStatus = result.IsSuccess
            ? $"Updated progress for {mediaId}."
            : result.Error.Message;

        await LoadLibraryAsync();
        await RefreshDetailIfOpenAsync(mediaId);
    }

    private async Task UpdateLibraryStatusAsync(string mediaId, MediaListStatus status)
    {
        var result = await userLibraryService.UpdateStatusAsync(mediaId, status);
        SearchStatus = result.IsSuccess
            ? $"Updated {mediaId} to {status}."
            : result.Error.Message;

        await LoadLibraryAsync();
        await RefreshDetailIfOpenAsync(mediaId);
    }

    private async Task IncreaseScoreAsync(string mediaId, int? currentScore)
    {
        var nextScore = currentScore is null or >= 10 ? 1 : currentScore.Value + 1;
        var result = await userLibraryService.UpdateScoreAsync(mediaId, nextScore);
        SearchStatus = result.IsSuccess
            ? $"Updated score for {mediaId}."
            : result.Error.Message;

        await LoadLibraryAsync();
        await RefreshDetailIfOpenAsync(mediaId);
    }

    private async Task RemoveFromLibraryAsync(string mediaId)
    {
        var result = await userLibraryService.RemoveEntryAsync(mediaId);
        SearchStatus = result.IsSuccess
            ? $"Removed {mediaId} from local library."
            : result.Error.Message;

        await LoadLibraryAsync();
        await RefreshDetailIfOpenAsync(mediaId);
    }

    private void SetVisiblePage(string pageKey)
    {
        IsHomeVisible = pageKey == "home";
        IsDiscoverVisible = pageKey == "discover";
        IsLibraryVisible = pageKey == "library";
        IsScheduleVisible = pageKey == "schedule";
        IsSettingsVisible = pageKey == "settings";
    }

    private void SelectNavigationItem(string key)
    {
        var item = NavigationItems.FirstOrDefault(item => item.Key == key);
        if (item is not null)
        {
            SelectedNavigationItem = item;
        }
    }

    private static MediaType? ParseMediaTypeFilter(string value)
    {
        return value switch
        {
            "Anime" => MediaType.Anime,
            "Manga" => MediaType.Manga,
            _ => null
        };
    }

    private static MediaListStatus? ParseStatusFilter(string value)
    {
        return Enum.TryParse<MediaListStatus>(value, ignoreCase: true, out var status)
            ? status
            : null;
    }

    private static int? ParseLibraryScore(string? value)
    {
        return int.TryParse(value, out var score) ? score : null;
    }

    private static string FormatScheduleDay(DateTimeOffset value)
    {
        var localDate = value.ToLocalTime().Date;
        var today = DateTimeOffset.Now.Date;

        if (localDate == today)
        {
            return "Today";
        }

        if (localDate == today.AddDays(1))
        {
            return "Tomorrow";
        }

        return localDate.ToString("ddd, MMM d");
    }
}
