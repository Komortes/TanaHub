using TanaHub.Domain.Enums;

namespace TanaHub.Application.Queries;

public sealed record UserLibraryQuery
{
    public MediaType? Type { get; init; }

    public MediaListStatus? Status { get; init; }

    public string SearchText { get; init; } = string.Empty;

    public string? Tag { get; init; }

    public string? CustomList { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}
