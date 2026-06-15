using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Catalog;

internal sealed class OfflineCatalogCache
{
    private readonly string cachePath;
    private readonly ConcurrentDictionary<string, MediaItem> items = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim flushLock = new(1, 1);
    private volatile bool dirty;

    public OfflineCatalogCache(string cachePath) => this.cachePath = cachePath;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(cachePath)) return;
        try
        {
            await using var stream = File.OpenRead(cachePath);
            var dtos = await JsonSerializer.DeserializeAsync<List<CachedItemDto>>(stream, cancellationToken: ct);
            if (dtos is null) return;
            foreach (var dto in dtos)
            {
                var item = dto.ToMediaItem();
                if (item is not null) items[item.Id] = item;
            }
        }
        catch { /* corrupt cache — start fresh */ }
    }

    public bool TryGet(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MediaItem? item)
        => items.TryGetValue(id, out item);

    public void Put(MediaItem item)
    {
        items[item.Id] = item;
        dirty = true;
    }

    public void PutRange(IEnumerable<MediaItem> newItems)
    {
        foreach (var item in newItems) items[item.Id] = item;
        dirty = true;
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (!dirty) return;
        await flushLock.WaitAsync(ct);
        try
        {
            if (!dirty) return;
            var dtos = items.Values.Select(CachedItemDto.From).ToList();
            var dir = Path.GetDirectoryName(cachePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var tmp = cachePath + ".tmp";
            await using (var stream = File.Create(tmp))
                await JsonSerializer.SerializeAsync(stream, dtos, cancellationToken: ct);
            File.Move(tmp, cachePath, overwrite: true);
            dirty = false;
        }
        finally
        {
            flushLock.Release();
        }
    }

    private sealed class CachedItemDto
    {
        [JsonPropertyName("id")]           public string Id { get; set; } = string.Empty;
        [JsonPropertyName("mediaType")]    public string MediaType { get; set; } = string.Empty;
        [JsonPropertyName("romaji")]       public string? Romaji { get; set; }
        [JsonPropertyName("english")]      public string? English { get; set; }
        [JsonPropertyName("native")]       public string? Native { get; set; }
        [JsonPropertyName("format")]       public string? Format { get; set; }
        [JsonPropertyName("status")]       public string? Status { get; set; }
        [JsonPropertyName("startYear")]    public int? StartYear { get; set; }
        [JsonPropertyName("avgScore")]     public int? AverageScore { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("genres")]       public List<string>? Genres { get; set; }
        [JsonPropertyName("poster")]       public string? Poster { get; set; }
        [JsonPropertyName("banner")]       public string? Banner { get; set; }
        [JsonPropertyName("thumb")]        public string? Thumbnail { get; set; }
        // Anime
        [JsonPropertyName("episodes")]     public int? Episodes { get; set; }
        [JsonPropertyName("duration")]     public int? Duration { get; set; }
        [JsonPropertyName("studio")]       public string? Studio { get; set; }
        // Manga
        [JsonPropertyName("chapters")]     public int? Chapters { get; set; }
        [JsonPropertyName("volumes")]      public int? Volumes { get; set; }

        public static CachedItemDto From(MediaItem item)
        {
            var dto = new CachedItemDto
            {
                Id           = item.Id,
                MediaType    = item.Type.ToString(),
                Romaji       = item.Title.Romaji,
                English      = item.Title.English,
                Native       = item.Title.Native,
                Format       = item.Format.ToString(),
                Status       = item.ReleaseStatus.ToString(),
                StartYear    = item.StartYear,
                AverageScore = item.AverageScore,
                Description  = item.Description,
                Genres       = item.Genres.Count > 0 ? [.. item.Genres] : null,
                Poster       = item.Images.PosterUri?.ToString(),
                Banner       = item.Images.BannerUri?.ToString(),
                Thumbnail    = item.Images.ThumbnailUri?.ToString(),
            };
            if (item is Anime anime)
            {
                dto.Episodes = anime.EpisodeCount;
                dto.Duration = anime.DurationMinutes;
                dto.Studio   = anime.Studio;
            }
            else if (item is Manga manga)
            {
                dto.Chapters = manga.ChapterCount;
                dto.Volumes  = manga.VolumeCount;
            }
            return dto;
        }

        public MediaItem? ToMediaItem()
        {
            if (string.IsNullOrWhiteSpace(Id)) return null;
            var title  = new MediaTitle(Romaji ?? $"#{Id}", English, Native);
            var format = Enum.TryParse<MediaFormat>(Format, out var f) ? f : MediaFormat.Unknown;
            var status = Enum.TryParse<MediaReleaseStatus>(Status, out var s) ? s : MediaReleaseStatus.Unknown;
            var images = new MediaImages(ToUri(Poster), ToUri(Banner), ToUri(Thumbnail));
            IReadOnlyList<string> genres = Genres ?? [];

            if (MediaType == nameof(TanaHub.Domain.Enums.MediaType.Anime))
            {
                return new Anime(Id, title, format, status)
                {
                    StartYear = StartYear, AverageScore = AverageScore,
                    Description = Description, Genres = genres, Images = images,
                    EpisodeCount = Episodes, DurationMinutes = Duration, Studio = Studio,
                };
            }
            if (MediaType == nameof(TanaHub.Domain.Enums.MediaType.Manga))
            {
                return new Manga(Id, title, format, status)
                {
                    StartYear = StartYear, AverageScore = AverageScore,
                    Description = Description, Genres = genres, Images = images,
                    ChapterCount = Chapters, VolumeCount = Volumes,
                };
            }
            return null;
        }

        private static Uri? ToUri(string? value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }
}
