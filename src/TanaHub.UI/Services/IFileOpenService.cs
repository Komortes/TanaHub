namespace TanaHub.UI.Services;

public interface IFileOpenService
{
    Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> PickImageAsync(
        CancellationToken cancellationToken = default);

    Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)> PasteImageAsync(
        CancellationToken cancellationToken = default);
}
