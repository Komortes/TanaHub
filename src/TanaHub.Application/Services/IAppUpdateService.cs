using TanaHub.Application.Common;
using TanaHub.Application.Updates;

namespace TanaHub.Application.Services;

public interface IAppUpdateService
{
    Task<Result<AppUpdateCheckResult>> CheckForUpdatesAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default);
}
