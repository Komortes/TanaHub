using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Settings;

namespace TanaHub.Infrastructure.Tests;

public sealed class FileAppSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsDefaultsWhenFileDoesNotExist()
    {
        var service = new FileAppSettingsService(CreateStoragePath());

        var result = await service.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Nebula dark", result.Value!.Theme);
        Assert.True(result.Value.NotificationsEnabled);
        Assert.True(result.Value.OfflineCacheEnabled);
        Assert.False(result.Value.RecognitionServicesEnabled);
    }

    [Fact]
    public async Task SaveAsync_PersistsSettingsToDisk()
    {
        var storagePath = CreateStoragePath();
        var service = new FileAppSettingsService(storagePath);

        await service.SaveAsync(new AppSettings
        {
            Theme = "High contrast",
            NotificationsEnabled = false,
            OfflineCacheEnabled = false,
            RecognitionServicesEnabled = true,
            PreferredSyncSource = "MangaDex"
        });

        var reloaded = new FileAppSettingsService(storagePath);
        var result = await reloaded.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("High contrast", result.Value!.Theme);
        Assert.False(result.Value.NotificationsEnabled);
        Assert.False(result.Value.OfflineCacheEnabled);
        Assert.True(result.Value.RecognitionServicesEnabled);
        Assert.Equal("MangaDex", result.Value.PreferredSyncSource);
    }

    private static string CreateStoragePath()
    {
        return Path.Combine(Path.GetTempPath(), "TanaHub.Tests", $"{Guid.NewGuid():N}", "settings.json");
    }
}
