using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TanaHub.UI.ViewModels;

public sealed partial class LibraryEntryViewModel : ObservableObject
{
    public LibraryEntryViewModel(
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
        string? Notes = null,
        IReadOnlyList<string>? TagValues = null,
        IReadOnlyList<string>? CustomListValues = null)
    {
        this.MediaId = MediaId;
        title = Title;
        type = Type;
        status = Status;
        progress = Progress;
        score = Score;
        posterUri = PosterUri;
        notes = Notes;
        tags = TagValues ?? [];
        customLists = CustomListValues ?? [];
        this.IncrementCommand = IncrementCommand;
        this.MarkCurrentCommand = MarkCurrentCommand;
        this.MarkCompletedCommand = MarkCompletedCommand;
        this.IncreaseScoreCommand = IncreaseScoreCommand;
        this.RemoveCommand = RemoveCommand;
        this.OpenDetailCommand = OpenDetailCommand;
    }

    public string MediaId { get; }
    public IAsyncRelayCommand IncrementCommand { get; }
    public IAsyncRelayCommand MarkCurrentCommand { get; }
    public IAsyncRelayCommand MarkCompletedCommand { get; }
    public IAsyncRelayCommand IncreaseScoreCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }
    public IAsyncRelayCommand OpenDetailCommand { get; }

    [ObservableProperty] private string title;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnime), nameof(IncrementLabel), nameof(ProgressLabel), nameof(StatusLabel))]
    private string type;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompleted), nameof(StatusLabel), nameof(StatusForeground))]
    private string status;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressLabel))]
    private string progress;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScoreForeground))]
    private string score;
    [ObservableProperty] private Uri? posterUri;
    [ObservableProperty] private string? notes;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagsLabel))]
    private IReadOnlyList<string> tags;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomListsLabel))]
    private IReadOnlyList<string> customLists;

    internal void UpdateFrom(LibraryEntryViewModel updated)
    {
        Title = updated.Title;
        Type = updated.Type;
        Status = updated.Status;
        Progress = updated.Progress;
        Score = updated.Score;
        PosterUri = updated.PosterUri;
        Notes = updated.Notes;
        if (!Tags.SequenceEqual(updated.Tags)) Tags = updated.Tags;
        if (!CustomLists.SequenceEqual(updated.CustomLists)) CustomLists = updated.CustomLists;
    }

    public bool IsCompleted => Status == "Completed";
    public bool IsAnime => Type == "Anime";
    public string IncrementLabel => IsAnime ? "+1 Episode" : "+1 Chapter";
    public string ProgressLabel => $"{Progress} {(IsAnime ? "ep." : "ch.")}";
    public string StatusLabel => Status switch
    {
        "Current" => IsAnime ? "Watching" : "Reading",
        "Repeating" => IsAnime ? "Rewatching" : "Rereading",
        _ => Status
    };
    public string TagsLabel => Tags.Count == 0 ? string.Empty : string.Join(" · ", Tags);

    public string CustomListsLabel => CustomLists.Count == 0 ? string.Empty : string.Join(" · ", CustomLists);

    public IBrush StatusForeground => Status switch
    {
        "Current" => new SolidColorBrush(Color.Parse("#4DD0E1")),
        "Completed" => new SolidColorBrush(Color.Parse("#A3E635")),
        "Planning" => new SolidColorBrush(Color.Parse("#FBBF24")),
        "Paused" => new SolidColorBrush(Color.Parse("#FB923C")),
        "Dropped" => new SolidColorBrush(Color.Parse("#F87171")),
        _ => new SolidColorBrush(Color.Parse("#A79ABB"))
    };

    public IBrush ScoreForeground => Score != "-"
        ? new SolidColorBrush(Color.Parse("#FBBF24"))
        : new SolidColorBrush(Color.Parse("#6E6282"));
}
