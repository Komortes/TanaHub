using CommunityToolkit.Mvvm.Input;

namespace TanaHub.UI.ViewModels;

public sealed record ScheduleItemViewModel(
    string MediaId,
    string Day,
    string Time,
    string Title,
    string Detail,
    string Episode,
    Uri? PosterUri,
    bool IsInLibrary,
    IAsyncRelayCommand OpenDetailCommand)
{
    public string LibraryMatchLabel => IsInLibrary ? "In library" : "Airing";
}
