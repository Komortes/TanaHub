using TanaHub.Application.Common;

namespace TanaHub.Application.Services;

public interface IAniListSyncService
{
    Task<Result<int>> SyncAsync(
        string accessToken,
        int userId,
        IUserLibraryService libraryService,
        CancellationToken cancellationToken = default);
}
