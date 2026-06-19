using TanaHub.Application.Common;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Services;

public interface IRecognitionInboxService
{
    Task<Result<IReadOnlyList<RecognitionAttempt>>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<Result<RecognitionAttempt>> SaveAsync(
        RecognitionAttempt attempt,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RemoveAsync(
        string attemptId,
        CancellationToken cancellationToken = default);
}
