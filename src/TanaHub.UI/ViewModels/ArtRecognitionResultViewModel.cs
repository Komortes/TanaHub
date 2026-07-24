using CommunityToolkit.Mvvm.Input;

namespace TanaHub.UI.ViewModels;

public sealed record ArtRecognitionResultViewModel(
    string Title,
    string AuthorLabel,
    string SimilarityLabel,
    Uri? ThumbnailUri,
    Uri? SourceUri,
    IRelayCommand OpenSourceCommand);
