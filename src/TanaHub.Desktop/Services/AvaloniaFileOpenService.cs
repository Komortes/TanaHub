using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using TanaHub.UI.Services;

namespace TanaHub.Desktop.Services;

public sealed class AvaloniaFileOpenService : IFileOpenService
{
    private static readonly FilePickerFileType TextType = new("Text and XML files")
    {
        Patterns = ["*.xml", "*.txt"],
        MimeTypes = ["application/xml", "text/xml", "text/plain"],
    };

    private static readonly FilePickerFileType ImageType = new("Image files")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.bmp"],
        MimeTypes = ["image/png", "image/jpeg", "image/webp", "image/gif", "image/bmp"],
    };

    public async Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> PickTextAsync(
        CancellationToken cancellationToken = default)
    {
        if (GetWindow() is not { } window || !window.StorageProvider.CanOpen)
            return (null, string.Empty, string.Empty, null);

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a MAL XML file",
            AllowMultiple = false,
            FileTypeFilter = [TextType],
        });

        if (files is not { Count: > 0 })
            return (null, string.Empty, string.Empty, null);

        var stream = await files[0].OpenReadAsync();
        var mime = GuessTextMime(files[0].Name);
        var path = files[0].Path.IsFile ? files[0].Path.LocalPath : null;
        return (stream, mime, files[0].Name, path);
    }

    public async Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> PickImageAsync(
        CancellationToken cancellationToken = default)
    {
        if (GetWindow() is not { } window || !window.StorageProvider.CanOpen)
            return (null, string.Empty, string.Empty, null);

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an image to recognise",
            AllowMultiple = false,
            FileTypeFilter = [ImageType],
        });

        if (files is not { Count: > 0 })
            return (null, string.Empty, string.Empty, null);

        var stream = await files[0].OpenReadAsync();
        var mime = GuessMime(files[0].Name);
        var path = files[0].Path.IsFile ? files[0].Path.LocalPath : null;
        return (stream, mime, files[0].Name, path);
    }

    public async Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> PasteImageAsync(
        CancellationToken cancellationToken = default)
    {
        if (GetWindow()?.Clipboard is not { } clipboard)
            return (null, string.Empty, "Clipboard image", null);

        // Avalonia 12: TryGetDataAsync → IAsyncDataTransfer; use TryGetBitmapAsync extension
        var transfer = await clipboard.TryGetDataAsync();
        if (transfer is null) return (null, string.Empty, "Clipboard image", null);

        var bitmap = await transfer.TryGetBitmapAsync();
        if (bitmap is null) return (null, string.Empty, "Clipboard image", null);

        // Save bitmap to a temp PNG file and hand back an open read stream
        var tmp = Path.Combine(Path.GetTempPath(), $"tanahub_paste_{Guid.NewGuid():N}.png");
        bitmap.Save(tmp);
        return (File.OpenRead(tmp), "image/png", "Clipboard image", tmp);
    }

    private static Avalonia.Controls.Window? GetWindow()
        => Avalonia.Application.Current?.ApplicationLifetime
               is IClassicDesktopStyleApplicationLifetime { MainWindow: { } w } ? w : null;

    private static string GuessTextMime(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext == ".xml" ? "application/xml" : "text/plain";
    }

    private static string GuessMime(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "image/png",
        };
    }

}
