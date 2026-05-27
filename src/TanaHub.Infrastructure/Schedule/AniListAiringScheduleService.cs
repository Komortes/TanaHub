using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TanaHub.Application.Common;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Schedule;

public sealed class AniListAiringScheduleService : IAiringScheduleService
{
    private const string GraphQlEndpoint = "https://graphql.anilist.co/";

    private readonly HttpClient httpClient;

    public AniListAiringScheduleService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<Result<IReadOnlyList<AiringScheduleItem>>> GetUpcomingAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (to <= from)
        {
            return Result<IReadOnlyList<AiringScheduleItem>>.Failure(
                ApplicationError.Validation("Schedule end must be after start."));
        }

        if (pageSize < 1)
        {
            return Result<IReadOnlyList<AiringScheduleItem>>.Failure(
                ApplicationError.Validation("Page size must be greater than zero."));
        }

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                GraphQlEndpoint,
                new GraphQlRequest(ScheduleQuery, new
                {
                    page = 1,
                    perPage = Math.Min(pageSize, 50),
                    from = from.ToUnixTimeSeconds(),
                    to = to.ToUnixTimeSeconds()
                }),
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<GraphQlResponse<AiringScheduleData>>(
                cancellationToken: cancellationToken);

            if (payload?.Errors?.Count > 0)
            {
                return Result<IReadOnlyList<AiringScheduleItem>>.Failure(
                    ApplicationError.ExternalService(payload.Errors[0].Message ?? "AniList schedule error."));
            }

            var items = payload?.Data?.Page?.AiringSchedules?
                .Where(schedule => schedule.Media is not null)
                .Select(ToDomain)
                .ToArray() ?? [];

            return Result<IReadOnlyList<AiringScheduleItem>>.Success(items);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<AiringScheduleItem>>.Failure(
                ApplicationError.ExternalService(ex.Message));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<AiringScheduleItem>>.Failure(
                ApplicationError.ExternalService("AniList schedule request timed out."));
        }
    }

    private static AiringScheduleItem ToDomain(AniListAiringSchedule schedule)
    {
        var media = schedule.Media!;
        var title = media.Title?.English ?? media.Title?.Romaji ?? $"AniList #{media.Id}";
        var airingAt = DateTimeOffset.FromUnixTimeSeconds(schedule.AiringAt);

        return new AiringScheduleItem(
            $"anilist:{media.Id}",
            title,
            schedule.Episode,
            airingAt,
            media.Format ?? "Unknown",
            media.Status ?? "Unknown",
            ToUri(media.CoverImage?.Large ?? media.CoverImage?.Medium));
    }

    private static Uri? ToUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private const string ScheduleQuery = """
        query UpcomingAiring($page: Int!, $perPage: Int!, $from: Int!, $to: Int!) {
          Page(page: $page, perPage: $perPage) {
            airingSchedules(airingAt_greater: $from, airingAt_lesser: $to, sort: TIME) {
              id
              airingAt
              episode
              media {
                id
                format
                status
                title { romaji english }
                coverImage { large medium }
              }
            }
          }
        }
        """;

    private sealed record GraphQlRequest(string Query, object Variables);

    private sealed record GraphQlResponse<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; init; }

        [JsonPropertyName("errors")]
        public IReadOnlyList<GraphQlError>? Errors { get; init; }
    }

    private sealed record GraphQlError
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    private sealed record AiringScheduleData
    {
        [JsonPropertyName("Page")]
        public AiringSchedulePage? Page { get; init; }
    }

    private sealed record AiringSchedulePage
    {
        [JsonPropertyName("airingSchedules")]
        public IReadOnlyList<AniListAiringSchedule>? AiringSchedules { get; init; }
    }

    private sealed record AniListAiringSchedule
    {
        [JsonPropertyName("airingAt")]
        public long AiringAt { get; init; }

        [JsonPropertyName("episode")]
        public int Episode { get; init; }

        [JsonPropertyName("media")]
        public AniListScheduleMedia? Media { get; init; }
    }

    private sealed record AniListScheduleMedia
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("format")]
        public string? Format { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("title")]
        public AniListScheduleTitle? Title { get; init; }

        [JsonPropertyName("coverImage")]
        public AniListScheduleCoverImage? CoverImage { get; init; }
    }

    private sealed record AniListScheduleTitle
    {
        [JsonPropertyName("romaji")]
        public string? Romaji { get; init; }

        [JsonPropertyName("english")]
        public string? English { get; init; }
    }

    private sealed record AniListScheduleCoverImage
    {
        [JsonPropertyName("large")]
        public string? Large { get; init; }

        [JsonPropertyName("medium")]
        public string? Medium { get; init; }
    }
}
