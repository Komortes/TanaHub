using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Services;

public interface IUserLibraryService
{
    Task<Result<PagedResult<UserMediaEntry>>> GetEntriesAsync(
        UserLibraryQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<UserMediaEntry>> UpsertEntryAsync(
        UserMediaEntry entry,
        CancellationToken cancellationToken = default);

    Task<Result<UserMediaEntry>> IncrementProgressAsync(
        string mediaId,
        int amount = 1,
        CancellationToken cancellationToken = default);

    Task<Result<UserMediaEntry>> UpdateStatusAsync(
        string mediaId,
        MediaListStatus status,
        CancellationToken cancellationToken = default);

    Task<Result<UserMediaEntry>> UpdateScoreAsync(
        string mediaId,
        int? score,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RemoveEntryAsync(
        string mediaId,
        CancellationToken cancellationToken = default);
}
