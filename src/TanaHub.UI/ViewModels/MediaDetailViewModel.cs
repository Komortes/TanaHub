using System.Text.RegularExpressions;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed partial class MediaDetailViewModel
{
    public MediaDetailViewModel(
        string id,
        string title,
        string nativeTitle,
        string romajiTitle,
        string mediaType,
        string format,
        string releaseStatus,
        string year,
        string score,
        string description,
        IReadOnlyList<string> genres,
        Uri? posterUri,
        Uri? bannerUri,
        string episodes,
        string duration,
        string studio,
        string chapters,
        string volumes,
        string libraryStatus,
        string libraryProgress,
        string libraryScore,
        bool isInLibrary,
        IAsyncRelayCommand addToLibraryCommand,
        IAsyncRelayCommand incrementProgressCommand,
        IAsyncRelayCommand markCurrentCommand,
        IAsyncRelayCommand markPlanningCommand,
        IAsyncRelayCommand markPausedCommand,
        IAsyncRelayCommand markCompletedCommand,
        IAsyncRelayCommand markDroppedCommand,
        IAsyncRelayCommand increaseScoreCommand,
        IAsyncRelayCommand<string?> setScoreCommand,
        IAsyncRelayCommand<string?> setProgressCommand,
        IAsyncRelayCommand setProgressMaxCommand,
        IAsyncRelayCommand setProgressZeroCommand,
        IAsyncRelayCommand removeCommand,
        string? notes,
        string? review,
        string tagsText,
        string customListsText,
        IAsyncRelayCommand<string?> saveTagsCommand,
        IAsyncRelayCommand<string?> saveCustomListsCommand,
        IReadOnlyList<CharacterInfo> characters,
        IAsyncRelayCommand<string?> saveNotesCommand,
        IAsyncRelayCommand<string?> saveReviewCommand)
    {
        Id = id;
        Title = title;
        NativeTitle = nativeTitle;
        RomajiTitle = romajiTitle;
        MediaType = mediaType;
        Format = format;
        ReleaseStatus = releaseStatus;
        Year = year;
        Score = score;
        Description = StripHtml(description);
        Genres = genres;
        PosterUri = posterUri;
        BannerUri = bannerUri;
        Episodes = episodes;
        Duration = duration;
        Studio = studio;
        Chapters = chapters;
        Volumes = volumes;
        LibraryStatus = libraryStatus;
        LibraryProgress = libraryProgress;
        LibraryScore = libraryScore;
        IsInLibrary = isInLibrary;
        AddToLibraryCommand = addToLibraryCommand;
        IncrementProgressCommand = incrementProgressCommand;
        MarkCurrentCommand = markCurrentCommand;
        MarkPlanningCommand = markPlanningCommand;
        MarkPausedCommand = markPausedCommand;
        MarkCompletedCommand = markCompletedCommand;
        MarkDroppedCommand = markDroppedCommand;
        IncreaseScoreCommand = increaseScoreCommand;
        SetScoreCommand = setScoreCommand;
        SetProgressCommand = setProgressCommand;
        SetProgressMaxCommand = setProgressMaxCommand;
        SetProgressZeroCommand = setProgressZeroCommand;
        RemoveCommand = removeCommand;
        Notes = notes;
        Review = review;
        TagsText = tagsText;
        CustomListsText = customListsText;
        SaveTagsCommand = saveTagsCommand;
        SaveCustomListsCommand = saveCustomListsCommand;
        Characters = characters;
        SaveNotesCommand = saveNotesCommand;
        SaveReviewCommand = saveReviewCommand;
    }

    public string Id { get; }
    public string Title { get; }
    public string NativeTitle { get; }
    public string RomajiTitle { get; }
    public string MediaType { get; }
    public string Format { get; }
    public string ReleaseStatus { get; }
    public string Year { get; }
    public string Score { get; }
    public string Description { get; }
    public IReadOnlyList<string> Genres { get; }
    public Uri? PosterUri { get; }
    public Uri? BannerUri { get; }
    public string Episodes { get; }
    public string Duration { get; }
    public string Studio { get; }
    public string Chapters { get; }
    public string Volumes { get; }
    public string LibraryStatus { get; }
    public string LibraryProgress { get; }
    public string LibraryScore { get; }
    public bool IsInLibrary { get; }

    public bool IsAnime => MediaType == "Anime";
    public bool HasBanner => BannerUri is not null;
    public bool HasRomajiTitle => !string.IsNullOrWhiteSpace(RomajiTitle);
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasStudio => IsAnime && !string.IsNullOrWhiteSpace(Studio) && Studio != "Unknown";
    public string IncrementLabel => IsAnime ? "+1 Episode" : "+1 Chapter";
    public string ProgressLabel => IsAnime ? "Episodes" : "Chapters";
    public string ProgressValue => IsAnime ? Episodes : Chapters;
    public string LibraryCurrentProgress =>
        LibraryProgress.Contains('/') ? LibraryProgress.Split('/')[0].Trim() : LibraryProgress;
    public bool CanSetToMax => int.TryParse(ProgressValue, out _);

    public MaterialIconKind TypeIcon => IsAnime
        ? MaterialIconKind.Television
        : MaterialIconKind.BookOpenPageVariant;

    public int UserScoreInt => int.TryParse(LibraryScore, out var v) ? v : 0;

    // Score-bar fill state — bar N is active when score >= N
    public bool IsScoreAtLeast1 => UserScoreInt >= 1;
    public bool IsScoreAtLeast2 => UserScoreInt >= 2;
    public bool IsScoreAtLeast3 => UserScoreInt >= 3;
    public bool IsScoreAtLeast4 => UserScoreInt >= 4;
    public bool IsScoreAtLeast5 => UserScoreInt >= 5;
    public bool IsScoreAtLeast6 => UserScoreInt >= 6;
    public bool IsScoreAtLeast7 => UserScoreInt >= 7;
    public bool IsScoreAtLeast8 => UserScoreInt >= 8;
    public bool IsScoreAtLeast9 => UserScoreInt >= 9;
    public bool IsScoreAtLeast10 => UserScoreInt >= 10;
    public string UserScoreDisplay => UserScoreInt == 0 ? "—" : UserScoreInt.ToString();

    // Status-pill active state
    public bool IsStatusCurrent => LibraryStatus == "Current";
    public bool IsStatusPlanning => LibraryStatus == "Planning";
    public bool IsStatusPaused => LibraryStatus == "Paused";
    public bool IsStatusCompleted => LibraryStatus == "Completed";
    public bool IsStatusDropped => LibraryStatus == "Dropped";
    public bool IsStatusRepeating => LibraryStatus == "Repeating";

    private static readonly IBrush BrushCurrent = new SolidColorBrush(Color.Parse("#4DD0E1"));
    private static readonly IBrush BrushCompleted = new SolidColorBrush(Color.Parse("#A3E635"));
    private static readonly IBrush BrushPlanning = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush BrushPaused = new SolidColorBrush(Color.Parse("#FB923C"));
    private static readonly IBrush BrushDropped = new SolidColorBrush(Color.Parse("#F87171"));
    private static readonly IBrush BrushDefault = new SolidColorBrush(Color.Parse("#A79ABB"));

    public IBrush StatusForeground => LibraryStatus switch
    {
        "Current" or "Repeating" => BrushCurrent,
        "Completed" => BrushCompleted,
        "Planning" => BrushPlanning,
        "Paused" => BrushPaused,
        "Dropped" => BrushDropped,
        _ => BrushDefault
    };

    public IAsyncRelayCommand AddToLibraryCommand { get; }
    public IAsyncRelayCommand IncrementProgressCommand { get; }
    public IAsyncRelayCommand MarkCurrentCommand { get; }
    public IAsyncRelayCommand MarkPlanningCommand { get; }
    public IAsyncRelayCommand MarkPausedCommand { get; }
    public IAsyncRelayCommand MarkCompletedCommand { get; }
    public IAsyncRelayCommand MarkDroppedCommand { get; }
    public IAsyncRelayCommand IncreaseScoreCommand { get; }
    public IAsyncRelayCommand<string?> SetScoreCommand { get; }
    public IAsyncRelayCommand<string?> SetProgressCommand { get; }
    public IAsyncRelayCommand SetProgressMaxCommand { get; }
    public IAsyncRelayCommand SetProgressZeroCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }
    public string? Notes { get; }
    public string? Review { get; }
    public string TagsText { get; }
    public string CustomListsText { get; }
    public IAsyncRelayCommand<string?> SaveTagsCommand { get; }
    public IAsyncRelayCommand<string?> SaveCustomListsCommand { get; }
    public IReadOnlyList<CharacterInfo> Characters { get; }
    public bool HasCharacters => Characters.Count > 0;
    public IAsyncRelayCommand<string?> SaveNotesCommand { get; }
    public IAsyncRelayCommand<string?> SaveReviewCommand { get; }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiNewlineRegex();

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var withNewlines = BrTagRegex().Replace(html, "\n");
        var stripped = HtmlTagRegex().Replace(withNewlines, "");
        return MultiNewlineRegex().Replace(stripped, "\n\n").Trim();
    }
}
