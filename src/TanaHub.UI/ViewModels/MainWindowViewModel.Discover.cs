using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<MediaSearchResultViewModel> SearchDropdownResults { get; } = [];
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

    [ObservableProperty]
    private string selectedDiscoverType = "All";

    [ObservableProperty]
    private string selectedDiscoverSort = "Popularity";

    [ObservableProperty]
    private string? selectedDiscoverGenre;

    [ObservableProperty]
    private bool isDiscoverSearchActive;

    [ObservableProperty]
    private bool hasSearchResults;

    [ObservableProperty]
    private bool hasMoreSearchResults;

    [ObservableProperty]
    private bool isLoadingMoreSearchResults;

    private int currentDiscoverPage = 1;
    private int? discoverYearFilter;
    private MediaFormat? discoverFormatFilter;
    private MediaReleaseStatus? discoverReleaseStatusFilter;
    private string? discoverCountryFilter;
    private bool suppressDiscoverFilterRefresh;

    public bool IsDiscoverBrowseActive => !IsDiscoverSearchActive;
    public bool IsDiscoverAllSelected => SelectedDiscoverType == "All";
    public bool IsDiscoverAnimeSelected => SelectedDiscoverType == "Anime";
    public bool IsDiscoverMangaSelected => SelectedDiscoverType == "Manga";
    public bool IsDiscoverPopularitySelected => SelectedDiscoverSort == "Popularity";
    public bool IsDiscoverScoreSelected => SelectedDiscoverSort == "Score";
    public bool IsDiscoverTrendingSelected => SelectedDiscoverSort == "Trending";
    public bool IsDiscoverNewSelected => SelectedDiscoverSort == "New";

    partial void OnIsDiscoverSearchActiveChanged(bool value) =>
        OnPropertyChanged(nameof(IsDiscoverBrowseActive));

    partial void OnSelectedDiscoverGenreChanged(string? value)
    {
        if (suppressDiscoverFilterRefresh) return;
        ResetDiscoverAdvancedFilters();
        _ = ApplyDiscoverFiltersAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsSearchDropdownOpen = false;
        SelectNavigationItem("discover");
        await LoadSearchResultsAsync(page: 1, append: false);
        IsDiscoverSearchActive = HasSearchResults || !string.IsNullOrWhiteSpace(SearchText);
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
        if (!HasMoreSearchResults || IsLoadingMoreSearchResults) return;

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

    private async Task ApplyDiscoverFiltersAsync()
    {
        await LoadSearchResultsAsync(page: 1, append: false);
        IsDiscoverSearchActive = true;
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

        if (!append) SearchResults.Clear();

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
                SearchResults.Add(MediaSearchResultViewModel.FromMediaItem(item, AddToLibraryAsync, OpenDetailAsync));
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
        if (result.IsFailure) return;

        foreach (var item in result.Value!.Items)
            target.Add(MediaSearchResultViewModel.FromMediaItem(item, AddToLibraryAsync, OpenDetailAsync));
    }

    private static MediaType? ParseMediaTypeFilter(string value) => value switch
    {
        "Anime" => MediaType.Anime,
        "Manga" => MediaType.Manga,
        _ => null
    };

    private static MediaSearchSort ParseDiscoverSort(string sort) => sort switch
    {
        "Score" => MediaSearchSort.Score,
        "Trending" => MediaSearchSort.Trending,
        "New" => MediaSearchSort.Newest,
        _ => MediaSearchSort.Popularity
    };

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
