using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TanaHub.UI.Views.Controls;

public sealed class RemoteImage : Image
{
    public static readonly StyledProperty<Uri?> SourceUriProperty =
        AvaloniaProperty.Register<RemoteImage, Uri?>(nameof(SourceUri));

    public static readonly StyledProperty<bool> AllowFileSourceProperty =
        AvaloniaProperty.Register<RemoteImage, bool>(nameof(AllowFileSource));

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private static readonly ConcurrentDictionary<Uri, Bitmap?> Cache = new();
    private static readonly ConcurrentQueue<Uri> CacheOrder = new();
    private const int MaxCacheEntries = 64;
    private const int MaxEncodedImageBytes = 8 * 1024 * 1024;
    private const long MaxDecodedPixels = 16_000_000;
    private const long MaxCacheableBitmapBytes = 4 * 1024 * 1024;

    private Uri? requestedUri;

    public RemoteImage()
    {
        Stretch = Stretch.UniformToFill;
    }

    public Uri? SourceUri
    {
        get => GetValue(SourceUriProperty);
        set => SetValue(SourceUriProperty, value);
    }

    public bool AllowFileSource
    {
        get => GetValue(AllowFileSourceProperty);
        set => SetValue(AllowFileSourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceUriProperty)
        {
            _ = LoadAsync(change.GetNewValue<Uri?>());
        }
    }

    private async Task LoadAsync(Uri? uri)
    {
        requestedUri = uri;
        Source = null;

        if (uri is null)
        {
            return;
        }

        if (Cache.TryGetValue(uri, out var cached))
        {
            if (requestedUri == uri)
            {
                Source = cached;
            }

            return;
        }

        Bitmap? bitmap = null;

        try
        {
            if (uri.IsFile)
            {
                if (!AllowFileSource || new FileInfo(uri.LocalPath).Length > MaxEncodedImageBytes)
                {
                    return;
                }

                await using var stream = File.OpenRead(uri.LocalPath);
                bitmap = new Bitmap(stream);
            }
            else
            {
                var bytes = await RemoteImageDownloader.DownloadAsync(
                    HttpClient,
                    uri,
                    MaxEncodedImageBytes,
                    CancellationToken.None);
                await using var stream = new MemoryStream(bytes);
                bitmap = new Bitmap(stream);
            }

            if ((long)bitmap.PixelSize.Width * bitmap.PixelSize.Height > MaxDecodedPixels)
            {
                bitmap.Dispose();
                bitmap = null;
            }
        }
        catch
        {
            // Keep the gradient fallback visible when remote images fail.
        }

        var estimatedBitmapBytes = bitmap is null
            ? 0
            : (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4;
        if (estimatedBitmapBytes <= MaxCacheableBitmapBytes)
        {
            Cache[uri] = bitmap;
            CacheOrder.Enqueue(uri);
            TrimCache();
        }

        if (requestedUri == uri)
        {
            Source = bitmap;
        }
    }

    private static void TrimCache()
    {
        while (Cache.Count > MaxCacheEntries && CacheOrder.TryDequeue(out var oldest))
        {
            Cache.TryRemove(oldest, out _);
        }
    }
}
