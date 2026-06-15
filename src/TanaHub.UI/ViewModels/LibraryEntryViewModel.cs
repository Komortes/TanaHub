using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;

namespace TanaHub.UI.ViewModels;

public sealed record LibraryEntryViewModel(
    string MediaId,
    string Title,
    string Type,
    string Status,
    string Progress,
    string Score,
    Uri? PosterUri,
    IAsyncRelayCommand IncrementCommand,
    IAsyncRelayCommand MarkCurrentCommand,
    IAsyncRelayCommand MarkCompletedCommand,
    IAsyncRelayCommand IncreaseScoreCommand,
    IAsyncRelayCommand RemoveCommand,
    IAsyncRelayCommand OpenDetailCommand,
    string? Notes = null)
{
    public IBrush StatusForeground => Status switch
    {
        "Current"   => new SolidColorBrush(Color.Parse("#4DD0E1")),
        "Completed" => new SolidColorBrush(Color.Parse("#A3E635")),
        "Planning"  => new SolidColorBrush(Color.Parse("#FBBF24")),
        "Paused"    => new SolidColorBrush(Color.Parse("#FB923C")),
        "Dropped"   => new SolidColorBrush(Color.Parse("#F87171")),
        _           => new SolidColorBrush(Color.Parse("#A79ABB"))
    };

    public IBrush ScoreForeground => Score != "-"
        ? new SolidColorBrush(Color.Parse("#FBBF24"))
        : new SolidColorBrush(Color.Parse("#6E6282"));
}
