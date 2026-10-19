using CommunityToolkit.Mvvm.Input;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed class RelatedTitleViewModel
{
    public RelatedTitleViewModel(RelatedTitle title, IReadOnlyList<UserMediaEntry> library, Func<string, Task> open)
    {
        var entry = library.FirstOrDefault(e => e.MediaType == title.MediaType && e.MediaId == title.MediaId)
            ?? library.FirstOrDefault(e => MatchesLibraryEntry(title, e));
        MediaId = entry?.MediaId ?? title.MediaId;
        Title = title.Title;
        PosterUri = title.PosterUri;
        Kind = title.Relation;
        Year = title.Year;
        RelationLabel = GetRelationLabel(title.Relation);
        var format = title.Format switch
        {
            MediaFormat.Tv => "TV",
            MediaFormat.TvShort => "TV short",
            MediaFormat.Ova => "OVA",
            MediaFormat.Ona => "ONA",
            MediaFormat.OneShot => "One-shot",
            MediaFormat.Novel => "Light novel",
            MediaFormat.Unknown => title.MediaType.ToString(),
            _ => title.Format.ToString()
        };
        var count = title.MediaType == MediaType.Anime ? title.EpisodeCount : title.ChapterCount;
        var unit = title.MediaType == MediaType.Anime ? "ep." : "ch.";
        Metadata = string.Join(" · ", new[]
        {
            format,
            title.Year?.ToString(),
            count is > 0 ? $"{count} {unit}" : null
        }.Where(value => value is not null));
        LibraryLabel = entry is null ? string.Empty : $"{StatusLabel(entry)} · {entry.Progress} {unit}";
        SourceLabel = title.Source;
        AccessibleName = $"Open {Title}, {RelationLabel}, {Metadata}";
        OpenCommand = new AsyncRelayCommand(() => open(MediaId));
    }

    public string MediaId { get; }
    public string Title { get; }
    public Uri? PosterUri { get; }
    public MediaRelationKind Kind { get; }
    public int? Year { get; }
    public string RelationLabel { get; }
    public string Metadata { get; }
    public string LibraryLabel { get; }
    public string SourceLabel { get; }
    public string AccessibleName { get; }
    public IAsyncRelayCommand OpenCommand { get; }

    private static bool MatchesLibraryEntry(RelatedTitle title, UserMediaEntry entry) =>
        entry.MediaType == title.MediaType &&
        ((!IsSeparateEdition(title) && title.AniListId is > 0 && entry.MediaId == $"anilist:{title.AniListId}") ||
         (!IsSeparateEdition(title) && title.MalId is > 0 && entry.MediaId == $"mal:{title.MediaType.ToString().ToLowerInvariant()}:{title.MalId}") ||
         (!string.IsNullOrWhiteSpace(title.MangaDexId) && entry.MediaId == $"mangadex:{title.MangaDexId}"));

    private static bool IsSeparateEdition(RelatedTitle title) => title.MangaDexId is not null &&
        title.Relation is MediaRelationKind.Colored or MediaRelationKind.Monochrome or
            MediaRelationKind.Alternative or MediaRelationKind.Serialization or MediaRelationKind.PreSerialization;

    private static string StatusLabel(UserMediaEntry entry) => entry.Status switch
    {
        MediaListStatus.Current => entry.MediaType == MediaType.Anime ? "Watching" : "Reading",
        MediaListStatus.Repeating => entry.MediaType == MediaType.Anime ? "Rewatching" : "Rereading",
        _ => entry.Status.ToString()
    };

    private static string GetRelationLabel(MediaRelationKind kind) => kind switch
    {
        MediaRelationKind.Prequel => "Previous part",
        MediaRelationKind.Sequel => "Next part",
        MediaRelationKind.Source => "Source material",
        MediaRelationKind.Adaptation => "Adaptation",
        MediaRelationKind.Parent => "Main story",
        MediaRelationKind.SideStory => "Side story",
        MediaRelationKind.SpinOff => "Spin-off",
        MediaRelationKind.Alternative => "Alternative version",
        MediaRelationKind.Summary => "Recap",
        MediaRelationKind.Compilation => "Compilation",
        MediaRelationKind.Contains => "Contains",
        MediaRelationKind.SameUniverse => "Same universe",
        MediaRelationKind.Character => "Shared characters",
        MediaRelationKind.Colored => "Colour edition",
        MediaRelationKind.Monochrome => "Monochrome edition",
        MediaRelationKind.Doujinshi => "Fan work",
        MediaRelationKind.Serialization => "Serialization",
        MediaRelationKind.PreSerialization => "Pilot version",
        _ => "Related title"
    };
}
