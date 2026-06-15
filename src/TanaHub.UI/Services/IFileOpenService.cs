namespace TanaHub.UI.Services;

public interface IFileOpenService
{
    Task<(Stream? Stream, string MimeType)> PickImageAsync(CancellationToken cancellationToken = default);
    Task<(Stream? Stream, string MimeType)> PasteImageAsync(CancellationToken cancellationToken = default);
}
