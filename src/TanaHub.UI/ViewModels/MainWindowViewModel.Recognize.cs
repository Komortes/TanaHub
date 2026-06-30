using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TanaHub.Application.Recognition;
using TanaHub.Application.Services;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<RecognitionResultViewModel> RecognitionResults { get; } = [];
    public ObservableCollection<RecognitionResultViewModel> RecognitionVariantResults { get; } = [];
    public ObservableCollection<RecognitionInboxItemViewModel> RecognitionInboxItems { get; } = [];

    [ObservableProperty]
    private bool isRecognizing;

    [ObservableProperty]
    private string recognitionStatus = string.Empty;

    [ObservableProperty]
    private string recognitionEmptyMessage = string.Empty;

    [ObservableProperty]
    private Uri? recognitionSourcePreviewUri;

    [ObservableProperty]
    private RecognitionResultViewModel? currentRecognitionResult;

    [ObservableProperty]
    private bool isRecognitionHistoryExpanded;

    public bool HasRecognitionInboxItems => RecognitionInboxItems.Count > 0;
    public bool HasRecognitionSourcePreview => RecognitionSourcePreviewUri is not null;
    public bool HasCurrentRecognitionResult => CurrentRecognitionResult is not null;
    public bool HasRecognitionVariantResults => RecognitionVariantResults.Count > 0;
    public bool IsRecognitionHistoryVisible => HasRecognitionInboxItems && IsRecognitionHistoryExpanded;
    public bool IsRecognitionHistoryEmptyVisible => !HasRecognitionInboxItems && IsRecognitionHistoryExpanded;
    public bool HasRecognitionEmptyMessage => !string.IsNullOrWhiteSpace(RecognitionEmptyMessage);

    partial void OnRecognitionEmptyMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasRecognitionEmptyMessage));

    partial void OnRecognitionSourcePreviewUriChanged(Uri? value) =>
        OnPropertyChanged(nameof(HasRecognitionSourcePreview));

    partial void OnCurrentRecognitionResultChanged(RecognitionResultViewModel? value) =>
        OnPropertyChanged(nameof(HasCurrentRecognitionResult));

    partial void OnIsRecognitionHistoryExpandedChanged(bool value) =>
        NotifyRecognitionHistoryVisibilityChanged();

    [RelayCommand]
    private Task RecognizeFromFileAsync() => RunRecognitionAsync(fileOpenService.PickImageAsync);

    [RelayCommand]
    private Task PasteAndRecognizeAsync() => RunRecognitionAsync(fileOpenService.PasteImageAsync);

    [RelayCommand]
    private void ToggleRecognitionHistory()
    {
        IsRecognitionHistoryExpanded = !IsRecognitionHistoryExpanded;
    }

    private async Task RunRecognitionAsync(
        Func<CancellationToken, Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)>> getImage)
    {
        var (stream, mime, sourceName, sourcePath) = await getImage(CancellationToken.None);
        ClearRecognitionAttemptState();
        if (stream is null)
        {
            RecognitionStatus = sourceName == "Clipboard image"
                ? "Clipboard does not contain a supported image."
                : "No image selected.";
            return;
        }

        IsRecognizing = true;
        RecognitionStatus = "Searching…";
        RecognitionSourcePreviewUri = CreateLocalFileUri(sourcePath);

        try
        {
            using (stream)
            {
                var result = await recognitionService.RecognizeAsync(stream, mime);
                if (result.IsFailure) { RecognitionStatus = result.Error.Message; return; }

                var matches = RecognitionMatchSelector.RankAndDedupe(result.Value!);
                if (matches.Count == 0)
                {
                    RecognitionStatus = "No matches found.";
                    RecognitionEmptyMessage = "No anime match found for this screenshot.";
                    return;
                }

                (string Title, Uri? PosterUri)? bestPreview = null;
                for (var index = 0; index < matches.Count; index++)
                {
                    var match = matches[index];
                    var mediaId = $"anilist:{match.AniListId}";
                    var preview = await GetRecognitionTitlePreviewAsync(mediaId, match);
                    if (index == 0) bestPreview = preview;
                    var vm = new RecognitionResultViewModel(
                        MediaId: mediaId,
                        Title: preview.Title,
                        EpisodeLabel: match.Episode is not null ? $"EP {match.Episode}" : string.Empty,
                        SimilarityLabel: $"{match.Similarity * 100:F1}%",
                        ThumbnailUri: preview.PosterUri,
                        OpenCommand: new AsyncRelayCommand(() => OpenDetailAsync(mediaId)));

                    RecognitionResults.Add(vm);
                    if (index == 0) CurrentRecognitionResult = vm;
                    else RecognitionVariantResults.Add(vm);
                }

                OnPropertyChanged(nameof(HasRecognitionVariantResults));
                await SaveBestRecognitionAttemptAsync(matches[0], bestPreview!.Value, sourceName, sourcePath);
                RecognitionStatus = $"{matches.Count} match{(matches.Count == 1 ? string.Empty : "es")} · powered by trace.moe";
            }
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    private async Task SaveBestRecognitionAttemptAsync(
        RecognitionMatch match, (string Title, Uri? PosterUri) preview, string sourceName, string? sourcePath)
    {
        var mediaId = $"anilist:{match.AniListId}";
        var attempt = new RecognitionAttempt
        {
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? "Selected image" : sourceName,
            SourcePath = sourcePath,
            AniListId = match.AniListId,
            RomajiTitle = preview.Title,
            EnglishTitle = preview.Title,
            NativeTitle = match.NativeTitle,
            Episode = match.Episode,
            Similarity = match.Similarity,
            ThumbnailUri = preview.PosterUri
        };

        var saved = await recognitionInboxService.SaveAsync(attempt);
        if (saved.IsSuccess)
        {
            RecognitionInboxItems.Insert(0, CreateRecognitionInboxItemViewModel(saved.Value!));
            OnPropertyChanged(nameof(HasRecognitionInboxItems));
            NotifyRecognitionHistoryVisibilityChanged();
        }
    }

    private async Task<(string Title, Uri? PosterUri)> GetRecognitionTitlePreviewAsync(
        string mediaId, RecognitionMatch match)
    {
        var media = await mediaCatalogService.GetByIdAsync(mediaId);
        return media.IsSuccess
            ? (media.Value!.Title.DisplayTitle, media.Value.Images.PosterUri)
            : (match.EnglishTitle ?? match.RomajiTitle, null);
    }

    private async Task LoadRecognitionInboxAsync()
    {
        var result = await recognitionInboxService.GetRecentAsync();
        if (result.IsFailure) { RecognitionStatus = result.Error.Message; return; }

        RecognitionInboxItems.Clear();
        foreach (var attempt in result.Value!)
            RecognitionInboxItems.Add(CreateRecognitionInboxItemViewModel(attempt));

        OnPropertyChanged(nameof(HasRecognitionInboxItems));
        NotifyRecognitionHistoryVisibilityChanged();
    }

    private RecognitionInboxItemViewModel CreateRecognitionInboxItemViewModel(RecognitionAttempt attempt)
    {
        var mediaId = attempt.MediaId ?? string.Empty;
        return new RecognitionInboxItemViewModel(
            attempt.Id, mediaId,
            attempt.EnglishTitle ?? attempt.RomajiTitle ?? attempt.NativeTitle ?? "Unknown title",
            string.IsNullOrWhiteSpace(attempt.SourceName) ? "Selected image" : attempt.SourceName,
            attempt.CreatedAt.ToLocalTime().ToString("MMM d, HH:mm"),
            attempt.Provider,
            attempt.Episode is not null ? $"EP {attempt.Episode}" : string.Empty,
            attempt.Similarity is not null ? $"{attempt.Similarity.Value * 100:F1}%" : "n/a",
            attempt.ThumbnailUri,
            new AsyncRelayCommand(() => OpenDetailAsync(mediaId)),
            new AsyncRelayCommand(() => AddToLibraryAsync(mediaId)),
            new AsyncRelayCommand(() => RemoveRecognitionAttemptAsync(attempt.Id)));
    }

    private async Task RemoveRecognitionAttemptAsync(string attemptId)
    {
        var result = await recognitionInboxService.RemoveAsync(attemptId);
        if (result.IsFailure) { RecognitionStatus = result.Error.Message; return; }

        var item = RecognitionInboxItems.FirstOrDefault(i => i.AttemptId == attemptId);
        if (item is not null)
        {
            RecognitionInboxItems.Remove(item);
            OnPropertyChanged(nameof(HasRecognitionInboxItems));
            NotifyRecognitionHistoryVisibilityChanged();
        }
    }

    private void NotifyRecognitionHistoryVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsRecognitionHistoryVisible));
        OnPropertyChanged(nameof(IsRecognitionHistoryEmptyVisible));
    }

    private void ClearRecognitionAttemptState()
    {
        RecognitionResults.Clear();
        RecognitionVariantResults.Clear();
        CurrentRecognitionResult = null;
        RecognitionEmptyMessage = string.Empty;
        RecognitionSourcePreviewUri = null;
        OnPropertyChanged(nameof(HasRecognitionVariantResults));
    }

    private static Uri? CreateLocalFileUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null;
        return new Uri(path);
    }
}
