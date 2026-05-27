using TanaHub.Application.Common;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Services;

public interface IAiringScheduleService
{
    Task<Result<IReadOnlyList<AiringScheduleItem>>> GetUpcomingAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
