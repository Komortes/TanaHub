using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TanaHub.UI.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<ArtRecognitionResultViewModel> ArtRecognitionResults { get; } = [];

    [ObservableProperty]
    private bool isArtRecognitionMode;

    partial void OnIsArtRecognitionModeChanged(bool value)
    {
        OnPropertyChanged(nameof(HasRecognitionEmptyMessage));
        OnPropertyChanged(nameof(HasCurrentRecognitionResult));
        OnPropertyChanged(nameof(HasRecognitionVariantResults));
    }

    [RelayCommand]
    private void SetRecognitionMode(string mode)
    {
        IsArtRecognitionMode = mode == "art";
    }

    private async Task RunArtRecognitionAsync(
        Func<CancellationToken, Task<(Stream? Stream, string MimeType, string SourceName, string? SourcePath)>> getImage)
    {
        var (stream, mime, sourceName, _) = await getImage(CancellationToken.None);
        ArtRecognitionResults.Clear();
        if (stream is null)
        {
            RecognitionStatus = sourceName == "Clipboard image"
                ? "Clipboard does not contain a supported image."
                : "No image selected.";
            return;
        }

        IsRecognizing = true;
        RecognitionStatus = "Searching…";

        try
        {
            using (stream)
            {
                var result = await artRecognitionService.RecognizeAsync(stream, mime, appSettings.SauceNaoApiKey);
                if (result.IsFailure) { RecognitionStatus = result.Error.Message; return; }

                foreach (var match in result.Value!)
                {
                    ArtRecognitionResults.Add(new ArtRecognitionResultViewModel(
                        Title: match.Title ?? "Unknown source",
                        AuthorLabel: match.Author ?? string.Empty,
                        SimilarityLabel: $"{match.Similarity * 100:F1}%",
                        ThumbnailUri: match.ThumbnailUri,
                        SourceUri: match.SourceUri,
                        OpenSourceCommand: new RelayCommand(() => OpenExternalUri(match.SourceUri), () => match.SourceUri is not null)));
                }

                RecognitionStatus = ArtRecognitionResults.Count == 0
                    ? "No matches found."
                    : $"{ArtRecognitionResults.Count} match{(ArtRecognitionResults.Count == 1 ? string.Empty : "es")} · powered by SauceNAO";
            }
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    private static void OpenExternalUri(Uri? uri)
    {
        if (uri is null) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", uri.ToString());
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", uri.ToString());
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Best-effort: no browser launcher available on this system.
        }
    }
}
