using CommunityToolkit.Mvvm.Input;
using TanaHub.Domain.Models;

namespace TanaHub.UI.ViewModels;

public sealed record MediaSearchResultViewModel(
    string Id,
    string Title,
    string Type,
    string Metadata,
    string Format,
    string Year,
    string Status,
    string Genres,
    string Score,
    Uri? PosterUri,
    IAsyncRelayCommand AddCommand,
    IAsyncRelayCommand OpenDetailCommand)
{
    public static MediaSearchResultViewModel FromMediaItem(
        MediaItem item,
        Func<string, Task> addToLibraryAsync,
        Func<string, Task> openDetailAsync)
    {
        var metadataParts = new[]
        {
            item.Format.ToString(),
            item.ReleaseStatus.ToString(),
            item.StartYear?.ToString(),
            item.AverageScore is null ? null : $"{item.AverageScore}/100"
        };

        return new MediaSearchResultViewModel(
            item.Id,
            item.Title.DisplayTitle,
            item.Type.ToString(),
            string.Join(" · ", metadataParts.Where(part => !string.IsNullOrWhiteSpace(part))),
            item.Format.ToString(),
            item.StartYear?.ToString() ?? string.Empty,
            item.ReleaseStatus.ToString(),
            string.Join(", ", item.Genres.Take(3)),
            item.AverageScore is null ? "—" : $"{item.AverageScore}",
            item.Images.PosterUri,
            new AsyncRelayCommand(() => addToLibraryAsync(item.Id)),
            new AsyncRelayCommand(() => openDetailAsync(item.Id)));
    }
}
