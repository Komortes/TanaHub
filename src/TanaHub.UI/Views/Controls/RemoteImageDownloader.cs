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

        if (uri.Scheme is not ("http" or "https"))
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
        await using var buffer = new MemoryStream(Math.Min(
            maxBytes,
            (int)Math.Max(0, response.Content.Headers.ContentLength ?? 0)));
        var chunk = new byte[81920];
        while (true)
        {
            var read = await responseStream.ReadAsync(chunk, cancellationToken);
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
