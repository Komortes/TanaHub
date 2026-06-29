using System.Text.RegularExpressions;

namespace TanaHub.Application.Updates;

public static partial class AppUpdateVersionComparer
{
    public static bool IsUpdateAvailable(Version currentVersion, Version latestVersion)
    {
        return latestVersion.CompareTo(currentVersion) > 0;
    }

    public static bool TryParseReleaseVersion(string? tagName, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(tagName)) return false;

        var match = ReleaseVersionRegex().Match(tagName.Trim());
        if (!match.Success) return false;

        version = Version.Parse(match.Groups["version"].Value);
        return true;
    }

    [GeneratedRegex(@"^(?:v|release-)?(?<version>\d+\.\d+\.\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ReleaseVersionRegex();
}
