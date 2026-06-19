using Microsoft.Extensions.DependencyInjection;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Catalog;
using TanaHub.Infrastructure.Library;
using TanaHub.Infrastructure.Schedule;
using TanaHub.Infrastructure.Settings;
using TanaHub.Infrastructure.Notifications;
using TanaHub.Infrastructure.Recognition;
using TanaHub.Infrastructure.Sync;

namespace TanaHub.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTanaHubInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryMediaCatalogService>();
        services.AddSingleton<OfflineCatalogCache>(provider =>
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var cache = new OfflineCatalogCache(Path.Combine(appData, "TanaHub", "catalog_cache.json"));
            Task.Run(() => cache.LoadAsync());
            return cache;
        });
        services.AddSingleton<AniListMediaCatalogService>(provider =>
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TanaHub/0.1");
            return new AniListMediaCatalogService(
                httpClient,
                provider.GetRequiredService<InMemoryMediaCatalogService>(),
                provider.GetRequiredService<OfflineCatalogCache>());
        });
        services.AddSingleton<MangaDexMediaCatalogService>(_ =>
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TanaHub/0.1");
            return new MangaDexMediaCatalogService(httpClient);
        });
        services.AddSingleton<RoutedMediaCatalogService>(provider => new RoutedMediaCatalogService(
            provider.GetRequiredService<AniListMediaCatalogService>(),
            provider.GetRequiredService<MangaDexMediaCatalogService>()));
        services.AddSingleton<IMediaCatalogService>(provider =>
            provider.GetRequiredService<RoutedMediaCatalogService>());
        services.AddSingleton<ICatalogSourceSelector>(provider =>
            provider.GetRequiredService<RoutedMediaCatalogService>());
        services.AddSingleton<IUserLibraryService>(_ => new FileUserLibraryService(
            GetDefaultLibraryPath(),
            CreateSeedLibraryEntries()));
        services.AddSingleton<IAppSettingsService>(_ => new FileAppSettingsService(
            GetDefaultSettingsPath()));
        services.AddSingleton<IAiringScheduleService>(_ =>
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TanaHub/0.1");
            return new AniListAiringScheduleService(httpClient);
        });

        services.AddSingleton<INotificationService, OsNotificationService>();
        services.AddSingleton<IRecognitionInboxService>(_ => new FileRecognitionInboxService(
            GetDefaultRecognitionInboxPath()));
        services.AddSingleton<IRecognitionService>(_ => new TraceMoeService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));

        services.AddSingleton<IAniListAuthService>(_ => new AniListOAuthService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(15) }));

        services.AddSingleton<IAniListSyncService>(_ => new AniListSyncService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));

        return services;
    }

    private static string GetDefaultLibraryPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TanaHub", "library.json");
    }

    private static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TanaHub", "settings.json");
    }

    private static string GetDefaultRecognitionInboxPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TanaHub", "recognition_inbox.json");
    }

    private static IReadOnlyList<UserMediaEntry> CreateSeedLibraryEntries()
    {
        return
        [
            new UserMediaEntry("anilist:154587", MediaType.Anime, MediaListStatus.Current)
            {
                Progress = 9,
                Score = 9,
                Tags = ["seasonal", "action"],
                CustomLists = ["Current cour"]
            },
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Completed)
            {
                Progress = 26,
                Score = 10,
                Tags = ["classic", "rewatch"],
                CustomLists = ["Favorites"]
            },
            new UserMediaEntry("mangadex:berserk", MediaType.Manga, MediaListStatus.Paused)
            {
                Progress = 373,
                Score = 10,
                Tags = ["dark fantasy"],
                CustomLists = ["Long reads"]
            }
        ];
    }
}
