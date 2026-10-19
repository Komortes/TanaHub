using System.Text.Json;
using TanaHub.Application.Recognition;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;

namespace TanaHub.Bridge;

public sealed partial class BridgeHandler
{
    private const int MaxImageBytes = 10 * 1024 * 1024;

    private async Task<object> RecognizeAsync(JsonElement parameters, CancellationToken ct)
    {
        var provider = RequiredString(parameters, "provider", 20);
        if (provider is not ("trace" or "art")) throw Validation("provider must be trace or art.");
        if (!Boolean(parameters, "uploadConfirmed", false))
            throw Validation("Confirm sending this image to the selected recognition provider.");
        var mime = RequiredString(parameters, "mimeType", 40);
        var encoded = RequiredText(parameters, "imageBase64", ((MaxImageBytes + 2) / 3) * 4);
        var bytes = new byte[Math.Min(MaxImageBytes + 1, (encoded.Length / 4) * 3 + 3)];
        if (!Convert.TryFromBase64String(encoded, bytes, out var count) || count == 0 || count > MaxImageBytes)
            throw Validation("The image must be valid base64 and at most 10 MiB.");
        if (!MatchesImageMime(bytes.AsSpan(0, count), mime)) throw Validation("The image must be PNG, JPEG or WebP and match its mimeType.");
        var settings = await SettingsAsync(ct);
        var sourceName = OptionalString(parameters, "sourceName", 255) ?? "Selected image";
        using var image = new MemoryStream(bytes, 0, count, writable: false);
        var matches = new List<RecognitionResultDto>();
        string? savedAttemptId = null;
        if (provider == "trace")
        {
            var result = await ReadCatalogAsync(() => Service<IRecognitionService>().RecognizeAsync(image, mime, ct), ct);
            if (result.IsFailure) throw new BridgeException("external_service_error", "trace.moe could not recognize this image. Try again later.");
            var ranked = RecognitionMatchSelector.RankAndDedupe(result.Value!
                .Where(match => match.AniListId > 0 && double.IsFinite(match.Similarity) && match.Similarity is >= 0 and <= 1)).Take(10);
            foreach (var match in ranked)
            {
                var id = $"anilist:{match.AniListId}";
                var media = await FindLocalMediaAsync(id, ct);
                matches.Add(new(id, media?.Title.DisplayTitle ?? match.EnglishTitle ?? match.RomajiTitle,
                    match.RomajiTitle, match.NativeTitle, match.Episode, match.Similarity,
                    media?.Images.PosterUri?.ToString() ?? SafeUrl(match.ThumbnailUri), null, null));
            }
            if (matches.FirstOrDefault() is { } best)
            {
                var attempt = new RecognitionAttempt
                {
                    Provider = "trace.moe",
                    SourceName = sourceName,
                    AniListId = int.Parse(best.MediaId!.AsSpan("anilist:".Length)),
                    RomajiTitle = best.Romaji,
                    EnglishTitle = best.Title,
                    NativeTitle = best.NativeTitle,
                    Episode = best.Episode,
                    Similarity = best.Similarity,
                    ThumbnailUri = ToSafeUri(best.ThumbnailUrl)
                };
                var saved = RequireSuccess(await Service<IRecognitionInboxService>().SaveAsync(attempt, ct));
                savedAttemptId = saved.Id;
            }
        }
        else
        {
            var result = await ReadCatalogAsync(() => Service<IArtRecognitionService>().RecognizeAsync(image, mime, settings.SauceNaoApiKey, ct), ct);
            if (result.IsFailure) throw new BridgeException("external_service_error", "SauceNAO could not recognize this image. Try again later.");
            matches.AddRange(result.Value!.Where(match => double.IsFinite(match.Similarity) && match.Similarity is >= 0 and <= 1)
                .OrderByDescending(match => match.Similarity).Take(10)
                .Select(match => new RecognitionResultDto(null, match.Title ?? "Unknown source", null, null, null,
                    match.Similarity, SafeUrl(match.ThumbnailUri), SafeUrl(match.SourceUri), match.Author)));
        }
        return new { provider, matches, savedAttemptId };
    }

    private async Task<object> RecognitionHistoryAsync(JsonElement parameters, CancellationToken ct) => new
    {
        items = RequireSuccess(await Service<IRecognitionInboxService>().GetRecentAsync(
            Pagination(parameters, "limit", 50, 200), ct)).Select(RecognitionHistoryDto.From).ToArray()
    };

    private async Task<RecognitionHistoryDto> SaveRecognitionAsync(JsonElement parameters, CancellationToken ct)
    {
        var provider = RequiredString(parameters, "provider", 20);
        if (provider is not ("trace" or "art" or "trace.moe" or "SauceNAO")) throw Validation("Unsupported recognition provider.");
        var id = OptionalString(parameters, "mediaId", 200);
        int? aniListId = null;
        if (id is not null)
        {
            if (!id.StartsWith("anilist:", StringComparison.OrdinalIgnoreCase) || !int.TryParse(id.AsSpan(8), out var value) || value < 1)
                throw Validation("mediaId must be a positive AniList identifier.");
            aniListId = value;
        }
        double? similarity = null;
        if (parameters.TryGetProperty("similarity", out var similarityElement) && similarityElement.ValueKind != JsonValueKind.Null)
        {
            if (similarityElement.ValueKind != JsonValueKind.Number || !similarityElement.TryGetDouble(out var value)
                || !double.IsFinite(value) || value is < 0 or > 1)
                throw Validation("similarity must be a number from 0 to 1.");
            similarity = value;
        }
        var thumbnail = OptionalString(parameters, "thumbnailUrl", 4096);
        var uri = thumbnail is null ? null : ToSafeUri(thumbnail) ?? throw Validation("thumbnailUrl must use HTTP or HTTPS.");
        var attempt = new RecognitionAttempt
        {
            Provider = provider is "trace" or "trace.moe" ? "trace.moe" : "SauceNAO",
            SourceName = OptionalString(parameters, "sourceName", 255) ?? "Selected image",
            AniListId = aniListId,
            EnglishTitle = RequiredString(parameters, "title", 500),
            RomajiTitle = OptionalString(parameters, "romaji", 500),
            NativeTitle = OptionalString(parameters, "nativeTitle", 500),
            Episode = OptionalString(parameters, "episode", 50),
            Similarity = similarity,
            ThumbnailUri = uri
        };
        return RecognitionHistoryDto.From(RequireSuccess(await Service<IRecognitionInboxService>().SaveAsync(attempt, ct)));
    }

    private static bool MatchesImageMime(ReadOnlySpan<byte> image, string mime) => mime switch
    {
        "image/png" => image.Length >= 8 && image[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/jpeg" => image.Length >= 3 && image[0] == 255 && image[1] == 216 && image[2] == 255,
        "image/webp" => image.Length >= 12 && image[..4].SequenceEqual("RIFF"u8) && image.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    private static Uri? ToSafeUri(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;
    private static string? SafeUrl(Uri? uri) => uri?.Scheme is "http" or "https" ? uri.ToString() : null;
}
