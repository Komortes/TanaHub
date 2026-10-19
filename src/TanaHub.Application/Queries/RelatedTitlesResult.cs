using TanaHub.Domain.Models;

namespace TanaHub.Application.Queries;

public sealed record RelatedTitlesResult(
    IReadOnlyList<RelatedTitle> Titles,
    string? Notice = null,
    bool IsStale = false);
