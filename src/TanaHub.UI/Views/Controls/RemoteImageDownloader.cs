namespace TanaHub.UI.Views.Controls;

internal static class RemoteImageDownloader
{
    public static async Task<byte[]> DownloadAsync(
        HttpClient httpClient,
        Uri uri,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException("Remote images must use HTTP or HTTPS.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Remote response is not an image.");
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > maxBytes)
        {
            throw new InvalidDataException("Remote image is too large.");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await ReadLimitedAsync(responseStream, maxBytes, cancellationToken);
    }

    internal static async Task<byte[]> ReadLimitedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidDataException("Remote image is too large.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }
}
