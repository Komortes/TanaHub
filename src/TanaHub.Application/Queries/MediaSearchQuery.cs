using TanaHub.Domain.Enums;

namespace TanaHub.Application.Queries;

public enum MediaSearchSort
{
    Popularity = 0,
    Score = 1,
    Trending = 2,
    Newest = 3
}

public sealed record MediaSearchQuery
{
    public string SearchText { get; init; } = string.Empty;

    public MediaType? Type { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];

    public int? SeasonYear { get; init; }

    public MediaFormat? Format { get; init; }

    public MediaReleaseStatus? ReleaseStatus { get; init; }

    public string? CountryCode { get; init; }

    public MediaSearchSort Sort { get; init; } = MediaSearchSort.Popularity;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
