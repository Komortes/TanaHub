using CommunityToolkit.Mvvm.Input;

namespace TanaHub.UI.ViewModels;

public sealed record RecognitionResultViewModel(
    string MediaId,
    string Title,
    string EpisodeLabel,
    string SimilarityLabel,
    Uri? ThumbnailUri,
    IAsyncRelayCommand OpenCommand);
