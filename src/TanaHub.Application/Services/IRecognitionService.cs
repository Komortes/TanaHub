using TanaHub.Application.Common;

namespace TanaHub.Application.Services;

public sealed record RecognitionMatch(
    int AniListId,
    string RomajiTitle,
    string? EnglishTitle,
    string? NativeTitle,
    string? Episode,
    double Similarity,
    Uri? ThumbnailUri);

public interface IRecognitionService
{
    Task<Result<IReadOnlyList<RecognitionMatch>>> RecognizeAsync(
        Stream imageStream,
        string mimeType,
        CancellationToken cancellationToken = default);
}
