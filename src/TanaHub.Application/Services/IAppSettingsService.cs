using TanaHub.Application.Common;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Services;

public interface IAppSettingsService
{
    Task<Result<AppSettings>> GetAsync(CancellationToken cancellationToken = default);

    Task<Result<AppSettings>> SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
}
