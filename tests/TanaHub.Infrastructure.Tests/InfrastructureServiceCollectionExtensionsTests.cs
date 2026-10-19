using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Catalog;
using TanaHub.Infrastructure.DependencyInjection;
using TanaHub.Infrastructure.Library;

namespace TanaHub.Infrastructure.Tests;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void GetDataDirectory_RequiresAnAbsoluteOverride()
    {
        Assert.Throws<ArgumentException>(() => InfrastructureServiceCollectionExtensions.GetDataDirectory("relative-directory"));
        var absolute = Path.Combine(Path.GetTempPath(), "TanaHub.Tests");
        Assert.Equal(Path.GetFullPath(absolute), InfrastructureServiceCollectionExtensions.GetDataDirectory(absolute));
    }

    [Fact]
    public void CreateUserLibraryService_DoesNotCaptureUiContextDuringLegacyImport()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TanaHub.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "library.db")};Pooling=False").Options);
        var legacyJsonPath = Path.Combine(directory, "library.json");
        File.WriteAllText(legacyJsonPath, """
            [{"MediaId":"anilist:20","MediaType":"Anime","Status":"Current","Progress":4,"Score":8,"PosterUri":null,"Notes":null,"Review":null,"Tags":[],"CustomLists":[],"StartedAt":null,"CompletedAt":null,"UpdatedAt":"2026-01-01T00:00:00+00:00"}]
            """ + new string(' ', 1024 * 1024));
        var originalContext = SynchronizationContext.Current;
        var context = new RecordingSynchronizationContext();
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);

            InfrastructureServiceCollectionExtensions.CreateUserLibraryService(factory, legacyJsonPath);

            Assert.Equal(0, context.PostCount);
            using var db = factory.CreateDbContext();
            Assert.Equal("anilist:20", Assert.Single(db.Entries).MediaId);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateUserLibraryService_KeepsNewAndClearedLibraryEmptyAcrossRestarts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TanaHub.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "library.db")};Pooling=False").Options);
        var legacyJsonPath = Path.Combine(directory, "library.json");
        try
        {
            var library = InfrastructureServiceCollectionExtensions.CreateUserLibraryService(factory, legacyJsonPath);
            Assert.Empty((await library.GetEntriesAsync(new UserLibraryQuery())).Value!.Items);
            Assert.True((await library.UpsertEntryAsync(new UserMediaEntry("anilist:1", MediaType.Anime, MediaListStatus.Current))).IsSuccess);
            Assert.True((await library.RemoveEntryAsync("anilist:1")).Value);

            var restarted = InfrastructureServiceCollectionExtensions.CreateUserLibraryService(factory, legacyJsonPath);

            Assert.Empty((await restarted.GetEntriesAsync(new UserLibraryQuery())).Value!.Items);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AddTanaHubInfrastructure_RegistersApplicationServices()
    {
        var services = new ServiceCollection();

        services.AddTanaHubInfrastructure();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMediaCatalogService)
            && descriptor.ImplementationFactory is not null
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(InMemoryMediaCatalogService)
            && descriptor.ImplementationType == typeof(InMemoryMediaCatalogService)
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IUserLibraryService)
            && descriptor.ImplementationFactory is not null
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IArtRecognitionService)
            && descriptor.ImplementationFactory is not null
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    private sealed class TestDbContextFactory(DbContextOptions<LibraryDbContext> options)
        : IDbContextFactory<LibraryDbContext>
    {
        public LibraryDbContext CreateDbContext() => new(options);
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int postCount;
        public int PostCount => Volatile.Read(ref postCount);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            Interlocked.Increment(ref postCount);
            // Allow a captured continuation to finish so a regression fails instead of hanging tests.
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }
}
