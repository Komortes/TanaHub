using CommunityToolkit.Mvvm.Input;

namespace TanaHub.UI.ViewModels;

public sealed record RecognitionInboxItemViewModel(
    string AttemptId,
    string MediaId,
    string Title,
    string SourceLabel,
    string TimestampLabel,
    string ProviderLabel,
    string EpisodeLabel,
    string SimilarityLabel,
    Uri? ThumbnailUri,
    IAsyncRelayCommand OpenCommand,
    IAsyncRelayCommand AddToLibraryCommand,
    IAsyncRelayCommand RemoveCommand);
