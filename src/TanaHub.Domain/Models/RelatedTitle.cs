using TanaHub.Domain.Enums;

namespace TanaHub.Domain.Models;

/// <summary>A direct, directed relation from the requested title to a catalog entry.</summary>
public sealed record RelatedTitle(
    string MediaId,
    string Title,
    MediaType MediaType,
    MediaFormat Format,
    MediaRelationKind Relation,
    string Source,
    string SourceRelation)
{
    public int? AniListId { get; init; }
    public int? MalId { get; init; }
    public string? MangaDexId { get; init; }
    public int? Year { get; init; }
    public int? EpisodeCount { get; init; }
    public int? ChapterCount { get; init; }
    public int? VolumeCount { get; init; }
    public Uri? PosterUri { get; init; }
}
