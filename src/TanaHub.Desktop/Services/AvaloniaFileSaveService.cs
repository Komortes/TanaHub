using System.Text;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using TanaHub.UI.Services;

namespace TanaHub.Desktop.Services;

public sealed class AvaloniaFileSaveService : IFileSaveService
{
    public async Task<bool> SaveTextAsync(
        string suggestedFileName,
        string content,
        string extension,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
                is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            || !window.StorageProvider.CanSave)
        {
            return false;
        }

        var fileType = new FilePickerFileType($"{extension.ToUpperInvariant()} file")
        {
            Patterns = [$"*.{extension}"],
            MimeTypes = [contentType]
        };

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export TanaHub library",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension,
            SuggestedFileType = fileType,
            FileTypeChoices = [fileType],
            ShowOverwritePrompt = true
        });

        if (file is null)
        {
            return false;
        }

        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
        return true;
    }
}
