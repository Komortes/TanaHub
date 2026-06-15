using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TanaHub.Application.Common;
using TanaHub.Application.Services;

namespace TanaHub.Infrastructure.Recognition;

internal sealed class TraceMoeService : IRecognitionService
{
    private const string SearchUrl = "https://api.trace.moe/search?anilistInfo";

    private readonly HttpClient httpClient;

    public TraceMoeService(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<Result<IReadOnlyList<RecognitionMatch>>> RecognizeAsync(
        Stream imageStream,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(imageStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
            content.Add(streamContent, "image", "image");

            using var response = await httpClient.PostAsync(SearchUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<TraceMoeResponse>(
                cancellationToken: cancellationToken);

            if (payload is null)
                return Failure("Empty response from trace.moe.");

            if (!string.IsNullOrEmpty(payload.Error))
                return Failure(payload.Error);

            var matches = payload.Result
                .Where(r => r.Anilist is not null && r.Similarity > 0.5)
                .OrderByDescending(r => r.Similarity)
                .Take(5)
                .Select(r =>
                {
                    var episode = r.Episode?.ToString();
                    Uri.TryCreate(r.Image, UriKind.Absolute, out var thumb);
                    return new RecognitionMatch(
                        AniListId:    r.Anilist!.Id,
                        RomajiTitle:  r.Anilist.Title?.Romaji ?? $"#{r.Anilist.Id}",
                        EnglishTitle: r.Anilist.Title?.English,
                        NativeTitle:  r.Anilist.Title?.Native,
                        Episode:      episode,
                        Similarity:   r.Similarity,
                        ThumbnailUri: thumb);
                })
                .ToArray();

            return Result<IReadOnlyList<RecognitionMatch>>.Success(matches);
        }
        catch (HttpRequestException ex)
        {
            return Failure($"trace.moe unavailable: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("trace.moe request timed out.");
        }
    }

    private static Result<IReadOnlyList<RecognitionMatch>> Failure(string msg)
        => Result<IReadOnlyList<RecognitionMatch>>.Failure(ApplicationError.Validation(msg));

    private sealed class TraceMoeResponse
    {
        [JsonPropertyName("error")]  public string? Error  { get; init; }
        [JsonPropertyName("result")] public List<TraceMoeResult> Result { get; init; } = [];
    }

    private sealed class TraceMoeResult
    {
        [JsonPropertyName("anilist")]    public TraceMoeAniList? Anilist    { get; init; }
        [JsonPropertyName("episode")]    public object?          Episode    { get; init; }
        [JsonPropertyName("similarity")] public double           Similarity { get; init; }
        [JsonPropertyName("image")]      public string?          Image      { get; init; }
    }

    private sealed class TraceMoeAniList
    {
        [JsonPropertyName("id")]    public int                Id    { get; init; }
        [JsonPropertyName("title")] public TraceMoeTitle?     Title { get; init; }
    }

    private sealed class TraceMoeTitle
    {
        [JsonPropertyName("romaji")]  public string? Romaji  { get; init; }
        [JsonPropertyName("english")] public string? English { get; init; }
        [JsonPropertyName("native")]  public string? Native  { get; init; }
    }
}
