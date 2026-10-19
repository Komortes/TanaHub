using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Catalog;

/// <summary>Routes direct relations and keeps a bounded cache separate from catalog search snippets.</summary>
internal sealed class CachedMediaRelationsService(
    IMediaRelationsService aniList,
    IMediaRelationsService mangaDex,
    string cachePath,
    TimeProvider? timeProvider = null) : IMediaRelationsService
{
    internal const int MaxCacheEntries = 250;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private bool loaded;
    private volatile bool offlineCacheEnabled = true;

    public void SetOfflineCacheEnabled(bool enabled) => offlineCacheEnabled = enabled;

    public async Task<Result<RelatedTitlesResult>> GetRelatedTitlesAsync(
        string mediaId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CacheEntry? cached = null;
        if (offlineCacheEnabled)
        {
            await cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await LoadAsync(cancellationToken).ConfigureAwait(false);
                entries.TryGetValue(mediaId, out cached);
                var age = cached is null ? TimeSpan.MaxValue : clock.GetUtcNow() - cached.FetchedAt;
                if (offlineCacheEnabled && !forceRefresh && cached is not null && age >= TimeSpan.Zero && age < CacheLifetime)
                    return Result<RelatedTitlesResult>.Success(new(cached.Titles));
            }
            finally
            {
                cacheLock.Release();
            }
        }

        var provider = mediaId.StartsWith("mangadex:", StringComparison.OrdinalIgnoreCase) ? mangaDex : aniList;
        Result<RelatedTitlesResult> result;
        try
        {
            result = await provider.GetRelatedTitlesAsync(mediaId, forceRefresh, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException ||
                                   ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            result = Result<RelatedTitlesResult>.Failure(ApplicationError.ExternalService("Related titles could not be loaded. Try again later."));
        }
        cancellationToken.ThrowIfCancellationRequested();

        if (result.IsFailure)
        {
            if (offlineCacheEnabled && cached is not null && result.Error.Code == "external_service_error")
                return Result<RelatedTitlesResult>.Success(new(cached.Titles,
                    "Showing saved related titles. The source is currently unavailable.", IsStale: true));
            return result;
        }

        var value = result.Value!;
        // Partial responses must remain retryable and must not replace a complete snapshot.
        if (offlineCacheEnabled && !value.IsStale && string.IsNullOrWhiteSpace(value.Notice))
        {
            await cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (offlineCacheEnabled)
                {
                    await LoadAsync(cancellationToken).ConfigureAwait(false);
                    entries[mediaId] = new(mediaId, clock.GetUtcNow(), value.Titles.ToArray());
                    TrimEntries();
                    await PersistAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                cacheLock.Release();
            }
        }

        return result;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (loaded) return;
        try
        {
            if (File.Exists(cachePath))
            {
                await using var stream = File.OpenRead(cachePath);
                var saved = await JsonSerializer.DeserializeAsync<CacheEntry[]>(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                foreach (var entry in saved ?? [])
                {
                    if (entry is null || string.IsNullOrWhiteSpace(entry.MediaId) || entry.Titles is null ||
                        entry.Titles.Any(title => title is null || string.IsNullOrWhiteSpace(title.MediaId) || string.IsNullOrWhiteSpace(title.Title)))
                        continue;
                    entries[entry.MediaId] = entry;
                }
                TrimEntries();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A cache is optional: unreadable or outdated files must not stop live requests.
        }
        loaded = true;
    }

    private void TrimEntries()
    {
        foreach (var key in entries.Values.OrderByDescending(entry => entry.FetchedAt)
                     .Skip(MaxCacheEntries).Select(entry => entry.MediaId).ToArray())
            entries.Remove(key);
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = cachePath + ".tmp";
            await using (var stream = File.Create(temporaryPath))
                await JsonSerializer.SerializeAsync(stream, entries.Values.ToArray(), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, cachePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed write leaves the previous file intact and does not hide a successful request.
        }
    }

    private sealed record CacheEntry(string MediaId, DateTimeOffset FetchedAt, RelatedTitle[] Titles);
}
