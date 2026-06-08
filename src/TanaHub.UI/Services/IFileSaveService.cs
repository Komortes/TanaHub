namespace TanaHub.UI.Services;

public interface IFileSaveService
{
    Task<bool> SaveTextAsync(
        string suggestedFileName,
        string content,
        string extension,
        string contentType,
        CancellationToken cancellationToken = default);
}
