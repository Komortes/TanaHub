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

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private static readonly ConcurrentDictionary<Uri, Bitmap?> Cache = new();
    private static readonly ConcurrentQueue<Uri> CacheOrder = new();
    private const int MaxCacheEntries = 300;

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
                await using var stream = File.OpenRead(uri.LocalPath);
                bitmap = new Bitmap(stream);
            }
            else
            {
                var bytes = await HttpClient.GetByteArrayAsync(uri);
                await using var stream = new MemoryStream(bytes);
                bitmap = new Bitmap(stream);
            }
        }
        catch
        {
            // Keep the gradient fallback visible when remote images fail.
        }

        Cache[uri] = bitmap;
        CacheOrder.Enqueue(uri);
        TrimCache();

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
