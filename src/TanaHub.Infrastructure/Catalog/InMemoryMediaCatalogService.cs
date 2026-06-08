using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Catalog;

public sealed class InMemoryMediaCatalogService : IMediaCatalogService
{
    private readonly IReadOnlyList<MediaItem> mediaItems;

    public InMemoryMediaCatalogService()
        : this(InMemoryMediaCatalogSeed.Create())
    {
    }

    public InMemoryMediaCatalogService(IReadOnlyList<MediaItem> mediaItems)
    {
        this.mediaItems = mediaItems;
    }

    public Task<Result<PagedResult<MediaItem>>> SearchAsync(
        MediaSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
        {
            return Failure<PagedResult<MediaItem>>("Page must be greater than zero.");
        }

        if (query.PageSize < 1)
        {
            return Failure<PagedResult<MediaItem>>("Page size must be greater than zero.");
        }

        IEnumerable<MediaItem> filtered = mediaItems;

        if (query.Type is not null)
        {
            filtered = filtered.Where(item => item.Type == query.Type.Value);
        }

        if (query.Format is not null)
        {
            filtered = filtered.Where(item => item.Format == query.Format.Value);
        }

        if (query.ReleaseStatus is not null)
        {
            filtered = filtered.Where(item => item.ReleaseStatus == query.ReleaseStatus.Value);
        }

        if (query.SeasonYear is not null)
        {
            filtered = filtered.Where(item => item.StartYear == query.SeasonYear.Value);
        }

        if (query.Genres.Count > 0)
        {
            filtered = filtered.Where(item => query.Genres.All(genre => HasGenre(item, genre)));
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            filtered = filtered.Where(item => MatchesCountry(item, query.CountryCode));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            filtered = filtered.Where(item => MatchesSearchText(item, query.SearchText));
        }

        filtered = query.Sort switch
        {
            MediaSearchSort.Score => filtered
                .OrderByDescending(item => item.AverageScore ?? -1)
                .ThenBy(item => item.Title.DisplayTitle, StringComparer.OrdinalIgnoreCase),
            MediaSearchSort.Newest => filtered
                .OrderByDescending(item => item.StartYear ?? int.MinValue)
                .ThenByDescending(item => item.AverageScore ?? -1),
            MediaSearchSort.Trending => filtered
                .OrderByDescending(item => item.ReleaseStatus == Domain.Enums.MediaReleaseStatus.Releasing)
                .ThenByDescending(item => item.AverageScore ?? -1),
            _ => filtered
        };

        var totalCount = filtered.Count();
        var items = filtered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        var page = new PagedResult<MediaItem>(items, query.Page, query.PageSize, totalCount);
        return Task.FromResult(Result<PagedResult<MediaItem>>.Success(page));
    }

    public Task<Result<MediaItem>> GetByIdAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return Failure<MediaItem>("Media id is required.");
        }

        var item = mediaItems.FirstOrDefault(item => item.Id.Equals(mediaId, StringComparison.OrdinalIgnoreCase));
        return item is null
            ? Task.FromResult(Result<MediaItem>.Failure(ApplicationError.NotFound($"Media '{mediaId}' was not found.")))
            : Task.FromResult(Result<MediaItem>.Success(item));
    }

    private static bool MatchesSearchText(MediaItem item, string searchText)
    {
        return Contains(item.Title.Romaji, searchText)
            || Contains(item.Title.English, searchText)
            || Contains(item.Title.Native, searchText);
    }

    private static bool HasGenre(MediaItem item, string genre)
    {
        return item.Genres.Any(itemGenre => itemGenre.Equals(genre, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesCountry(MediaItem item, string countryCode)
    {
        return countryCode.ToUpperInvariant() switch
        {
            "KR" => item.Format == Domain.Enums.MediaFormat.Manhwa,
            "CN" => item.Format == Domain.Enums.MediaFormat.Manhua,
            _ => true
        };
    }

    private static bool Contains(string? value, string searchText)
    {
        return value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static Task<Result<T>> Failure<T>(string message)
    {
        return Task.FromResult(Result<T>.Failure(ApplicationError.Validation(message)));
    }
}
