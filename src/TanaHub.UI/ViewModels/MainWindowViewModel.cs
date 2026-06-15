using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using TanaHub.Application.Common;
using TanaHub.Application.Export;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.UI.Services;
using Anime = TanaHub.Domain.Models.Anime;
using Manga = TanaHub.Domain.Models.Manga;

namespace TanaHub.UI.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IMediaCatalogService mediaCatalogService;
    private readonly IUserLibraryService userLibraryService;
    private readonly IAppSettingsService appSettingsService;
    private readonly IAiringScheduleService airingScheduleService;
    private readonly IAppThemeService appThemeService;
    private readonly IFileSaveService fileSaveService;
    private readonly IAniListAuthService aniListAuthService;
    private readonly IAniListSyncService aniListSyncService;
    private AppSettings appSettings = new();

    public MainWindowViewModel(
        IMediaCatalogService mediaCatalogService,
        IUserLibraryService userLibraryService,
        IAppSettingsService appSettingsService,
        IAiringScheduleService airingScheduleService,
        IAppThemeService appThemeService,
        IFileSaveService fileSaveService,
        IAniListAuthService aniListAuthService,
        IAniListSyncService aniListSyncService)
    {
        this.mediaCatalogService = mediaCatalogService;
        this.userLibraryService = userLibraryService;
        this.appSettingsService = appSettingsService;
        this.airingScheduleService = airingScheduleService;
        this.appThemeService = appThemeService;
        this.fileSaveService = fileSaveService;
        this.aniListAuthService = aniListAuthService;
        this.aniListSyncService = aniListSyncService;

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

    public ObservableCollection<MediaSearchResultViewModel> TrendingItems { get; } = [];
    public ObservableCollection<MediaSearchResultViewModel> ThisSeasonItems { get; } = [];
    public ObservableCollection<MediaSearchResultViewModel> TopRatedItems { get; } = [];
    public ObservableCollection<MediaSearchResultViewModel> UpcomingItems { get; } = [];
    public ObservableCollection<MediaSearchResultViewModel> ManhwaItems { get; } = [];

    public IReadOnlyList<string> DiscoverGenres { get; } =
    [
        "Action", "Adventure", "Comedy", "Drama", "Fantasy",
        "Horror", "Isekai", "Mecha", "Romance", "Sci-Fi",
        "Slice of Life", "Thriller"
    ];

    public IReadOnlyList<string> DiscoverSortOptions { get; } =
    [
        "Popularity", "Score", "Trending", "New"
    ];

    public IReadOnlyList<string> LibraryTypeOptions { get; } = ["All", "Anime", "Manga"];
    public IReadOnlyList<string> LibrarySortOptions { get; } = ["Updated", "Title", "Score", "Progress"];

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
    private string selectedDiscoverSort = "Popularity";

    [ObservableProperty]
    private string? selectedDiscoverGenre;

    [ObservableProperty]
    private bool isDiscoverSearchActive;

    public bool IsDiscoverBrowseActive => !IsDiscoverSearchActive;

    partial void OnIsDiscoverSearchActiveChanged(bool value) =>
        OnPropertyChanged(nameof(IsDiscoverBrowseActive));

    partial void OnSelectedDiscoverGenreChanged(string? value)
    {
        if (suppressDiscoverFilterRefresh)
        {
            return;
        }

        ResetDiscoverAdvancedFilters();
        _ = ApplyDiscoverFiltersAsync();
    }

    partial void OnSelectedLibraryStatusChanged(string value) => _ = LoadLibraryAsync();
    partial void OnSelectedLibraryTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLibraryAllSelected));
        OnPropertyChanged(nameof(IsLibraryAnimeSelected));
        OnPropertyChanged(nameof(IsLibraryMangaSelected));
        _ = LoadLibraryAsync();
    }

    public bool IsLibraryAllSelected => SelectedLibraryType == "All";
    public bool IsLibraryAnimeSelected => SelectedLibraryType == "Anime";
    public bool IsLibraryMangaSelected => SelectedLibraryType == "Manga";
    public bool IsDiscoverAllSelected => SelectedDiscoverType == "All";
    public bool IsDiscoverAnimeSelected => SelectedDiscoverType == "Anime";
    public bool IsDiscoverMangaSelected => SelectedDiscoverType == "Manga";
    public bool IsDiscoverPopularitySelected => SelectedDiscoverSort == "Popularity";
    public bool IsDiscoverScoreSelected => SelectedDiscoverSort == "Score";
    public bool IsDiscoverTrendingSelected => SelectedDiscoverSort == "Trending";
    public bool IsDiscoverNewSelected => SelectedDiscoverSort == "New";
    partial void OnSelectedLibrarySortChanged(string value) => _ = LoadLibraryAsync();

    partial void OnSelectedLibraryViewModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLibraryListVisible));
        OnPropertyChanged(nameof(IsLibraryGridVisible));
        OnPropertyChanged(nameof(IsLibraryListMode));
        OnPropertyChanged(nameof(IsLibraryGridMode));
    }

    [ObservableProperty]
    private string selectedLibraryType = "All";

    [ObservableProperty]
    private string selectedLibraryStatus = "All";

    [ObservableProperty]
    private string librarySearchText = string.Empty;

    [ObservableProperty]
    private string selectedLibrarySort = "Updated";

    [ObservableProperty]
    private string selectedLibraryViewMode = "List";

    [ObservableProperty]
    private string selectedScheduleRange = "Today";

    [ObservableProperty]
    private bool hasSearchResults;

    [ObservableProperty]
    private bool hasMoreSearchResults;

    [ObservableProperty]
    private bool isLoadingMoreSearchResults;

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

    public bool IsLibraryListVisible => SelectedLibraryViewMode == "List";
    public bool IsLibraryGridVisible => SelectedLibraryViewMode == "Grid";
    public bool IsLibraryListMode => SelectedLibraryViewMode == "List";
    public bool IsLibraryGridMode => SelectedLibraryViewMode == "Grid";
    public bool IsLibraryEmpty => LibraryEntries.Count == 0;
    public bool HasContinueItems => ContinueItems.Count > 0;
    public bool HasScheduleItems => ScheduleItems.Count > 0;
    public bool IsScheduleTodaySelected => SelectedScheduleRange == "Today";
    public bool IsScheduleTomorrowSelected => SelectedScheduleRange == "Tomorrow";
    public bool IsScheduleThisWeekSelected => SelectedScheduleRange == "This week";
    public bool IsScheduleNextWeekSelected => SelectedScheduleRange == "Next week";
    public bool IsNebulaThemeSelected => SettingsTheme == "Nebula dark";
    public bool IsHighContrastThemeSelected => SettingsTheme == "High contrast";
    public bool IsAniListSyncSelected => PreferredSyncSource == "AniList";
    public bool IsMyAnimeListSyncSelected => PreferredSyncSource == "MyAnimeList";
    public bool IsMangaDexSyncSelected => PreferredSyncSource == "MangaDex";
    public string ScheduleHeading => SelectedScheduleRange.ToUpperInvariant();
    public string ScheduleRangeDetail => SelectedScheduleRange switch
    {
        "Today" => "AniList airing · remaining today",
        "Tomorrow" => "AniList airing · tomorrow",
        "Next week" => "AniList airing · following 7 days",
        _ => "AniList airing · next 7 days"
    };
    public string LibraryEmptyMessage =>
        SelectedLibraryStatus == "All" && string.IsNullOrWhiteSpace(LibrarySearchText)
            ? "Your library is empty"
            : "No entries match your current filter";

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

    [ObservableProperty]
    private string libraryExportStatus = "Export a portable CSV copy of your local library.";

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

    private string previousPageKey = "home";
    private int currentDiscoverPage = 1;
    private int? discoverYearFilter;
    private MediaFormat? discoverFormatFilter;
    private MediaReleaseStatus? discoverReleaseStatusFilter;
    private string? discoverCountryFilter;
    private bool suppressDiscoverFilterRefresh;

    partial void OnSelectedScheduleRangeChanged(string value)
    {
        OnPropertyChanged(nameof(IsScheduleTodaySelected));
        OnPropertyChanged(nameof(IsScheduleTomorrowSelected));
        OnPropertyChanged(nameof(IsScheduleThisWeekSelected));
        OnPropertyChanged(nameof(IsScheduleNextWeekSelected));
        OnPropertyChanged(nameof(ScheduleHeading));
        OnPropertyChanged(nameof(ScheduleRangeDetail));
    }

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
            new AsyncRelayCommand<string?>(s => SetScoreDirectAsync(mediaId, int.TryParse(s, out var v) ? v : 0)),
            new AsyncRelayCommand(() => RemoveFromLibraryAsync(mediaId)),
            libraryVm?.Notes,
            new AsyncRelayCommand<string?>(notes => SaveNotesAsync(mediaId, notes)));

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
        await LoadSearchResultsAsync(page: 1, append: false);
        IsDiscoverSearchActive = HasSearchResults || !string.IsNullOrWhiteSpace(SearchText);
    }

    private async Task LoadSearchResultsAsync(int page = 1, bool append = false)
    {
        var result = await mediaCatalogService.SearchAsync(new MediaSearchQuery
        {
            SearchText = SearchText,
            Type = ParseMediaTypeFilter(SelectedDiscoverType),
            Genres = string.IsNullOrWhiteSpace(SelectedDiscoverGenre) ? [] : [SelectedDiscoverGenre],
            Sort = ParseDiscoverSort(SelectedDiscoverSort),
            SeasonYear = discoverYearFilter,
            Format = discoverFormatFilter,
            ReleaseStatus = discoverReleaseStatusFilter,
            CountryCode = discoverCountryFilter,
            Page = page,
            PageSize = 12
        });

        if (!append)
        {
            SearchResults.Clear();
        }

        if (result.IsFailure)
        {
            SearchStatus = result.Error.Message;
            HasSearchResults = SearchResults.Count > 0;
            HasMoreSearchResults = false;
            return;
        }

        foreach (var item in result.Value!.Items)
        {
            if (SearchResults.All(existing => !existing.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase)))
            {
                SearchResults.Add(MediaSearchResultViewModel.FromMediaItem(item, AddToLibraryAsync, OpenDetailAsync));
            }
        }

        currentDiscoverPage = page;
        HasSearchResults = SearchResults.Count > 0;
        HasMoreSearchResults = result.Value.HasNextPage;
        SearchStatus = HasSearchResults
            ? result.Value.TotalCount is { } total
                ? $"Showing {SearchResults.Count} of {total} result(s)."
                : $"Showing {SearchResults.Count} result(s)."
            : "No local results found.";
    }

    [RelayCommand]
    private async Task SelectDiscoverTypeAsync(string type)
    {
        ResetDiscoverAdvancedFilters();
        SelectedDiscoverType = string.IsNullOrWhiteSpace(type) ? "All" : type;
        OnPropertyChanged(nameof(IsDiscoverAllSelected));
        OnPropertyChanged(nameof(IsDiscoverAnimeSelected));
        OnPropertyChanged(nameof(IsDiscoverMangaSelected));
        await ApplyDiscoverFiltersAsync();
    }

    [RelayCommand]
    private async Task SelectDiscoverSortAsync(string sort)
    {
        ResetDiscoverAdvancedFilters();
        SelectedDiscoverSort = string.IsNullOrWhiteSpace(sort) ? "Popularity" : sort;
        OnPropertyChanged(nameof(IsDiscoverPopularitySelected));
        OnPropertyChanged(nameof(IsDiscoverScoreSelected));
        OnPropertyChanged(nameof(IsDiscoverTrendingSelected));
        OnPropertyChanged(nameof(IsDiscoverNewSelected));
        await ApplyDiscoverFiltersAsync();
    }

    private async Task ApplyDiscoverFiltersAsync()
    {
        await LoadSearchResultsAsync(page: 1, append: false);
        IsDiscoverSearchActive = true;
    }

    [RelayCommand]
    private async Task ShowDiscoverCollectionAsync(string collection)
    {
        SearchText = string.Empty;
        suppressDiscoverFilterRefresh = true;
        SelectedDiscoverGenre = null;
        suppressDiscoverFilterRefresh = false;
        ResetDiscoverAdvancedFilters();

        switch (collection)
        {
            case "Trending":
                SelectedDiscoverType = "Anime";
                SelectedDiscoverSort = "Trending";
                break;
            case "ThisSeason":
                SelectedDiscoverType = "Anime";
                SelectedDiscoverSort = "Popularity";
                discoverYearFilter = DateTimeOffset.Now.Year;
                break;
            case "TopRated":
                SelectedDiscoverType = "All";
                SelectedDiscoverSort = "Score";
                break;
            case "Upcoming":
                SelectedDiscoverType = "Anime";
                SelectedDiscoverSort = "New";
                discoverReleaseStatusFilter = MediaReleaseStatus.NotYetReleased;
                break;
            case "Manhwa":
                SelectedDiscoverType = "Manga";
                SelectedDiscoverSort = "Popularity";
                discoverCountryFilter = "KR";
                break;
        }

        NotifyDiscoverFilterSelectionChanged();
        await ApplyDiscoverFiltersAsync();
    }

    [RelayCommand]
    private async Task LoadMoreSearchResultsAsync()
    {
        if (!HasMoreSearchResults || IsLoadingMoreSearchResults)
        {
            return;
        }

        IsLoadingMoreSearchResults = true;
        try
        {
            await LoadSearchResultsAsync(currentDiscoverPage + 1, append: true);
        }
        finally
        {
            IsLoadingMoreSearchResults = false;
        }
    }

    [RelayCommand]
    private void NavigateToDiscover() => SelectNavigationItem("discover");

    [RelayCommand]
    private void NavigateToLibrary() => SelectNavigationItem("library");

    [RelayCommand]
    private async Task NavigateToScheduleAsync()
    {
        SelectedScheduleRange = "Today";
        SelectNavigationItem("schedule");
        await LoadScheduleAsync();
    }

    [RelayCommand]
    private void ClearDiscoverSearch()
    {
        SearchText = string.Empty;
        SearchResults.Clear();
        HasSearchResults = false;
        HasMoreSearchResults = false;
        currentDiscoverPage = 1;
        IsDiscoverSearchActive = false;
        SearchStatus = "Search uses AniList with local fallback.";
    }

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private async Task SearchLibraryAsync()
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
    private async Task SelectLibrarySortAsync(string sort)
    {
        SelectedLibrarySort = string.IsNullOrWhiteSpace(sort) ? "Updated" : sort;
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private void SelectLibraryViewMode(string viewMode)
    {
        SelectedLibraryViewMode = string.IsNullOrWhiteSpace(viewMode) ? "List" : viewMode;
        OnPropertyChanged(nameof(IsLibraryListVisible));
        OnPropertyChanged(nameof(IsLibraryGridVisible));
    }

    [RelayCommand]
    private async Task SelectLibraryStatusAsync(string status)
    {
        SelectedLibraryStatus = string.IsNullOrWhiteSpace(status) ? "All" : status;
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private async Task SelectScheduleRangeAsync(string range)
    {
        SelectedScheduleRange = string.IsNullOrWhiteSpace(range) ? "Today" : range;
        await LoadScheduleAsync();
    }

    [RelayCommand]
    private async Task RefreshScheduleAsync()
    {
        await LoadScheduleAsync();
    }

    private async Task LoadPageDataAsync()
    {
        await LoadSettingsAsync();
        await LoadDiscoverBrowseAsync();
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
        appThemeService.Apply(SettingsTheme);
        OnPropertyChanged(nameof(IsNebulaThemeSelected));
        OnPropertyChanged(nameof(IsHighContrastThemeSelected));
        NotificationsEnabled = appSettings.NotificationsEnabled;
        OfflineCacheEnabled = appSettings.OfflineCacheEnabled;
        RecognitionServicesEnabled = appSettings.RecognitionServicesEnabled;
        PreferredSyncSource = appSettings.PreferredSyncSource;
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
    }

    [RelayCommand]
    private async Task ConnectAniListAsync()
    {
        AniListConnectionStatus = "Opening browser…";
        var result = await aniListAuthService.AuthorizeAsync(AniListClientId, AniListClientSecret);
        if (result.IsFailure)
        {
            AniListConnectionStatus = result.Error.Message;
            return;
        }

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
        var result = await aniListSyncService.SyncAsync(
            appSettings.AniListAccessToken,
            appSettings.AniListUserId,
            userLibraryService);

        if (result.IsFailure)
        {
            AniListSyncStatus = result.Error.Message;
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

    [RelayCommand]
    private async Task ExportLibraryAsync()
    {
        var result = await userLibraryService.GetEntriesAsync(new UserLibraryQuery
        {
            PageSize = 500
        });

        if (result.IsFailure)
        {
            LibraryExportStatus = result.Error.Message;
            return;
        }

        var rows = new List<LibraryExportItem>();
        foreach (var entry in result.Value!.Items)
        {
            var media = await mediaCatalogService.GetByIdAsync(entry.MediaId);
            rows.Add(new LibraryExportItem(
                entry.MediaId,
                media.IsSuccess ? media.Value!.Title.DisplayTitle : entry.MediaId,
                entry.MediaType.ToString(),
                entry.Status.ToString(),
                entry.Progress,
                entry.Score));
        }

        var saved = await fileSaveService.SaveTextAsync(
            $"tanahub-library-{DateTimeOffset.Now:yyyy-MM-dd}.csv",
            LibraryCsvExporter.Export(rows),
            "csv",
            "text/csv");

        LibraryExportStatus = saved
            ? $"Exported {rows.Count} library item(s)."
            : "Export canceled.";
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
        appThemeService.Apply(SettingsTheme);
        OnPropertyChanged(nameof(IsNebulaThemeSelected));
        OnPropertyChanged(nameof(IsHighContrastThemeSelected));
        NotificationsEnabled = appSettings.NotificationsEnabled;
        OfflineCacheEnabled = appSettings.OfflineCacheEnabled;
        RecognitionServicesEnabled = appSettings.RecognitionServicesEnabled;
        PreferredSyncSource = appSettings.PreferredSyncSource;
        NotifySyncSourceSelectionChanged();
        SettingsStorageDetail = $"Settings saved {appSettings.UpdatedAt:yyyy-MM-dd HH:mm}";
        IsAniListConnected = !string.IsNullOrWhiteSpace(appSettings.AniListAccessToken);
        AniListConnectionStatus = IsAniListConnected
            ? $"Connected as {appSettings.AniListUsername}"
            : "Not connected";
        AniListSyncStatus = appSettings.AniListLastSyncAt.HasValue
            ? $"Last sync: {appSettings.AniListLastSyncAt:yyyy-MM-dd HH:mm}"
            : string.Empty;
        SearchStatus = "Settings saved.";
    }

    private void NotifySyncSourceSelectionChanged()
    {
        OnPropertyChanged(nameof(IsAniListSyncSelected));
        OnPropertyChanged(nameof(IsMyAnimeListSyncSelected));
        OnPropertyChanged(nameof(IsMangaDexSyncSelected));
    }

    private async Task LoadLibraryAsync()
    {
        var result = await userLibraryService.GetEntriesAsync(new UserLibraryQuery
        {
            Type = ParseMediaTypeFilter(SelectedLibraryType),
            Status = ParseStatusFilter(SelectedLibraryStatus),
            PageSize = 500
        });

        if (result.IsFailure)
        {
            return;
        }

        LibraryEntries.Clear();
        ContinueItems.Clear();

        var hydratedEntries = new List<LibraryEntryViewModel>();

        foreach (var entry in result.Value!.Items)
        {
            var viewModel = await CreateLibraryEntryViewModelAsync(entry);
            hydratedEntries.Add(viewModel);

            if (entry.Status is MediaListStatus.Current or MediaListStatus.Paused)
            {
                ContinueItems.Add(viewModel);
            }
        }

        foreach (var viewModel in SortLibraryEntries(FilterLibraryEntries(hydratedEntries)))
        {
            LibraryEntries.Add(viewModel);
        }

        DashboardMetrics.Clear();
        DashboardMetrics.Add(new("Library", LibraryEntries.Count.ToString(), "seeded local entries", MaterialIconKind.Bookshelf));
        DashboardMetrics.Add(new("Watching", ContinueItems.Count.ToString(), "current or paused", MaterialIconKind.PlayCircle));
        DashboardMetrics.Add(new("Search", SearchResults.Count.ToString(), "local catalog results", MaterialIconKind.Magnify));

        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(LibraryEmptyMessage));
        OnPropertyChanged(nameof(HasContinueItems));
    }

    private async Task LoadScheduleAsync()
    {
        var (from, to) = GetScheduleWindow();
        var result = await airingScheduleService.GetUpcomingAsync(from, to, pageSize: 50);

        ScheduleItems.Clear();

        if (result.IsFailure)
        {
            SearchStatus = result.Error.Message;
            OnPropertyChanged(nameof(HasScheduleItems));
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

        OnPropertyChanged(nameof(HasScheduleItems));
    }

    private (DateTimeOffset From, DateTimeOffset To) GetScheduleWindow()
    {
        var now = DateTimeOffset.Now;
        var startOfToday = new DateTimeOffset(now.Date, now.Offset);

        return SelectedScheduleRange switch
        {
            "Tomorrow" => (startOfToday.AddDays(1), startOfToday.AddDays(2)),
            "Next week" => (startOfToday.AddDays(7), startOfToday.AddDays(14)),
            "This week" => (now, now.AddDays(7)),
            _ => (now, startOfToday.AddDays(1))
        };
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
            new AsyncRelayCommand(() => OpenDetailAsync(entry.MediaId)),
            entry.Notes);
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

    private async Task SaveNotesAsync(string mediaId, string? notes)
    {
        var result = await userLibraryService.UpdateNotesAsync(mediaId, notes);
        if (result.IsFailure)
        {
            SearchStatus = result.Error.Message;
        }
    }

    private async Task SetScoreDirectAsync(string mediaId, int score)
    {
        var result = await userLibraryService.UpdateScoreAsync(mediaId, score);
        SearchStatus = result.IsSuccess
            ? $"Score set to {score} for {mediaId}."
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

    private IEnumerable<LibraryEntryViewModel> FilterLibraryEntries(IEnumerable<LibraryEntryViewModel> entries)
    {
        if (string.IsNullOrWhiteSpace(LibrarySearchText))
        {
            return entries;
        }

        return entries.Where(entry =>
            entry.Title.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase)
            || entry.MediaId.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<LibraryEntryViewModel> SortLibraryEntries(IEnumerable<LibraryEntryViewModel> entries)
    {
        return SelectedLibrarySort switch
        {
            "Title" => entries.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            "Score" => entries.OrderByDescending(entry => ParseLibraryScore(entry.Score) ?? -1),
            "Progress" => entries.OrderByDescending(entry => ParseProgressValue(entry.Progress)),
            _ => entries
        };
    }

    private static int ParseProgressValue(string progress)
    {
        var separatorIndex = progress.IndexOf('/', StringComparison.Ordinal);
        var value = separatorIndex >= 0 ? progress[..separatorIndex] : progress;
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private async Task LoadDiscoverBrowseAsync()
    {
        var trendingTask = mediaCatalogService.SearchAsync(new MediaSearchQuery
        {
            Type = MediaType.Anime,
            Sort = MediaSearchSort.Trending,
            PageSize = 10
        });
        var thisSeasonTask = mediaCatalogService.SearchAsync(new MediaSearchQuery
        {
            Type = MediaType.Anime,
            SeasonYear = DateTimeOffset.Now.Year,
            Sort = MediaSearchSort.Popularity,
            PageSize = 8
        });
        var topRatedTask = mediaCatalogService.SearchAsync(new MediaSearchQuery
        {
            Sort = MediaSearchSort.Score,
            PageSize = 8
        });
        var upcomingTask = mediaCatalogService.SearchAsync(new MediaSearchQuery
        {
            Type = MediaType.Anime,
            ReleaseStatus = MediaReleaseStatus.NotYetReleased,
            Sort = MediaSearchSort.Newest,
            PageSize = 8
        });
        var manhwaTask = mediaCatalogService.SearchAsync(new MediaSearchQuery
        {
            Type = MediaType.Manga,
            CountryCode = "KR",
            Sort = MediaSearchSort.Popularity,
            PageSize = 8
        });

        await Task.WhenAll(trendingTask, thisSeasonTask, topRatedTask, upcomingTask, manhwaTask);

        PopulateDiscoverCollection(TrendingItems, await trendingTask);
        PopulateDiscoverCollection(ThisSeasonItems, await thisSeasonTask);
        PopulateDiscoverCollection(TopRatedItems, await topRatedTask);
        PopulateDiscoverCollection(UpcomingItems, await upcomingTask);
        PopulateDiscoverCollection(ManhwaItems, await manhwaTask);
    }

    private void PopulateDiscoverCollection(
        ObservableCollection<MediaSearchResultViewModel> target,
        Result<PagedResult<MediaItem>> result)
    {
        target.Clear();
        if (result.IsFailure)
        {
            return;
        }

        foreach (var item in result.Value!.Items)
        {
            target.Add(MediaSearchResultViewModel.FromMediaItem(item, AddToLibraryAsync, OpenDetailAsync));
        }
    }

    private static MediaSearchSort ParseDiscoverSort(string sort)
    {
        return sort switch
        {
            "Score" => MediaSearchSort.Score,
            "Trending" => MediaSearchSort.Trending,
            "New" => MediaSearchSort.Newest,
            _ => MediaSearchSort.Popularity
        };
    }

    private void ResetDiscoverAdvancedFilters()
    {
        discoverYearFilter = null;
        discoverFormatFilter = null;
        discoverReleaseStatusFilter = null;
        discoverCountryFilter = null;
    }

    private void NotifyDiscoverFilterSelectionChanged()
    {
        OnPropertyChanged(nameof(IsDiscoverAllSelected));
        OnPropertyChanged(nameof(IsDiscoverAnimeSelected));
        OnPropertyChanged(nameof(IsDiscoverMangaSelected));
        OnPropertyChanged(nameof(IsDiscoverPopularitySelected));
        OnPropertyChanged(nameof(IsDiscoverScoreSelected));
        OnPropertyChanged(nameof(IsDiscoverTrendingSelected));
        OnPropertyChanged(nameof(IsDiscoverNewSelected));
    }
}
