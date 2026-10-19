using TanaHub.Domain.Models;

namespace TanaHub.Bridge;

public sealed record MediaDto(
    string Id,
    string Type,
    string Title,
    string Romaji,
    string? PosterUrl,
    string? BannerUrl,
    string Format,
    string ReleaseStatus,
    int? Year,
    int? AverageScore,
    int? TotalUnits,
    string? Description,
    IReadOnlyList<string> Genres)
{
    public string? NativeTitle { get; init; }
    public int? EpisodeCount { get; init; }
    public int? DurationMinutes { get; init; }
    public string? Studio { get; init; }
    public int? ChapterCount { get; init; }
    public int? VolumeCount { get; init; }
    public string? PreviewUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public IReadOnlyList<CharacterDto> Characters { get; init; } = [];

    internal static MediaDto From(MediaItem media, Uri? savedPoster = null) => new(
        media.Id,
        media.Type.ToString(),
        media.Title.DisplayTitle,
        media.Title.Romaji,
        (savedPoster ?? media.Images.PosterUri)?.ToString(),
        media.Images.BannerUri?.ToString(),
        media.Format.ToString(),
        media.ReleaseStatus.ToString(),
        media.StartYear,
        media.AverageScore,
        media switch { Anime anime => anime.EpisodeCount, Manga manga => manga.ChapterCount, _ => null },
        media.Description,
        media.Genres)
    {
        NativeTitle = media.Title.Native,
        PreviewUrl = savedPoster is null || savedPoster == media.Images.PosterUri ? media.Images.PreviewUri?.ToString() : null,
        ThumbnailUrl = savedPoster is null || savedPoster == media.Images.PosterUri ? media.Images.ThumbnailUri?.ToString() : null,
        EpisodeCount = (media as Anime)?.EpisodeCount,
        DurationMinutes = (media as Anime)?.DurationMinutes,
        Studio = (media as Anime)?.Studio,
        ChapterCount = (media as Manga)?.ChapterCount,
        VolumeCount = (media as Manga)?.VolumeCount,
        Characters = media.Characters.Select(character => new CharacterDto(character.Name, character.ImageUri?.ToString(), character.Role)).ToArray()
    };

    internal static MediaDto Missing(UserMediaEntry entry) => new(
        entry.MediaId, entry.MediaType.ToString(), entry.MediaId, entry.MediaId,
        entry.PosterUri?.ToString(), null, "Unknown", "Unknown", null, null, null, null, []);
}

public sealed record LibraryEntryDto(
    string MediaId,
    string MediaType,
    string Status,
    int Progress,
    int? Score,
    string? Notes,
    string? Review,
    DateTimeOffset UpdatedAt,
    MediaDto Media)
{
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> CustomLists { get; init; } = [];
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

public sealed record CharacterDto(string Name, string? ImageUrl, string Role);

public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int? TotalCount, bool HasNextPage);

public sealed record BridgeError(string Code, string Message);
