using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TanaHub.Application.Services;
using TanaHub.Infrastructure.Catalog;
using TanaHub.Infrastructure.Library;
using TanaHub.Infrastructure.Schedule;
using TanaHub.Infrastructure.Settings;
using TanaHub.Infrastructure.Notifications;
using TanaHub.Infrastructure.Recognition;
using TanaHub.Infrastructure.Sync;
using TanaHub.Infrastructure.Updates;

namespace TanaHub.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTanaHubInfrastructure(this IServiceCollection services)
    {
        var dataDirectory = GetDataDirectory(Environment.GetEnvironmentVariable("TANAHUB_DATA_DIRECTORY"));
        services.AddSingleton<InMemoryMediaCatalogService>();
        services.AddSingleton<OfflineCatalogCache>(provider =>
        {
            var cache = new OfflineCatalogCache(Path.Combine(dataDirectory, "catalog_cache.json"));
            cache.LoadAsync().GetAwaiter().GetResult();
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
            return new MangaDexMediaCatalogService(CreateMangaDexHttpClient());
        });
        services.AddSingleton<RoutedMediaCatalogService>(provider => new RoutedMediaCatalogService(
            provider.GetRequiredService<AniListMediaCatalogService>(),
            provider.GetRequiredService<MangaDexMediaCatalogService>()));
        services.AddSingleton<IMediaCatalogService>(provider =>
            provider.GetRequiredService<RoutedMediaCatalogService>());
        services.AddSingleton<ICatalogSourceSelector>(provider =>
            provider.GetRequiredService<RoutedMediaCatalogService>());
        services.AddSingleton<AniListMediaRelationsService>(_ =>
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TanaHub/0.1");
            return new AniListMediaRelationsService(httpClient);
        });
        services.AddSingleton<MangaDexMediaRelationsService>(provider =>
        {
            return new MangaDexMediaRelationsService(CreateMangaDexHttpClient(), provider.GetRequiredService<AniListMediaRelationsService>());
        });
        services.AddSingleton<IMediaRelationsService>(provider => new CachedMediaRelationsService(
            provider.GetRequiredService<AniListMediaRelationsService>(),
            provider.GetRequiredService<MangaDexMediaRelationsService>(),
            Path.Combine(dataDirectory, "relations_cache.json")));
        services.AddSingleton<IDbContextFactory<LibraryDbContext>>(_ =>
        {
            var options = new DbContextOptionsBuilder<LibraryDbContext>()
                .UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = Path.Combine(dataDirectory, "library.db")
                }.ToString())
                .Options;
            return new SimpleLibraryDbContextFactory(options);
        });
        services.AddSingleton<IUserLibraryService>(provider =>
        {
            var factory = provider.GetRequiredService<IDbContextFactory<LibraryDbContext>>();
            Directory.CreateDirectory(dataDirectory);
            return CreateUserLibraryService(factory, Path.Combine(dataDirectory, "library.json"));
        });
        services.AddSingleton<IAppSettingsService>(_ => new FileAppSettingsService(
            Path.Combine(dataDirectory, "settings.json")));
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
            Path.Combine(dataDirectory, "recognition_inbox.json")));
        services.AddSingleton<IRecognitionService>(_ => new TraceMoeService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));
        services.AddSingleton<IArtRecognitionService>(_ => new SauceNaoService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));

        services.AddSingleton<IAniListAuthService>(_ => new AniListOAuthService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(15) }));

        services.AddSingleton<IAniListSyncService>(_ => new AniListSyncService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));

        services.AddSingleton<IAppUpdateService>(_ =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.github.com/"),
                Timeout = TimeSpan.FromSeconds(10)
            };

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TanaHub/0.1");
            return new GitHubReleaseUpdateService(httpClient, "Komortes", "TanaHub");
        });

        return services;
    }

    private static HttpClient CreateMangaDexHttpClient()
    {
        var client = OperatingSystem.IsMacOS() ? new HttpClient(new MangaDexCurlHandler()) : new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TanaHub/0.1");
        return client;
    }

    internal static string GetDataDirectory(string? configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            if (!Path.IsPathFullyQualified(configuredDirectory))
            {
                throw new ArgumentException("TANAHUB_DATA_DIRECTORY must be an absolute path.", nameof(configuredDirectory));
            }

            return Path.GetFullPath(configuredDirectory);
        }
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TanaHub");
    }

    private sealed class SimpleLibraryDbContextFactory(DbContextOptions<LibraryDbContext> options)
        : IDbContextFactory<LibraryDbContext>
    {
        public LibraryDbContext CreateDbContext() => new(options);

        public Task<LibraryDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    internal static SqliteUserLibraryService CreateUserLibraryService(
        IDbContextFactory<LibraryDbContext> factory, string legacyJsonPath)
    {
        using var db = factory.CreateDbContext();
        db.Database.Migrate();
        SqliteUserLibraryService.ImportFromJsonIfNeededAsync(db, legacyJsonPath)
            .GetAwaiter().GetResult();
        return new SqliteUserLibraryService(factory);
    }
}
