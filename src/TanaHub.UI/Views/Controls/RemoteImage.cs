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

    private static readonly ConcurrentDictionary<Uri, Bitmap> Cache = new();
    private static readonly ConcurrentQueue<Uri> CacheOrder = new();
    private static readonly SemaphoreSlim LoadSlots = new(4);
    private const int MaxCacheEntries = 64;
    private const int MaxEncodedImageBytes = 8 * 1024 * 1024;
    private const long MaxDecodedPixels = 16_000_000;
    private const long MaxCacheableBitmapBytes = 4 * 1024 * 1024;

    private CancellationTokenSource? loadCancellation;
    private bool isAttached;

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

        if (change.Property == SourceUriProperty || change.Property == AllowFileSourceProperty)
        {
            Reload();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        isAttached = true;
        Reload();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        isAttached = false;
        Reload();
        base.OnDetachedFromVisualTree(e);
    }

    private void Reload()
    {
        loadCancellation?.Cancel();
        loadCancellation = null;
        Source = null;

        var uri = SourceUri;
        if (!isAttached || uri is null || !uri.IsAbsoluteUri ||
            (uri.IsFile ? !AllowFileSource : uri.Scheme is not ("http" or "https")))
        {
            return;
        }

        if (!uri.IsFile && Cache.TryGetValue(uri, out var cached))
        {
            Source = cached;
            return;
        }

        loadCancellation = new CancellationTokenSource();
        _ = LoadAsync(uri, loadCancellation);
    }

    private async Task LoadAsync(Uri uri, CancellationTokenSource cancellation)
    {
        Bitmap? bitmap = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var token = timeout.Token;

        try
        {
            await LoadSlots.WaitAsync(token);
            try
            {
                // Both file IO and bitmap decoding must stay off the UI thread.
                bitmap = await Task.Run(async () =>
                {
                    byte[] bytes;
                    if (uri.IsFile)
                    {
                        await using var file = File.OpenRead(uri.LocalPath);
                        bytes = await RemoteImageDownloader.ReadLimitedAsync(file, MaxEncodedImageBytes, token);
                    }
                    else
                    {
                        bytes = await RemoteImageDownloader.DownloadAsync(HttpClient, uri, MaxEncodedImageBytes, token);
                    }

                    token.ThrowIfCancellationRequested();
                    using var stream = new MemoryStream(bytes);
                    return new Bitmap(stream);
                }, token);
            }
            finally
            {
                LoadSlots.Release();
            }

            token.ThrowIfCancellationRequested();
            if ((long)bitmap.PixelSize.Width * bitmap.PixelSize.Height > MaxDecodedPixels)
            {
                return;
            }

            var estimatedBitmapBytes = (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4;
            if (!uri.IsFile && estimatedBitmapBytes <= MaxCacheableBitmapBytes && Cache.TryAdd(uri, bitmap))
            {
                CacheOrder.Enqueue(uri);
                TrimCache();
            }

            Source = bitmap;
            bitmap = null; // Ownership has passed to the control and, optionally, the shared cache.
        }
        catch
        {
            // Keep the fallback visible on failure; failed requests remain retryable.
        }
        finally
        {
            bitmap?.Dispose(); // Only dispose unpublished images, never shared or displayed images.
            if (ReferenceEquals(loadCancellation, cancellation))
            {
                loadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private static void TrimCache()
    {
        while (Cache.Count > MaxCacheEntries && CacheOrder.TryDequeue(out var oldest))
        {
            // Other controls may still display this bitmap after it leaves the cache.
            Cache.TryRemove(oldest, out _);
        }
    }
}
