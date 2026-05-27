using TanaHub.Domain.Enums;

namespace TanaHub.Application.Queries;

public sealed record MediaSearchQuery
{
    public string SearchText { get; init; } = string.Empty;

    public MediaType? Type { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];

    public int? SeasonYear { get; init; }

    public MediaFormat? Format { get; init; }

    public MediaReleaseStatus? ReleaseStatus { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
