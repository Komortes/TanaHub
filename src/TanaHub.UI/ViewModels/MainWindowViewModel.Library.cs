using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using TanaHub.Application.Insights;
using TanaHub.Application.Queries;
using TanaHub.Application.Recommendations;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using Anime = TanaHub.Domain.Models.Anime;
using Manga = TanaHub.Domain.Models.Manga;

namespace TanaHub.UI.ViewModels;

public sealed partial class MainWindowViewModel
{
    private List<LibraryEntryViewModel> allLibraryEntries;
    private bool suppressLibraryFilterRefresh;
    private int libraryLoadVersion;

    public IReadOnlyList<string> LibraryTypeOptions { get; } = ["All", "Anime", "Manga"];
    public IReadOnlyList<string> LibrarySortOptions { get; } = ["Updated", "Title", "Score", "Progress"];
    public ObservableCollection<string> LibraryTagFilters { get; } = ["All tags"];
    public ObservableCollection<string> LibraryCustomListFilters { get; } = ["All lists"];

    public IReadOnlyList<string> LibraryStatusFilters { get; } =
    [
        "All", "Current", "Completed", "Planning", "Paused", "Dropped"
    ];

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

    public bool IsLibraryAllSelected => SelectedLibraryType == "All";
    public bool IsLibraryAnimeSelected => SelectedLibraryType == "Anime";
    public bool IsLibraryMangaSelected => SelectedLibraryType == "Manga";
    public bool IsLibraryListVisible => SelectedLibraryViewMode == "List";
    public bool IsLibraryGridVisible => SelectedLibraryViewMode == "Grid";
    public bool IsLibraryEmpty => LibraryEntries.Count == 0;
    public bool HasContinueItems => ContinueItems.Count > 0;

    public string LibraryEmptyMessage =>
        SelectedLibraryType == "All"
        && SelectedLibraryStatus == "All"
        && SelectedLibraryTag == "All tags"
        && SelectedLibraryCustomList == "All lists"
        && string.IsNullOrWhiteSpace(LibrarySearchText)
            ? "Your library is empty"
            : "No entries match your current filter";

    partial void OnSelectedLibraryStatusChanged(string value) => ApplyLibraryFilters();

    partial void OnSelectedLibraryTagChanged(string value)
    {
        if (!suppressLibraryFilterRefresh) ApplyLibraryFilters();
    }

    partial void OnSelectedLibraryCustomListChanged(string value)
    {
        if (!suppressLibraryFilterRefresh) ApplyLibraryFilters();
    }

    partial void OnSelectedLibraryTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLibraryAllSelected));
        OnPropertyChanged(nameof(IsLibraryAnimeSelected));
        OnPropertyChanged(nameof(IsLibraryMangaSelected));
        ApplyLibraryFilters();
    }

    partial void OnSelectedLibrarySortChanged(string value) => ApplyLibraryFilters();

    partial void OnSelectedLibraryViewModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLibraryListVisible));
        OnPropertyChanged(nameof(IsLibraryGridVisible));
    }

    [RelayCommand]
    private async Task RefreshLibraryAsync() => await LoadLibraryAsync();

    [RelayCommand]
    private void SearchLibrary() => ApplyLibraryFilters();

    [RelayCommand]
    private async Task SelectLibraryTypeAsync(string type)
    {
        SelectedLibraryType = string.IsNullOrWhiteSpace(type) ? "All" : type;
    }

    [RelayCommand]
    private void SelectLibrarySort(string sort)
    {
        SelectedLibrarySort = string.IsNullOrWhiteSpace(sort) ? "Updated" : sort;
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
    }

    [RelayCommand]
    private void NavigateToLibrary() => SelectNavigationItem("library");

    private async Task LoadLibraryAsync()
    {
        var loadVersion = ++libraryLoadVersion;
        var result = await userLibraryService.GetEntriesAsync(new UserLibraryQuery
        {
            PageSize = int.MaxValue
        });

        if (result.IsFailure || loadVersion != libraryLoadVersion) return;

        var hydrated = new List<LibraryEntryViewModel>();
        var continueItems = new List<LibraryEntryViewModel>();
        var mediaById = new Dictionary<string, MediaItem>(StringComparer.OrdinalIgnoreCase);
        var entries = result.Value!.Items.ToArray();

        var mediaTasks = entries
            .Select(entry => mediaCatalogService.GetByIdAsync(entry.MediaId))
            .ToArray();
        var mediaResults = await Task.WhenAll(mediaTasks);

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var mediaResult = mediaResults[i];
            if (mediaResult.IsSuccess)
                mediaById[entry.MediaId] = mediaResult.Value!;

            var vm = CreateLibraryEntryViewModel(entry, mediaResult.IsSuccess ? mediaResult.Value : null);
            hydrated.Add(vm);
            if (entry.Status is MediaListStatus.Current or MediaListStatus.Paused)
                continueItems.Add(vm);
        }

        if (loadVersion != libraryLoadVersion) return;

        allLibraryEntries = hydrated;
        ContinueItems.Clear();
        foreach (var item in continueItems)
            ContinueItems.Add(item);

        RefreshDashboardMetrics(LibraryInsightsCalculator.Calculate(entries, mediaById));
        await RefreshRecommendationsAsync(entries, mediaById, loadVersion);
        if (loadVersion != libraryLoadVersion) return;
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
        DashboardMetrics.Add(new("Completion", FormatRatio(insights.CompletionRate),
            $"{insights.CompletedEntries}/{insights.TotalEntries} titles completed",
            MaterialIconKind.CheckCircleOutline));
        DashboardMetrics.Add(new("Backlog", insights.BacklogEntries.ToString(),
            "planned titles waiting", MaterialIconKind.BookmarkPlusOutline));
        DashboardMetrics.Add(new("Dropped", FormatRatio(insights.DroppedRatio),
            $"{insights.DroppedEntries} title(s) dropped", MaterialIconKind.CloseCircleOutline));
        DashboardMetrics.Add(new("Avg score",
            insights.AverageScore is null ? "n/a" : insights.AverageScore.Value.ToString("F1"),
            "scored local entries", MaterialIconKind.StarOutline));
        DashboardMetrics.Add(new("Watch time", FormatMinutes(insights.EstimatedWatchMinutes),
            $"{insights.EpisodesWatched} episode(s) tracked", MaterialIconKind.ClockOutline));
        DashboardMetrics.Add(new("Chapters", insights.ChaptersRead.ToString(),
            "manga progress tracked", MaterialIconKind.BookOpenPageVariant));

        DashboardCharts.Clear();
        DashboardCharts.Add(DashboardChartViewModel.FromSegments(
            "Library mix",
            insights.TotalEntries == 0 ? "No titles tracked yet" : $"{FormatTitleCount(insights.TotalEntries)} by type",
            insights.ByMediaType, insights.TotalEntries, MaterialIconKind.ChartDonut));
        DashboardCharts.Add(DashboardChartViewModel.FromSegments(
            "Status breakdown",
            insights.TotalEntries == 0 ? "No titles tracked yet" : $"{FormatTitleCount(insights.TotalEntries)} by list status",
            insights.ByStatus, insights.TotalEntries, MaterialIconKind.ChartBar));
    }

    private async Task<LibraryEntryViewModel> CreateLibraryEntryViewModelAsync(UserMediaEntry entry)
    {
        var media = await mediaCatalogService.GetByIdAsync(entry.MediaId);
        return CreateLibraryEntryViewModel(entry, media.IsSuccess ? media.Value : null);
    }

    private LibraryEntryViewModel CreateLibraryEntryViewModel(UserMediaEntry entry, MediaItem? media)
    {
        var title = media is not null ? media.Title.DisplayTitle : entry.MediaId;
        var posterUri = entry.PosterUri ?? media?.Images.PosterUri;
        var total = media switch
        {
            Anime anime when anime.EpisodeCount is not null => anime.EpisodeCount.ToString(),
            Manga manga when manga.ChapterCount is not null => manga.ChapterCount.ToString(),
            _ => "?"
        };

        return new LibraryEntryViewModel(
            entry.MediaId, title, entry.MediaType.ToString(), entry.Status.ToString(),
            ProgressDisplayFormatter.Format(entry.Progress, total),
            entry.Score?.ToString() ?? "-", posterUri,
            new AsyncRelayCommand(() => IncrementProgressAsync(entry.MediaId)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(entry.MediaId, MediaListStatus.Current)),
            new AsyncRelayCommand(() => UpdateLibraryStatusAsync(entry.MediaId, MediaListStatus.Completed)),
            new AsyncRelayCommand(() => IncreaseScoreAsync(entry.MediaId, entry.Score)),
            new AsyncRelayCommand(() => RemoveFromLibraryAsync(entry.MediaId)),
            new AsyncRelayCommand(() => OpenDetailAsync(entry.MediaId)),
            entry.Notes, entry.Tags, entry.CustomLists);
    }

    private async Task RefreshRecommendationsAsync(
        IReadOnlyList<UserMediaEntry> entries,
        IReadOnlyDictionary<string, MediaItem> mediaById,
        int loadVersion)
    {
        if (loadVersion != libraryLoadVersion) return;

        RecommendedItems.Clear();
        var genreProfile = LibraryRecommendationBuilder.BuildGenreProfile(entries, mediaById, maxGenres: 3);
        if (genreProfile.Count == 0)
        {
            RecommendationSummary = "Add scored or in-progress titles to get recommendations.";
            OnPropertyChanged(nameof(HasRecommendedItems));
            return;
        }

        var candidatePool = new List<MediaItem>();
        foreach (var genre in genreProfile)
        {
            var result = await mediaCatalogService.SearchAsync(new MediaSearchQuery
            {
                Genres = [genre.Name],
                Sort = MediaSearchSort.Score,
                PageSize = 8
            });

            if (result.IsSuccess)
            {
                candidatePool.AddRange(result.Value!.Items);
            }
        }

        if (loadVersion != libraryLoadVersion) return;

        var recommendations = LibraryRecommendationBuilder.RankCandidates(
            entries,
            genreProfile,
            candidatePool
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()),
            maxItems: 8);

        foreach (var item in recommendations)
        {
            RecommendedItems.Add(MediaSearchResultViewModel.FromMediaItem(item, AddToLibraryAsync, OpenDetailAsync));
        }

        RecommendationSummary = RecommendedItems.Count == 0
            ? $"No new matches for {FormatRecommendationGenres(genreProfile)} yet."
            : $"Based on {FormatRecommendationGenres(genreProfile)}.";
        OnPropertyChanged(nameof(HasRecommendedItems));
    }

    private static string FormatRecommendationGenres(IReadOnlyList<LibraryRecommendationGenre> genres)
    {
        return string.Join(", ", genres.Take(3).Select(genre => genre.Name));
    }

    private async Task AddToLibraryAsync(string mediaId)
    {
        var media = await mediaCatalogService.GetByIdAsync(mediaId);
        if (media.IsFailure) { SearchStatus = media.Error.Message; return; }

        var status = media.Value!.Type == MediaType.Anime
            ? MediaListStatus.Current : MediaListStatus.Planning;

        var result = await userLibraryService.UpsertEntryAsync(
            new UserMediaEntry(media.Value.Id, media.Value.Type, status)
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
        SearchStatus = result.IsSuccess ? $"Progress set to {progress}." : result.Error.Message;

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

    private async Task SetScoreDirectAsync(string mediaId, int score)
    {
        var result = await userLibraryService.UpdateScoreAsync(mediaId, score);
        SearchStatus = result.IsSuccess
            ? $"Score set to {score} for {mediaId}."
            : result.Error.Message;

        await LoadLibraryAsync();
        await RefreshDetailIfOpenAsync(mediaId);
    }

    private async Task SaveNotesAsync(string mediaId, string? notes)
    {
        var result = await userLibraryService.UpdateNotesAsync(mediaId, notes);
        if (result.IsFailure) SearchStatus = result.Error.Message;
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
        if (entry is null) { SearchStatus = "Add this title to your library before editing tags."; return; }

        var result = await userLibraryService.UpdateOrganizationAsync(
            mediaId, ParseOrganizationValues(tagsText), entry.CustomLists);
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
        if (entry is null) { SearchStatus = "Add this title to your library before editing custom lists."; return; }

        var result = await userLibraryService.UpdateOrganizationAsync(
            mediaId, entry.Tags, ParseOrganizationValues(customListsText));
        SearchStatus = result.IsSuccess ? "Custom lists saved." : result.Error.Message;

        if (result.IsSuccess)
        {
            await LoadLibraryAsync();
            await RefreshDetailIfOpenAsync(mediaId);
        }
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

    private IEnumerable<LibraryEntryViewModel> FilterLibraryEntries(IEnumerable<LibraryEntryViewModel> entries)
    {
        var filtered = entries;

        if (SelectedLibraryType != "All")
            filtered = filtered.Where(e => e.Type.Equals(SelectedLibraryType, StringComparison.OrdinalIgnoreCase));

        if (SelectedLibraryStatus != "All")
            filtered = filtered.Where(e => e.Status.Equals(SelectedLibraryStatus, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(LibrarySearchText))
            filtered = filtered.Where(e =>
                e.Title.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase)
                || e.MediaId.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase)
                || e.Tags.Any(tag => tag.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase))
                || e.CustomLists.Any(list => list.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase)));

        if (SelectedLibraryTag != "All tags")
            filtered = filtered.Where(e =>
                e.Tags.Any(tag => tag.Equals(SelectedLibraryTag, StringComparison.OrdinalIgnoreCase)));

        if (SelectedLibraryCustomList != "All lists")
            filtered = filtered.Where(e =>
                e.CustomLists.Any(list => list.Equals(SelectedLibraryCustomList, StringComparison.OrdinalIgnoreCase)));

        return filtered;
    }

    private IEnumerable<LibraryEntryViewModel> SortLibraryEntries(IEnumerable<LibraryEntryViewModel> entries)
    {
        return SelectedLibrarySort switch
        {
            "Title" => entries.OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase),
            "Score" => entries.OrderByDescending(e => ParseLibraryScore(e.Score) ?? -1),
            "Progress" => entries.OrderByDescending(e => ParseProgressValue(e.Progress)),
            _ => entries
        };
    }

    private void RefreshLibraryOrganizationFilters(IReadOnlyList<LibraryEntryViewModel> entries)
    {
        suppressLibraryFilterRefresh = true;
        try
        {
            RefreshFilterOptions(LibraryTagFilters, "All tags",
                entries.SelectMany(e => e.Tags), SelectedLibraryTag,
                selected => SelectedLibraryTag = selected);

            RefreshFilterOptions(LibraryCustomListFilters, "All lists",
                entries.SelectMany(e => e.CustomLists), SelectedLibraryCustomList,
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
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var expected = new string[nextValues.Length + 1];
        expected[0] = allLabel;
        nextValues.CopyTo(expected, 1);

        if (!target.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
        {
            target.Clear();
            foreach (var v in expected) target.Add(v);
        }

        if (!target.Contains(selectedValue, StringComparer.OrdinalIgnoreCase))
            setSelectedValue(allLabel);
    }

    private static string FormatTitleCount(int count) =>
        count == 1 ? "1 title" : $"{count} titles";

    private static string FormatRatio(double value) => $"{value * 100:F0}%";

    private static string FormatMinutes(int minutes)
    {
        if (minutes <= 0) return "0h";
        var hours = minutes / 60;
        var rem = minutes % 60;
        return rem == 0 ? $"{hours}h" : $"{hours}h {rem}m";
    }

    private static int? ParseLibraryScore(string? value) =>
        int.TryParse(value, out var score) ? score : null;

    private static int ParseProgressValue(string progress)
    {
        var sep = progress.IndexOf('/', StringComparison.Ordinal);
        var val = sep >= 0 ? progress[..sep] : progress;
        return int.TryParse(val, out var parsed) ? parsed : 0;
    }

    private static IReadOnlyList<string> ParseOrganizationValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
