using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;
using TanaHub.Infrastructure.Common;

namespace TanaHub.Infrastructure.Settings;

public sealed class FileAppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string storagePath;
    private readonly string keyPath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public FileAppSettingsService(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        this.storagePath = storagePath;
        keyPath = Path.Combine(Path.GetDirectoryName(storagePath) ?? ".", ".settings.key");
    }

    public async Task<Result<AppSettings>> GetAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return Result<AppSettings>.Success(await LoadAsync(cancellationToken));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<AppSettings>> SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var updated = settings with
            {
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var onDisk = updated with
            {
                AniListClientSecret = LocalSecretProtector.Protect(updated.AniListClientSecret, keyPath),
                AniListAccessToken = LocalSecretProtector.Protect(updated.AniListAccessToken, keyPath)
            };

            await AtomicFileWriter.WriteJsonAsync(storagePath, onDisk, JsonOptions, cancellationToken);

            return Result<AppSettings>.Success(updated);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(storagePath))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(storagePath);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                ?? new AppSettings();

            return loaded with
            {
                AniListClientSecret = LocalSecretProtector.Unprotect(loaded.AniListClientSecret, keyPath),
                AniListAccessToken = LocalSecretProtector.Unprotect(loaded.AniListAccessToken, keyPath)
            };
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }
}
