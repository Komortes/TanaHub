using TanaHub.Infrastructure.Sync;

namespace TanaHub.Infrastructure.Tests;

public sealed class AniListOAuthServiceTests
{
    [Fact]
    public void CreateAuthorizationUrl_IncludesEscapedState()
    {
        var url = AniListOAuthService.CreateAuthorizationUrl("client id", "state/value");

        Assert.Contains("client_id=client%20id", url);
        Assert.Contains("state=state%2Fvalue", url);
    }

    [Fact]
    public void TryReadCallback_RejectsMismatchedState()
    {
        var accepted = AniListOAuthService.TryReadCallback(
            "?code=attacker-code&state=wrong",
            "expected",
            out var code);

        Assert.False(accepted);
        Assert.Null(code);
    }

    [Fact]
    public void TryReadCallback_AcceptsMatchingStateAndDecodesCode()
    {
        var accepted = AniListOAuthService.TryReadCallback(
            "?code=valid%2Bcode&state=expected",
            "expected",
            out var code);

        Assert.True(accepted);
        Assert.Equal("valid+code", code);
    }
}
