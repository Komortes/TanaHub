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
    private long changeVersion;
    private long persistedVersion;

    public OfflineCatalogCache(string cachePath) => this.cachePath = cachePath;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(cachePath)) return;
        try
        {
            using var stream = File.OpenRead(cachePath);
            var dtos = await JsonSerializer.DeserializeAsync<List<CachedItemDto>>(stream, cancellationToken: ct)
                .ConfigureAwait(false);
            if (dtos is null) return;
            foreach (var dto in dtos)
            {
                var item = dto.ToMediaItem();
                if (item is not null) items.TryAdd(item.Id, item);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An unavailable or corrupt cache must not prevent startup.
        }
    }

    public bool TryGet(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MediaItem? item)
        => items.TryGetValue(id, out item);

    public void Put(MediaItem item)
    {
        items[item.Id] = item;
        Interlocked.Increment(ref changeVersion);
    }

    public void PutRange(IEnumerable<MediaItem> newItems)
    {
        foreach (var item in newItems) Put(item);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref changeVersion) == Volatile.Read(ref persistedVersion)) return;
        await flushLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var snapshotVersion = Volatile.Read(ref changeVersion);
            if (snapshotVersion == Volatile.Read(ref persistedVersion)) return;
            var dtos = items.Values.Select(CachedItemDto.From).ToList();
            var dir = Path.GetDirectoryName(cachePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var tmp = cachePath + ".tmp";
            await using (var stream = File.Create(tmp))
                await JsonSerializer.SerializeAsync(stream, dtos, cancellationToken: ct).ConfigureAwait(false);
            File.Move(tmp, cachePath, overwrite: true);
            // Writes arriving after this snapshot still need a subsequent flush.
            Volatile.Write(ref persistedVersion, snapshotVersion);
        }
        finally
        {
            flushLock.Release();
        }
    }

    private sealed class CachedItemDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("mediaType")] public string MediaType { get; set; } = string.Empty;
        [JsonPropertyName("romaji")] public string? Romaji { get; set; }
        [JsonPropertyName("english")] public string? English { get; set; }
        [JsonPropertyName("native")] public string? Native { get; set; }
        [JsonPropertyName("format")] public string? Format { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("startYear")] public int? StartYear { get; set; }
        [JsonPropertyName("avgScore")] public int? AverageScore { get; set; }
        [JsonPropertyName("malId")] public int? MalId { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("genres")] public List<string>? Genres { get; set; }
        [JsonPropertyName("poster")] public string? Poster { get; set; }
        [JsonPropertyName("banner")] public string? Banner { get; set; }
        [JsonPropertyName("thumb")] public string? Thumbnail { get; set; }
        // Anime
        [JsonPropertyName("episodes")] public int? Episodes { get; set; }
        [JsonPropertyName("duration")] public int? Duration { get; set; }
        [JsonPropertyName("studio")] public string? Studio { get; set; }
        // Manga
        [JsonPropertyName("chapters")] public int? Chapters { get; set; }
        [JsonPropertyName("volumes")] public int? Volumes { get; set; }
        // Characters
        [JsonPropertyName("chars")] public List<CachedCharDto>? Characters { get; set; }

        public static CachedItemDto From(MediaItem item)
        {
            var dto = new CachedItemDto
            {
                Id = item.Id,
                MediaType = item.Type.ToString(),
                Romaji = item.Title.Romaji,
                English = item.Title.English,
                Native = item.Title.Native,
                Format = item.Format.ToString(),
                Status = item.ReleaseStatus.ToString(),
                StartYear = item.StartYear,
                AverageScore = item.AverageScore,
                MalId = item.MalId,
                Description = item.Description,
                Genres = item.Genres.Count > 0 ? [.. item.Genres] : null,
                Poster = item.Images.PosterUri?.ToString(),
                Banner = item.Images.BannerUri?.ToString(),
                Thumbnail = item.Images.ThumbnailUri?.ToString(),
                Characters = item.Characters.Count > 0
                    ? item.Characters.Select(c => new CachedCharDto { N = c.Name, I = c.ImageUri?.ToString(), R = c.Role }).ToList()
                    : null,
            };
            if (item is Anime anime)
            {
                dto.Episodes = anime.EpisodeCount;
                dto.Duration = anime.DurationMinutes;
                dto.Studio = anime.Studio;
            }
            else if (item is Manga manga)
            {
                dto.Chapters = manga.ChapterCount;
                dto.Volumes = manga.VolumeCount;
            }
            return dto;
        }

        public MediaItem? ToMediaItem()
        {
            if (string.IsNullOrWhiteSpace(Id)) return null;
            var title = new MediaTitle(Romaji ?? $"#{Id}", English, Native);
            var format = Enum.TryParse<MediaFormat>(Format, out var f) ? f : MediaFormat.Unknown;
            var status = Enum.TryParse<MediaReleaseStatus>(Status, out var s) ? s : MediaReleaseStatus.Unknown;
            var images = new MediaImages(ToUri(Poster), ToUri(Banner), ToUri(Thumbnail));
            IReadOnlyList<string> genres = Genres ?? [];

            IReadOnlyList<CharacterInfo> characters = Characters?
                .Where(c => c.N is not null)
                .Select(c => new CharacterInfo(c.N!, ToUri(c.I), c.R ?? "Background"))
                .ToList() ?? [];

            if (MediaType == nameof(TanaHub.Domain.Enums.MediaType.Anime))
            {
                return new Anime(Id, title, format, status)
                {
                    StartYear = StartYear,
                    AverageScore = AverageScore,
                    MalId = MalId,
                    Description = Description,
                    Genres = genres,
                    Characters = characters,
                    Images = images,
                    EpisodeCount = Episodes,
                    DurationMinutes = Duration,
                    Studio = Studio,
                };
            }
            if (MediaType == nameof(TanaHub.Domain.Enums.MediaType.Manga))
            {
                return new Manga(Id, title, format, status)
                {
                    StartYear = StartYear,
                    AverageScore = AverageScore,
                    MalId = MalId,
                    Description = Description,
                    Genres = genres,
                    Characters = characters,
                    Images = images,
                    ChapterCount = Chapters,
                    VolumeCount = Volumes,
                };
            }
            return null;
        }

        private static Uri? ToUri(string? value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private sealed class CachedCharDto
    {
        [JsonPropertyName("n")] public string? N { get; set; }
        [JsonPropertyName("i")] public string? I { get; set; }
        [JsonPropertyName("r")] public string? R { get; set; }
    }
}
