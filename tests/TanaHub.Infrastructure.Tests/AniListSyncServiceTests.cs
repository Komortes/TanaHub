using System.Net;
using System.Text;
using System.Text.Json;
using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Library;
using TanaHub.Infrastructure.Sync;

namespace TanaHub.Infrastructure.Tests;

public sealed class AniListSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_PreservesLocalOnlyFields()
    {
        var library = new InMemoryUserLibraryService([
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current)
            {
                Review = "Keep",
                Tags = ["favorite"],
                CustomLists = ["Weekend"]
            }
        ]);
        var service = new AniListSyncService(new HttpClient(new QueueResponseHandler([
            SuccessResponse("ANIME", 1, "COMPLETED", 26),
            EmptyResponse()
        ])));

        var result = await service.SyncAsync("token", 42, library);
        var entry = await library.GetEntryAsync("anilist:1");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Equal(MediaListStatus.Completed, entry.Value!.Status);
        Assert.Equal(26, entry.Value.Progress);
        Assert.Equal("Keep", entry.Value.Review);
        Assert.Equal(["favorite"], entry.Value.Tags);
        Assert.Equal(["Weekend"], entry.Value.CustomLists);
    }

    [Fact]
    public async Task SyncAsync_DoesNotApplyAnimeWhenMangaFetchFails()
    {
        var library = new InMemoryUserLibraryService();
        var service = new AniListSyncService(new HttpClient(new QueueResponseHandler([
            SuccessResponse("ANIME", 1, "CURRENT", 3),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        ])));

        var result = await service.SyncAsync("token", 42, library);
        var entries = await library.GetEntriesAsync(new UserLibraryQuery());

        Assert.True(result.IsFailure);
        Assert.Empty(entries.Value!.Items);
    }

    private static HttpResponseMessage SuccessResponse(string type, int id, string status, int progress)
    {
        var json = JsonSerializer.Serialize(new
        {
            data = new
            {
                MediaListCollection = new
                {
                    lists = new[]
                    {
                        new
                        {
                            entries = new[]
                            {
                                new { mediaId = id, status, progress, score = 8, media = new { type } }
                            }
                        }
                    }
                }
            }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage EmptyResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            "{\"data\":{\"MediaListCollection\":{\"lists\":[]}}}",
            Encoding.UTF8,
            "application/json")
    };

    private sealed class QueueResponseHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
    {
        public QueueResponseHandler(IEnumerable<HttpResponseMessage> responses)
            : this(new Queue<HttpResponseMessage>(responses))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responses.Dequeue());
    }
}
