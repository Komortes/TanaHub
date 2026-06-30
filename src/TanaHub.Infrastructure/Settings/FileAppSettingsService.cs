using System.Text.Json;
using TanaHub.Application.Common;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;

namespace TanaHub.Infrastructure.Settings;

public sealed class FileAppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string storagePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public FileAppSettingsService(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        this.storagePath = storagePath;
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

            var directory = Path.GetDirectoryName(storagePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(storagePath);
            await JsonSerializer.SerializeAsync(stream, updated, JsonOptions, cancellationToken);

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
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }
}
