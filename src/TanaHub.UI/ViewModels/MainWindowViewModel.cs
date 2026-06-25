using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using TanaHub.Application.Common;
using TanaHub.Application.Export;
using TanaHub.Application.Insights;
using TanaHub.Application.Queries;
using TanaHub.Application.Recognition;
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
    private readonly INotificationService notificationService;
    private readonly ICatalogSourceSelector catalogSourceSelector;
    private readonly IRecognitionService recognitionService;
    private readonly IRecognitionInboxService recognitionInboxService;
    private readonly IFileOpenService fileOpenService;
    private readonly HashSet<string> notifiedThisSession = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings appSettings = new();
    private CancellationTokenSource? searchDebounce;

    public MainWindowViewModel(
        IMediaCatalogService mediaCatalogService,
        IUserLibraryService userLibraryService,
        IAppSettingsService appSettingsService,
        IAiringScheduleService airingScheduleService,
        IAppThemeService appThemeService,
        IFileSaveService fileSaveService,
        IAniListAuthService aniListAuthService,
        IAniListSyncService aniListSyncService,
        INotificationService notificationService,
        ICatalogSourceSelector catalogSourceSelector,
        IRecognitionService recognitionService,
        IRecognitionInboxService recognitionInboxService,
        IFileOpenService fileOpenService)
    {
        this.mediaCatalogService = mediaCatalogService;
        this.userLibraryService = userLibraryService;
        this.appSettingsService = appSettingsService;
        this.airingScheduleService = airingScheduleService;
        this.appThemeService = appThemeService;
        this.fileSaveService = fileSaveService;
        this.aniListAuthService = aniListAuthService;
        this.aniListSyncService = aniListSyncService;
        this.notificationService = notificationService;
        this.catalogSourceSelector = catalogSourceSelector;
        this.recognitionService = recognitionService;
        this.recognitionInboxService = recognitionInboxService;
        this.fileOpenService = fileOpenService;

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
                "recognize",
                "Recognize",
                "Identify anime from a screenshot using trace.moe reverse image search.",
                "Enable Recognition services in Settings → Preferences to use this feature."),
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
        allLibraryEntries = [];
        SetVisiblePage(selectedNavigationItem.Key);

        _ = LoadPageDataAsync();
    }

    public string AppName => "TanaHub";

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public ObservableCollection<MediaSearchResultViewModel> SearchResults { get; }

    public ObservableCollection<DashboardMetricViewModel> DashboardMetrics { get; }

    public ObservableCollection<LibraryEntryViewModel> ContinueItems { get; }

    public ObservableCollection<LibraryEntryViewModel> LibraryEntries { get; }

    private List<LibraryEntryViewModel> allLibraryEntries;

    public ObservableCollection<ScheduleItemViewModel> ScheduleItems { get; }

    public ObservableCollection<MediaSearchResultViewModel> TrendingItems { get; } = [];
    public ObservableCollection<MediaSearchResultViewModel> ThisSeasonItems { get; } = [];
    public ObservableCollection<MediaSearchResultViewModel> TopRatedItems { get; } = [];
    public ObservableCollection<MediaSearchResultViewModel> UpcomingItems { get; } = [];
    public ObservableCollection<MediaSearchResultViewModel> ManhwaItems { get; } = [];
    public ObservableCollection<MediaSearchResultViewModel> SearchDropdownResults { get; } = [];

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

    public ObservableCollection<string> LibraryTagFilters { get; } = ["All tags"];

    public ObservableCollection<string> LibraryCustomListFilters { get; } = ["All lists"];

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
    private bool isSearchDropdownOpen;

    partial void OnSearchTextChanged(string value)
    {
        searchDebounce?.Cancel();
        searchDebounce?.Dispose();
        searchDebounce = null;

        if (IsDiscoverVisible || string.IsNullOrWhiteSpace(value))
        {
            IsSearchDropdownOpen = false;
            SearchDropdownResults.Clear();
            return;
        }

        var cts = new CancellationTokenSource();
        searchDebounce = cts;
        _ = DebounceDropdownAsync(value, cts.Token);
    }

    [ObservableProperty]
    private string searchStatus = "Search uses AniList with local fallback.";

    [ObservableProperty]
    private string appConnectionStatusText = "Cached";

    [ObservableProperty]
    private string appConnectionStatusDetail = "Loading cache status...";

    [ObservableProperty]
    private string appConnectionStatusKind = "Cached";

    public bool IsConnectionStatusOnline => AppConnectionStatusKind == "Online";
    public bool IsConnectionStatusCached => AppConnectionStatusKind == "Cached";
    public bool IsConnectionStatusOffline => AppConnectionStatusKind == "Offline";

    partial void OnAppConnectionStatusKindChanged(string value)
    {
        OnPropertyChanged(nameof(IsConnectionStatusOnline));
        OnPropertyChanged(nameof(IsConnectionStatusCached));
        OnPropertyChanged(nameof(IsConnectionStatusOffline));
    }

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

    partial void OnSelectedLibraryStatusChanged(string value) => ApplyLibraryFilters();
    partial void OnSelectedLibraryTagChanged(string value)
    {
        if (!suppressLibraryFilterRefresh)
            ApplyLibraryFilters();
    }

    partial void OnSelectedLibraryCustomListChanged(string value)
    {
        if (!suppressLibraryFilterRefresh)
            ApplyLibraryFilters();
    }

    partial void OnSelectedLibraryTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLibraryAllSelected));
        OnPropertyChanged(nameof(IsLibraryAnimeSelected));
        OnPropertyChanged(nameof(IsLibraryMangaSelected));
        ApplyLibraryFilters();
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
    partial void OnSelectedLibrarySortChanged(string value) => ApplyLibraryFilters();

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
    private string selectedLibraryTag = "All tags";

    [ObservableProperty]
    private string selectedLibraryCustomList = "All lists";

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
    private bool isRecognizeVisible;

    [ObservableProperty]
    private bool isRecognizing;

    [ObservableProperty]
    private string recognitionStatus = string.Empty;

    [ObservableProperty]
    private string recognitionEmptyMessage = string.Empty;

    [ObservableProperty]
    private Uri? recognitionSourcePreviewUri;

    [ObservableProperty]
    private RecognitionResultViewModel? currentRecognitionResult;

    [ObservableProperty]
    private bool isRecognitionHistoryExpanded;

    public ObservableCollection<RecognitionResultViewModel> RecognitionResults { get; } = [];

    public ObservableCollection<RecognitionResultViewModel> RecognitionVariantResults { get; } = [];

    public ObservableCollection<RecognitionInboxItemViewModel> RecognitionInboxItems { get; } = [];

    public bool HasRecognitionInboxItems => RecognitionInboxItems.Count > 0;
    public bool HasRecognitionSourcePreview => RecognitionSourcePreviewUri is not null;
    public bool HasCurrentRecognitionResult => CurrentRecognitionResult is not null;
    public bool HasRecognitionVariantResults => RecognitionVariantResults.Count > 0;
    public bool IsRecognitionHistoryVisible => HasRecognitionInboxItems && IsRecognitionHistoryExpanded;
    public bool IsRecognitionHistoryEmptyVisible => !HasRecognitionInboxItems && IsRecognitionHistoryExpanded;
    public bool HasRecognitionEmptyMessage => !string.IsNullOrWhiteSpace(RecognitionEmptyMessage);

    partial void OnRecognitionEmptyMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasRecognitionEmptyMessage));

    partial void OnRecognitionSourcePreviewUriChanged(Uri? value) =>
        OnPropertyChanged(nameof(HasRecognitionSourcePreview));

    partial void OnCurrentRecognitionResultChanged(RecognitionResultViewModel? value) =>
        OnPropertyChanged(nameof(HasCurrentRecognitionResult));

    partial void OnIsRecognitionHistoryExpandedChanged(bool value) =>
        NotifyRecognitionHistoryVisibilityChanged();

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
        SelectedLibraryType == "All"
        && SelectedLibraryStatus == "All"
        && SelectedLibraryTag == "All tags"
        && SelectedLibraryCustomList == "All lists"
        && string.IsNullOrWhiteSpace(LibrarySearchText)
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
    private bool suppressLibraryFilterRefresh;
    private int libraryLoadVersion;

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
        IsSearchDropdownOpen = false;
        CurrentPageTitle = value?.Title ?? AppName;
        CurrentPageSummary = value?.Summary ?? string.Empty;
        CurrentEmptyState = value?.EmptyState ?? string.Empty;
        SelectedNavigationIndex = value is null ? 0 : NavigationItems.IndexOf(value);
        SelectedNavigationOffset = Math.Max(0, SelectedNavigationIndex) * 48;
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
        IsSearchDropdownOpen = false;
        previousPageKey = SelectedNavigationItem?.Key ?? "home";

        var mediaResult = await mediaCatalogService.GetByIdAsync(mediaId);
        if (mediaResult.IsFailure)
        {
            SearchStatus = mediaResult.Error.Message;
            return;
        }

        var item = mediaResult.Value!;
        var libraryEntry = await GetLibraryEntryAsync(mediaId);

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

        var libraryProgress = libraryEntry is null
            ? string.Empty
            : $"{libraryEntry.Progress}/{(item is Anime ? episodes : chapters)}";
        var libraryScore = libraryEntry?.Score?.ToString() ?? string.Empty;

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
            libraryEntry?.Status.ToString() ?? string.Empty,
            libraryProgress,
            libraryScore,
            libraryEntry is not null,
            new AsyncRelayCommand(() => AddToLibraryAsync(mediaId)),
            new AsyncRelayCommand(() => IncrementProgressAsync(mediaId)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Current)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Planning)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Paused)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Completed)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(mediaId, MediaListStatus.Dropped)),
            new AsyncRelayCommand(() => IncreaseScoreAsync(mediaId, libraryEntry?.Score)),
            new AsyncRelayCommand<string?>(s => SetScoreDirectAsync(mediaId, int.TryParse(s, out var v) ? v : 0)),
            new AsyncRelayCommand<string?>(s => SetProgressAsync(mediaId, int.TryParse(s, out var v) ? v : 0)),
            new AsyncRelayCommand(() => SetProgressAsync(mediaId, int.TryParse(item is Anime a ? a.EpisodeCount?.ToString() : item is Manga m ? m.ChapterCount?.ToString() : null, out var total) ? total : 0)),
            new AsyncRelayCommand(() => SetProgressAsync(mediaId, 0)),
            new AsyncRelayCommand(() => RemoveFromLibraryAsync(mediaId)),
            libraryEntry?.Notes,
            libraryEntry?.Review,
            string.Join(", ", libraryEntry?.Tags ?? []),
            string.Join(", ", libraryEntry?.CustomLists ?? []),
            new AsyncRelayCommand<string?>(tags => SaveTagsAsync(mediaId, tags)),
            new AsyncRelayCommand<string?>(lists => SaveCustomListsAsync(mediaId, lists)),
            item.Characters,
            new AsyncRelayCommand<string?>(notes => SaveNotesAsync(mediaId, notes)),
            new AsyncRelayCommand<string?>(review => SaveReviewAsync(mediaId, review)));

        CurrentPageTitle = item.Title.DisplayTitle;
        CurrentPageSummary = $"{item.Format} · {item.ReleaseStatus}";
        IsDetailVisible = true;
        IsHomeVisible = false;
        IsDiscoverVisible = false;
        IsLibraryVisible = false;
        IsScheduleVisible = false;
        IsSettingsVisible = false;
        IsRecognizeVisible = false;
    }

    private async Task<UserMediaEntry?> GetLibraryEntryAsync(string mediaId)
    {
        var result = await userLibraryService.GetEntryAsync(mediaId);
        return result.IsSuccess ? result.Value : null;
    }

    private async Task RefreshDetailIfOpenAsync(string mediaId)
    {
        if (IsDetailVisible && SelectedMedia?.Id == mediaId)
        {
            await OpenDetailAsync(mediaId);
        }
    }

    [RelayCommand]
    private void CloseSearchDropdown()
    {
        IsSearchDropdownOpen = false;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsSearchDropdownOpen = false;
        SelectNavigationItem("discover");
        await LoadSearchResultsAsync(page: 1, append: false);
        IsDiscoverSearchActive = HasSearchResults || !string.IsNullOrWhiteSpace(SearchText);
    }

    private async Task DebounceDropdownAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(350, ct);
            var result = await mediaCatalogService.SearchAsync(new MediaSearchQuery
            {
                SearchText = query,
                Page = 1,
                PageSize = 6
            });

            if (ct.IsCancellationRequested) return;

            SearchDropdownResults.Clear();
            if (result.IsSuccess)
            {
                foreach (var item in result.Value!.Items)
                {
                    if (ct.IsCancellationRequested) break;
                    SearchDropdownResults.Add(
                        MediaSearchResultViewModel.FromMediaItem(item, AddToLibraryAsync, OpenDetailAsync));
                }
            }

            IsSearchDropdownOpen = SearchDropdownResults.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Debounced — newer keystroke superseded this search
        }
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
    private void SearchLibrary() => ApplyLibraryFilters();

    [RelayCommand]
    private async Task SelectLibraryTypeAsync(string type)
    {
        SelectedLibraryType = string.IsNullOrWhiteSpace(type) ? "All" : type;
        // OnSelectedLibraryTypeChanged applies the in-memory filter.
    }

    [RelayCommand]
    private void SelectLibrarySort(string sort)
    {
        SelectedLibrarySort = string.IsNullOrWhiteSpace(sort) ? "Updated" : sort;
        // OnSelectedLibrarySortChanged fires ApplyLibraryFilters
    }

    [RelayCommand]
    private void SelectLibraryViewMode(string viewMode)
    {
        SelectedLibraryViewMode = string.IsNullOrWhiteSpace(viewMode) ? "List" : viewMode;
        OnPropertyChanged(nameof(IsLibraryListVisible));
        OnPropertyChanged(nameof(IsLibraryGridVisible));
    }

    [RelayCommand]
    private void SelectLibraryStatus(string status)
    {
        SelectedLibraryStatus = string.IsNullOrWhiteSpace(status) ? "All" : status;
        // OnSelectedLibraryStatusChanged fires ApplyLibraryFilters
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
        await LoadRecognitionInboxAsync();
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

        appSettings = result.Value!;
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
        SetAppConnectionStatus("Online", "Syncing AniList...");
        var result = await aniListSyncService.SyncAsync(
            appSettings.AniListAccessToken,
            appSettings.AniListUserId,
            userLibraryService);

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
        var resolved = string.IsNullOrWhiteSpace(source) ? "AniList" : source;
        catalogSourceSelector.SetSource(resolved);
        await SaveSettingsAsync(appSettings with { PreferredSyncSource = resolved });
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
                entry.Score)
            {
                Tags = entry.Tags,
                CustomLists = entry.CustomLists
            });
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
            SetAppConnectionStatus("Offline", "Settings save failed");
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
        catalogSourceSelector.SetSource(PreferredSyncSource);
        NotifySyncSourceSelectionChanged();
        SettingsStorageDetail = $"Settings saved {appSettings.UpdatedAt:yyyy-MM-dd HH:mm}";
        IsAniListConnected = !string.IsNullOrWhiteSpace(appSettings.AniListAccessToken);
        AniListConnectionStatus = IsAniListConnected
            ? $"Connected as {appSettings.AniListUsername}"
            : "Not connected";
        AniListSyncStatus = appSettings.AniListLastSyncAt.HasValue
            ? $"Last sync: {appSettings.AniListLastSyncAt:yyyy-MM-dd HH:mm}"
            : string.Empty;
        RefreshAppConnectionStatus();
        SearchStatus = "Settings saved.";
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

    private void SetAppConnectionStatus(string kind, string detail)
    {
        var normalizedKind = kind switch
        {
            "Online" => "Online",
            "Offline" => "Offline",
            _ => "Cached"
        };

        AppConnectionStatusKind = normalizedKind;
        AppConnectionStatusText = normalizedKind;
        AppConnectionStatusDetail = detail;
    }

    private void NotifySyncSourceSelectionChanged()
    {
        OnPropertyChanged(nameof(IsAniListSyncSelected));
        OnPropertyChanged(nameof(IsMyAnimeListSyncSelected));
        OnPropertyChanged(nameof(IsMangaDexSyncSelected));
    }

    private async Task LoadLibraryAsync()
    {
        var loadVersion = ++libraryLoadVersion;
        var result = await userLibraryService.GetEntriesAsync(new UserLibraryQuery
        {
            PageSize = int.MaxValue
        });

        if (result.IsFailure || loadVersion != libraryLoadVersion)
        {
            return;
        }

        var hydrated = new List<LibraryEntryViewModel>();
        var continueItems = new List<LibraryEntryViewModel>();
        var entries = result.Value!.Items.ToArray();

        foreach (var entry in entries)
        {
            var vm = await CreateLibraryEntryViewModelAsync(entry);
            hydrated.Add(vm);
            if (entry.Status is MediaListStatus.Current or MediaListStatus.Paused)
            {
                continueItems.Add(vm);
            }
        }

        if (loadVersion != libraryLoadVersion)
        {
            return;
        }

        allLibraryEntries = hydrated;
        ContinueItems.Clear();
        foreach (var item in continueItems)
        {
            ContinueItems.Add(item);
        }
        RefreshDashboardMetrics(LibraryInsightsCalculator.Calculate(entries));
        OnPropertyChanged(nameof(HasContinueItems));
        ApplyLibraryFilters();
    }

    private void ApplyLibraryFilters()
    {
        RefreshLibraryOrganizationFilters(allLibraryEntries);

        LibraryEntries.Clear();
        foreach (var vm in SortLibraryEntries(FilterLibraryEntries(allLibraryEntries)))
            LibraryEntries.Add(vm);

        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(LibraryEmptyMessage));
    }

    private void RefreshDashboardMetrics(LibraryInsightsSummary insights)
    {
        DashboardMetrics.Clear();
        DashboardMetrics.Add(new(
            "Completion",
            FormatRatio(insights.CompletionRate),
            $"{insights.CompletedEntries}/{insights.TotalEntries} titles completed",
            MaterialIconKind.CheckCircleOutline));
        DashboardMetrics.Add(new(
            "Backlog",
            insights.BacklogEntries.ToString(),
            "planned titles waiting",
            MaterialIconKind.BookmarkPlusOutline));
        DashboardMetrics.Add(new(
            "Dropped",
            FormatRatio(insights.DroppedRatio),
            $"{insights.DroppedEntries} title(s) dropped",
            MaterialIconKind.CloseCircleOutline));
        DashboardMetrics.Add(new(
            "Avg score",
            insights.AverageScore is null ? "n/a" : insights.AverageScore.Value.ToString("F1"),
            "scored local entries",
            MaterialIconKind.StarOutline));
        DashboardMetrics.Add(new(
            "Watch time",
            FormatMinutes(insights.EstimatedWatchMinutes),
            $"{insights.EpisodesWatched} episode(s) tracked",
            MaterialIconKind.ClockOutline));
        DashboardMetrics.Add(new(
            "Chapters",
            insights.ChaptersRead.ToString(),
            "manga progress tracked",
            MaterialIconKind.BookOpenPageVariant));
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

        if (NotificationsEnabled && SelectedScheduleRange == "Today")
            await NotifyAiringTodayAsync(result.Value!, libraryIds);
    }

    private async Task NotifyAiringTodayAsync(
        IReadOnlyList<AiringScheduleItem> items,
        IReadOnlySet<string> libraryIds)
    {
        var today = DateTimeOffset.Now.Date;
        foreach (var item in items)
        {
            if (!libraryIds.Contains(item.MediaId)) continue;
            if (item.AiringAt.LocalDateTime.Date != today) continue;
            if (!notifiedThisSession.Add(item.MediaId + ":" + item.Episode)) continue;

            await notificationService.NotifyAsync(
                "TanaHub — Airing today",
                $"EP {item.Episode} of {item.Title} is out!");
        }
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
            entry.Notes,
            entry.Tags,
            entry.CustomLists);
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

    private async Task SetProgressAsync(string mediaId, int progress)
    {
        var entryResult = await userLibraryService.GetEntryAsync(mediaId);
        if (entryResult.IsFailure) return;

        var updated = entryResult.Value! with
        {
            Progress = Math.Max(0, progress),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var result = await userLibraryService.UpsertEntryAsync(updated);
        SearchStatus = result.IsSuccess
            ? $"Progress set to {progress}."
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

    private async Task SaveReviewAsync(string mediaId, string? review)
    {
        var result = await userLibraryService.UpdateReviewAsync(mediaId, review);
        SearchStatus = result.IsSuccess ? "Review saved." : result.Error.Message;

        if (result.IsSuccess)
        {
            await LoadLibraryAsync();
            await RefreshDetailIfOpenAsync(mediaId);
        }
    }

    private async Task SaveTagsAsync(string mediaId, string? tagsText)
    {
        var entry = await GetLibraryEntryAsync(mediaId);
        if (entry is null)
        {
            SearchStatus = "Add this title to your library before editing tags.";
            return;
        }

        var result = await userLibraryService.UpdateOrganizationAsync(
            mediaId,
            ParseOrganizationValues(tagsText),
            entry.CustomLists);
        SearchStatus = result.IsSuccess ? "Tags saved." : result.Error.Message;

        if (result.IsSuccess)
        {
            await LoadLibraryAsync();
            await RefreshDetailIfOpenAsync(mediaId);
        }
    }

    private async Task SaveCustomListsAsync(string mediaId, string? customListsText)
    {
        var entry = await GetLibraryEntryAsync(mediaId);
        if (entry is null)
        {
            SearchStatus = "Add this title to your library before editing custom lists.";
            return;
        }

        var result = await userLibraryService.UpdateOrganizationAsync(
            mediaId,
            entry.Tags,
            ParseOrganizationValues(customListsText));
        SearchStatus = result.IsSuccess ? "Custom lists saved." : result.Error.Message;

        if (result.IsSuccess)
        {
            await LoadLibraryAsync();
            await RefreshDetailIfOpenAsync(mediaId);
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

    [RelayCommand]
    private Task RecognizeFromFileAsync() => RunRecognitionAsync(fileOpenService.PickImageAsync);

    [RelayCommand]
    private Task PasteAndRecognizeAsync() => RunRecognitionAsync(fileOpenService.PasteImageAsync);

    [RelayCommand]
    private void ToggleRecognitionHistory()
    {
        IsRecognitionHistoryExpanded = !IsRecognitionHistoryExpanded;
    }

    private async Task RunRecognitionAsync(
        Func<CancellationToken, Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)>> getImage)
    {
        var (stream, mime, sourceName, sourcePath) = await getImage(CancellationToken.None);
        if (stream is null)
        {
            RecognitionStatus = sourceName == "Clipboard image"
                ? "Clipboard does not contain a supported image."
                : "No image selected.";
            return;
        }

        IsRecognizing = true;
        RecognitionStatus = "Searching…";
        RecognitionResults.Clear();
        RecognitionVariantResults.Clear();
        CurrentRecognitionResult = null;
        RecognitionEmptyMessage = string.Empty;
        RecognitionSourcePreviewUri = CreateLocalFileUri(sourcePath);
        OnPropertyChanged(nameof(HasRecognitionVariantResults));

        try
        {
            using (stream)
            {
                var result = await recognitionService.RecognizeAsync(stream, mime);
                if (result.IsFailure)
                {
                    RecognitionStatus = result.Error.Message;
                    return;
                }

                var matches = RecognitionMatchSelector.RankAndDedupe(result.Value!);
                if (matches.Count == 0)
                {
                    RecognitionStatus = "No matches found.";
                    RecognitionEmptyMessage = "No anime match found for this screenshot.";
                    return;
                }

                for (var index = 0; index < matches.Count; index++)
                {
                    var match = matches[index];
                    var mediaId = $"anilist:{match.AniListId}";
                    var preview = await GetRecognitionTitlePreviewAsync(mediaId, match);
                    var ep = match.Episode is not null ? $"EP {match.Episode}" : string.Empty;
                    var pct = $"{match.Similarity * 100:F1}%";
                    var viewModel = new RecognitionResultViewModel(
                        MediaId: mediaId,
                        Title: preview.Title,
                        EpisodeLabel: ep,
                        SimilarityLabel: pct,
                        ThumbnailUri: preview.PosterUri,
                        OpenCommand: new AsyncRelayCommand(() => OpenDetailAsync(mediaId)));

                    RecognitionResults.Add(viewModel);

                    if (index == 0)
                    {
                        CurrentRecognitionResult = viewModel;
                    }
                    else
                    {
                        RecognitionVariantResults.Add(viewModel);
                    }
                }

                OnPropertyChanged(nameof(HasRecognitionVariantResults));
                await SaveBestRecognitionAttemptAsync(matches[0], sourceName, sourcePath);
                RecognitionStatus = $"{matches.Count} match{(matches.Count == 1 ? string.Empty : "es")} · powered by trace.moe";
            }
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    private async Task SaveBestRecognitionAttemptAsync(
        RecognitionMatch match,
        string sourceName,
        string? sourcePath)
    {
        var mediaId = $"anilist:{match.AniListId}";
        var preview = await GetRecognitionTitlePreviewAsync(mediaId, match);
        var attempt = new RecognitionAttempt
        {
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? "Selected image" : sourceName,
            SourcePath = sourcePath,
            AniListId = match.AniListId,
            RomajiTitle = preview.Title,
            EnglishTitle = preview.Title,
            NativeTitle = match.NativeTitle,
            Episode = match.Episode,
            Similarity = match.Similarity,
            ThumbnailUri = preview.PosterUri
        };

        var saved = await recognitionInboxService.SaveAsync(attempt);
        if (saved.IsSuccess)
        {
            RecognitionInboxItems.Insert(0, CreateRecognitionInboxItemViewModel(saved.Value!));
            OnPropertyChanged(nameof(HasRecognitionInboxItems));
            NotifyRecognitionHistoryVisibilityChanged();
        }
    }

    private async Task<(string Title, Uri? PosterUri)> GetRecognitionTitlePreviewAsync(
        string mediaId,
        RecognitionMatch match)
    {
        var media = await mediaCatalogService.GetByIdAsync(mediaId);
        if (media.IsSuccess)
        {
            return (media.Value!.Title.DisplayTitle, media.Value.Images.PosterUri);
        }

        return (match.EnglishTitle ?? match.RomajiTitle, null);
    }

    private async Task LoadRecognitionInboxAsync()
    {
        var result = await recognitionInboxService.GetRecentAsync();
        if (result.IsFailure)
        {
            RecognitionStatus = result.Error.Message;
            return;
        }

        RecognitionInboxItems.Clear();
        foreach (var attempt in result.Value!)
        {
            RecognitionInboxItems.Add(CreateRecognitionInboxItemViewModel(attempt));
        }

        OnPropertyChanged(nameof(HasRecognitionInboxItems));
        NotifyRecognitionHistoryVisibilityChanged();
    }

    private RecognitionInboxItemViewModel CreateRecognitionInboxItemViewModel(RecognitionAttempt attempt)
    {
        var mediaId = attempt.MediaId ?? string.Empty;
        return new RecognitionInboxItemViewModel(
            attempt.Id,
            mediaId,
            attempt.EnglishTitle ?? attempt.RomajiTitle ?? attempt.NativeTitle ?? "Unknown title",
            string.IsNullOrWhiteSpace(attempt.SourceName) ? "Selected image" : attempt.SourceName,
            attempt.CreatedAt.ToLocalTime().ToString("MMM d, HH:mm"),
            attempt.Provider,
            attempt.Episode is not null ? $"EP {attempt.Episode}" : string.Empty,
            attempt.Similarity is not null ? $"{attempt.Similarity.Value * 100:F1}%" : "n/a",
            attempt.ThumbnailUri,
            new AsyncRelayCommand(() => OpenDetailAsync(mediaId)),
            new AsyncRelayCommand(() => AddToLibraryAsync(mediaId)),
            new AsyncRelayCommand(() => RemoveRecognitionAttemptAsync(attempt.Id)));
    }

    private async Task RemoveRecognitionAttemptAsync(string attemptId)
    {
        var result = await recognitionInboxService.RemoveAsync(attemptId);
        if (result.IsFailure)
        {
            RecognitionStatus = result.Error.Message;
            return;
        }

        var item = RecognitionInboxItems.FirstOrDefault(item => item.AttemptId == attemptId);
        if (item is not null)
        {
            RecognitionInboxItems.Remove(item);
            OnPropertyChanged(nameof(HasRecognitionInboxItems));
            NotifyRecognitionHistoryVisibilityChanged();
        }
    }

    private void NotifyRecognitionHistoryVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsRecognitionHistoryVisible));
        OnPropertyChanged(nameof(IsRecognitionHistoryEmptyVisible));
    }

    private void SetVisiblePage(string pageKey)
    {
        IsHomeVisible = pageKey == "home";
        IsDiscoverVisible = pageKey == "discover";
        IsLibraryVisible = pageKey == "library";
        IsScheduleVisible = pageKey == "schedule";
        IsSettingsVisible = pageKey == "settings";
        IsRecognizeVisible = pageKey == "recognize";
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

    private static IReadOnlyList<string> ParseOrganizationValues(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
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
        var filtered = entries;

        if (SelectedLibraryType != "All")
        {
            filtered = filtered.Where(entry =>
                entry.Type.Equals(SelectedLibraryType, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedLibraryStatus != "All")
        {
            filtered = filtered.Where(e =>
                e.Status.Equals(SelectedLibraryStatus, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(LibrarySearchText))
        {
            filtered = filtered.Where(entry =>
                entry.Title.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase)
                || entry.MediaId.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase)
                || entry.Tags.Any(tag => tag.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase))
                || entry.CustomLists.Any(list => list.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase)));
        }

        if (SelectedLibraryTag != "All tags")
        {
            filtered = filtered.Where(entry => entry.Tags.Any(tag =>
                tag.Equals(SelectedLibraryTag, StringComparison.OrdinalIgnoreCase)));
        }

        if (SelectedLibraryCustomList != "All lists")
        {
            filtered = filtered.Where(entry => entry.CustomLists.Any(list =>
                list.Equals(SelectedLibraryCustomList, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered;
    }

    private void RefreshLibraryOrganizationFilters(IReadOnlyList<LibraryEntryViewModel> entries)
    {
        suppressLibraryFilterRefresh = true;
        try
        {
            RefreshFilterOptions(
                LibraryTagFilters,
                "All tags",
                entries.SelectMany(entry => entry.Tags),
                SelectedLibraryTag,
                selected => SelectedLibraryTag = selected);

            RefreshFilterOptions(
                LibraryCustomListFilters,
                "All lists",
                entries.SelectMany(entry => entry.CustomLists),
                SelectedLibraryCustomList,
                selected => SelectedLibraryCustomList = selected);
        }
        finally
        {
            suppressLibraryFilterRefresh = false;
        }
    }

    private static void RefreshFilterOptions(
        ObservableCollection<string> target,
        string allLabel,
        IEnumerable<string> values,
        string selectedValue,
        Action<string> setSelectedValue)
    {
        var nextValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var expected = new string[nextValues.Length + 1];
        expected[0] = allLabel;
        nextValues.CopyTo(expected, 1);

        if (!target.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
        {
            target.Clear();
            foreach (var v in expected)
                target.Add(v);
        }

        if (!target.Contains(selectedValue, StringComparer.OrdinalIgnoreCase))
        {
            setSelectedValue(allLabel);
        }
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

    private static string FormatRatio(double value)
    {
        return $"{value * 100:F0}%";
    }

    private static string FormatMinutes(int minutes)
    {
        if (minutes <= 0)
        {
            return "0h";
        }

        var hours = minutes / 60;
        var remainingMinutes = minutes % 60;
        return remainingMinutes == 0
            ? $"{hours}h"
            : $"{hours}h {remainingMinutes}m";
    }

    private static Uri? CreateLocalFileUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return null;
        }

        return new Uri(path);
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
