using System.Net;
using System.Text;
using TanaHub.Domain.Enums;
using TanaHub.Infrastructure.Catalog;

namespace TanaHub.Infrastructure.Tests;

public sealed class MangaDexMediaRelationsServiceTests
{
    private const string RootId = "304ceac3-8cdb-4fe7-acf7-2b6ff7a60613";
    private const string TargetId = "048eed21-5e52-4936-9064-2331ab830965";

    [Theory]
    [InlineData("prequel", MediaRelationKind.Prequel)]
    [InlineData("sequel", MediaRelationKind.Sequel)]
    [InlineData("spin_off", MediaRelationKind.SpinOff)]
    [InlineData("main_story", MediaRelationKind.Parent)]
    [InlineData("side_story", MediaRelationKind.SideStory)]
    [InlineData("colored", MediaRelationKind.Colored)]
    [InlineData("doujinshi", MediaRelationKind.Doujinshi)]
    [InlineData("preserialization", MediaRelationKind.PreSerialization)]
    [InlineData("unknown_future_kind", MediaRelationKind.Other)]
    public async Task DirectRelations_PreserveDirectionAndProviderMetadata(string kind, MediaRelationKind expected)
    {
        var handler = new ProviderHandler(Root(), Relations(Relation(TargetId, kind)));
        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsSuccess);
        var title = Assert.Single(result.Value!.Titles);
        Assert.Equal(expected, title.Relation);
        Assert.Equal("MangaDex", title.Source);
        Assert.Equal(kind, title.SourceRelation);
        Assert.Equal($"mangadex:{TargetId}", title.MediaId);
        Assert.Equal(MediaType.Manga, title.MediaType);
        Assert.Equal("Related manga", title.Title);
        Assert.Equal(2020, title.Year);
        Assert.Null(title.ChapterCount); // lastChapter is a label, not a total.
        Assert.Null(title.VolumeCount);
        Assert.Null(result.Value.Notice);
        Assert.Equal(0, handler.AniListRequests);
    }

    [Fact]
    public async Task ExactBridge_MergesDuplicateMangaButKeepsAnimeWithSameMalNumber()
    {
        var handler = new ProviderHandler(
            Root("\"al\":\"53390\",\"mal\":\"23390\""),
            Relations(Relation(TargetId, "spin_off", links: "\"al\":\"400\",\"mal\":\"70\"")),
            AniList("""
                {"relationType":"SPIN_OFF","node":{"id":400,"idMal":70,"type":"MANGA","format":"MANGA","title":{"romaji":"Duplicate"},"chapters":12}},
                {"relationType":"ADAPTATION","node":{"id":401,"idMal":70,"type":"ANIME","format":"TV","title":{"romaji":"Anime adaptation"},"episodes":12}},
                {"relationType":"OTHER","node":{"id":53390,"idMal":23390,"type":"MANGA","format":"MANGA","title":{"romaji":"Self"}}}
                """));

        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Titles.Count);
        var manga = Assert.Single(result.Value.Titles, x => x.MediaType == MediaType.Manga);
        Assert.Equal($"mangadex:{TargetId}", manga.MediaId);
        Assert.Equal(400, manga.AniListId);
        Assert.Equal(70, manga.MalId);
        Assert.Equal(12, manga.ChapterCount);
        Assert.Equal("MangaDex", manga.Source);
        var anime = Assert.Single(result.Value.Titles, x => x.MediaType == MediaType.Anime);
        Assert.Equal("anilist:401", anime.MediaId);
        Assert.Equal(MediaRelationKind.Adaptation, anime.Relation);
        Assert.Contains("53390", handler.AniListBody);
    }

    [Fact]
    public async Task MissingAniListMapping_UsesTypedMalMangaMapping()
    {
        var handler = new ProviderHandler(Root("\"mal\":\"23390\""), Relations(), AniList("""
            {"relationType":"ADAPTATION","node":{"id":401,"type":"ANIME","format":"TV","title":{"romaji":"Adaptation"}}}
            """));

        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.Single(result.Value!.Titles);
        Assert.Contains("23390", handler.AniListBody);
        Assert.Contains("MANGA", handler.AniListBody);
    }

    [Fact]
    public async Task Bridge_PreservesDistinctMangaDexEditionsThatShareExternalAliases()
    {
        var handler = new ProviderHandler(Root("\"al\":\"53390\""), Relations(
            Relation(TargetId, "spin_off", links: "\"al\":\"400\""),
            Relation("0574b968-63da-467a-a41e-31a666817dca", "spin_off", links: "\"mal\":\"70\"")), AniList("""
                {"relationType":"SPIN_OFF","node":{"id":400,"idMal":70,"type":"MANGA","format":"MANGA","title":{"romaji":"Different catalog title"}}}
                """));

        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.Equal(2, result.Value!.Titles.Count);
        Assert.Contains(result.Value.Titles, title => title.MediaId == $"mangadex:{TargetId}");
        Assert.Contains(result.Value.Titles, title => title.MediaId == "mangadex:0574b968-63da-467a-a41e-31a666817dca");
    }

    [Fact]
    public async Task ColoredEdition_SharingRootExternalIds_IsNotTreatedAsSelf()
    {
        var handler = new ProviderHandler(Root("\"al\":\"53390\",\"mal\":\"23390\""), Relations(
            Relation(TargetId, "colored", links: "\"al\":\"53390\",\"mal\":\"23390\"")), AniList(""));

        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        var title = Assert.Single(result.Value!.Titles);
        Assert.Equal($"mangadex:{TargetId}", title.MediaId);
        Assert.Equal(MediaRelationKind.Colored, title.Relation);
    }

    [Theory]
    [InlineData("pornographic", "\"al\":\"400\"")]
    [InlineData("erotica", "\"mal\":\"70\"")]
    [InlineData("unknown", "\"al\":\"400\",\"mal\":\"70\"")]
    public async Task Bridge_DoesNotReintroduceFilteredMangaThroughExternalAliases(string rating, string links)
    {
        var handler = new ProviderHandler(Root("\"al\":\"53390\""), Relations(
            Relation(TargetId, "doujinshi", rating: rating, links: links)), AniList("""
                {"relationType":"SPIN_OFF","node":{"id":400,"idMal":70,"type":"MANGA","format":"MANGA","title":{"romaji":"Conflicting rating"}}},
                {"relationType":"ADAPTATION","node":{"id":401,"idMal":70,"type":"ANIME","format":"TV","title":{"romaji":"Separate anime"}}}
                """));

        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        var title = Assert.Single(result.Value!.Titles);
        Assert.Equal("anilist:401", title.MediaId);
        Assert.Equal(MediaType.Anime, title.MediaType);
    }

    [Fact]
    public async Task ValidEmptyRelations_WithoutMapping_AreAnEmptySuccess()
    {
        var result = await CreateService(new ProviderHandler(Root(), Relations()))
            .GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Titles);
        Assert.Null(result.Value.Notice);
    }

    [Fact]
    public async Task RootWithoutAttributes_IsReportedAsPartial()
    {
        var root = """
            {"result":"ok","data":{"id":"$ROOT$","type":"manga"}}
            """.Replace("$ROOT$", RootId);
        var result = await CreateService(new ProviderHandler(root, Relations()))
            .GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Titles);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Notice));
    }

    [Fact]
    public async Task TargetWithoutExpandedAttributes_IsReportedAsPartial()
    {
        var relation = """
            {"type":"manga_relation","attributes":{"relation":"sequel"},"relationships":[{"id":"$TARGET$","type":"manga"}]}
            """.Replace("$TARGET$", TargetId);
        var result = await CreateService(new ProviderHandler(Root(), Relations(relation)))
            .GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Titles);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Notice));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"result\":\"error\",\"data\":[]}")]
    [InlineData("not-json")]
    public async Task InvalidRelationsResponse_IsFailure(string response)
    {
        var result = await CreateService(new ProviderHandler(Root(), response))
            .GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UnsafeMalformedAndSelfRelations_AreExcluded()
    {
        var handler = new ProviderHandler(Root("\"al\":\"invalid\",\"mal\":\"0\""), Relations(
            Relation(RootId, "sequel"),
            Relation("not-a-uuid", "prequel"),
            Relation(Guid.NewGuid().ToString(), "doujinshi", rating: "pornographic"),
            Relation(Guid.NewGuid().ToString(), "doujinshi", rating: "erotica"),
            Relation(TargetId, "sequel"),
            Relation(TargetId, "sequel")));

        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsSuccess);
        Assert.Equal($"mangadex:{TargetId}", Assert.Single(result.Value!.Titles).MediaId);
        Assert.Equal(0, handler.AniListRequests);
    }

    [Fact]
    public async Task BridgeFailure_PreservesDirectRelationsAndReportsPartialResult()
    {
        var handler = new ProviderHandler(Root("\"al\":\"53390\""), Relations(Relation(TargetId, "prequel")))
        {
            AniListStatus = HttpStatusCode.ServiceUnavailable
        };

        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Titles);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Notice));
    }

    [Fact]
    public async Task RelationRequestFailure_IsNotReportedAsEmptySuccess()
    {
        var handler = new ProviderHandler(Root(), Relations()) { RelationsStatus = HttpStatusCode.ServiceUnavailable };

        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RelationRequestFailure_StillReturnsAvailableAnimeBridgeWithNotice()
    {
        var handler = new ProviderHandler(Root("\"al\":\"53390\""), Relations(), AniList("""
            {"relationType":"ADAPTATION","node":{"id":401,"type":"ANIME","format":"TV","title":{"romaji":"Adaptation"}}}
            """)) { RelationsStatus = HttpStatusCode.ServiceUnavailable };

        var result = await CreateService(handler).GetRelatedTitlesAsync($"mangadex:{RootId}");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Titles);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Notice));
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var service = CreateService(new ProviderHandler(Root(), Relations()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetRelatedTitlesAsync($"mangadex:{RootId}", cancellationToken: source.Token));
    }

    [Fact]
    public async Task CancellationDuringRequest_IsPropagatedInsteadOfPartialFailure()
    {
        using var source = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = CreateService(new WaitingHandler(started))
            .GetRelatedTitlesAsync($"mangadex:{RootId}", cancellationToken: source.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Theory]
    [InlineData("mangadex:not-a-uuid")]
    [InlineData("anilist:1")]
    [InlineData("mangadex:../x")]
    public async Task InvalidRootId_IsRejected(string mediaId)
    {
        var result = await CreateService(new ProviderHandler(Root(), Relations())).GetRelatedTitlesAsync(mediaId);
        Assert.True(result.IsFailure);
    }

    private static MangaDexMediaRelationsService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        return new MangaDexMediaRelationsService(client, new AniListMediaRelationsService(client));
    }

    private static string Root(string links = "") => """
        {"result":"ok","data":{"id":"$ROOT$","type":"manga","attributes":{"links":{ $LINKS$ }}}}
        """.Replace("$ROOT$", RootId).Replace("$LINKS$", links);

    private static string Relations(params string[] relations) => "{\"result\":\"ok\",\"data\":[" + string.Join(",", relations) + "]}";

    private static string Relation(string id, string kind, string rating = "safe", string links = "") => """
        {"type":"manga_relation","attributes":{"relation":"$KIND$"},"relationships":[
          {"id":"$ID$","type":"manga","attributes":{"title":{"en":"Related manga"},"contentRating":"$RATING$","year":2020,"links":{ $LINKS$ },"lastChapter":"12.5","lastVolume":"3"}}]}
        """.Replace("$KIND$", kind).Replace("$ID$", id).Replace("$RATING$", rating).Replace("$LINKS$", links);

    private static string AniList(string edges) => """
        {"data":{"Media":{"id":53390,"idMal":23390,"type":"MANGA","isAdult":false,"relations":{"edges":[$EDGES$]}}}}
        """.Replace("$EDGES$", edges.Replace("\"node\":{", "\"node\":{\"isAdult\":false,"));

    private sealed class ProviderHandler(string root, string relations, string aniList = "{}") : HttpMessageHandler
    {
        public HttpStatusCode AniListStatus { get; init; } = HttpStatusCode.OK;
        public HttpStatusCode RelationsStatus { get; init; } = HttpStatusCode.OK;
        public int AniListRequests { get; private set; }
        public string AniListBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isAniList = request.RequestUri!.Host == "graphql.anilist.co";
            var isRelations = request.RequestUri.AbsolutePath.EndsWith("/relation", StringComparison.Ordinal);
            if (isAniList)
            {
                AniListRequests++;
                AniListBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(isAniList ? AniListStatus : isRelations ? RelationsStatus : HttpStatusCode.OK)
            {
                Content = new StringContent(isAniList ? aniList : isRelations ? relations : root, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class WaitingHandler(TaskCompletionSource started) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The canceled request should not complete.");
        }
    }
}
