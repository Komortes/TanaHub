using System.Text.Json;

namespace TanaHub.Infrastructure.Common;

internal static class AtomicFileWriter
{
    public static async Task WriteJsonAsync<T>(
        string path,
        T value,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, options, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
