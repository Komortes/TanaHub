using System.Text.Json;
using TanaHub.Infrastructure.Common;

namespace TanaHub.Infrastructure.Tests;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public async Task WriteJsonAsync_RemovesTemporaryFileWhenSerializationFails()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TanaHub.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "value.json");

        await Assert.ThrowsAsync<InvalidOperationException>(() => AtomicFileWriter.WriteJsonAsync(
            path,
            new ThrowingPayload(),
            new JsonSerializerOptions(),
            CancellationToken.None));

        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    private sealed class ThrowingPayload
    {
        public string Value => throw new InvalidOperationException("Serialization failed.");
    }
}
