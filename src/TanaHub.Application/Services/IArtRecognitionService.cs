using TanaHub.Application.Common;

namespace TanaHub.Application.Services;

public sealed record ArtRecognitionMatch(
    double Similarity,
    string? Title,
    string? Author,
    Uri? SourceUri,
    Uri? ThumbnailUri);

public interface IArtRecognitionService
{
    Task<Result<IReadOnlyList<ArtRecognitionMatch>>> RecognizeAsync(
        Stream imageStream,
        string mimeType,
        string apiKey,
        CancellationToken cancellationToken = default);
}
