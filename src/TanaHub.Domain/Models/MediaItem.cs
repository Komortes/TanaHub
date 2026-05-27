using TanaHub.Domain.Enums;

namespace TanaHub.Domain.Models;

public abstract record MediaItem
{
    protected MediaItem(
        string id,
        MediaType type,
        MediaTitle title,
        MediaFormat format,
        MediaReleaseStatus releaseStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
        Type = type;
        Title = title;
        Format = format;
        ReleaseStatus = releaseStatus;
    }

    public string Id { get; }

    public MediaType Type { get; }

    public MediaTitle Title { get; }

    public MediaFormat Format { get; init; }

    public MediaReleaseStatus ReleaseStatus { get; init; }

    public int? StartYear { get; init; }

    public int? AverageScore { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];

    public MediaImages Images { get; init; } = new();
}
