using Microsoft.Extensions.DependencyInjection;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Catalog;
using TanaHub.Infrastructure.Library;
using TanaHub.Infrastructure.Schedule;
using TanaHub.Infrastructure.Settings;

namespace TanaHub.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTanaHubInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryMediaCatalogService>();
        services.AddSingleton<IMediaCatalogService>(provider =>
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TanaHub/0.1");

            return new AniListMediaCatalogService(
                httpClient,
                provider.GetRequiredService<InMemoryMediaCatalogService>());
        });
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

    private static IReadOnlyList<UserMediaEntry> CreateSeedLibraryEntries()
    {
        return
        [
            new UserMediaEntry("anilist:154587", MediaType.Anime, MediaListStatus.Current)
            {
                Progress = 9,
                Score = 9
            },
            new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Completed)
            {
                Progress = 26,
                Score = 10
            },
            new UserMediaEntry("mangadex:berserk", MediaType.Manga, MediaListStatus.Paused)
            {
                Progress = 373,
                Score = 10
            }
        ];
    }
}
