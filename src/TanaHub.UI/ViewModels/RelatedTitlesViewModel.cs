using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed partial class RelatedTitlesViewModel(Func<Task> retry) : ObservableObject
{
    public IAsyncRelayCommand RetryCommand { get; } = new AsyncRelayCommand(retry);

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool canRetry;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<RelatedTitleGroupViewModel> groups = [];

    public void BeginLoading()
    {
        IsLoading = true;
        StatusMessage = "Loading related titles…";
    }

    public void ShowError(string message)
    {
        IsLoading = false;
        CanRetry = true;
        StatusMessage = message;
    }

    public void Apply(RelatedTitlesResult result, IReadOnlyList<UserMediaEntry> library, Func<string, Task> open)
    {
        var titles = result.Titles.Select(t => new RelatedTitleViewModel(t, library, open)).ToArray();
        Groups = titles.GroupBy(t => GroupIndex(t.Kind)).OrderBy(g => g.Key)
            .Select(g => new RelatedTitleGroupViewModel(GroupName(g.Key),
                g.OrderBy(t => t.Kind == MediaRelationKind.Prequel ? 0 : t.Kind == MediaRelationKind.Sequel ? 1 : 2)
                    .ThenBy(t => t.Year ?? int.MaxValue).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToArray(),
                g.Key < 3)).ToArray();
        IsLoading = false;
        CanRetry = result.IsStale || !string.IsNullOrWhiteSpace(result.Notice);
        StatusMessage = result.Notice ?? (titles.Length == 0
            ? "No related titles listed by this source."
            : $"{titles.Length} related titles · direct connections");
    }

    private static int GroupIndex(MediaRelationKind kind) => kind switch
    {
        MediaRelationKind.Prequel or MediaRelationKind.Sequel => 0,
        MediaRelationKind.Source or MediaRelationKind.Adaptation => 1,
        MediaRelationKind.Parent or MediaRelationKind.SideStory or MediaRelationKind.SpinOff => 2,
        MediaRelationKind.Doujinshi => 4,
        _ => 3
    };

    private static string GroupName(int index) => index switch
    {
        0 => "Previous & next parts",
        1 => "Source & adaptations",
        2 => "Side stories & spin-offs",
        4 => "Fan works",
        _ => "Other versions & connections"
    };
}

public sealed partial class RelatedTitleGroupViewModel : ObservableObject
{
    public RelatedTitleGroupViewModel(string title, IReadOnlyList<RelatedTitleViewModel> titles, bool isExpanded)
    {
        Title = title;
        Titles = titles;
        IsExpanded = isExpanded;
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public string Title { get; }
    public IReadOnlyList<RelatedTitleViewModel> Titles { get; }
    public IRelayCommand ToggleCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleLabel), nameof(ExpansionIcon))]
    private bool isExpanded;

    public string Header => $"{Title} · {Titles.Count}";
    public string ToggleLabel => $"{(IsExpanded ? "Collapse" : "Expand")} {Header}";
    public MaterialIconKind ExpansionIcon => IsExpanded ? MaterialIconKind.ChevronUp : MaterialIconKind.ChevronDown;
}
