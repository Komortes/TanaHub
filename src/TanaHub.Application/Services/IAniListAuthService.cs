using TanaHub.Application.Common;

namespace TanaHub.Application.Services;

public interface IAniListAuthService
{
    Task<Result<AniListAuthResult>> AuthorizeAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default);
}

public sealed record AniListAuthResult(string AccessToken, string Username, int UserId);
