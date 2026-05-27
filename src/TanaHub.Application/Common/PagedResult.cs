namespace TanaHub.Application.Common;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int? TotalCount = null)
{
    public bool HasItems => Items.Count > 0;
}
