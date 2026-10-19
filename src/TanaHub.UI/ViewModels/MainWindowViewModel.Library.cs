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
    private const int MaxCatalogConcurrency = 6;
    private readonly SemaphoreSlim libraryMutationGate = new(1, 1);
    private List<LibraryEntryViewModel> allLibraryEntries;

    public ObservableCollection<LibraryEntryViewModel> RecentLibraryEntries { get; } = [];
    public bool HasRecentLibraryEntries => RecentLibraryEntries.Count > 0;
    private bool suppressLibraryFilterRefresh;
    private int libraryLoadVersion;
    internal Task LibraryRefresh { get; private set; } = Task.CompletedTask;

    private void RefreshLibraryAfterChange(int context) => LibraryRefresh = RefreshLibraryAfterChangeAsync(context);

    private async Task RefreshLibraryAfterChangeAsync(int context)
    {
        try { await LoadLibraryAsync(); }
        catch (Exception)
        {
            ShowFeedback("Changes saved. The library could not refresh; try refreshing it again.", context, isError: true);
        }
    }

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
    private void SelectLibraryType(string type)
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
        var mediaById = new Dictionary<string, MediaItem>(StringComparer.OrdinalIgnoreCase);
        var entries = result.Value!.Items.ToArray();

        var mediaResults = await BoundedAsyncLoader.RunAsync(
            entries,
            MaxCatalogConcurrency,
            (entry, ct) => mediaCatalogService.GetByIdAsync(entry.MediaId, ct));

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var mediaResult = mediaResults[i];
            if (mediaResult.IsSuccess)
                mediaById[entry.MediaId] = mediaResult.Value!;

            var vm = CreateLibraryEntryViewModel(entry, mediaResult.IsSuccess ? mediaResult.Value : null);
            hydrated.Add(vm);
        }

        if (loadVersion != libraryLoadVersion) return;

        var existingById = allLibraryEntries.ToDictionary(item => item.MediaId, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < hydrated.Count; i++)
        {
            if (!existingById.TryGetValue(hydrated[i].MediaId, out var existing)) continue;
            existing.UpdateFrom(hydrated[i]);
            hydrated[i] = existing;
        }
        allLibraryEntries = hydrated;
        SynchronizeLibraryCollection(RecentLibraryEntries, hydrated.Take(6));
        OnPropertyChanged(nameof(HasRecentLibraryEntries));
        SynchronizeLibraryCollection(ContinueItems, hydrated.Where(item => item.Status is "Current" or "Paused"));

        RefreshDashboardMetrics(LibraryInsightsCalculator.Calculate(entries, mediaById));
        OnPropertyChanged(nameof(HasContinueItems));
        ApplyLibraryFilters();
        await RefreshRecommendationsAsync(entries, mediaById, loadVersion);
    }

    private void ApplyLibraryFilters()
    {
        RefreshLibraryOrganizationFilters(allLibraryEntries);

        SynchronizeLibraryCollection(LibraryEntries, SortLibraryEntries(FilterLibraryEntries(allLibraryEntries)));

        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(LibraryEmptyMessage));
    }

    private static void SynchronizeLibraryCollection(
        ObservableCollection<LibraryEntryViewModel> target, IEnumerable<LibraryEntryViewModel> items)
    {
        var desired = items.ToArray();
        for (var i = 0; i < desired.Length; i++)
        {
            if (i < target.Count && ReferenceEquals(target[i], desired[i])) continue;
            var oldIndex = target.IndexOf(desired[i]);
            if (oldIndex >= 0) target.Move(oldIndex, i);
            else target.Insert(i, desired[i]);
        }
        while (target.Count > desired.Length) target.RemoveAt(target.Count - 1);
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
            new AsyncRelayCommand(() => IncreaseScoreAsync(entry.MediaId)),
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

        var genreProfile = LibraryRecommendationBuilder.BuildGenreProfile(entries, mediaById, maxGenres: 3);
        if (genreProfile.Count == 0)
        {
            RecommendedItems.Clear();
            RecommendationSummary = "Add scored or in-progress titles to get recommendations.";
            OnPropertyChanged(nameof(HasRecommendedItems));
            return;
        }

        var results = await BoundedAsyncLoader.RunAsync(genreProfile, 3,
            (genre, ct) => mediaCatalogService.SearchAsync(new MediaSearchQuery
            {
                Genres = [genre.Name],
                Sort = MediaSearchSort.Score,
                PageSize = 8
            }, ct));
        var candidatePool = results.Where(result => result.IsSuccess)
            .SelectMany(result => result.Value!.Items);

        if (loadVersion != libraryLoadVersion) return;

        var recommendations = LibraryRecommendationBuilder.RankCandidates(
            entries,
            genreProfile,
            candidatePool
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()),
            maxItems: 8);

        RecommendedItems.Clear();
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

    // Imports and every UI write share this queue, including read-modify-write actions.
    private async Task<T> SerializeLibraryImportAsync<T>(Func<Task<T>> importAsync)
    {
        await libraryMutationGate.WaitAsync();
        try { return await importAsync(); }
        finally { libraryMutationGate.Release(); }
    }

    private async Task SerializeLibraryMutationAsync(Func<int, Task> mutationAsync)
    {
        var context = detailLoadVersion;
        await libraryMutationGate.WaitAsync();
        try { await mutationAsync(context); }
        finally { libraryMutationGate.Release(); }
    }

    private async Task AddToLibraryAsync(string mediaId)
    {
        var context = detailLoadVersion;
        await libraryMutationGate.WaitAsync();
        try
        {
            var existing = await userLibraryService.GetEntryAsync(mediaId);
            if (existing.IsSuccess)
            {
                ShowFeedback("This title is already in your library.", context);
                return;
            }
            if (existing.Error.Code != "not_found")
            {
                ShowFeedback(existing.Error.Message, context, isError: true);
                return;
            }
            var media = await mediaCatalogService.GetByIdAsync(mediaId);
            if (media.IsFailure) { ShowFeedback(media.Error.Message, context, isError: true); return; }

            var status = media.Value!.Type == MediaType.Anime
                ? MediaListStatus.Current : MediaListStatus.Planning;

            var result = await userLibraryService.UpsertEntryAsync(
                new UserMediaEntry(media.Value.Id, media.Value.Type, status)
                {
                    PosterUri = media.Value.Images.PosterUri
                });

            ShowFeedback(result.IsSuccess
                ? $"Added {media.Value.Title.DisplayTitle} to local library."
                : result.Error.Message, context, isError: result.IsFailure);
            if (result.IsSuccess)
            {
                await RefreshDetailIfOpenAsync(mediaId);
                RefreshLibraryAfterChange(context);
            }
        }
        finally { libraryMutationGate.Release(); }
    }

    private Task IncrementProgressAsync(string mediaId) => SerializeLibraryMutationAsync(async context =>
    {
        var result = await userLibraryService.IncrementProgressAsync(mediaId);
        ShowFeedback(result.IsSuccess
            ? "Progress updated."
            : result.Error.Message, context, isError: result.IsFailure);

        if (result.IsSuccess)
        {
            await RefreshDetailIfOpenAsync(mediaId);
            RefreshLibraryAfterChange(context);
        }
    });

    private Task SetNumericInputAsync(string? input, Func<int, Task> saveAsync)
    {
        if (int.TryParse(input, out var value) && value >= 0) return saveAsync(value);
        ShowFeedback("Enter a whole number of zero or more.", detailLoadVersion, isError: true);
        return Task.CompletedTask;
    }

    private Task SetProgressAsync(string mediaId, int progress) => SerializeLibraryMutationAsync(async context =>
    {
        var entryResult = await userLibraryService.GetEntryAsync(mediaId);
        if (entryResult.IsFailure) { ShowFeedback(entryResult.Error.Message, context, isError: true); return; }

        var updated = entryResult.Value! with
        {
            Progress = Math.Max(0, progress),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var result = await userLibraryService.UpsertEntryAsync(updated);
        ShowFeedback(result.IsSuccess ? $"Progress set to {progress}." : result.Error.Message, context, isError: result.IsFailure);

        if (result.IsSuccess)
        {
            await RefreshDetailIfOpenAsync(mediaId);
            RefreshLibraryAfterChange(context);
        }
    });

    private Task UpdateLibraryStatusAsync(string mediaId, MediaListStatus status) => SerializeLibraryMutationAsync(async context =>
    {
        var result = await userLibraryService.UpdateStatusAsync(mediaId, status);
        ShowFeedback(result.IsSuccess
            ? $"Status changed to {status}."
            : result.Error.Message, context, isError: result.IsFailure);

        if (result.IsSuccess)
        {
            await RefreshDetailIfOpenAsync(mediaId);
            RefreshLibraryAfterChange(context);
        }
    });

    private Task IncreaseScoreAsync(string mediaId) => SerializeLibraryMutationAsync(async context =>
    {
        var entry = await userLibraryService.GetEntryAsync(mediaId);
        if (entry.IsFailure) { ShowFeedback(entry.Error.Message, context, isError: true); return; }
        var currentScore = entry.Value!.Score;
        var nextScore = currentScore is null or >= 10 ? 1 : currentScore.Value + 1;
        var result = await userLibraryService.UpdateScoreAsync(mediaId, nextScore);
        ShowFeedback(result.IsSuccess
            ? "Score updated."
            : result.Error.Message, context, isError: result.IsFailure);

        if (result.IsSuccess)
        {
            await RefreshDetailIfOpenAsync(mediaId);
            RefreshLibraryAfterChange(context);
        }
    });

    private Task SetScoreDirectAsync(string mediaId, int score) => SerializeLibraryMutationAsync(async context =>
    {
        var result = await userLibraryService.UpdateScoreAsync(mediaId, score);
        ShowFeedback(result.IsSuccess
            ? $"Score set to {score}."
            : result.Error.Message, context, isError: result.IsFailure);

        if (result.IsSuccess)
        {
            await RefreshDetailIfOpenAsync(mediaId);
            RefreshLibraryAfterChange(context);
        }
    });

    private Task SaveNotesAsync(string mediaId, string? notes) => SerializeLibraryMutationAsync(async context =>
    {
        var result = await userLibraryService.UpdateNotesAsync(mediaId, notes);
        ShowFeedback(result.IsSuccess ? "Notes saved." : result.Error.Message, context, isError: result.IsFailure);
    });

    private Task SaveReviewAsync(string mediaId, string? review) => SerializeLibraryMutationAsync(async context =>
    {
        var result = await userLibraryService.UpdateReviewAsync(mediaId, review);
        ShowFeedback(result.IsSuccess ? "Review saved." : result.Error.Message, context, isError: result.IsFailure);

        if (result.IsSuccess)
        {
            await RefreshDetailIfOpenAsync(mediaId);
            RefreshLibraryAfterChange(context);
        }
    });

    private Task SaveTagsAsync(string mediaId, string? tagsText) => SerializeLibraryMutationAsync(async context =>
    {
        var entry = await GetLibraryEntryAsync(mediaId);
        if (entry is null) { ShowFeedback("Add this title to your library before editing tags.", context); return; }

        var result = await userLibraryService.UpdateOrganizationAsync(
            mediaId, ParseOrganizationValues(tagsText), entry.CustomLists);
        ShowFeedback(result.IsSuccess ? "Tags saved." : result.Error.Message, context, isError: result.IsFailure);

        if (result.IsSuccess)
        {
            await RefreshDetailIfOpenAsync(mediaId);
            RefreshLibraryAfterChange(context);
        }
    });

    private Task SaveCustomListsAsync(string mediaId, string? customListsText) => SerializeLibraryMutationAsync(async context =>
    {
        var entry = await GetLibraryEntryAsync(mediaId);
        if (entry is null) { ShowFeedback("Add this title to your library before editing custom lists.", context); return; }

        var result = await userLibraryService.UpdateOrganizationAsync(
            mediaId, entry.Tags, ParseOrganizationValues(customListsText));
        ShowFeedback(result.IsSuccess ? "Custom lists saved." : result.Error.Message, context, isError: result.IsFailure);

        if (result.IsSuccess)
        {
            await RefreshDetailIfOpenAsync(mediaId);
            RefreshLibraryAfterChange(context);
        }
    });

    private Task RemoveFromLibraryAsync(string mediaId) => SerializeLibraryMutationAsync(async context =>
    {
        var result = await userLibraryService.RemoveEntryAsync(mediaId);
        ShowFeedback(result.IsSuccess
            ? "Title removed from your library."
            : result.Error.Message, context, isError: result.IsFailure);

        if (result.IsSuccess)
        {
            await RefreshDetailIfOpenAsync(mediaId);
            RefreshLibraryAfterChange(context);
        }
    });

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

    private static string FormatMinutes(long minutes)
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
