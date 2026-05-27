using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Services;

public interface IMediaCatalogService
{
    Task<Result<PagedResult<MediaItem>>> SearchAsync(
        MediaSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<MediaItem>> GetByIdAsync(
        string mediaId,
        CancellationToken cancellationToken = default);
}
