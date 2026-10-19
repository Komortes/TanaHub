using TanaHub.Application.Common;
using TanaHub.Application.Queries;

namespace TanaHub.Application.Services;

public interface IMediaRelationsService
{
    void SetOfflineCacheEnabled(bool enabled) { }

    /// <summary>Loads direct relations only. Empty success is distinct from a failed request.</summary>
    Task<Result<RelatedTitlesResult>> GetRelatedTitlesAsync(
        string mediaId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
