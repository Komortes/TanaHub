using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Application.Updates;
using TanaHub.Bridge;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Catalog;
using TanaHub.Infrastructure.Library;

namespace TanaHub.Bridge.Tests;

public sealed class BridgeFeatureTests
{
    [Theory]
    [InlineData("Nebula dark")]
    [InlineData("High contrast")]
    [InlineData("Light")]
    [InlineData("System")]
    public async Task ThemeSaveReturnsThePreferenceAndPreservesItDuringUnrelatedSaves(string theme)
    {
        var fixture = new Fixture();
        var saved = await Call(fixture.Handler, "settings.save", new { theme });

        Assert.Equal(theme, saved.GetProperty("theme").GetString());
        Assert.Equal(theme, fixture.Settings.Value.Theme);

        var updated = await Call(fixture.Handler, "settings.save", new { notificationsEnabled = false });
        Assert.Equal(theme, updated.GetProperty("theme").GetString());
        Assert.False(updated.GetProperty("notificationsEnabled").GetBoolean());
        Assert.Equal(theme, fixture.Settings.Value.Theme);
    }

    [Fact]
    public async Task UnknownThemeIsRejectedWithoutChangingSettings()
    {
        var fixture = new Fixture();
        var previous = fixture.Settings.Value;

        var error = await Error(fixture.Handler, "settings.save", new { theme = "Unknown", notificationsEnabled = false });

        Assert.Equal("validation_error", error.GetProperty("code").GetString());
        Assert.Equal(previous, fixture.Settings.Value);
    }

    [Theory]
    [InlineData("library.progress", "anilist:1")]
    [InlineData("library.progress.set", "anilist:1")]
    [InlineData("library.progress", "anilist:2")]
    [InlineData("library.progress.set", "anilist:2")]
    public async Task FirstProgressStartsAPlannedTitleAndMakesItAvailableToContinue(string method, string mediaId)
    {
        var fixture = new Fixture();
        await Call(fixture.Handler, "library.add", new { mediaId });
        var updated = await Call(fixture.Handler, method, new { mediaId, progress = 1 });
        Assert.Equal("Watching", updated.GetProperty("status").GetString());
        Assert.Equal(1, updated.GetProperty("progress").GetInt32());
        Assert.NotNull(updated.GetProperty("startedAt").GetString());
        var home = await Call(fixture.Handler, "home.get", new { });
        Assert.Equal(mediaId, Assert.Single(home.GetProperty("continueItems").EnumerateArray()).GetProperty("mediaId").GetString());
    }

    [Theory]
    [InlineData(MediaListStatus.Planning, 1, 2)]
    [InlineData(MediaListStatus.Planning, 0, 0)]
    [InlineData(MediaListStatus.Paused, 0, 1)]
    [InlineData(MediaListStatus.Completed, 0, 1)]
    [InlineData(MediaListStatus.Dropped, 0, 1)]
    [InlineData(MediaListStatus.Repeating, 0, 1)]
    public async Task SettingProgressPreservesAnExplicitStatusAndDates(MediaListStatus status, int before, int progress)
    {
        var start = DateTimeOffset.UtcNow.AddDays(-10);
        var completion = start.AddDays(1);
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, status) { Progress = before, StartedAt = start, CompletedAt = completion }]);
        await Call(fixture.Handler, "library.progress.set", new { mediaId = Anime.Id, progress });
        var saved = (await fixture.Library.GetEntryAsync(Anime.Id)).Value!;
        Assert.Equal(status, saved.Status);
        Assert.Equal(start, saved.StartedAt);
        Assert.Equal(completion, saved.CompletedAt);
    }

    [Fact]
    public async Task StartingProgressPreservesDatesAndDoesNotInventAContradictoryStart()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-10);
        var completion = start.AddDays(1);
        var fixture = new Fixture([
            new(Anime.Id, MediaType.Anime, MediaListStatus.Planning) { StartedAt = start, CompletedAt = completion },
            new(Manga.Id, MediaType.Manga, MediaListStatus.Planning) { CompletedAt = completion }
        ]);
        await Call(fixture.Handler, "library.progress.set", new { mediaId = Anime.Id, progress = 1 });
        await Call(fixture.Handler, "library.progress", new { mediaId = Manga.Id });
        var anime = (await fixture.Library.GetEntryAsync(Anime.Id)).Value!;
        var manga = (await fixture.Library.GetEntryAsync(Manga.Id)).Value!;
        Assert.Equal(start, anime.StartedAt);
        Assert.Equal(completion, anime.CompletedAt);
        Assert.Equal(MediaListStatus.Current, manga.Status);
        Assert.Null(manga.StartedAt);
        Assert.Equal(completion, manga.CompletedAt);
    }

    [Theory]
    [InlineData(1_000_001)]
    [InlineData(int.MaxValue)]
    public async Task AbsoluteProgressSupportsTheStoredIntegerRangeAndKeepsOtherFields(int progress)
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current) { Progress = 3, Score = 9, Notes = "Keep notes" }]);
        var updated = await Call(fixture.Handler, "library.progress.set", new { mediaId = Anime.Id, progress });
        Assert.Equal(progress, updated.GetProperty("progress").GetInt32());
        Assert.Equal(9, updated.GetProperty("score").GetInt32());
        Assert.Equal("Keep notes", updated.GetProperty("notes").GetString());
        Assert.Equal("Watching", updated.GetProperty("status").GetString());
        if (progress == int.MaxValue)
        {
            var error = await Error(fixture.Handler, "library.progress", new { mediaId = Anime.Id, amount = 1 });
            Assert.Equal("validation_error", error.GetProperty("code").GetString());
            Assert.Equal(progress, (await fixture.Library.GetEntryAsync(Anime.Id)).Value!.Progress);
        }
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    public async Task InvalidAbsoluteProgressLeavesTheSavedCounterUnchanged(string number)
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current) { Progress = 3 }]);
        var error = await Error(fixture.Handler, "library.progress.set", new { mediaId = Anime.Id, progress = JsonSerializer.Deserialize<JsonElement>(number) });
        Assert.Equal("validation_error", error.GetProperty("code").GetString());
        Assert.Equal(3, (await fixture.Library.GetEntryAsync(Anime.Id)).Value!.Progress);
    }

    private static readonly Anime Anime = new("anilist:1", new("First", "First title", "原題"), MediaFormat.Tv, MediaReleaseStatus.Finished)
    {
        EpisodeCount = 12,
        DurationMinutes = 20,
        Studio = "Studio",
        MalId = 10,
        Genres = ["Drama"],
        Characters = [new("Character", new("https://example.test/character.jpg"), "Main")]
    };
    private static readonly Manga Manga = new("anilist:2", new("Manga"), MediaFormat.Manga, MediaReleaseStatus.Releasing)
    {
        ChapterCount = 100,
        VolumeCount = 12,
        Genres = ["Drama"]
    };

    [Fact]
    public async Task HomeUsesExistingCalculatorsAndFiltersSelectedMediaType()
    {
        var fixture = new Fixture([
            new(Anime.Id, MediaType.Anime, MediaListStatus.Current) { Progress = 3, Score = 8 },
            new(Manga.Id, MediaType.Manga, MediaListStatus.Completed) { Progress = 50, Score = 9 }
        ]);
        var result = await Call(fixture.Handler, "home.get", new { type = "Anime" });

        var insights = result.GetProperty("insights");
        Assert.Equal(1, insights.GetProperty("totalEntries").GetInt32());
        Assert.Equal(60, insights.GetProperty("estimatedWatchMinutes").GetInt32());
        Assert.Equal(0, insights.GetProperty("chaptersRead").GetInt32());
        Assert.Equal("Watching", Assert.Single(insights.GetProperty("byStatus").EnumerateArray()).GetProperty("key").GetString());
        Assert.Single(result.GetProperty("continueItems").EnumerateArray());
    }

    [Fact]
    public async Task RecommendationsExcludeOwnedTitlesAndUseTheSelectedType()
    {
        var extra = new Anime("anilist:3", new("Recommended"), MediaFormat.Tv, MediaReleaseStatus.Finished) { Genres = ["Drama"] };
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current)], [Anime, Manga, extra]);
        var result = await Call(fixture.Handler, "home.recommendations", new { type = "Anime", limit = 8 });

        var recommendation = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal(extra.Id, recommendation.GetProperty("id").GetString());
        Assert.Equal("Drama", Assert.Single(result.GetProperty("genres").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task FullMediaAndLocalEditsPreserveUnchangedTrackingFields()
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current) { Score = 9, Notes = "Remember" }]);
        var detail = await Call(fixture.Handler, "media.detail", new { mediaId = Anime.Id });
        Assert.Equal("原題", detail.GetProperty("nativeTitle").GetString());
        Assert.Equal(20, detail.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("Studio", detail.GetProperty("studio").GetString());
        Assert.Single(detail.GetProperty("characters").EnumerateArray());

        await Call(fixture.Handler, "library.progress.set", new { mediaId = Anime.Id, progress = 7 });
        await Call(fixture.Handler, "library.review", new { mediaId = Anime.Id, review = "A review\nwith lines" });
        await Call(fixture.Handler, "library.organization", new { mediaId = Anime.Id, tags = new[] { "Favourite", "favourite" }, customLists = new[] { "Weekend" } });
        var updated = await Call(fixture.Handler, "library.dates", new { mediaId = Anime.Id, startedAt = "2026-09-01T00:00:00+02:00", completedAt = (string?)null });

        Assert.Equal(7, updated.GetProperty("progress").GetInt32());
        Assert.Equal(9, updated.GetProperty("score").GetInt32());
        Assert.Equal("Remember", updated.GetProperty("notes").GetString());
        Assert.Equal("A review\nwith lines", updated.GetProperty("review").GetString());
        Assert.Single(updated.GetProperty("tags").EnumerateArray());
        Assert.Equal("Weekend", Assert.Single(updated.GetProperty("customLists").EnumerateArray()).GetString());
        var filters = await Call(fixture.Handler, "library.filters", new { });
        Assert.Equal("Favourite", Assert.Single(filters.GetProperty("tags").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task LibraryScoreSortAndOrganizationSearchRunBeforePagination()
    {
        var fixture = new Fixture([
            new(Anime.Id, MediaType.Anime, MediaListStatus.Current) { Score = 4, Tags = ["Great"] },
            new(Manga.Id, MediaType.Manga, MediaListStatus.Planning) { Score = 9, CustomLists = ["Great"] }
        ]);
        var result = await Call(fixture.Handler, "library.list", new { search = "great", sort = "Score", pageSize = 1 });
        Assert.Equal(2, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(Manga.Id, Assert.Single(result.GetProperty("items").EnumerateArray()).GetProperty("mediaId").GetString());
        Assert.True(result.GetProperty("hasNextPage").GetBoolean());
    }

    [Fact]
    public async Task LibraryLookupReturnsDistinctMatchesWithoutRemoteHydrationAndRefreshesAfterSync()
    {
        var fixture = new Fixture([
            new(Anime.Id, MediaType.Anime, MediaListStatus.Current) { Progress = 3, Score = 7, PosterUri = new("https://example.test/saved.jpg") },
            new(Manga.Id, MediaType.Manga, MediaListStatus.Planning),
            new("anilist:3", MediaType.Anime, MediaListStatus.Planning)
        ]);
        var handler = new BridgeHandler(new UnexpectedCatalog(), fixture.Library,
            id => id == Anime.Id ? Anime : id == Manga.Id ? Manga : null);
        var response = await Call(handler, "library.lookup", new { mediaIds = new[] { Anime.Id, "anilist:missing", Manga.Id, Anime.Id } });
        var items = response.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(2, items.Length);
        var anime = Assert.Single(items, item => item.GetProperty("mediaId").GetString() == Anime.Id);
        Assert.Equal(3, anime.GetProperty("progress").GetInt32());
        Assert.Equal("First title", anime.GetProperty("media").GetProperty("title").GetString());
        Assert.Equal("https://example.test/saved.jpg", anime.GetProperty("media").GetProperty("posterUrl").GetString());
        Assert.Empty((await Call(handler, "library.lookup", new { mediaIds = Array.Empty<string>() })).GetProperty("items").EnumerateArray());

        fixture.Settings.Value = new() { AniListAccessToken = "saved-private-token", AniListUserId = 42 };
        fixture.Services.Items[typeof(IAniListSyncService)] = new SyncFake();
        await Call(fixture.Handler, "anilist.sync", new { });
        var refreshed = Assert.Single((await Call(handler, "library.lookup", new { mediaIds = new[] { Anime.Id } })).GetProperty("items").EnumerateArray());
        Assert.Equal("Completed", refreshed.GetProperty("status").GetString());
        Assert.Equal(0, refreshed.GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task LibraryLookupValidatesItsBoundedArrayBeforeReadingEntries()
    {
        var fixture = new Fixture();
        object[] invalid = [new { }, new { mediaIds = "bad" }, new { mediaIds = new[] { 1 } },
            new { mediaIds = new[] { " " } }, new { mediaIds = new[] { "anilist:1\n" } },
            new { mediaIds = new[] { new string('a', 201) } },
            new { mediaIds = Enumerable.Repeat(Anime.Id, 51).ToArray() }];
        foreach (var parameters in invalid)
            Assert.Equal("validation_error", (await Error(fixture.Handler, "library.lookup", parameters)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task SettingsNeverExposeSecretsAndPartialSavesPreserveCredentials()
    {
        var fixture = new Fixture();
        fixture.Settings.Value = new()
        {
            AniListClientSecret = "client-private",
            AniListAccessToken = "token-private",
            SauceNaoApiKey = "sauce-private",
            AniListUserId = 12,
            AniListUsername = "User"
        };
        var response = await Call(fixture.Handler, "settings.save", new { notificationsEnabled = false });
        Assert.DoesNotContain("client-private", response.GetRawText());
        Assert.DoesNotContain("token-private", response.GetRawText());
        Assert.DoesNotContain("sauce-private", response.GetRawText());
        Assert.True(response.GetProperty("hasAniListClientSecret").GetBoolean());
        Assert.True(response.GetProperty("isAniListConnected").GetBoolean());
        Assert.Equal("client-private", fixture.Settings.Value.AniListClientSecret);

        await Call(fixture.Handler, "settings.save", new { sauceNaoApiKey = "" });
        Assert.Empty(fixture.Settings.Value.SauceNaoApiKey);
        await Call(fixture.Handler, "anilist.disconnect", new { });
        Assert.Empty(fixture.Settings.Value.AniListAccessToken);
        Assert.Equal("client-private", fixture.Settings.Value.AniListClientSecret);
    }

    [Fact]
    public async Task OAuthCompletionPreservesPreferencesChangedWhileBrowserIsOpen()
    {
        var fixture = new Fixture();
        fixture.Services.Items[typeof(IAniListAuthService)] = new AuthFake(() =>
        {
            fixture.Settings.Value = fixture.Settings.Value with { Theme = "High contrast", NotificationsEnabled = false };
        });
        var response = await Call(fixture.Handler, "anilist.connect", new { clientId = "123", clientSecret = "secret" });

        Assert.Equal("High contrast", response.GetProperty("theme").GetString());
        Assert.False(response.GetProperty("notificationsEnabled").GetBoolean());
        Assert.True(response.GetProperty("isAniListConnected").GetBoolean());
        Assert.Equal("new-access-token", fixture.Settings.Value.AniListAccessToken);
        Assert.DoesNotContain("new-access-token", response.GetRawText());
    }

    [Fact]
    public async Task OAuthFailureDoesNotExposeRemotePayloads()
    {
        var fixture = new Fixture();
        fixture.Services.Items[typeof(IAniListAuthService)] = new AuthFake(failure: true);
        var error = await Error(fixture.Handler, "anilist.connect", new { clientId = "123", clientSecret = "secret" });
        Assert.Equal("authorization_failed", error.GetProperty("code").GetString());
        Assert.DoesNotContain("secret", error.GetRawText());
    }

    [Fact]
    public async Task SyncUsesSavedCredentialsAndPersistsOnlyPublicConnectionMetadata()
    {
        var fixture = new Fixture();
        fixture.Settings.Value = new() { AniListAccessToken = "saved-private-token", AniListUserId = 42 };
        fixture.Services.Items[typeof(IAniListSyncService)] = new SyncFake();
        var result = await Call(fixture.Handler, "anilist.sync", new { });
        Assert.Equal(1, result.GetProperty("importedCount").GetInt32());
        Assert.NotNull(fixture.Settings.Value.AniListLastSyncAt);
        Assert.True((await fixture.Library.GetEntryAsync("anilist:1")).IsSuccess);
        Assert.DoesNotContain("saved-private-token", result.GetRawText());
    }

    [Fact]
    public async Task ScheduleFiltersBoundariesAndLibraryMembershipAndDeduplicatesNotifications()
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current)]);
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var tomorrow = today.AddDays(1);
        fixture.Services.Items[typeof(IAiringScheduleService)] = new ScheduleFake([
            new(Anime.Id, "Anime", 1, today.AddHours(12), "TV", "Releasing", null),
            new("anilist:9", "Other", 2, today.AddHours(15), "TV", "Releasing", null),
            new(Anime.Id, "Tomorrow", 3, tomorrow, "TV", "Releasing", null)
        ]);
        var result = await Call(fixture.Handler, "schedule.list", new { from = today, to = tomorrow, libraryOnly = true });
        var item = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("Watching", item.GetProperty("status").GetString());
        Assert.True(item.GetProperty("inLibrary").GetBoolean());
        Assert.Equal(1, (await Call(fixture.Handler, "schedule.notify", new { from = today, to = tomorrow })).GetProperty("notifiedCount").GetInt32());
        Assert.Equal(0, (await Call(fixture.Handler, "schedule.notify", new { from = today, to = tomorrow })).GetProperty("notifiedCount").GetInt32());
        Assert.Single(fixture.Notifications.Messages);
    }

    [Fact]
    public async Task NotificationSupportExplainsUnsupportedBuildsWithoutSending()
    {
        var fixture = new Fixture();
        fixture.Notifications.Supported = false;

        var support = await Call(fixture.Handler, "notifications.support", new { });

        Assert.False(support.GetProperty("supported").GetBoolean());
        Assert.Equal("System reminders are not supported on Windows in this build.", support.GetProperty("reason").GetString());
        Assert.Equal(0, fixture.Notifications.Attempts);
    }

    [Fact]
    public async Task UnsupportedNotificationsDoNotCountOrPoisonSessionDeduplication()
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current)]);
        fixture.Notifications.Supported = false;
        var window = await PrepareNotificationSchedule(fixture);

        Assert.Equal(0, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Equal(0, fixture.Notifications.Attempts);
        fixture.Notifications.Supported = true;
        Assert.Equal(1, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Equal(0, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Single(fixture.Notifications.Messages);
    }

    [Fact]
    public async Task FailedNotificationsRemainRetryableWithoutExposingProcessDetails()
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current)]);
        fixture.Notifications.Outcomes.Enqueue(false);
        fixture.Notifications.Outcomes.Enqueue(true);
        var window = await PrepareNotificationSchedule(fixture);

        var error = await Error(fixture.Handler, "schedule.notify", window);
        Assert.Equal("notification_failed", error.GetProperty("code").GetString());
        Assert.Contains("retry", error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-process-diagnostic", error.GetRawText());
        Assert.Empty(fixture.Notifications.Messages);
        Assert.Equal(1, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Equal(0, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Equal(2, fixture.Notifications.Attempts);
        Assert.Single(fixture.Notifications.Messages);
    }

    [Fact]
    public async Task DisabledNotificationsRemainEligibleAfterThePreferenceIsEnabled()
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current)]);
        fixture.Settings.Value = fixture.Settings.Value with { NotificationsEnabled = false };
        var window = await PrepareNotificationSchedule(fixture);

        Assert.Equal(0, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Equal(0, fixture.Notifications.Attempts);
        fixture.Settings.Value = fixture.Settings.Value with { NotificationsEnabled = true };
        Assert.Equal(1, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Single(fixture.Notifications.Messages);
    }

    [Fact]
    public async Task PartialNotificationFailureRetriesOnlyEpisodesThatWereNotAccepted()
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current)]);
        foreach (var outcome in new[] { true, false, true }) fixture.Notifications.Outcomes.Enqueue(outcome);
        var window = await PrepareNotificationSchedule(fixture, episodes: 2);

        var error = await Error(fixture.Handler, "schedule.notify", window);
        Assert.Equal("notification_failed", error.GetProperty("code").GetString());
        Assert.Single(fixture.Notifications.Messages);
        Assert.Equal(1, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Equal(0, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Equal(3, fixture.Notifications.Attempts);
        Assert.Equal(2, fixture.Notifications.Messages.Count);
        Assert.Single(fixture.Notifications.Messages, message => message.StartsWith("EP 1 ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancelledNotificationsRemainEligibleForTheNextRequest()
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current)]);
        var window = await PrepareNotificationSchedule(fixture);
        using var cancellation = new CancellationTokenSource();
        fixture.Notifications.BeforeSend = cancellation.Cancel;
        using var response = JsonDocument.Parse(await fixture.Handler.HandleAsync(JsonSerializer.Serialize(new { id = "cancel-notification", method = "schedule.notify", @params = window }), cancellation.Token));

        Assert.Equal("cancelled", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(fixture.Notifications.Messages);
        fixture.Notifications.BeforeSend = null;
        Assert.Equal(1, (await Call(fixture.Handler, "schedule.notify", window)).GetProperty("notifiedCount").GetInt32());
        Assert.Single(fixture.Notifications.Messages);
    }

    private static async Task<object> PrepareNotificationSchedule(Fixture fixture, int episodes = 1)
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var to = from.AddHours(1);
        fixture.Services.Items[typeof(IAiringScheduleService)] = new ScheduleFake(Enumerable.Range(1, episodes)
            .Select(episode => new AiringScheduleItem(Anime.Id, "Anime", episode, from.AddMinutes(episode * 10), "TV", "Releasing", null)).ToArray());
        var window = new { from, to };
        await Call(fixture.Handler, "schedule.list", new { from, to, libraryOnly = true });
        return window;
    }

    [Fact]
    public async Task MalRoundTripUpdatesTheExistingAniListEntryWithoutCreatingADuplicate()
    {
        var fixture = new Fixture([new(Anime.Id, MediaType.Anime, MediaListStatus.Current)
        {
            Progress = 3, Notes = "Local note", Review = "Local review", Tags = ["Favourite"]
        }]);
        var export = await Call(fixture.Handler, "library.export", new { format = "malXml" });
        var xml = export.GetProperty("text").GetString()!.Replace("<my_watched_episodes>3", "<my_watched_episodes>7");
        var preview = await Call(fixture.Handler, "library.import.preview", new { xml });
        var item = Assert.Single(preview.GetProperty("items").EnumerateArray());
        Assert.Equal(Anime.Id, item.GetProperty("mediaId").GetString());
        Assert.Equal(Anime.Title.DisplayTitle, item.GetProperty("media").GetProperty("title").GetString());
        Assert.Equal(3, (await fixture.Library.GetEntryAsync(Anime.Id)).Value!.Progress);
        await Call(fixture.Handler, "library.import.apply", new { xml });
        await Call(fixture.Handler, "library.import.apply", new { xml });
        var entry = Assert.Single((await fixture.Library.GetEntriesAsync(new UserLibraryQuery())).Value!.Items);
        Assert.Equal(Anime.Id, entry.MediaId);
        Assert.Equal(7, entry.Progress);
        Assert.Equal("Local note", entry.Notes);
        Assert.Equal("Local review", entry.Review);
        Assert.Equal("Favourite", Assert.Single(entry.Tags));
    }

    [Fact]
    public async Task MalImportUsesXmlTitlesForAnEmptyOfflineLibrary()
    {
        const string xml = "<myanimelist><anime><series_animedb_id>999</series_animedb_id><series_title>История &amp; звёзды</series_title><series_episodes>12</series_episodes><my_watched_episodes>2</my_watched_episodes></anime></myanimelist>";
        var fixture = new Fixture();
        var preview = await Call(fixture.Handler, "library.import.preview", new { xml });
        Assert.Equal("История & звёзды", Assert.Single(preview.GetProperty("items").EnumerateArray()).GetProperty("media").GetProperty("title").GetString());
        Assert.Empty((await fixture.Library.GetEntriesAsync(new UserLibraryQuery())).Value!.Items);
        await Call(fixture.Handler, "library.import.apply", new { xml });
        var listed = await Call(fixture.Handler, "library.list", new { });
        var media = Assert.Single(listed.GetProperty("items").EnumerateArray()).GetProperty("media");
        Assert.Equal("История & звёзды", media.GetProperty("title").GetString());
        Assert.Equal(12, media.GetProperty("totalUnits").GetInt32());
    }

    [Fact]
    public async Task MalImportMetadataIsPersistedBeforeEntriesAndAvailableAfterRestart()
    {
        const string xml = "<myanimelist><manga><manga_mangadb_id>999</manga_mangadb_id><manga_title>Local manga</manga_title></manga></myanimelist>";
        var library = new InMemoryUserLibraryService();
        var saved = new Dictionary<string, MediaItem>();
        var staged = new Dictionary<string, MediaItem>();
        var failOnce = true;
        var handler = new BridgeHandler(new UnexpectedCatalog(), library, id => staged.GetValueOrDefault(id), persistImportedMedia: async (items, ct) =>
        {
            Assert.Empty((await library.GetEntriesAsync(new UserLibraryQuery(), ct)).Value!.Items);
            foreach (var item in items) staged[item.Id] = item;
            if (failOnce) { failOnce = false; throw new IOException("Test cache unavailable"); }
            foreach (var item in staged.Values) saved[item.Id] = item;
        });
        await Call(handler, "library.import.preview", new { xml });
        Assert.Empty(saved);
        Assert.Empty(staged);
        var error = await Error(handler, "library.import.apply", new { xml });
        Assert.Equal("storage_error", error.GetProperty("code").GetString());
        Assert.Contains("Your library has not changed", error.GetProperty("message").GetString());
        Assert.Empty((await library.GetEntriesAsync(new UserLibraryQuery())).Value!.Items);
        await Call(handler, "library.import.apply", new { xml });
        var restarted = new BridgeHandler(new UnexpectedCatalog(), library, id => saved.GetValueOrDefault(id));
        var entry = await Call(restarted, "library.get", new { mediaId = "mal:manga:999" });
        Assert.Equal("Local manga", entry.GetProperty("media").GetProperty("title").GetString());
    }

    [Fact]
    public async Task MalIdentityIncludesMediaTypeWhenAnimeAndMangaShareTheSameNumber()
    {
        const string xml = "<myanimelist><anime><series_animedb_id>10</series_animedb_id><my_watched_episodes>7</my_watched_episodes></anime><manga><manga_mangadb_id>10</manga_mangadb_id><my_read_chapters>9</my_read_chapters></manga></myanimelist>";
        var fixture = new Fixture([
            new(Anime.Id, MediaType.Anime, MediaListStatus.Planning),
            new(Manga.Id, MediaType.Manga, MediaListStatus.Planning)
        ], [Anime, Manga with { MalId = 10 }]);
        await Call(fixture.Handler, "library.import.apply", new { xml });
        Assert.Equal(2, (await fixture.Library.GetEntriesAsync(new UserLibraryQuery())).Value!.Items.Count);
        Assert.Equal(7, (await fixture.Library.GetEntryAsync(Anime.Id)).Value!.Progress);
        Assert.Equal(9, (await fixture.Library.GetEntryAsync(Manga.Id)).Value!.Progress);
    }

    [Fact]
    public async Task XmlPreviewIsReadOnlyAndApplyPreservesLocalNotesOrganizationAndReview()
    {
        const string xml = "<myanimelist>\n<anime><series_animedb_id>10</series_animedb_id><my_watched_episodes>7</my_watched_episodes><my_score>8</my_score><my_status>Completed</my_status></anime>\n</myanimelist>";
        var fixture = new Fixture([new("mal:anime:10", MediaType.Anime, MediaListStatus.Current)
        {
            Progress = 1, Notes = "Local notes", Review = "Local review", Tags = ["Favourite"],
            PosterUri = new("https://example.test/saved.jpg")
        }]);
        var preview = await Call(fixture.Handler, "library.import.preview", new { xml });
        Assert.Equal(1, preview.GetProperty("count").GetInt32());
        Assert.Equal(1, (await fixture.Library.GetEntryAsync("mal:anime:10")).Value!.Progress);
        var apply = await Call(fixture.Handler, "library.import.apply", new { xml });
        Assert.Equal(1, apply.GetProperty("importedCount").GetInt32());
        var entry = (await fixture.Library.GetEntryAsync("mal:anime:10")).Value!;
        Assert.Equal(7, entry.Progress);
        Assert.Equal(MediaListStatus.Completed, entry.Status);
        Assert.Equal("Local notes", entry.Notes);
        Assert.Equal("Local review", entry.Review);
        Assert.Equal("Favourite", Assert.Single(entry.Tags));
        Assert.NotNull(entry.PosterUri);
        var export = await Call(fixture.Handler, "library.export", new { format = "malXml" });
        Assert.Equal(1, export.GetProperty("exportedCount").GetInt32());
        Assert.Contains("<my_watched_episodes>7</my_watched_episodes>", export.GetProperty("text").GetString());
    }

    [Fact]
    public async Task TraceRecognitionRanksDeduplicatesAndSavesBestWithoutLocalPaths()
    {
        var fixture = new Fixture();
        var result = await Call(fixture.Handler, "recognition.run", new
        {
            provider = "trace",
            mimeType = "image/png",
            imageBase64 = PngBase64,
            sourceName = "Screenshot.png",
            sourcePath = "/private/do-not-read.png",
            uploadConfirmed = true
        });
        Assert.False(fixture.Settings.Value.RecognitionServicesEnabled);
        var matches = result.GetProperty("matches").EnumerateArray().ToArray();
        Assert.Equal(2, matches.Length);
        Assert.Equal("anilist:1", matches[0].GetProperty("mediaId").GetString());
        Assert.Equal(0.99, matches[0].GetProperty("similarity").GetDouble());
        Assert.NotNull(result.GetProperty("savedAttemptId").GetString());
        var history = await Call(fixture.Handler, "recognition.history", new { });
        var saved = Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal("Screenshot.png", saved.GetProperty("sourceName").GetString());
        Assert.False(saved.TryGetProperty("sourcePath", out _));
        Assert.Null(fixture.Inbox.Attempts.Single().SourcePath);
        await Call(fixture.Handler, "recognition.remove", new { id = saved.GetProperty("id").GetString() });
        Assert.Empty(fixture.Inbox.Attempts);
    }

    [Theory]
    [InlineData("invalid", "image/png")]
    [InlineData("PHN2Zz48L3N2Zz4=", "image/png")]
    [InlineData("iVBORw0KGgo=", "image/svg+xml")]
    [InlineData("iVBORw0KGgo=", "image/jpeg")]
    public async Task RecognitionRejectsInvalidBase64MimeOrSignature(string imageBase64, string mimeType)
    {
        var fixture = new Fixture();
        fixture.Settings.Value = fixture.Settings.Value with { RecognitionServicesEnabled = true };
        var error = await Error(fixture.Handler, "recognition.run", new { provider = "trace", imageBase64, mimeType, uploadConfirmed = true });
        Assert.Equal("validation_error", error.GetProperty("code").GetString());
        Assert.Empty(fixture.Inbox.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitUploadConfirmationIsRequiredRegardlessOfLegacyPreference(bool legacyEnabled)
    {
        var fixture = new Fixture();
        fixture.Settings.Value = fixture.Settings.Value with { RecognitionServicesEnabled = legacyEnabled };
        var denied = await Error(fixture.Handler, "recognition.run", new { provider = "art", imageBase64 = PngBase64, mimeType = "image/png" });
        Assert.Equal("validation_error", denied.GetProperty("code").GetString());
        var notConfirmed = await Error(fixture.Handler, "recognition.run", new { provider = "art", imageBase64 = PngBase64, mimeType = "image/png", uploadConfirmed = false });
        Assert.Equal("validation_error", notConfirmed.GetProperty("code").GetString());
        var result = await Call(fixture.Handler, "recognition.run", new { provider = "art", imageBase64 = PngBase64, mimeType = "image/png", uploadConfirmed = true });
        var match = Assert.Single(result.GetProperty("matches").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, match.GetProperty("sourceUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("savedAttemptId").ValueKind);
        Assert.Equal(legacyEnabled, fixture.Settings.Value.RecognitionServicesEnabled);
    }

    [Fact]
    public async Task RecognitionSaveRejectsNonNumericSimilarityAsValidation()
    {
        var fixture = new Fixture();
        var error = await Error(fixture.Handler, "recognition.save", new { provider = "trace", title = "Title", similarity = "high" });
        Assert.Equal("validation_error", error.GetProperty("code").GetString());
        Assert.Empty(fixture.Inbox.Attempts);
    }

    [Fact]
    public async Task RecognitionHistoryDoesNotExposeLegacyLocalImagePaths()
    {
        var fixture = new Fixture();
        fixture.Inbox.Attempts.Add(new() { EnglishTitle = "Old result", SourcePath = "/private/source.png", ThumbnailUri = new("file:///private/image.png") });
        var result = await Call(fixture.Handler, "recognition.history", new { });
        Assert.DoesNotContain("/private/", result.GetRawText());
    }

    [Fact]
    public async Task RelationsAndUpdateChecksPreserveMeaningfulMetadata()
    {
        var fixture = new Fixture();
        var related = await Call(fixture.Handler, "media.related", new { mediaId = Anime.Id });
        Assert.True(related.GetProperty("isStale").GetBoolean());
        Assert.Equal("Saved offline", related.GetProperty("notice").GetString());
        Assert.Equal("Sequel", Assert.Single(related.GetProperty("titles").EnumerateArray()).GetProperty("relation").GetString());
        var update = await Call(fixture.Handler, "updates.check", new { });
        Assert.True(update.GetProperty("isUpdateAvailable").GetBoolean());
        Assert.Equal("https://example.test/releases/1", update.GetProperty("latestRelease").GetProperty("releaseUrl").GetString());
    }

    private const string PngBase64 = "iVBORw0KGgo=";

    private static async Task<JsonElement> Call(BridgeHandler handler, string method, object parameters)
    {
        using var document = JsonDocument.Parse(await handler.HandleAsync(JsonSerializer.Serialize(new { id = "feature", method, @params = parameters })));
        Assert.True(document.RootElement.TryGetProperty("result", out var result), document.RootElement.GetRawText());
        return result.Clone();
    }

    private static async Task<JsonElement> Error(BridgeHandler handler, string method, object parameters)
    {
        using var document = JsonDocument.Parse(await handler.HandleAsync(JsonSerializer.Serialize(new { id = "feature", method, @params = parameters })));
        Assert.True(document.RootElement.TryGetProperty("error", out var error), document.RootElement.GetRawText());
        return error.Clone();
    }

    private sealed class Fixture
    {
        public readonly InMemoryUserLibraryService Library;
        public readonly SettingsFake Settings = new();
        public readonly InboxFake Inbox = new();
        public readonly NotificationsFake Notifications = new();
        public readonly ServiceRegistry Services = new();
        public readonly BridgeHandler Handler;

        public Fixture(UserMediaEntry[]? entries = null, MediaItem[]? media = null)
        {
            Library = new(entries ?? []);
            var items = media ?? [Anime, Manga];
            Services.Items[typeof(IAppSettingsService)] = Settings;
            Services.Items[typeof(IRecognitionInboxService)] = Inbox;
            Services.Items[typeof(IRecognitionService)] = new TraceFake();
            Services.Items[typeof(IArtRecognitionService)] = new ArtFake();
            Services.Items[typeof(INotificationService)] = Notifications;
            Services.Items[typeof(IMediaRelationsService)] = new RelationsFake();
            Services.Items[typeof(IAppUpdateService)] = new UpdatesFake();
            Handler = new(new InMemoryMediaCatalogService(items), Library,
                id => items.FirstOrDefault(item => item.Id == id), services: Services);
        }
    }

    private sealed class ServiceRegistry : IServiceProvider
    {
        public Dictionary<Type, object> Items { get; } = [];
        public object? GetService(Type serviceType) => Items.GetValueOrDefault(serviceType);
    }

    private sealed class UnexpectedCatalog : IMediaCatalogService
    {
        public Task<Result<PagedResult<MediaItem>>> SearchAsync(MediaSearchQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Lookup must not query the remote catalog.");
        public Task<Result<MediaItem>> GetByIdAsync(string mediaId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Lookup must not query the remote catalog.");
    }

    private sealed class SettingsFake : IAppSettingsService
    {
        public AppSettings Value { get; set; } = new();
        public Task<Result<AppSettings>> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<AppSettings>.Success(Value));
        public Task<Result<AppSettings>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        { Value = settings; return Task.FromResult(Result<AppSettings>.Success(Value)); }
    }

    private sealed class AuthFake(Action? duringAuth = null, bool failure = false) : IAniListAuthService
    {
        public Task<Result<AniListAuthResult>> AuthorizeAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default)
        {
            duringAuth?.Invoke();
            return Task.FromResult(failure ? Result<AniListAuthResult>.Failure(ApplicationError.Validation("HTTP payload secret=secret"))
                : Result<AniListAuthResult>.Success(new("new-access-token", "User", 42)));
        }
    }

    private sealed class SyncFake : IAniListSyncService
    {
        public Task<Result<int>> SyncAsync(string accessToken, int userId, IUserLibraryService libraryService, CancellationToken cancellationToken = default)
        {
            Assert.Equal("saved-private-token", accessToken);
            Assert.Equal(42, userId);
            return libraryService.MergeEntriesAsync([new("anilist:1", MediaType.Anime, MediaListStatus.Completed)], cancellationToken);
        }
    }

    private sealed class ScheduleFake(IReadOnlyList<AiringScheduleItem> items) : IAiringScheduleService
    {
        public Task<Result<IReadOnlyList<AiringScheduleItem>>> GetUpcomingAsync(DateTimeOffset from, DateTimeOffset to, int pageSize = 20, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<AiringScheduleItem>>.Success(items));
    }

    private sealed class NotificationsFake : INotificationService
    {
        public readonly List<string> Messages = [];
        public readonly Queue<bool> Outcomes = new();
        public bool Supported { get; set; } = true;
        public NotificationSupport Support => new(Supported, Supported ? null : "System reminders are not supported on Windows in this build.");
        public int Attempts { get; private set; }
        public Action? BeforeSend { get; set; }
        public Task<bool> NotifyAsync(string title, string message, CancellationToken cancellationToken = default)
        {
            Attempts++;
            BeforeSend?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (Outcomes.TryDequeue(out var accepted) && !accepted) return Task.FromResult(false);
            Messages.Add(message);
            return Task.FromResult(true);
        }
    }

    private sealed class InboxFake : IRecognitionInboxService
    {
        public readonly List<RecognitionAttempt> Attempts = [];
        public Task<Result<IReadOnlyList<RecognitionAttempt>>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<RecognitionAttempt>>.Success(Attempts.Take(limit).ToArray()));
        public Task<Result<RecognitionAttempt>> SaveAsync(RecognitionAttempt attempt, CancellationToken cancellationToken = default)
        { Attempts.Add(attempt); return Task.FromResult(Result<RecognitionAttempt>.Success(attempt)); }
        public Task<Result<bool>> RemoveAsync(string attemptId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(Attempts.RemoveAll(attempt => attempt.Id == attemptId) > 0));
    }

    private sealed class TraceFake : IRecognitionService
    {
        public Task<Result<IReadOnlyList<RecognitionMatch>>> RecognizeAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<RecognitionMatch>>.Success([
                new(1, "First", null, null, "3", 0.8, null),
                new(2, "Second", null, null, null, 0.9, null),
                new(1, "First", null, null, "3", 0.99, null)
            ]));
    }

    private sealed class ArtFake : IArtRecognitionService
    {
        public Task<Result<IReadOnlyList<ArtRecognitionMatch>>> RecognizeAsync(Stream imageStream, string mimeType, string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<ArtRecognitionMatch>>.Success([new(0.98, "Art", "Author", new("file:///private/secret"), null)]));
    }

    private sealed class RelationsFake : IMediaRelationsService
    {
        public Task<Result<RelatedTitlesResult>> GetRelatedTitlesAsync(string mediaId, bool forceRefresh = false, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<RelatedTitlesResult>.Success(new([
                new("anilist:3", "Sequel", MediaType.Anime, MediaFormat.Tv, MediaRelationKind.Sequel, "AniList", "SEQUEL")
            ], "Saved offline", true)));
    }

    private sealed class UpdatesFake : IAppUpdateService
    {
        public Task<Result<AppUpdateCheckResult>> CheckForUpdatesAsync(Version currentVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AppUpdateCheckResult>.Success(new(currentVersion,
                new(new(1, 0, 0), "v1.0.0", "New release", new("https://example.test/releases/1"), null))));
    }
}
