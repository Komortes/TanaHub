using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TanaHub.Application.Queries;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed partial class MainWindowViewModel
{
    [ObservableProperty]
    private string selectedScheduleRange = "Today";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScheduleError))]
    private string scheduleError = string.Empty;

    public bool HasScheduleError => !string.IsNullOrWhiteSpace(ScheduleError);

    public bool HasScheduleItems => ScheduleItems.Count > 0;
    public bool IsScheduleTodaySelected => SelectedScheduleRange == "Today";
    public bool IsScheduleTomorrowSelected => SelectedScheduleRange == "Tomorrow";
    public bool IsScheduleThisWeekSelected => SelectedScheduleRange == "This week";
    public bool IsScheduleNextWeekSelected => SelectedScheduleRange == "Next week";
    public string ScheduleHeading => SelectedScheduleRange.ToUpperInvariant();

    public string ScheduleRangeDetail => SelectedScheduleRange switch
    {
        "Today" => "AniList airing · remaining today",
        "Tomorrow" => "AniList airing · tomorrow",
        "Next week" => "AniList airing · following 7 days",
        _ => "AniList airing · next 7 days"
    };

    partial void OnSelectedScheduleRangeChanged(string value)
    {
        OnPropertyChanged(nameof(IsScheduleTodaySelected));
        OnPropertyChanged(nameof(IsScheduleTomorrowSelected));
        OnPropertyChanged(nameof(IsScheduleThisWeekSelected));
        OnPropertyChanged(nameof(IsScheduleNextWeekSelected));
        OnPropertyChanged(nameof(ScheduleHeading));
        OnPropertyChanged(nameof(ScheduleRangeDetail));
    }

    [RelayCommand]
    private async Task SelectScheduleRangeAsync(string range)
    {
        SelectedScheduleRange = string.IsNullOrWhiteSpace(range) ? "Today" : range;
        await LoadScheduleAsync();
    }

    [RelayCommand]
    private async Task RefreshScheduleAsync() => await LoadScheduleAsync();

    [RelayCommand]
    private async Task NavigateToScheduleAsync()
    {
        SelectedScheduleRange = "Today";
        SelectNavigationItem("schedule");
        await LoadScheduleAsync();
    }

    private async Task LoadScheduleAsync()
    {
        ScheduleError = string.Empty;
        var (from, to) = GetScheduleWindow();
        var result = await airingScheduleService.GetUpcomingAsync(from, to, pageSize: 50);

        ScheduleItems.Clear();

        if (result.IsFailure)
        {
            ScheduleError = result.Error.Message;
            OnPropertyChanged(nameof(HasScheduleItems));
            return;
        }

        var libraryResult = await userLibraryService.GetEntriesAsync(new UserLibraryQuery
        {
            PageSize = int.MaxValue
        });
        var libraryIds = libraryResult.IsSuccess
            ? libraryResult.Value!.Items
                .Select(entry => entry.MediaId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

    private static string FormatScheduleDay(DateTimeOffset value)
    {
        var localDate = value.ToLocalTime().Date;
        var today = DateTimeOffset.Now.Date;

        if (localDate == today) return "Today";
        if (localDate == today.AddDays(1)) return "Tomorrow";
        return localDate.ToString("ddd, MMM d");
    }
}
