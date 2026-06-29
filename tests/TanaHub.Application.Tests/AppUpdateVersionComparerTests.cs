using TanaHub.Application.Updates;

namespace TanaHub.Application.Tests;

public sealed class AppUpdateVersionComparerTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("release-2.0.0", "2.0.0")]
    public void TryParseReleaseVersion_AcceptsCommonReleaseTags(string tagName, string expected)
    {
        var parsed = AppUpdateVersionComparer.TryParseReleaseVersion(tagName, out var version);

        Assert.True(parsed);
        Assert.Equal(Version.Parse(expected), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1.2")]
    [InlineData("1.2.3-beta")]
    public void TryParseReleaseVersion_RejectsUnsupportedReleaseTags(string tagName)
    {
        var parsed = AppUpdateVersionComparer.TryParseReleaseVersion(tagName, out var version);

        Assert.False(parsed);
        Assert.Null(version);
    }

    [Theory]
    [InlineData("1.2.4", true)]
    [InlineData("1.2.3", false)]
    [InlineData("1.2.2", false)]
    public void IsUpdateAvailable_UsesSemanticVersionOrdering(string latest, bool expected)
    {
        var available = AppUpdateVersionComparer.IsUpdateAvailable(
            Version.Parse("1.2.3"),
            Version.Parse(latest));

        Assert.Equal(expected, available);
    }
}
