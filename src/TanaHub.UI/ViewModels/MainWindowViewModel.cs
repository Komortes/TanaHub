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
    private readonly IAppUpdateService appUpdateService;
    private readonly HashSet<string> notifiedThisSession = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings appSettings = new();
    private CancellationTokenSource? searchDebounce;
    private string previousPageKey = "home";

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
        IFileOpenService fileOpenService,
        IAppUpdateService appUpdateService)
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
        this.appUpdateService = appUpdateService;

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
        RecommendedItems = [];
        DashboardMetrics = [];
        DashboardCharts = [];
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

    public ObservableCollection<MediaSearchResultViewModel> RecommendedItems { get; }

    public ObservableCollection<DashboardMetricViewModel> DashboardMetrics { get; }

    public ObservableCollection<DashboardChartViewModel> DashboardCharts { get; }

    public ObservableCollection<LibraryEntryViewModel> ContinueItems { get; }

    public ObservableCollection<LibraryEntryViewModel> LibraryEntries { get; }

    public ObservableCollection<ScheduleItemViewModel> ScheduleItems { get; }

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

    [ObservableProperty]
    private string recommendationSummary = "Personal picks appear after you add titles to your library.";

    public bool HasRecommendedItems => RecommendedItems.Count > 0;

    [ObservableProperty]
    private string searchStatus = "Search uses AniList with local fallback.";

    [ObservableProperty]
    private string appConnectionStatusDetail = "Loading cache status...";

    [ObservableProperty]
    private string appConnectionStatusKind = "Cached";

    [ObservableProperty]
    private bool isDetailVisible;

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
    private MediaDetailViewModel? selectedMedia;

    public bool IsConnectionStatusOnline => AppConnectionStatusKind == "Online";
    public bool IsConnectionStatusCached => AppConnectionStatusKind == "Cached";
    public bool IsConnectionStatusOffline => AppConnectionStatusKind == "Offline";

    partial void OnAppConnectionStatusKindChanged(string value)
    {
        OnPropertyChanged(nameof(IsConnectionStatusOnline));
        OnPropertyChanged(nameof(IsConnectionStatusCached));
        OnPropertyChanged(nameof(IsConnectionStatusOffline));
    }

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

    [RelayCommand]
    private void CloseSearchDropdown()
    {
        IsSearchDropdownOpen = false;
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
            : ProgressDisplayFormatter.Format(libraryEntry.Progress, item is Anime ? episodes : chapters);
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

    private async Task LoadPageDataAsync()
    {
        await LoadSettingsAsync();
        _ = RefreshUpdateStatusAsync(isManualCheck: false);
        await Task.WhenAll(
            LoadDiscoverBrowseAsync(),
            LoadSearchResultsAsync(),
            LoadLibraryAsync(),
            LoadScheduleAsync(),
            LoadRecognitionInboxAsync());
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

    private void SetAppConnectionStatus(string kind, string detail)
    {
        AppConnectionStatusKind = kind switch
        {
            "Online" => "Online",
            "Offline" => "Offline",
            _ => "Cached"
        };
        AppConnectionStatusDetail = detail;
    }
}
