using System.Text.RegularExpressions;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed class MediaDetailViewModel
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
        IAsyncRelayCommand removeCommand,
        string? notes,
        IReadOnlyList<CharacterInfo> characters,
        IAsyncRelayCommand<string?> saveNotesCommand)
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
        RemoveCommand = removeCommand;
        Notes = notes;
        Characters = characters;
        SaveNotesCommand = saveNotesCommand;
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

    public MaterialIconKind TypeIcon => IsAnime
        ? MaterialIconKind.Television
        : MaterialIconKind.BookOpenPageVariant;

    public int UserScoreInt => int.TryParse(LibraryScore, out var v) ? v : 0;

    // Score-dot active state — each button binds to its own bool
    public bool IsScore1  => UserScoreInt == 1;
    public bool IsScore2  => UserScoreInt == 2;
    public bool IsScore3  => UserScoreInt == 3;
    public bool IsScore4  => UserScoreInt == 4;
    public bool IsScore5  => UserScoreInt == 5;
    public bool IsScore6  => UserScoreInt == 6;
    public bool IsScore7  => UserScoreInt == 7;
    public bool IsScore8  => UserScoreInt == 8;
    public bool IsScore9  => UserScoreInt == 9;
    public bool IsScore10 => UserScoreInt == 10;

    // Status-pill active state
    public bool IsStatusCurrent   => LibraryStatus == "Current";
    public bool IsStatusPlanning  => LibraryStatus == "Planning";
    public bool IsStatusPaused    => LibraryStatus == "Paused";
    public bool IsStatusCompleted => LibraryStatus == "Completed";
    public bool IsStatusDropped   => LibraryStatus == "Dropped";

    public IBrush StatusForeground => LibraryStatus switch
    {
        "Current"   => new SolidColorBrush(Color.Parse("#4DD0E1")),
        "Completed" => new SolidColorBrush(Color.Parse("#A3E635")),
        "Planning"  => new SolidColorBrush(Color.Parse("#FBBF24")),
        "Paused"    => new SolidColorBrush(Color.Parse("#FB923C")),
        "Dropped"   => new SolidColorBrush(Color.Parse("#F87171")),
        _           => new SolidColorBrush(Color.Parse("#A79ABB"))
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
    public IAsyncRelayCommand RemoveCommand { get; }
    public string? Notes { get; }
    public IReadOnlyList<CharacterInfo> Characters { get; }
    public bool HasCharacters => Characters.Count > 0;
    public IAsyncRelayCommand<string?> SaveNotesCommand { get; }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var withNewlines = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        var stripped = Regex.Replace(withNewlines, "<[^>]+>", "");
        var collapsed = Regex.Replace(stripped, @"\n{3,}", "\n\n");
        return collapsed.Trim();
    }
}
