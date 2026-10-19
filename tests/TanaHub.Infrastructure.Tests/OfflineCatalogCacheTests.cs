using System.Collections;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Catalog;

namespace TanaHub.Infrastructure.Tests;

public sealed class OfflineCatalogCacheTests
{
    [Fact]
    public async Task LoadAsync_CompletesStartupBridgeWithoutPostingToUiContext()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TanaHub.Tests", Guid.NewGuid().ToString("N"));
        var cachePath = Path.Combine(directory, "catalog_cache.json");
        try
        {
            var original = new OfflineCatalogCache(cachePath);
            original.Put(CreateAnime("anilist:1") with { Description = new string('a', 1024 * 1024) });
            await original.FlushAsync();
            var reloaded = new OfflineCatalogCache(cachePath);
            var previousContext = SynchronizationContext.Current;
            var context = new RecordingSynchronizationContext();
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
#pragma warning disable xUnit1031 // Reproduce the synchronous DI startup bridge with a nonblocking test context.
                reloaded.LoadAsync().GetAwaiter().GetResult();
#pragma warning restore xUnit1031
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }

            Assert.Equal(0, context.PostCount);
            Assert.True(reloaded.TryGet("anilist:1", out _));
            reloaded.Put(CreateAnime("anilist:2"));
            await reloaded.FlushAsync();
            var restarted = new OfflineCatalogCache(cachePath);
            await restarted.LoadAsync();
            Assert.True(restarted.TryGet("anilist:1", out _));
            Assert.True(restarted.TryGet("anilist:2", out _));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FlushAsync_RetriesUnpersistedChangesAfterWriteFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TanaHub.Tests", Guid.NewGuid().ToString("N"));
        var cachePath = Path.Combine(directory, "catalog_cache.json");
        Directory.CreateDirectory(cachePath);
        try
        {
            var cache = new OfflineCatalogCache(cachePath);
            cache.Put(CreateAnime("anilist:1"));
            await Assert.ThrowsAnyAsync<IOException>(() => cache.FlushAsync());
            Directory.Delete(cachePath);

            await cache.FlushAsync();
            var reloaded = new OfflineCatalogCache(cachePath);
            await reloaded.LoadAsync();
            Assert.True(reloaded.TryGet("anilist:1", out _));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FlushAsync_PersistsItemsAddedWhileEarlierSnapshotIsBeingWritten()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TanaHub.Tests", Guid.NewGuid().ToString("N"));
        var cachePath = Path.Combine(directory, "catalog_cache.json");
        using var snapshotBlocked = new ManualResetEventSlim();
        using var releaseSnapshot = new ManualResetEventSlim();
        var cache = new OfflineCatalogCache(cachePath);
        cache.Put(CreateAnime("anilist:1") with
        {
            Genres = new BlockingGenres(snapshotBlocked, releaseSnapshot)
        });

        var firstFlush = Task.Run(() => cache.FlushAsync());
        Task secondFlush = Task.CompletedTask;
        try
        {
            Assert.True(snapshotBlocked.Wait(TimeSpan.FromSeconds(5)));
            cache.Put(CreateAnime("anilist:2"));
            secondFlush = cache.FlushAsync();
            releaseSnapshot.Set();
            await Task.WhenAll(firstFlush, secondFlush);

            var reloaded = new OfflineCatalogCache(cachePath);
            await reloaded.LoadAsync();
            Assert.True(reloaded.TryGet("anilist:1", out _));
            Assert.True(reloaded.TryGet("anilist:2", out _));
        }
        finally
        {
            releaseSnapshot.Set();
            await Task.WhenAll(firstFlush, secondFlush);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static Anime CreateAnime(string id) => new(
        id, new MediaTitle("Cached title"), MediaFormat.Tv, MediaReleaseStatus.Releasing);

    private sealed class BlockingGenres(ManualResetEventSlim entered, ManualResetEventSlim release)
        : IReadOnlyList<string>
    {
        public int Count => 1;
        public string this[int index] => "Action";
        public IEnumerator<string> GetEnumerator()
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Snapshot was not released.");
            yield return "Action";
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int postCount;
        public int PostCount => Volatile.Read(ref postCount);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            Interlocked.Increment(ref postCount);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }
}
