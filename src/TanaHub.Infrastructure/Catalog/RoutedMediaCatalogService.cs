using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Application.Services;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Catalog;

// Routes catalog requests to the correct backend based on the user's source preference
// and the mediaId prefix ("anilist:" vs "mangadex:").
internal sealed class RoutedMediaCatalogService : IMediaCatalogService, ICatalogSourceSelector
{
    private readonly AniListMediaCatalogService aniList;
    private readonly MangaDexMediaCatalogService mangaDex;

    public string CurrentSource { get; private set; } = "AniList";

    public void SetSource(string source) => CurrentSource = source;

    public RoutedMediaCatalogService(
        AniListMediaCatalogService aniList,
        MangaDexMediaCatalogService mangaDex)
    {
        this.aniList  = aniList;
        this.mangaDex = mangaDex;
    }

    public Task<Result<PagedResult<MediaItem>>> SearchAsync(
        MediaSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (CurrentSource == "MangaDex" && query.Type is null or MediaType.Manga)
            return mangaDex.SearchAsync(query, cancellationToken);

        return aniList.SearchAsync(query, cancellationToken);
    }

    public Task<Result<MediaItem>> GetByIdAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (mediaId.StartsWith("mangadex:", StringComparison.OrdinalIgnoreCase))
            return mangaDex.GetByIdAsync(mediaId, cancellationToken);

        return aniList.GetByIdAsync(mediaId, cancellationToken);
    }
}
