using TanaHub.Domain.Enums;

namespace TanaHub.Domain.Models;

public sealed record Anime : MediaItem
{
    public Anime(
        string id,
        MediaTitle title,
        MediaFormat format,
        MediaReleaseStatus releaseStatus)
        : base(id, MediaType.Anime, title, format, releaseStatus)
    {
    }

    public int? EpisodeCount { get; init; }

    public int? DurationMinutes { get; init; }

    public string? Studio { get; init; }
}
