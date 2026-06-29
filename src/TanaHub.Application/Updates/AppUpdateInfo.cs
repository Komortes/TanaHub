namespace TanaHub.Application.Updates;

public sealed record AppUpdateInfo(
    Version Version,
    string TagName,
    string Name,
    Uri ReleaseUri,
    DateTimeOffset? PublishedAt);

public sealed record AppUpdateCheckResult(
    Version CurrentVersion,
    AppUpdateInfo? LatestRelease)
{
    public bool IsUpdateAvailable =>
        LatestRelease is not null &&
        AppUpdateVersionComparer.IsUpdateAvailable(CurrentVersion, LatestRelease.Version);
}
