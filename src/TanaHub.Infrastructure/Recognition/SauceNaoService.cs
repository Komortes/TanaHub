using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TanaHub.Application.Common;
using TanaHub.Application.Services;

namespace TanaHub.Infrastructure.Recognition;

internal sealed class SauceNaoService : IArtRecognitionService
{
    private const string BaseUrl = "https://saucenao.com/search.php";
    private const double MinimumSimilarityPercent = 50;

    private readonly HttpClient httpClient;

    public SauceNaoService(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<Result<IReadOnlyList<ArtRecognitionMatch>>> RecognizeAsync(
        Stream imageStream,
        string mimeType,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}?output_type=2&numres=5";
            if (!string.IsNullOrWhiteSpace(apiKey))
                url += $"&api_key={Uri.EscapeDataString(apiKey)}";

            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(imageStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
            content.Add(streamContent, "file", "image");

            using var response = await httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<SauceNaoResponse>(
                cancellationToken: cancellationToken);

            if (payload is null)
                return Failure("Empty response from SauceNAO.");

            if (payload.Header is { Status: < 0 } header)
                return Failure(header.Message ?? $"SauceNAO error (status {header.Status}).");

            var matches = payload.Results
                .Where(r => r.Header is not null && ParseSimilarityPercent(r.Header.Similarity) > MinimumSimilarityPercent)
                .Select(r =>
                {
                    var similarity = ParseSimilarityPercent(r.Header!.Similarity) / 100.0;
                    Uri.TryCreate(r.Header.Thumbnail, UriKind.Absolute, out var thumbnail);
                    Uri? source = null;
                    var firstUrl = r.Data?.ExtUrls?.FirstOrDefault();
                    if (firstUrl is not null)
                        Uri.TryCreate(firstUrl, UriKind.Absolute, out source);

                    return new ArtRecognitionMatch(
                        Similarity: similarity,
                        Title: r.Data?.Title,
                        Author: r.Data?.MemberName,
                        SourceUri: source,
                        ThumbnailUri: thumbnail);
                })
                .OrderByDescending(match => match.Similarity)
                .Take(5)
                .ToArray();

            return Result<IReadOnlyList<ArtRecognitionMatch>>.Success(matches);
        }
        catch (HttpRequestException ex)
        {
            return Failure($"SauceNAO unavailable: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("SauceNAO request timed out.");
        }
    }

    private static double ParseSimilarityPercent(string? raw) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static Result<IReadOnlyList<ArtRecognitionMatch>> Failure(string message)
        => Result<IReadOnlyList<ArtRecognitionMatch>>.Failure(ApplicationError.Validation(message));

    private sealed class SauceNaoResponse
    {
        [JsonPropertyName("header")] public SauceNaoHeader? Header { get; init; }
        [JsonPropertyName("results")] public List<SauceNaoResult> Results { get; init; } = [];
    }

    private sealed class SauceNaoHeader
    {
        [JsonPropertyName("status")] public int Status { get; init; }
        [JsonPropertyName("message")] public string? Message { get; init; }
    }

    private sealed class SauceNaoResult
    {
        [JsonPropertyName("header")] public SauceNaoResultHeader? Header { get; init; }
        [JsonPropertyName("data")] public SauceNaoResultData? Data { get; init; }
    }

    private sealed class SauceNaoResultHeader
    {
        [JsonPropertyName("similarity")] public string? Similarity { get; init; }
        [JsonPropertyName("thumbnail")] public string? Thumbnail { get; init; }
    }

    private sealed class SauceNaoResultData
    {
        [JsonPropertyName("title")] public string? Title { get; init; }
        [JsonPropertyName("member_name")] public string? MemberName { get; init; }
        [JsonPropertyName("ext_urls")] public List<string>? ExtUrls { get; init; }
    }
}
