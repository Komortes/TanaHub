using TanaHub.Domain.Enums;

namespace TanaHub.Domain.Models;

public sealed record Manga : MediaItem
{
    public Manga(
        string id,
        MediaTitle title,
        MediaFormat format,
        MediaReleaseStatus releaseStatus)
        : base(id, MediaType.Manga, title, format, releaseStatus)
    {
    }

    public int? ChapterCount { get; init; }

    public int? VolumeCount { get; init; }
}
